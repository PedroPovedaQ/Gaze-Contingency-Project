#!/usr/bin/env python3
"""Export a versioned derived gaze audit for explicitly reviewed recording hashes.
Never overwrites the recording, hit IDs, trial summaries, or an existing output directory.
"""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import statistics

PROFILES = Path(__file__).parent / 'Resources/GazeCorrectionProfiles.json'
METHOD = 'reflect_origin_across_recorded_xr_local_x_v1'


def vector(value):
    result = [float(value[k]) for k in 'xyz']
    if not all(math.isfinite(v) for v in result):
        raise ValueError('Non-finite vector')
    return result


def subtract(a, b):
    return [x-y for x, y in zip(a, b)]


def dot(a, b):
    return sum(x*y for x, y in zip(a, b))


def distance(a, b):
    d = subtract(a, b)
    return math.sqrt(dot(d, d))


def unmirror(gaze, origin, quaternion):
    x, y, z, w = (float(quaternion[k]) for k in 'xyzw')
    norm = x*x+y*y+z*z+w*w
    if not math.isfinite(norm) or abs(norm-1) > .01:
        raise ValueError('Invalid XR reference rotation')
    x, y, z, w = (v/math.sqrt(norm) for v in (x, y, z, w))
    # World/recording-space normal of the XR-local x=0 reflection plane.
    axis = [1-2*(y*y+z*z), 2*(x*y+w*z), 2*(x*z-w*y)]
    offset = dot(subtract(gaze, origin), axis)
    return [g-2*offset*a for g, a in zip(gaze, axis)]


def angle(origin, direction, target):
    delta = subtract(target, origin)
    length = math.sqrt(dot(delta, delta)*dot(direction, direction))
    return math.degrees(math.acos(max(-1, min(1, dot(delta, direction)/length)))) if length else None


def match_profile(header, digest):
    matches = [p for p in json.loads(PROFILES.read_text())['profiles']
               if p['sha256'] == digest and p['recordingId'] == header.get('recordingId')
               and p['buildGuid'] == header.get('buildGuid') and p['method'] == METHOD]
    if len(matches) != 1:
        raise ValueError('No reviewed correction profile for this exact recording')
    return matches[0]


def stats(values):
    return {'count': len(values), 'min': min(values), 'median': statistics.median(values), 'max': max(values)} if values else {'count': 0}


def export(source, output):
    data = source.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    lines = data.splitlines()
    profile = match_profile(json.loads(lines[0]), digest)
    records = []
    omitted = False
    for index, line in enumerate(lines):
        try:
            records.append(json.loads(line))
        except json.JSONDecodeError:
            if index != len(lines)-1 or data.endswith(b'\n'):
                raise
            omitted = True  # Same recoverable incomplete final row as the viewer; disclose it.
    rows = []
    raw_gaps, corrected_gaps, raw_cross, corrected_cross = [], [], [], []
    objects, target_id = {}, None
    for record in records:
        if record['kind'] == 'trial':
            objects = {o['id']: o['position'] for o in record['objects']}
            target_id = record.get('targetId')
        for change in record.get('changes') or []:
            objects[change['id']] = change['position']
        if record['kind'] != 'sample':
            continue
        row = {'sequence': record['sequence'], 'time_seconds': record['time'], 'trial_id': record.get('trialId'),
               'profile': profile['id'], 'correction_available': 0, 'unavailable_reason': '',
               'gaze_tracked': record.get('gazeTracked'), 'head_tracked': record.get('headTracked'),
               'recorded_gaze_hit_id_unchanged': record.get('hoveredId'),
               'raw_head_distance_m': None, 'corrected_head_distance_m': None,
               'raw_cross_angle_deg': None, 'corrected_cross_angle_deg': None,
               'raw_target_angle_deg': None, 'corrected_target_angle_deg': None}
        for prefix, key in [('raw_origin', 'gazeOrigin'), ('direction_unchanged', 'gazeDirection'),
                            ('head', 'headPosition'), ('xr_origin', 'xrOriginPosition')]:
            for axis in 'xyz':
                row[f'{prefix}_{axis}'] = (record.get(key) or {}).get(axis)
        for axis in 'xyz':
            row[f'corrected_origin_{axis}'] = None
        try:
            if not record.get('gazeAvailable') or record.get('gazeTracked') != 1:
                raise ValueError('gaze_unavailable_or_untracked')
            if not record.get('xrOriginAvailable'):
                raise ValueError('missing_xr_reference')
            gaze, direction, origin = (vector(record[k]) for k in ('gazeOrigin', 'gazeDirection', 'xrOriginPosition'))
            if dot(direction, direction) <= 1e-6:
                raise ValueError('invalid_direction')
            corrected = unmirror(gaze, origin, record['xrOriginRotation'])
            row['correction_available'] = 1
            for axis, value in zip('xyz', corrected):
                row[f'corrected_origin_{axis}'] = value
            if record.get('headAvailable') and record.get('headTracked') == 1:
                head = vector(record['headPosition'])
                row['raw_head_distance_m'] = distance(gaze, head)
                row['corrected_head_distance_m'] = distance(corrected, head)
                raw_gaps.append(row['raw_head_distance_m']); corrected_gaps.append(row['corrected_head_distance_m'])
            # Readiness membership is historical, NOT independent fixation ground truth.
            if record.get('readinessActive') and not record.get('readinessPaused') and record.get('readinessValidInside'):
                target = vector(record['readinessPosition'])
                row['raw_cross_angle_deg'] = angle(gaze, direction, target)
                row['corrected_cross_angle_deg'] = angle(corrected, direction, target)
                if row['raw_cross_angle_deg'] is not None:
                    raw_cross.append(row['raw_cross_angle_deg']); corrected_cross.append(row['corrected_cross_angle_deg'])
            if record.get('searchActive') and target_id in objects:
                target = vector(objects[target_id])
                row['raw_target_angle_deg'] = angle(gaze, direction, target)
                row['corrected_target_angle_deg'] = angle(corrected, direction, target)
        except (ValueError, KeyError, TypeError) as exc:
            row['unavailable_reason'] = str(exc)
        rows.append(row)
    output.mkdir(parents=True, exist_ok=False)
    with (output/'derived-gaze.csv').open('w', newline='') as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0]))
        writer.writeheader(); writer.writerows(rows)
    report = {'source_sha256': digest, 'profile': profile, 'samples': len(rows),
              'corrected_samples': sum(r['correction_available'] for r in rows), 'incomplete_trailing_record_omitted': omitted,
              'raw_head_distance_m': stats(raw_gaps), 'corrected_head_distance_m': stats(corrected_gaps),
              'raw_readiness_cross_angle_deg': stats(raw_cross), 'corrected_readiness_cross_angle_deg': stats(corrected_cross),
              'limitations': ['Coordinate recovery only; not calibrated gaze accuracy.',
                             'Direction unchanged because the historical bug reflected position only.',
                             'Readiness gate membership is not independent fixation ground truth.',
                             'Target angles describe geometry; participants need not have been looking at the target.',
                             'Recorded hit IDs, timing summaries and scientific measures have not been recomputed.',
                             'Missing/invalid samples are retained with unavailable flags, not filled.']}
    (output/'manifest.json').write_text(json.dumps(report, indent=2)+'\n')
    if hashlib.sha256(source.read_bytes()).hexdigest() != digest:
        raise RuntimeError('Source changed during export; discard this derived output')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    print(json.dumps(export(args.source, args.output), indent=2))
