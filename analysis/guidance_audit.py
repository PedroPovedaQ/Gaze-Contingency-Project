"""Join guidance decisions to exact raw eye observations and software audio events.

This is a recording-consistency audit, not proof of correct directions, acoustic
onset, physiological eye events or native sensor rate. Legacy recordings without
the decision contract remain unsupported rather than receiving invented links.
"""
import argparse
import csv
import json
import math
from collections import Counter, defaultdict
from pathlib import Path

from eye_quality import read_recording, recording_status
from mega_csv import replay_rows

VERSION = 'guidance-audit-v1'
CONTRACT = 'guidance-decision-v1'


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def close(a, b):
    return finite(a) and finite(b) and math.isclose(a, b, abs_tol=1e-6, rel_tol=0)


def snapshot_issues(decision, record, eye):
    """Compare saved values; never use the nearest pose as a substitute."""
    problems = []
    if decision.get('schema') != 1:
        problems.append('unsupported_decision_schema')
    if not finite(decision.get('decisionTime')):
        problems.append('invalid_decision_time')
    for saved, context in (('participant', 'participantCode'), ('run', 'runNumber'),
                           ('sourceTrialId', 'sourceTrialId'), ('round', 'round'), ('block', 'block')):
        if decision.get(saved) != record.get(context):
            problems.append('replay_context_mismatch:' + saved)
    if not decision.get('observationAvailable'):
        if decision.get('gazeUsable'):
            problems.append('usable_without_observation')
        return problems
    if eye is None:
        problems.append('missing_exact_eye_observation')
        return problems
    for key in ('participant', 'run', 'sourceTrialId', 'round', 'block', 'source', 'phase'):
        if decision.get(key) != eye.get(key):
            problems.append('eye_context_mismatch:' + key)
    for saved, raw in (('gazeReceiptTime', 'receiptTime'), ('gazeInputEventTime', 'inputEventTime'),
                        ('maxDeliveryAgeSeconds', 'maxDeliveryAgeSeconds')):
        if not close(decision.get(saved), eye.get(raw)):
            problems.append('eye_value_mismatch:' + saved)
    if decision.get('gazeDeviceAvailable') != eye.get('deviceAvailable'):
        problems.append('eye_value_mismatch:gazeDeviceAvailable')
    now, delivered = decision.get('decisionTime'), eye.get('inputEventTime')
    known = bool(eye.get('deviceAvailable') and finite(delivered) and delivered > 0)
    if decision.get('gazeDeliveryTimeKnown') != known:
        problems.append('delivery_time_known_mismatch')
    if finite(now) and finite(eye.get('receiptTime')) and now < eye['receiptTime']-1e-6:
        problems.append('decision_before_observation')
    if known and finite(now) and not close(decision.get('gazeAgeAtDecision'), max(0, now-delivered)):
        problems.append('delivery_age_mismatch')
    if decision.get('gazeUsable'):
        max_age = eye.get('maxDeliveryAgeSeconds')
        if not eye.get('valid'):
            problems.append('usable_from_invalid_observation')
        if not (known and finite(now) and finite(max_age) and max_age > 0 and
                0 <= now-delivered <= max_age+1e-6):
            problems.append('usable_from_stale_or_unknown_delivery')
    return problems


