"""Audit exact run/replay/raw-eye links without guessing from filenames or nearby times.

This complements verify-study-session.py and guidance_audit.py. A consistent
package is not evidence of native eye rate, spatial accuracy or acoustic timing.
"""
import argparse
import csv
import hashlib
import json
import math
from collections import Counter, defaultdict
from pathlib import Path

from eye_quality import read_recording, recording_status
from mega_csv import csv_rows, replay_rows

VERSION = 'recording-links-v1'
CONTRACT = 'gaze-observation-link-v1'


def finite(value):
    return isinstance(value, (float, int)) and not isinstance(value, bool) and math.isfinite(value)


def close(a, b, tolerance=1e-6):
    return finite(a) and finite(b) and abs(a-b) <= tolerance


def vector(value, axes='xyz'):
    if not isinstance(value, dict) or any(not finite(value.get(k)) for k in axes):
        raise ValueError('Missing/non-finite coordinate')
    return [value[k] for k in axes]


def rotate(q, v):
    """Rotate replay-local coordinates to world using the header quaternion."""
    x, y, z, w = vector(q, 'xyzw')
    norm = x*x+y*y+z*z+w*w
    if abs(norm-1) > 1e-3:
        raise ValueError('Recording origin quaternion is not normalized')
    # Quaternion vector rotation; no Euler conversion or X-axis mirroring.
    tx, ty, tz = 2*(y*v[2]-z*v[1]), 2*(z*v[0]-x*v[2]), 2*(x*v[1]-y*v[0])
    return [v[0]+w*tx+y*tz-z*ty, v[1]+w*ty+z*tx-x*tz, v[2]+w*tz+x*ty-y*tx]


def fingerprint(path):
    digest = hashlib.sha256()
    with path.open('rb') as file:
        for chunk in iter(lambda: file.read(1024*1024), b''):
            digest.update(chunk)
    return dict(path=str(path.resolve()), bytes=path.stat().st_size, sha256=digest.hexdigest())


