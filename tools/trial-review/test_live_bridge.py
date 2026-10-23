import hashlib
import gzip
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch

from live_bridge import Mirror, Receiver, safe_relative, main, LINK_SCAN_CHUNK, Adb, validate_headset_state


class Remote:
    def __init__(self):
        self.files = {}
        self.fail = False
        self.endpoint = '127.0.0.1:5555'

    def inventory(self):
        return {p: (len(data), len(data)) for p, data in self.files.items()}

    def replay_inventory(self):
        return {p: (len(data), len(data)) for p, data in self.files.items()
                if p.startswith('GazeReplays/') and p.endswith('/recording.jsonl')}

    def read(self, path, offset, count):
        if self.fail:
            raise OSError('wireless disconnected')
        return self.files[path][offset:offset + count]

    def digest(self, path):
        return hashlib.sha256(self.files[path]).hexdigest()


class LiveBridgeTests(unittest.TestCase):
    def test_preparation_heartbeat_requires_change_and_expires_without_refresh(self):
        from concurrent.futures import Future
        with tempfile.TemporaryDirectory() as d:
            receiver=Receiver(Path(d),Remote())
            value={'schema':1,'sessionId':'a','phase':'preparing','progressKnown':True,
                   'progress01':.4,'updatedUtc':10,'revision':1}
            def complete(v):
                receiver.headset_future=Future(); receiver.headset_future.set_result(v)
                receiver.poll_headset_state()
            with patch('live_bridge.time.time',return_value=100): complete(value)
            self.assertEqual(receiver.headset_observed,0,'a stale file on first join is not live evidence')
            with patch('live_bridge.time.time',return_value=101): complete(dict(value,updatedUtc=11,revision=2))
            self.assertEqual(receiver.headset_observed,101)
            with patch('live_bridge.time.time',return_value=110): complete(dict(value,updatedUtc=11,revision=2))
            self.assertEqual(receiver.headset_observed,101,'unchanged cached file does not refresh liveness')
            complete(dict(value,phase='ready',updatedUtc=12,revision=3))
            self.assertEqual(receiver.headset_state['phase'],'ready')
            complete(None); self.assertIsNone(receiver.headset_state)

    def test_preparation_payload_is_bounded_and_not_archived(self):
        remote=Adb('adb','127.0.0.1:5555','test.app')
        value={'schema':1,'sessionId':'a','phase':'preparing','progressKnown':True,
               'progress01':.25,'updatedUtc':10,'revision':1,'secret':'do-not-forward'}
        with patch.object(remote,'command',return_value=json.dumps(value).encode()):
            self.assertNotIn('secret',remote.headset_state())
        with patch.object(remote,'command',return_value=b'x'*4097):
            with self.assertRaises(ValueError): remote.headset_state()
        for changes in ({'progress01':float('nan')},{'progress01':2},{'phase':'invented'}):
            with self.assertRaises(ValueError): validate_headset_state(dict(value,**changes))
        self.assertEqual(remote.parse_inventory(('10|1|'+remote.root+'PreparationLogs/live-headset-state.json').encode()),{})

    def test_large_text_transport_compresses_without_changing_saved_bytes(self):
        remote = Adb('adb', '127.0.0.1:5555', 'test.app'); remote.compress_text = True
        data = b'{"kind":"sample"}\n' * 70000
        with patch.object(remote, 'command', return_value=gzip.compress(data)) as command:
            self.assertEqual(remote.read('GazeReplays/run/recording.jsonl', 123, len(data)), data)
            self.assertIn('skip=123', command.call_args.args[1])
            self.assertTrue(command.call_args.args[1].endswith(' | gzip -1'))
        with patch.object(remote, 'command', return_value=b'raw') as command:
            self.assertEqual(remote.read('GazeReplays/run/recording.jsonl', 0, 3), b'raw')
            self.assertNotIn('gzip', command.call_args.args[1])
            remote.read('GazeReplays/run/audio/clip.wav', 0, len(data))
            self.assertNotIn('gzip', command.call_args.args[1])
        with patch.object(remote, 'command', return_value=b'broken gzip'):
            with self.assertRaises(OSError): remote.read('GazeReplays/run/recording.jsonl', 0, len(data))
        remote.compress_text = False
        with patch.object(remote, 'command', return_value=data) as command:
            self.assertEqual(remote.read('GazeReplays/run/recording.jsonl', 0, len(data)), data)
            self.assertNotIn('gzip', command.call_args.args[1])

    def test_active_replay_does_not_rescan_the_entire_archive_each_tick(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            with patch.object(remote, 'inventory', wraps=remote.inventory) as inventory, \
                 patch('live_bridge.time.monotonic', return_value=0):
                receiver = Receiver(Path(d), remote); receiver.cycle()
                remote.files[name] += b'{"kind":"sample"}\n'
                receiver.cycle()
                self.assertEqual(inventory.call_count, 1, 'live replay must not wait on the full archive scan every tick')
                self.assertEqual((Path(d)/'data'/name).read_bytes(), remote.files[name])

    def test_slow_hash_does_not_block_new_live_samples(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            release, entered, finished = threading.Event(), threading.Event(), threading.Event()
            original = remote.digest
            def slow_digest(path):
                entered.set(); release.wait(5); return original(path)
            remote.digest = slow_digest
            receiver = Receiver(Path(d), remote)
            with patch('live_bridge.time.monotonic', return_value=0): receiver.cycle()
            def verify_cycle():
                try:
                    with patch('live_bridge.time.monotonic', return_value=4): receiver.cycle()
                finally: finished.set()
            worker = threading.Thread(target=verify_cycle)
            worker.start()
            try:
                self.assertTrue(entered.wait(2), 'fixture reaches slow verification')
                self.assertTrue(finished.wait(.3), 'verification must not hold up live polling')
                remote.files[name] += b'{"kind":"sample"}\n'
                receiver.cycle()
                self.assertEqual((Path(d)/'data'/name).read_bytes(), remote.files[name])
            finally:
                release.set(); worker.join(3)
                if hasattr(receiver, 'close'): receiver.close()

    def test_slow_archive_scan_does_not_block_new_session_discovery(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            receiver = Receiver(Path(d), remote)
            with patch('live_bridge.time.monotonic', return_value=0): receiver.cycle()
            release, entered = threading.Event(), threading.Event()
            original = remote.inventory
            def slow_inventory():
                entered.set(); release.wait(5); return original()
            remote.inventory = slow_inventory
            new = 'GazeReplays/run2/recording.jsonl'
            remote.files[new] = b'{"kind":"header"}\n'
            try:
                with patch('live_bridge.time.monotonic', return_value=4): receiver.cycle()
                self.assertTrue(entered.wait(1))
                self.assertFalse(release.is_set())
                self.assertEqual((Path(d)/'data'/new).read_bytes(), remote.files[new])
                self.assertFalse(receiver.inventory_future.done())
            finally:
                release.set(); receiver.close()

    def test_background_hash_does_not_certify_a_newer_file_version(self):
        from concurrent.futures import Future
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            receiver = Receiver(Path(d), remote); receiver.cycle()
            old = remote.inventory()[name]
            result = Future(); result.set_result(('old-digest', 'old-digest'))
            remote.files[name] += b'{"kind":"sample"}\n'
            receiver.mirror.copy(name, *remote.inventory()[name])
            receiver.verification = (name, old, result)
            receiver.finish_verification()
            self.assertFalse(receiver.mirror.entries[name]['verified'])

    def test_historical_link_scan_is_bounded_and_resumes_without_file_growth(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); name = 'GazeReplays/run/recording.jsonl'
            filler = b'{"kind":"sample"}\n' * (LINK_SCAN_CHUNK // 18 + 10)
            remote.files[name] = filler + b'{"kind":"trial_begin","participantCode":"p1","sourceRunFolder":"linked"}\n'
            receiver = Receiver(Path(d), remote)
            with patch('live_bridge.time.monotonic', return_value=0):
                receiver.cycle()
                self.assertLessEqual(receiver.parsed[name], LINK_SCAN_CHUNK)
                self.assertNotIn('GazeData/p1/linked', receiver.selected)
                receiver.cycle()
                self.assertIn('GazeData/p1/linked', receiver.selected)
                self.assertEqual((Path(d)/'data'/name).read_bytes(), remote.files[name])
            with patch('live_bridge.time.monotonic', return_value=2): receiver.cycle()
            resumed = Receiver(Path(d), remote)
            self.assertEqual(resumed.parsed[name], len(remote.files[name]))
            self.assertIn('GazeData/p1/linked', resumed.selected)

    def test_library_transfer_yields_to_new_playback_events(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); elapsed = [0.0]; reads = []
            name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            for i in range(10): remote.files[f'GazeReplays/run/audio/{i}.wav'] = b'audio'
            read = remote.read
            def slow_read(path, offset, count):
                reads.append(path); elapsed[0] += .2
                return read(path, offset, count)
            remote.read = slow_read
            with patch('live_bridge.time.monotonic', side_effect=lambda: elapsed[0]):
                receiver = Receiver(Path(d), remote); receiver.cycle()
                self.assertEqual(len(reads), 2, 'large library yields after bounded copying batch')
                reads.clear(); remote.files[name] += b'{"kind":"audio_playback_start"}\n'
                receiver.cycle()
                self.assertEqual(reads[0], name, 'new events take priority over remaining clips')

    def test_spoken_clip_overtakes_unused_library_in_same_live_cycle(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote(); elapsed = [0.0]; reads = []
            name = 'GazeReplays/run/recording.jsonl'
            remote.files[name] = b'{"kind":"header"}\n'
            for i in range(500): remote.files[f'GazeReplays/run/audio/{i}.wav'] = b'audio'
            read = remote.read
            def slow_read(path, offset, count):
                reads.append(path); elapsed[0] += .3
                return read(path, offset, count)
            remote.read = slow_read
            with patch('live_bridge.time.monotonic', side_effect=lambda: elapsed[0]):
                receiver = Receiver(Path(d), remote); receiver.cycle()
                reads.clear()
                remote.files[name] += b'{"kind":"audio_playback_start","clip":"audio/499.wav"}\n'
                receiver.cycle()
                self.assertEqual(reads[:2], [name, 'GazeReplays/run/audio/499.wav'],
                                 'heard speech must not wait behind 499 unused clips, even when replay consumes the tick budget')
                self.assertTrue((Path(d)/'data/GazeReplays/run/audio/499.wav').is_file())
                receiver.close()

    def test_prepared_audio_is_copied_before_trial_and_bulk_data(self):
        with tempfile.TemporaryDirectory() as d:
            remote = Remote()
            remote.files['GazeReplays/run/recording.jsonl'] = b''
            remote.files['EyeMeasurements/eye/raw.jsonl'] = b'bulk\n'
            remote.files['GazeReplays/run/audio/clip.wav'] = b'prepared audio'
            reads = []
            read = remote.read
            remote.read = lambda name, offset, count: (reads.append(name), read(name, offset, count))[1]
            receiver = Receiver(Path(d), remote); receiver.cycle()
            self.assertLess(reads.index('GazeReplays/run/audio/clip.wav'), reads.index('EyeMeasurements/eye/raw.jsonl'))
            self.assertEqual((Path(d)/'data/GazeReplays/run/audio/clip.wav').read_bytes(), b'prepared audio')
            reads.clear()
            remote.files['GazeReplays/run/recording.jsonl'] = b'{"kind":"header"}\n'
            receiver.cycle()
            self.assertNotIn('GazeReplays/run/audio/clip.wav', reads, 'playback reuses already copied assets')

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

    @patch('live_bridge.time.monotonic', return_value=0)
    def test_new_runs_links_raw_eye_audio_and_restart_catchup(self, _clock):
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