def audit(eyes, replay):
    """Audit one replay; replay time is relative, eye/decision time is absolute.

    Latencies below subtract replay event times only. The commitment callback may
    run after its audio request in the same Unity frame, so source order alone is
    not a failed correction. Stable decisions are not evidence of spatial accuracy.
    """
    if not replay or replay[0].get('kind') != 'header':
        raise ValueError('Replay requires a header')
    supported = replay[0].get('guidanceAuditVersion') == CONTRACT
    snapshots, audio = defaultdict(list), defaultdict(list)
    issues, records = [], []
    def issue(code, record, decision_id=''):
        issues.append(dict(code=code, replay_sequence=record.get('sequence'), decision_id=decision_id))
    for record in replay:
        kind, identity = record.get('kind'), record.get('guidanceDecisionId')
        if kind == 'guidance_decision':
            decision = record.get('guidanceDecision')
            if not isinstance(decision, dict) or not identity or decision.get('decisionId') != identity:
                issue('missing_or_mismatched_decision_identity', record, identity)
                continue
            snapshots[identity].append((record, decision))
        elif identity:
            audio[identity].append(record)
        elif supported and kind in ('audio_request', 'audio_playback_start', 'audio_playback_end',
                                     'audio_cancelled', 'audio_failed', 'guidance_decision_aborted'):
            if (record.get('detail') or '').split(';', 1)[0] in ('tip', 'guidance_repeat'):
                issue('guidance_audio_missing_decision_id', record)
    for identity in audio.keys() - snapshots.keys():
        issue('audio_without_decision_snapshot', audio[identity][0], identity)
    for identity, entries in snapshots.items():
        requested = [(r, d) for r, d in entries if d.get('stage') == 'requested']
        committed = [(r, d) for r, d in entries if d.get('stage') == 'committed']
        events = audio[identity]
        starts = [r for r in events if r.get('kind') == 'audio_playback_start']
        ends = [r for r in events if r.get('kind') == 'audio_playback_end']
        cancelled = [r for r in events if r.get('kind') == 'audio_cancelled']
        aborted = [r for r in events if r.get('kind') == 'guidance_decision_aborted']
        if len(requested) > 1 or len(committed) > 1:
            issue('duplicate_decision_stage', entries[0][0], identity)
        if (starts or committed) and not requested:
            issue('playback_or_commit_without_request', entries[0][0], identity)
        if starts and aborted:
            issue('abandoned_correction_has_playback', starts[0], identity)
        if ends and not starts:
            issue('playback_end_without_start', ends[0], identity)
        if len(starts) > 1:
            issue('multiple_playbacks_for_decision', starts[1], identity)
        first_record, first = requested[0] if requested else entries[0]
        if requested and first.get('reason') in ('stable_wall_entry', 'stable_wall_exit') and starts and not committed:
            issue('correction_playback_without_revalidation', starts[0], identity)
        for record, decision in entries:
            key = (decision.get('eyeRecordingId'), decision.get('observationSequence'))
            eye = eyes.get(key)
            for problem in snapshot_issues(decision, record, eye):
                issue(problem, record, identity)
            if decision.get('stage') in ('requested', 'committed') and not decision.get('gazeUsable'):
                issue('requested_or_committed_without_usable_gaze', record, identity)
            if requested and decision.get('stage') == 'committed':
                for key in ('phrase', 'category'):
                    if decision.get(key) != first.get(key):
                        issue('commit_request_mismatch:' + key, record, identity)
        # Context must stay attached to the request even if playback occurs later.
        for event in events:
            for key in ('participantCode', 'runNumber', 'sourceTrialId', 'round', 'block'):
                if event.get(key) != first_record.get(key):
                    issue('audio_context_mismatch:' + key, event, identity)
            if event.get('kind') in ('audio_request', 'audio_playback_start') and event.get('text'):
                if event['text'] != first.get('phrase'):
                    issue('audio_phrase_mismatch', event, identity)
        latency = starts[0]['time']-first_record['time'] if starts and requested else None
        if latency is not None and latency < 0:
            issue('playback_before_decision_record', starts[0], identity)
        if starts and ends and ends[0]['time'] < starts[0]['time']:
            issue('playback_end_before_start', ends[0], identity)
        if starts:
            outcome = 'played_cancelled' if cancelled else 'played_ended' if ends else 'played_end_unobserved'
        else:
            outcome = ('abandoned_before_commit' if aborted else 'cancelled_before_playback' if cancelled
                       else 'requested_no_playback_observed' if requested else first.get('stage'))
        records.append(dict(decision_id=identity, participant_id=first.get('participant'),
                            run_number=first.get('run'), trial_id=first.get('sourceTrialId'),
                            round=first.get('round'), block=first.get('block'),
                            eye_recording_id=first.get('eyeRecordingId'),
                            observation_sequence=first.get('observationSequence'),
                            category=first.get('category'), phrase=first.get('phrase'), reason=first.get('reason'),
                            stage_count=len(entries), committed=bool(committed), outcome=outcome,
                            software_request_to_playback_seconds=latency,
                            acoustic_onset_status='not_measured',
                            issue_count=sum(i['decision_id'] == identity for i in issues)))
    return dict(report_version=VERSION, contract=supported, status='issues_found' if issues else
                'legacy_or_unknown_contract' if not supported else 'no_decisions' if not records else 'consistent',
                decisions=records, issues=issues, issue_counts=dict(Counter(i['code'] for i in issues)),
                replay_complete=replay[-1].get('kind') == 'end' and replay[-1].get('complete') is True,
                limits='Consistency only; no native rate, spatial correctness, fixation or acoustic onset validation.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--eye', type=Path, action='append', required=True, help='Raw gaze_observations.jsonl; repeat for multiple recordings')
    parser.add_argument('--replay', type=Path, action='append', required=True, help='Replay JSONL file; repeat per trial')
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    eyes, statuses, warnings = {}, [], []
    for path in args.eye:
        rows, recovered = read_recording(path)
        warnings.extend(recovered)
        statuses.append(dict(path=str(path), **recording_status(path, rows, recovered)))
        for row in rows:
            key = row['recordingId'], row['sequence']
            if key in eyes:
                parser.error('Duplicate raw observation identity in eye inputs')
            eyes[key] = row
    reports, decisions, issues = [], [], []
    for path in args.replay:
        replay = [r for _, r in replay_rows(path, warnings)]
        report = audit(eyes, replay)
        reports.append(dict(replay_file=str(path), **report))
        decisions.extend(dict(replay_file=str(path), **r) for r in report['decisions'])
        issues.extend(dict(replay_file=str(path), **r) for r in report['issues'])
    args.out.mkdir(parents=True, exist_ok=False)
    (args.out/'guidance_audit.json').write_text(json.dumps(dict(report_version=VERSION,
        eye_end_status=statuses, warnings=warnings, replays=reports), indent=2, allow_nan=False))
    for name, rows, empty_columns in [('guidance_decisions.csv', decisions, ['replay_file', 'decision_id', 'outcome']),
                                       ('guidance_issues.csv', issues, ['replay_file', 'replay_sequence', 'decision_id', 'code'])]:
        with (args.out/name).open('w', newline='') as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0]) if rows else empty_columns)
            writer.writeheader(); writer.writerows(rows)
    print(f'Wrote {args.out}: {len(decisions)} decisions, {len(issues)} consistency issues. Hardware validation remains separate.')
    return 1 if issues else 0


if __name__ == '__main__':
    raise SystemExit(main())