def audit(run, eye_paths, replay_paths):
    run = Path(run)
    summary_path = run/'trial_summary.json'
    summary = json.loads(summary_path.read_text(encoding='utf-8-sig'))
    if not isinstance(summary, dict) or not isinstance(summary.get('objectives'), list):
        raise ValueError('Invalid summary structure')
    participant, number = summary['participant_id'], summary['run_number']
    if not isinstance(participant, str) or not participant or type(number) is not int or number <= 0:
        raise ValueError('Invalid summary participant/run identity')
    trials = {}
    for trial in summary['objectives']:
        if not isinstance(trial, dict):
            raise ValueError('Invalid summary trial structure')
        tid = trial.get('trial_id')
        if not isinstance(tid, str) or not tid or tid in trials:
            raise ValueError('Missing/duplicate summary trial identity')
        if type(trial.get('index')) is not int or type(trial.get('completed')) is not bool or not trial.get('voice_condition'):
            raise ValueError('Invalid summary trial context/completion')
        trials[tid] = trial
    if not trials:
        raise ValueError('No summary trials')
    issues, incomplete, notes, sources, statuses = [], [], [], [fingerprint(summary_path)], []
    stats = {tid: dict(trial_id=tid, completed=t.get('completed') is True, replay_files=set(),
                      trial_markers=0, correct_selections=0, replay_samples=0, linked_samples=0,
                      raw_search_rows=0, unavailable_samples=0) for tid, t in trials.items()}
    def issue(code, path='', tid='', detail=''):
        issues.append(dict(code=code, source=str(path), trial_id=tid, detail=detail))
    def missing(code, path='', tid=''):
        incomplete.append(dict(code=code, source=str(path), trial_id=tid))
    def source(path):
        path = Path(path).resolve()
        if any(item['path'] == str(path) for item in sources):
            raise ValueError(f'Duplicate input path: {path}')
        sources.append(fingerprint(path))
        return path

    eyes, eye_ids = {}, set()
    for path in eye_paths:
        path = source(path)
        rows, warnings = read_recording(path)
        identity = rows[0]['recordingId']
        if not isinstance(identity, str) or not identity:
            raise ValueError('Invalid raw recording identity')
        if identity in eye_ids:
            raise ValueError('Multiple files claim the same raw recording identity')
        eye_ids.add(identity)
        receipt = path.parent/'end_status.json'
        if receipt.exists(): source(receipt)
        status = recording_status(path, rows, warnings)
        statuses.append(dict(path=str(path), recording_id=identity, **status))
        if status.get('complete') is not True or type(status.get('acceptedRows')) is not int or status['acceptedRows'] != len(rows):
            missing('raw_recording_not_cleanly_closed', path)
        notes.extend(warnings)
        previous = -1
        for row in rows:
            if type(row['sequence']) is not int or not finite(row['receiptTime']) or row['receiptTime'] < previous:
                raise ValueError('Invalid raw observation sequence or receipt ordering')
            previous = row['receiptTime']
            eyes[(identity, row['sequence'])] = row
            tid = row.get('sourceTrialId')
            if tid in trials and not row.get('practice'):
                if row.get('participant') != participant or row.get('run') != number:
                    issue('raw_trial_identity_mismatch', path, tid)
                if row.get('round') != trials[tid]['index'] or row.get('voice') != trials[tid]['voice_condition'] or ('block' in trials[tid] and row.get('block') != trials[tid]['block']):
                    issue('raw_trial_context_mismatch', path, tid)
                elif row.get('phase') == 'search' and row.get('participant') == participant and row.get('run') == number:
                    stats[tid]['raw_search_rows'] += 1

    objects = defaultdict(dict)
    manifest = run/'object_manifest.csv'
    if manifest.exists():
        source(manifest)
        for _, obj in csv_rows(manifest):
            tid, oid = obj.get('trial_id'), obj.get('object_id')
            if tid in trials:
                if not oid or oid in objects[tid]:
                    issue('missing_or_duplicate_manifest_object', manifest, tid)
                objects[tid][oid] = obj
    else:
        missing('missing_object_manifest', manifest)

    recording_ids = set()
    for path in replay_paths:
        path = source(path)
        warnings = []
        rows = [row for _, row in replay_rows(path, warnings)]
        if not rows:
            raise ValueError('Empty replay')
        header = rows[0]
        if type(header.get('sequence')) is not int or not finite(header.get('time')):
            raise ValueError('Invalid replay header sequence/time')
        identity = header.get('recordingId')
        if not isinstance(identity, str) or not identity or identity in recording_ids:
            raise ValueError('Missing/duplicate replay recording identity')
        recording_ids.add(identity)
        linked = header.get('gazeLinkVersion') == CONTRACT
        if not linked: missing('legacy_or_unknown_sample_link_contract', path)
        if warnings or rows[-1].get('kind') != 'end' or rows[-1].get('complete') is not True:
            missing('replay_not_cleanly_completed', path)
        notes.extend(warnings)
        for row in rows[1:]:
            if row.get('kind') == 'header' or type(row.get('sequence')) is not int or not finite(row.get('time')):
                raise ValueError('Invalid replay record/header ordering')
            tid = row.get('sourceTrialId')
            if row.get('practice') or not tid:
                continue  # Practice/setup never supplies measured-trial evidence.
            if tid not in trials:
                issue('replay_trial_absent_from_summary', path, tid)
                continue
            trial, counts = trials[tid], stats[tid]
            counts['replay_files'].add(str(path))
            if (row.get('participantCode') != participant or row.get('runNumber') != number or
                row.get('round') != trial['index'] or row.get('voice') != trial.get('voice_condition') or
                ('block' in trial and row.get('block') != trial['block'])):
                issue('replay_trial_context_mismatch', path, tid, str(row.get('sequence')))
            if row.get('kind') == 'trial':
                counts['trial_markers'] += 1
                items = row.get('objects') or []
                if not isinstance(items, list) or any(not isinstance(item, dict) for item in items):
                    raise ValueError('Invalid replay object snapshot')
                ids = [item.get('id') for item in items]
                if not ids or len(set(ids)) != len(ids) or set(ids) != set(objects[tid]):
                    issue('replay_manifest_object_set_mismatch', path, tid)
                for item in items:
                    obj = objects[tid].get(item.get('id'))
                    if obj and (item.get('shape') != obj.get('shape') or item.get('color') != obj.get('color') or
                                item.get('target') is not (obj.get('is_target') == '1')):
                        issue('replay_manifest_object_metadata_mismatch', path, tid, str(item.get('id')))
                targets = [item['id'] for item in items if item.get('target') is True]
                if targets != [row.get('targetId')]:
                    issue('replay_target_identity_mismatch', path, tid)
            if row.get('kind') == 'selection' and row.get('correct') is True:
                counts['correct_selections'] += 1
                obj = objects[tid].get(row.get('objectId'))
                if not obj or obj.get('is_target') != '1':
                    issue('correct_selection_is_not_manifest_target', path, tid)
            if row.get('kind') not in ('sample', 'selection'):
                continue
            counts['replay_samples'] += 1
            if not linked:
                continue
            if type(row.get('gazeTracked')) is not int or row['gazeTracked'] not in (-1, 0, 1):
                issue('invalid_replay_tracking_flag', path, tid)
            sequence = row.get('gazeObservationSequence', -1)
            if type(sequence) is not int or sequence < -1:
                issue('invalid_raw_observation_reference', path, tid)
                continue
            if sequence == -1:
                counts['unavailable_samples'] += 1
                if row.get('gazeTracked') == 1:
                    issue('tracked_gaze_without_observation', path, tid)
                continue
            eye = eyes.get((row.get('gazeRecordingId'), sequence))
            if eye is None:
                issue('missing_exact_raw_observation', path, tid, str(row.get('sequence')))
                continue
            counts['linked_samples'] += 1
            for saved, raw in (('gazeReceiptTime','receiptTime'), ('gazeInputEventTime','inputEventTime')):
                if not close(row.get(saved), eye.get(raw)):
                    issue('raw_timestamp_mismatch', path, tid, saved)
            if row.get('gazeSource') != eye.get('source'):
                issue('raw_source_mismatch', path, tid)
            if eye.get('participant') != participant:
                issue('raw_observation_participant_mismatch', path, tid)
            if eye.get('run') != number:
                if row.get('searchActive') or row.get('kind') == 'selection':
                    issue('raw_observation_run_mismatch', path, tid)
                else:
                    notes.append(f'Cross-run transition observation: {path}:{row.get("sequence")}')
            if eye.get('sourceTrialId') != tid:
                # A first transition frame can retain the preceding observation.
                # Preserve this fact; never relabel or substitute a nearby row.
                if row.get('searchActive') or row.get('kind') == 'selection':
                    issue('active_sample_cross_trial_observation', path, tid)
                else:
                    notes.append(f'Cross-trial transition observation: {path}:{row.get("sequence")}')
            if row.get('gazeTracked') == 1:
                if eye.get('valid') is not True:
                    issue('tracked_replay_from_invalid_raw', path, tid)
                try:
                    origin = [a+b for a,b in zip(rotate(header.get('originRotation'), vector(row.get('gazeOrigin'))), vector(header.get('origin')))]
                    direction = rotate(header.get('originRotation'), vector(row.get('gazeDirection')))
                    if any(not close(a,b,1e-4) for a,b in zip(origin,vector(eye.get('worldOrigin')))):
                        issue('gaze_origin_transform_mismatch', path, tid)
                    if any(not close(a,b,1e-4) for a,b in zip(direction,vector(eye.get('worldDirection')))):
                        issue('gaze_direction_transform_mismatch', path, tid)
                except ValueError as error:
                    issue('invalid_gaze_coordinates', path, tid, str(error))
    for tid, counts in stats.items():
        trial = trials[tid]
        if len(counts['replay_files']) > 1: issue('trial_in_multiple_replays', tid=tid)
        if counts['trial_markers'] > 1: issue('duplicate_replay_trial_marker', tid=tid)
        started = trial.get('completed') is True or (finite(trial.get('search_started_at')) and trial['search_started_at'] >= 0)
        if started:
            if counts['trial_markers'] == 0: missing('missing_replay_trial', tid=tid)
            if counts['raw_search_rows'] == 0: missing('missing_raw_search_rows', tid=tid)
            if counts['replay_samples'] == 0: missing('missing_replay_samples', tid=tid)
        expected = int(trial.get('completed') is True)
        if counts['replay_files'] and counts['correct_selections'] != expected:
            issue('summary_selection_count_mismatch', tid=tid)
        counts['replay_files'] = sorted(counts['replay_files'])
    # Reports describe an immutable snapshot, not a file changing during parsing.
    for saved in sources:
        if fingerprint(Path(saved['path'])) != saved:
            raise ValueError(f'Input changed during audit: {saved["path"]}')
    if not eye_paths: missing('no_raw_eye_inputs')
    if not replay_paths: missing('no_replay_inputs')
    return dict(report_version=VERSION, participant_id=participant, run_number=number,
                status='issues_found' if issues else 'evidence_incomplete' if incomplete else 'consistent',
                sources=sources, raw_status=statuses, trials=list(stats.values()), issues=issues,
                incomplete=incomplete, notes=notes, issue_counts=dict(Counter(i['code'] for i in issues)),
                limits='File/link consistency only. Run session and guidance validators separately. No native rate, physiological event, spatial accuracy, acoustic or hardware acceptance claim.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    parser.add_argument('--eye', type=Path, action='append', default=[])
    parser.add_argument('--replay', type=Path, action='append', default=[])
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    if args.out.exists(): parser.error('Output exists; choose a new directory')
    report = audit(args.run, args.eye, args.replay)
    args.out.mkdir(parents=True, exist_ok=False)
    (args.out/'recording_links.json').write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
    for name, rows in (('trials', report['trials']), ('issues', report['issues']), ('incomplete', report['incomplete'])):
        with (args.out/(name+'.csv')).open('w', newline='') as file:
            fields = list(rows[0]) if rows else ['code']
            writer = csv.DictWriter(file, fieldnames=fields)
            writer.writeheader()
            for row in rows:
                writer.writerow({k:json.dumps(v) if isinstance(v,list) else v for k,v in row.items()})
    print(report['status'])
    return 1 if report['issues'] else 2 if report['incomplete'] else 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise SystemExit(str(error)) from None
