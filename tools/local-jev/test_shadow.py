"""Read-only replay shadowing: joins, incremental reads, and failure accounting."""
import copy
import json
from pathlib import Path
import random
import tempfile
import unittest

from continuation_data import example
from shadow import Shadow


class Policy:
    def __init__(self): self.calls = []

    def predict(self, payload, guard=True):
        self.calls.append(copy.deepcopy(payload))
        return {'raw_choice': 'orient', 'source': 'model',
                'answers': {'navigation_action': {'type': 'choice', 'choice': 'refine'}}}


def trace(stage, **fields):
    return {'kind': 'jev_trace', 'trialId': 'trial-1', 'time': 12,
            'detail': 'jev;' + json.dumps(dict(schema='jev-trace-v1', request_id='same-id', stage=stage, **fields))}


class ShadowChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.recording = self.root/'input/GazeReplays/run-one/recording.jsonl'
        self.recording.parent.mkdir(parents=True)
        self.recording.touch()
        self.policy = Policy()
        self.shadow = Shadow(self.root/'input', self.root/'output', self.policy, 'model-hash', from_start=True)
        self.payload = example(random.Random(2), 1, 'train')['payload']
        self.payload['questions']['navigation_action']['criteria'] = {
            'refine': {'text': 'Look left.', 'locally_eligible': True}, 'wait': {'text': None}}

    def tearDown(self):
        self.shadow.close()
        self.temp.cleanup()

    def append(self, *rows, path=None):
        with (path or self.recording).open('a') as f:
            for row in rows: f.write(json.dumps(row)+'\n')

    def request(self): return trace('request', payload=self.payload, deadline_ms=450)

    def response(self):
        return trace('response', status='ok', elapsed_ms=200,
                     answers={'navigation_action': {'type': 'choice', 'choice': 'refine'}})

    def test_response_before_request_and_duplicate_are_joined_once(self):
        self.append(self.response(), self.request(), self.request())
        self.shadow.poll()
        report = self.shadow.report()
        self.assertEqual(len(self.policy.calls), 1)
        self.assertNotIn('answers', self.policy.calls[0])
        self.assertEqual(report['comparable'], 1)
        self.assertEqual(report['masked_matches'], 1)
        self.assertEqual(report['raw_matches'], 0)
        self.assertEqual(report['gated_matches'], 1)
        self.assertEqual(report['jev_latency_ms']['median'], 200)

    def test_active_local_traces_are_not_counted_as_cloud_comparisons(self):
        self.append(trace('request', provider='local_minilm', payload=self.payload),
                    trace('response', provider='local_minilm', status='ok', answers={}))
        self.shadow.poll()
        self.assertEqual(self.policy.calls, [])
        self.assertEqual(self.shadow.report()['comparable'], 0)

    def test_partial_line_restart_and_session_identity(self):
        raw = json.dumps(self.request()).encode()
        self.recording.write_bytes(raw[:30])
        self.shadow.poll()
        self.assertFalse(self.policy.calls)
        with self.recording.open('ab') as f: f.write(raw[30:]+b'\n')
        self.shadow.poll()
        self.shadow.close()
        self.shadow = Shadow(self.root/'input', self.root/'output', self.policy, 'model-hash', from_start=True)
        self.append(self.response())
        second = self.recording.parent.parent/'run-two/recording.jsonl'
        second.parent.mkdir(); second.touch()
        self.append(self.request(), self.response(), path=second)
        self.shadow.poll()
        self.assertEqual(len(self.policy.calls), 2)
        self.assertEqual(self.shadow.report()['comparable'], 2)

    def test_unsupported_failure_and_discard_not_counted_as_agreement(self):
        self.payload['questions'] = {'transmit': {}}
        self.append(self.request(), self.response())
        self.shadow.poll()
        self.assertEqual(self.shadow.report()['unsupported'], 1)
        self.assertFalse(self.policy.calls)
        self.assertEqual(self.shadow.report()['comparable'], 0)

    def test_closed_gate_is_visible_separately_from_model(self):
        self.payload['state']['runtime']['evidence']['valid'] = False
        self.append(self.request(), trace('response', status='timeout', elapsed_ms=450),
                    trace('application', outcome='discarded_stale'))
        self.shadow.poll()
        item = self.shadow.decisions()[0]
        self.assertEqual(item['local']['masked_choice'], 'refine')
        self.assertEqual(item['local']['gated_choice'], 'wait')
        self.assertEqual(item['application'], 'discarded_stale')
        self.assertEqual(self.shadow.report()['comparable'], 0)
        self.assertEqual(self.shadow.report()['jev_statuses'], {'timeout': 1})

    def test_error_does_not_prevent_later_response(self):
        def fail(*args, **kwargs): raise RuntimeError('sensitive provider detail')
        self.policy.predict = fail
        self.append(self.request(), self.response())
        self.shadow.poll()
        self.assertEqual(self.shadow.report()['local_errors'], 1)
        self.assertNotIn('sensitive', json.dumps(self.shadow.decisions()))

    def test_from_now_skips_old_bytes_including_partial_record(self):
        self.shadow.close()
        self.recording.write_text(json.dumps(self.request())[:-1])
        self.shadow = Shadow(self.root/'input', self.root/'live-output', self.policy, 'model-hash')
        self.shadow.poll()
        with self.recording.open('a') as f: f.write('}\n')
        self.append(self.request(), self.response())
        self.shadow.poll()
        self.assertEqual(len(self.policy.calls), 1)

    def test_model_mismatch_and_source_truncation_refuse_resume(self):
        self.append(self.request()); self.shadow.poll()
        self.recording.write_bytes(b'')
        with self.assertRaisesRegex(ValueError, 'shrank'): self.shadow.poll()
        self.shadow.close()
        with self.assertRaisesRegex(ValueError, 'different'):
            Shadow(self.root/'input', self.root/'output', self.policy, 'other-model')

    def test_exports_do_not_modify_recordings_and_include_late_playback(self):
        self.append(self.request(), self.response())
        self.shadow.poll()
        self.append({'kind': 'audio_playback_start', 'guidanceDecisionId': 'same-id'})
        before = self.recording.read_bytes()
        self.shadow.poll()
        report = self.shadow.export('observing')
        self.assertEqual(report['playback_observed'], 1)
        self.assertEqual(self.recording.read_bytes(), before)
        self.assertEqual(json.loads((self.root/'output/summary.json').read_text())['comparable'], 1)
        self.assertEqual((self.root/'output/decisions.json').stat().st_mode & 0o777, 0o600)

    def test_output_cannot_write_into_source_and_orphans_are_explicit(self):
        with self.assertRaisesRegex(ValueError, 'read-only'):
            Shadow(self.root/'input', self.root/'input/results', self.policy, 'model-hash')
        self.append(self.response()); self.shadow.poll()
        self.assertEqual(self.shadow.report()['orphan_responses'], 1)
        self.assertEqual(self.shadow.report()['comparable'], 0)


if __name__ == '__main__': unittest.main()
