import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from live_bridge import Mirror, Receiver, safe_relative, main


class Remote:
    def __init__(self):
        self.files = {}
        self.fail = False
        self.endpoint = '127.0.0.1:5555'

    def inventory(self):
        return {p: (len(data), len(data)) for p, data in self.files.items()}

    def read(self, path, offset, count):
        if self.fail:
            raise OSError('wireless disconnected')
        return self.files[path][offset:offset + count]

    def digest(self, path):
        return hashlib.sha256(self.files[path]).hexdigest()


class LiveBridgeTests(unittest.TestCase):
    def test_initially_offline_headset_retries_before_copying(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); remote.connects = 0
            def connect():
                remote.connects += 1
                if remote.connects == 1: raise OSError('headset asleep')
            remote.connect = connect
            remote.command = lambda *args: b'test-headset'
            real_cycle = Receiver.cycle
            def cycle(receiver):
                real_cycle(receiver)
                self.assertEqual(remote.connects, 2)
                self.assertTrue((Path(d)/'headset.json').exists())
                (Path(d)/'stop-request').write_text('test finished')
            with patch('sys.argv', ['live_bridge', '--output', d, '--endpoint', remote.endpoint]), \
                 patch('live_bridge.Adb', return_value=remote), patch('live_bridge.time.sleep'), \
                 patch.object(Receiver, 'cycle', cycle):
                main()
            self.assertEqual(remote.connects, 2)

    def test_archive_catchup_and_other_files_do_not_claim_live_headset_signal(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            receiver = Receiver(Path(d), remote)
            def status(): return json.loads((Path(d)/'live-status.json').read_text())
            with patch('live_bridge.time.time', return_value=100):
                receiver.cycle()
                self.assertEqual(status().get('replayUpdatedUtc'), 0, 'first archive copy is not live activity')
            remote.files[name] += b'{"kind":"sample"}\n'
            with patch('live_bridge.time.time', return_value=101):
                receiver.cycle()
                self.assertEqual(status()['replayUpdatedUtc'], 101)
                self.assertEqual(status()['replayPendingBytes'], 0)
            remote.files['GazeReplays/run/audio/new.wav'] = b'late audio'
            with patch('live_bridge.time.time', return_value=110):
                receiver.cycle()
                self.assertEqual(status()['replayUpdatedUtc'], 101, 'late audio must not revive a paused headset')
            receiver = Receiver(Path(d), remote); receiver.cycle()
            self.assertEqual(status()['replayUpdatedUtc'], 0, 'restart must re-establish fresh source activity')

    def test_new_runs_links_raw_eye_audio_and_restart_catchup(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote()
            remote.files['GazeReplays/old/recording.jsonl'] = b'{"kind":"header"}\n'
            def add_run(run, eye):
                records = [dict(kind='header', gazeRecordingPath='/device/files/EyeMeasurements/'+eye+'/gaze_observations.jsonl'),
                           dict(kind='trial', participantCode='P20', sourceRunFolder=run)]
                remote.files[f'GazeReplays/{run}/recording.jsonl'] = b''.join(json.dumps(r).encode()+b'\n' for r in records)
                remote.files[f'GazeReplays/{run}/audio/clip.wav'] = b'fixture audio'
                remote.files[f'GazeData/P20/{run}/trial_events.csv'] = b'time,event\n1,start\n'
                remote.files[f'EyeMeasurements/{eye}/gaze_observations.jsonl'] = b'{"raw":true}\n'
            add_run('run1', 'eye1')
            receiver = Receiver(Path(d), remote); receiver.cycle()
            self.assertFalse((Path(d)/'data/GazeReplays/old').exists())
            for name, data in remote.files.items():
                if '/old/' not in name: self.assertEqual((Path(d)/'data'/name).read_bytes(), data)
            # A run started while the desktop receiver was closed must be recovered.
            add_run('run2', 'eye2')
            receiver = Receiver(Path(d), remote); receiver.cycle()
            for name, data in remote.files.items():
                if '/old/' not in name: self.assertEqual((Path(d)/'data'/name).read_bytes(), data)
            status = json.loads((Path(d)/'live-status.json').read_text())
            self.assertEqual(status['state'], 'connected')
            self.assertIn('/run2/', status['recording'])
            self.assertEqual(status['pendingBytes'], 0)

    def test_resume_and_verify_exact_bytes_after_disconnect_and_restart(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote()
            name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            mirror = Mirror(Path(d), remote)
            mirror.copy(name, len(remote.files[name]), 1)
            remote.files[name] += b'{"kind":"sample"}\n'
            remote.fail = True
            with self.assertRaises(OSError):
                mirror.copy(name, len(remote.files[name]), 2)
            remote.fail = False
            mirror = Mirror(Path(d), remote)
            mirror.copy(name, len(remote.files[name]), 2)
            self.assertEqual((Path(d)/name).read_bytes(), remote.files[name])
            self.assertTrue(mirror.verify(name, len(remote.files[name]), 2))

    def test_partial_transfer_does_not_publish_or_advance_offset(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'abc'
            mirror = Mirror(Path(d), remote)
            with self.assertRaises(OSError): mirror.copy(name, 10, 1)
            self.assertFalse((Path(d)/name).exists())
            remote.files[name] = b'0123456789'
            mirror.copy(name, 10, 1)
            self.assertEqual((Path(d)/name).read_bytes(), b'0123456789')

    def test_rewritten_summary_is_replaced_and_changed_hash_is_not_verified(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeData/P20/run/trial_summary.json'
            remote.files[name] = b'{"n":1}'
            mirror = Mirror(Path(d), remote); mirror.copy(name, 7, 1)
            remote.files[name] = b'{"n":2}'
            mirror.copy(name, 7, 2)
            self.assertEqual((Path(d)/name).read_bytes(), b'{"n":2}')
            remote.files[name] = b'{"n":3}'
            self.assertFalse(mirror.verify(name, 7, 2))

    def test_truncated_source_preserves_local_evidence(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'EyeMeasurements/run/gaze_observations.jsonl'
            remote.files[name] = b'original\n'
            mirror = Mirror(Path(d), remote); mirror.copy(name, 9, 1)
            remote.files[name] = b'new\n'
            with self.assertRaises(ValueError): mirror.copy(name, 4, 2)
            self.assertEqual((Path(d)/name).read_bytes(), b'original\n')

    def test_paths_cannot_escape_archive(self):
        for path in ['../secret', '/etc/passwd', 'GazeReplays/../secret', 'tts_cache/clone.wav', 'GazeData/x\ny']:
            with self.assertRaises(ValueError): safe_relative(path)
        with tempfile.TemporaryDirectory() as d, tempfile.TemporaryDirectory() as outside:
            (Path(d)/'GazeData').symlink_to(outside)
            remote = Remote(); remote.files['GazeData/test.csv'] = b'data'
            with self.assertRaises(ValueError): Mirror(Path(d),remote).copy('GazeData/test.csv',4,1)


if __name__ == '__main__': unittest.main()
