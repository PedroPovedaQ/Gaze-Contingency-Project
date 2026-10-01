import copy
import csv
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from guidance_audit import audit, CONTRACT


def fixture():
    eye = dict(kind='gaze_observation', schema=1, recordingId='eyes', sequence=51,
               receiptTime=100., inputEventTime=99.99, maxDeliveryAgeSeconds=.1,
               deviceAvailable=True, valid=True, participant='p1', run=1,
               sourceTrialId='p1_run001_r00', round=0, block=0, source='EXT', phase='search')
    context = dict(schema=2, participantCode='p1', runNumber=1, sourceTrialId='p1_run001_r00',
                   round=0, block=0)
    decision = dict(schema=1, decisionId='a', stage='requested', reason='stable_direction',
                    category='Look left.', phrase='Look left.', eyeRecordingId='eyes', observationSequence=51,
                    participant='p1', run=1, sourceTrialId='p1_run001_r00', round=0, block=0, source='EXT',
                    phase='search', observationAvailable=True, gazeUsable=True, gazeDeviceAvailable=True,
                    gazeDeliveryTimeKnown=True, decisionTime=100.01, gazeReceiptTime=100.,
                    gazeInputEventTime=99.99, gazeAgeAtDecision=.02, maxDeliveryAgeSeconds=.1)
    replay = [dict(context, kind='header', time=0., sequence=0, guidanceAuditVersion=CONTRACT),
              dict(context, kind='guidance_decision', time=2., sequence=1,
                   guidanceDecisionId='a', guidanceDecision=decision),
              dict(context, kind='audio_request', time=2.001, sequence=2, guidanceDecisionId='a'),
              dict(context, kind='audio_playback_start', time=2.2, sequence=3, guidanceDecisionId='a'),
              dict(context, kind='audio_playback_end', time=2.9, sequence=4, guidanceDecisionId='a'),
              dict(context, kind='end', time=3., sequence=5, complete=True)]
    return {('eyes', 51): eye}, replay


