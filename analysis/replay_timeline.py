"""Prepare a browser timeline from one saved replay, without resampling or interpolation."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import statistics
from pathlib import Path

from mega_csv import csv_rows, replay_rows
from timeline_page import build


def number(value):
    return type(value) in (int, float) and math.isfinite(value)


def vector(value):
    if isinstance(value, dict) and all(number(value.get(k)) for k in ('x', 'y', 'z')):
        return [value[k] for k in ('x', 'y', 'z')]
    return None


def controller(value):
    value = value if isinstance(value, dict) else {}
    origin, end = vector(value.get('rayOrigin')), vector(value.get('rayEnd'))
    return [value.get('available') is True,
            value.get('rayAvailable') is True and origin is not None and end is not None,
            value.get('tracked', -1), value.get('triggerHeld', -1),
            value.get('triggerValue'), origin, end, value.get('hoveredId')]


def fingerprint(path):
    digest = hashlib.sha256()
    with path.open('rb') as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def summaries(package, source_hash, recording_id):
    """Require the explicit exporter manifest to link summaries to these exact bytes."""
    if package is None:
        return {}, None
    csv_hash, manifest_hash = fingerprint(package / 'trials.csv'), fingerprint(package / 'manifest.json')
    manifest = json.loads((package / 'manifest.json').read_text())
    if manifest.get('schema') != 'trial-performance-subjective-v1':
        raise ValueError('Unsupported trial summary export schema')
    if not any(item.get('sha256') == source_hash for item in manifest.get('inputs', [])):
        raise ValueError('Trial export does not reference this exact replay; regenerate the export')
    result = {}
    for _, row in csv_rows(package / 'trials.csv'):
        if row.get('replay_recording_id') != recording_id:
            continue
        tid = row.get('trial_id')
        if not tid or tid in result:
            raise ValueError('Missing/duplicate summary trial identity')
        result[tid] = row
    if not result:
        raise ValueError('No trial summaries match this recording identity')
    if csv_hash != fingerprint(package / 'trials.csv') or manifest_hash != fingerprint(package / 'manifest.json'):
        raise ValueError('Trial export changed while reading')
    return result, dict(csvSha256=csv_hash, manifestSha256=manifest_hash,
                        scope='Final trial summary. Block/session responses repeat as context, not independent observations.')


def prepare(source: Path, trial_export: Path | None = None, max_age: float = .1) -> dict:
    if not number(max_age) or max_age <= 0:
        raise ValueError('Maximum displayed sample age must be finite and positive')
    source = source.resolve()
    if source.stat().st_size > 1024**3:
        raise ValueError('Replay exceeds the 1 GiB viewer limit')
    digest = fingerprint(source)
    warnings = []
    records = list(replay_rows(source, warnings))
    if not records:
        raise ValueError('Empty replay')
    # The shared reader handles interrupted tails. Tighten numeric types and reject
    # non-finite nested values before constructing a portable JSON page.
    for line, r in records:
        json.dumps(r, allow_nan=False)
        if type(r.get('sequence')) is not int or not number(r.get('time')):
            raise ValueError(f'Invalid sequence/time at source line {line}')
    header = records[0][1]
    recording_id = header.get('recordingId')
    if not recording_id or not header.get('utc'):
        raise ValueError('Replay needs recordingId and utc; do not invent run identity')
    groups = []
    seen = set()
    for line, r in records[1:]:
        tid = r.get('trialId')
        if r['kind'] == 'end':
            continue
        if not tid:
            continue
        if not groups or groups[-1][0] != tid:
            if tid in seen:
                raise ValueError('Non-contiguous replay trial identity')
            seen.add(tid)
            groups.append((tid, []))
        groups[-1][1].append((line, r))
    if not groups:
        raise ValueError('No trials in replay')
    participants = {r.get('participantCode') for _, group in groups for _, r in group}
    if len(participants) != 1 or not next(iter(participants)):
        raise ValueError('Missing/mixed participant identity')
    summary_rows, summary_provenance = summaries(trial_export, digest, recording_id)
    trials = []
    for index, (tid, group) in enumerate(groups):
        first = group[0][1]
        context = tuple(first.get(k) for k in ('participantCode', 'runNumber', 'sourceTrialId', 'practice', 'voice', 'block'))
        if any(tuple(r.get(k) for k in ('participantCode', 'runNumber', 'sourceTrialId', 'practice', 'voice', 'block')) != context for _, r in group):
            raise ValueError(f'Changing trial context: {tid}')
        start = first['time']
        end = groups[index + 1][1][0][1]['time'] if index + 1 < len(groups) else records[-1][1]['time']
        if end <= start:
            warnings.append(f'{tid}: zero-duration trial omitted (no invented duration)')
            continue
        snapshots = [r for _, r in group if r['kind'] == 'trial']
        if len(snapshots) > 1:
            raise ValueError(f'Multiple object snapshots for {tid}')
        snapshot = snapshots[0] if snapshots else {}
        objects = []
        for obj in snapshot.get('objects') or []:
            position = vector(obj.get('position'))
            if not position or not obj.get('id') or any(o[0] == obj['id'] for o in objects):
                raise ValueError(f'Invalid object identity/position: {tid}')
            objects.append([obj['id'], ' '.join(filter(None, [obj.get('color'), obj.get('shape')])), *position, obj.get('planeId')])
        target = snapshot.get('targetId')
        target_name = next((o[1] for o in objects if o[0] == target), 'Unknown target')
        samples, events, speech, changes, states, segments = [], [], [], [], [], []
        state = dict(searching=False, visible=False, readiness=False, completed=False)
        segment_start = None
        speech_start = None
        search_start = complete_time = duration = None
        for line, r in group:
            kind, at = r['kind'], r['time']
            previous = dict(state)
            if kind in ('search_start', 'search_resumed'):
                state.update(searching=True, visible=True, readiness=False)
                if search_start is None:
                    search_start = at
            if kind in ('search_paused', 'application_paused', 'stop', 'transition'):
                state.update(searching=False, visible=False)
            if kind.startswith('readiness_'):
                state['readiness'] = r.get('readinessActive') is True
            if kind == 'sample' or kind == 'selection' and 'headAvailable' in r:
                head = vector(r.get('headPosition'))
                origin, direction = vector(r.get('gazeOrigin')), vector(r.get('gazeDirection'))
                samples.append([at, r.get('frame'), r.get('searchSeconds'), r.get('searchActive'),
                                head, origin, direction, r.get('gazeTracked', -1), r.get('hoveredId'),
                                controller(r.get('leftController')), controller(r.get('rightController')),
                                r.get('readinessActive'), r.get('visible'),
                                r.get('headAvailable') is True and r.get('headTracked') == 1 and head is not None,
                                r.get('gazeAvailable') is True and origin is not None and direction is not None,
                                r, line])
                state.update(searching=r.get('searchActive') is True, visible=r.get('visible') is True,
                             readiness=r.get('readinessActive') is True)
            if kind == 'selection' and r.get('correct') is True:
                if complete_time is not None:
                    raise ValueError(f'Multiple correct selections: {tid}')
                complete_time, duration = at, r.get('searchSeconds')
                state.update(searching=False, visible=False, completed=True)
            if state != previous:
                states.append(dict(time=at, sequence=r['sequence'], **state))
            if state['searching'] and segment_start is None:
                segment_start = at
            if not state['searching'] and segment_start is not None:
                segments.append([segment_start, at]); segment_start = None
            if r.get('changes'):
                for change in r['changes']:
                    if not vector(change.get('position')) or not any(o[0] == change.get('id') for o in objects):
                        raise ValueError(f'Invalid transform change: {tid}')
                changes.append(dict(time=at, sequence=r['sequence'], changes=r['changes']))
            if kind != 'sample':
                events.append(dict(time=at, kind=kind, object=r.get('objectId'), correct=r.get('correct'),
                                   search=r.get('searchSeconds'), sourceLine=line, raw=r))
            if kind == 'audio_playback_start':
                # One-shot success chimes can overlap the voice channel. They have
                # a recorded duration but no playback-end event of their own.
                detail = r.get('detail') or ''
                effect = 'software_one_shot_start' in detail.split(';')
                match = re.search(r'(?:^|;)duration=([0-9.]+)(?:;|$)', detail) if effect else None
                if effect and not match:
                    warnings.append(f'{tid}: one-shot audio duration unknown at sequence {r["sequence"]}')
                audio = dict(start=at, end=min(end, at + float(match[1])) if match else end,
                             text=r.get('text') or '(Recorded audio; no transcript)',
                             sourceLine=line, sequence=r['sequence'], effect=effect)
                if not effect:
                    if speech_start:
                        speech_start['end'] = at
                    speech_start = audio
                speech.append(audio)
            elif kind in ('audio_playback_end', 'audio_cancelled', 'audio_failure', 'stop', 'application_paused') and speech_start:
                speech_start['end'] = at; speech_start = None
        if segment_start is not None:
            segments.append([segment_start, end])
        intervals = [b[0]-a[0] for a, b in zip(samples, samples[1:]) if b[0] > a[0]]
        gaps = [[a[0] + max_age, b[0]] for a, b in zip(samples, samples[1:]) if b[0]-a[0] > max_age]
        if samples and end - samples[-1][0] > max_age:
            gaps.append([samples[-1][0] + max_age, end])
        source_id = f'{recording_id}:{tid}' if first.get('practice') else first.get('sourceTrialId')
        summary = summary_rows.get(source_id)
        if summary:
            expected = {'participant_id': first['participantCode'], 'voice_condition': first.get('voice')}
            if not first.get('practice'):
                expected.update(run_number=str(first.get('runNumber')), block_index=str(first.get('block')))
            if any(summary.get(key) not in (None, '', value) for key, value in expected.items()):
                raise ValueError('Summary participant/run/voice/block mismatch')
        label = ('Practice' if first.get('practice') else 'Trial')
        label += f' {first["round"] + 1}' if type(first.get('round')) is int else f' {tid}'
        trials.append(dict(id=tid, sourceId=source_id, label=label,
                           target=target, targetName=target_name, theta=snapshot.get('absoluteTheta'),
                           start=start, end=end, snapshotTime=snapshot.get('time', end), searchStart=search_start,
                           completeTime=complete_time, duration=duration, practice=first.get('practice') is True,
                           objects=objects, samples=samples, speech=speech, events=events, gaps=gaps,
                           sampleCount=sum(r['kind'] == 'sample' for _, r in group),
                           medianHz=round(1/statistics.median(intervals), 2) if intervals else None,
                           stateChanges=states, searchSegments=segments, objectChanges=changes,
                           summary=summary, trialRecord=snapshot, maxSampleAge=max_age))
    if fingerprint(source) != digest:
        raise ValueError('Replay changed while reading; copy/close the recording before export')
    if not trials:
        raise ValueError('No positive-duration trials')
    return dict(schema='replay-timeline-v1', participant=next(iter(participants)), utc=header['utc'],
                recordingId=recording_id, source=dict(name=source.name, sha256=digest), header=header,
                complete=records[-1][1].get('kind') == 'end' and records[-1][1].get('complete') is True,
                warnings=warnings, trials=trials, summaryProvenance=summary_provenance,
                displayPolicy='All saved frame samples and pose-bearing selections; latest preceding sample, no interpolation. '
                              f'Poses expire after {max_age:g} s. This display-age rule is not a validated fixation or sensor-rate threshold.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('replay', type=Path)
    parser.add_argument('--trial-export', type=Path, help='Explicit trial_csv export directory, including manifest.json')
    parser.add_argument('--output', required=True, type=Path, help='New prepared timeline JSON')
    parser.add_argument('--html', type=Path, help='Also create a new standalone browser page')
    parser.add_argument('--max-sample-age', type=float, default=.1)
    args = parser.parse_args()
    if args.output.exists() or args.html and args.html.exists():
        parser.error('Output exists; choose new paths')
    data = prepare(args.replay, args.trial_export, args.max_sample_age)
    with args.output.open('x') as handle:
        json.dump(data, handle, allow_nan=False, ensure_ascii=True, separators=(',', ':'))
    trial_count = len(data['trials'])
    # The page packager reads the saved JSON. Release this full-session object
    # graph before that second parse rather than retaining two copies together.
    del data
    if args.html:
        build(args.output, args.html)
    print(f'Prepared {trial_count} trials')