class GuidanceAuditTests(unittest.TestCase):
    def test_exact_link_and_latency_respect_two_clock_origins(self):
        eyes, replay = fixture()
        report = audit(eyes, replay)
        self.assertEqual(report['status'], 'consistent', report['issues'])
        cue = report['decisions'][0]
        self.assertEqual(cue['observation_sequence'], 51)
        self.assertAlmostEqual(cue['software_request_to_playback_seconds'], .2)
        self.assertEqual(cue['outcome'], 'played_ended')
        self.assertEqual(cue['acoustic_onset_status'], 'not_measured')

    def test_nearest_eye_row_is_never_a_substitute(self):
        eyes, replay = fixture()
        eyes[('eyes', 52)] = eyes.pop(('eyes', 51))
        report = audit(eyes, replay)
        self.assertEqual(report['issue_counts'], {'missing_exact_eye_observation': 1})

    def test_stale_and_invalid_usable_claims_fail_even_with_saved_valid_flag(self):
        eyes, replay = fixture()
        decision = replay[1]['guidanceDecision']
        decision.update(decisionTime=100.3, gazeAgeAtDecision=.31)
        eyes[('eyes', 51)]['valid'] = False
        codes = audit(eyes, replay)['issue_counts']
        self.assertIn('usable_from_stale_or_unknown_delivery', codes)
        self.assertIn('usable_from_invalid_observation', codes)

    def test_trial_and_raw_value_mismatches_are_visible(self):
        eyes, replay = fixture()
        replay[1]['guidanceDecision']['gazeReceiptTime'] = 99.
        replay[3]['sourceTrialId'] = 'another-trial'
        eyes[('eyes', 51)]['participant'] = 'p2'
        codes = audit(eyes, replay)['issue_counts']
        self.assertIn('eye_value_mismatch:gazeReceiptTime', codes)
        self.assertIn('eye_context_mismatch:participant', codes)
        self.assertIn('audio_context_mismatch:sourceTrialId', codes)

    def test_correction_requires_revalidation_and_preserves_original_source(self):
        eyes, replay = fixture()
        replay[1]['guidanceDecision']['reason'] = 'stable_wall_entry'
        self.assertIn('correction_playback_without_revalidation', audit(eyes, replay)['issue_counts'])
        committed = copy.deepcopy(replay[1])
        committed.update(sequence=2.5, time=2.01)
        committed['guidanceDecision'].update(stage='committed', reason='wall_entry_revalidated')
        replay.insert(3, committed)
        report = audit(eyes, replay)
        self.assertEqual(report['issues'], [])
        self.assertTrue(report['decisions'][0]['committed'])
        # Commitment is allowed after audio_request in the same coroutine.
        self.assertEqual(report['decisions'][0]['observation_sequence'], 51)

    def test_requested_cancelled_and_abandoned_are_not_delivered(self):
        for kind, expected in [('audio_cancelled', 'cancelled_before_playback'),
                               ('guidance_decision_aborted', 'abandoned_before_commit')]:
            with self.subTest(kind=kind):
                eyes, replay = fixture()
                replay[3]['kind'] = kind
                del replay[4]
                report = audit(eyes, replay)
                self.assertEqual(report['issues'], [])
                cue = report['decisions'][0]
                self.assertEqual(cue['outcome'], expected)
                self.assertIsNone(cue['software_request_to_playback_seconds'])
        eyes, replay = fixture()
        replay.insert(4, dict(replay[3], kind='guidance_decision_aborted'))
        self.assertIn('abandoned_correction_has_playback', audit(eyes, replay)['issue_counts'])

    def test_suspension_with_no_observation_and_legacy_are_not_false_passes(self):
        eyes, replay = fixture()
        replay[1]['guidanceDecision'].update(stage='suspended', gazeUsable=False,
                                             observationAvailable=False, eyeRecordingId='', observationSequence=-1)
        del replay[2:5]
        report = audit(eyes, replay)
        self.assertEqual(report['issues'], [])
        self.assertEqual(report['decisions'][0]['outcome'], 'suspended')
        replay = [replay[0], replay[-1]]
        replay[0].pop('guidanceAuditVersion')
        self.assertEqual(audit(eyes, replay)['status'], 'legacy_or_unknown_contract')
        replay[0]['guidanceAuditVersion'] = CONTRACT
        self.assertEqual(audit(eyes, replay)['status'], 'no_decisions')

    def test_missing_audio_ids_and_orphans_are_flagged(self):
        eyes, replay = fixture()
        replay[2].pop('guidanceDecisionId')
        replay[2]['detail'] = 'tip;text=Look left.'
        replay[3]['guidanceDecisionId'] = 'unknown'
        codes = audit(eyes, replay)['issue_counts']
        self.assertIn('guidance_audio_missing_decision_id', codes)
        self.assertIn('audio_without_decision_snapshot', codes)

    def test_spoken_phrase_must_match_requested_phrase(self):
        eyes, replay = fixture()
        replay[3]['text'] = 'Look right.'
        self.assertIn('audio_phrase_mismatch', audit(eyes, replay)['issue_counts'])

    def test_cli_reports_interrupted_evidence_and_does_not_overwrite(self):
        eyes, replay = fixture()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            eye_file, replay_file = root/'gaze.jsonl', root/'replay.jsonl'
            eye_file.write_text(json.dumps(eyes[('eyes', 51)])+'\n')
            replay_file.write_text(''.join(json.dumps(r)+'\n' for r in replay[:-1]))
            command = [sys.executable, str(Path(__file__).with_name('guidance_audit.py')),
                       '--eye', str(eye_file), '--replay', str(replay_file), '--out', str(root/'audit')]
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            report = json.loads((root/'audit/guidance_audit.json').read_text())
            self.assertFalse(report['eye_end_status'][0]['complete'])
            self.assertFalse(report['replays'][0]['replay_complete'])
            self.assertTrue(report['warnings'])
            with (root/'audit/guidance_decisions.csv').open(newline='') as f:
                rows = list(csv.DictReader(f))
            self.assertEqual(rows[0]['outcome'], 'played_ended')
            before = (root/'audit/guidance_decisions.csv').read_bytes()
            self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)
            self.assertEqual((root/'audit/guidance_decisions.csv').read_bytes(), before)
            replay[3]['text'] = 'A contradictory phrase.'
            replay_file.write_text(''.join(json.dumps(r)+'\n' for r in replay))
            command[-1] = str(root/'bad-audit')
            failed = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(failed.returncode, 1, failed.stderr)
            report = json.loads((root/'bad-audit/guidance_audit.json').read_text())
            self.assertIn('audio_phrase_mismatch', report['replays'][0]['issue_counts'])


if __name__ == '__main__':
    unittest.main()
