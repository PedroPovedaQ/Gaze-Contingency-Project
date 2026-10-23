from pathlib import Path
import struct
import tempfile
import unittest
import wave

from replay_assets import asset_path, inspect_audio_assets, inspect_wav


def make_wav(path, channels=1, rate=24000):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as file:
        file.setnchannels(channels); file.setsampwidth(2); file.setframerate(rate)
        file.writeframes(struct.pack('<h',1000)*240*channels)


class ReplayAssetTests(unittest.TestCase):
    def test_pcm_shape_and_repeated_references_are_inspected_once(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);path=root/'audio/clip_0000.wav';make_wav(path,channels=2)
            rows=[dict(kind='audio_playback_start',sequence=i,clip='audio/clip_0000.wav',audioVolume=.7) for i in (2,9)]
            seen=[];report=inspect_audio_assets(root/'recording.jsonl',rows,on_file=seen.append)
            self.assertEqual(report['issues'],[]);self.assertEqual(report['incomplete'],[])
            self.assertEqual(seen,[path.resolve()]);self.assertEqual(report['files'],[path.resolve()])
            asset=report['assets'][0]
            self.assertEqual(asset['status'],'consistent_pcm16');self.assertEqual(asset['playback_references'],2)
            self.assertEqual(asset['event_sequences'],[2,9]);self.assertEqual(asset['frames'],240)
            self.assertEqual(asset['channels'],2);self.assertEqual(asset['sample_rate_hz'],24000)
            self.assertAlmostEqual(asset['duration_seconds'],.01)

    def test_traversal_absolute_links_and_segments_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);package=root/'package';package.mkdir();outside=root/'outside.wav';make_wav(outside)
            (package/'linked.wav').symlink_to(outside)
            (package/'linked-directory').symlink_to(root,target_is_directory=True)
            for relative in ('../outside.wav',str(outside),'C:/outside.wav','audio\\clip.wav','./outside.wav','audio//clip.wav','linked.wav','linked-directory/outside.wav'):
                with self.subTest(relative=relative):
                    with self.assertRaises(ValueError):asset_path(package,relative)
                    report=inspect_audio_assets(package/'recording.jsonl',[dict(kind='audio_playback_start',sequence=1,clip=relative)])
                    self.assertEqual(report['files'],[])
                    self.assertEqual(report['issues'][0]['code'],'unsafe_audio_asset_reference')

    def test_missing_audio_and_recorder_failures_remain_incomplete(self):
        with tempfile.TemporaryDirectory() as directory:
            rows=[dict(kind='audio_playback_start',sequence=1,clip='audio/missing.wav'),
                  dict(kind='audio_playback_start',sequence=2,clip='',detail='tip;audio_asset_missing=write failed')]
            report=inspect_audio_assets(Path(directory)/'recording.jsonl',rows)
            self.assertEqual(report['issues'],[])
            self.assertEqual({r['code'] for r in report['incomplete']},
                             {'missing_replay_audio_file','playback_without_saved_audio','recorder_reported_audio_asset_failure'})
            self.assertEqual(report['assets'][0]['status'],'missing')

    def test_corruption_empty_audio_and_size_fields_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'clip.wav';make_wav(path);valid=path.read_bytes()
            changes=[valid[:20],valid[:-2],valid+b'junk',b'NOTW'+valid[4:]]
            for offset,value in [(4,0),(16,17),(20,3),(22,3),(24,100),(28,1),(32,1),(34,8),(40,0)]:
                bad=bytearray(valid);fmt='<H' if offset in (20,22,32,34) else '<I'
                struct.pack_into(fmt,bad,offset,value);changes.append(bytes(bad))
            for changed in changes:
                path.write_bytes(changed)
                with self.assertRaises(ValueError):inspect_wav(path)
            make_wav(path,rate=192000);self.assertEqual(inspect_wav(path)['sample_rate_hz'],192000)

    def test_every_playback_checks_volume_and_typed_reference(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);make_wav(root/'clip.wav')
            rows=[dict(kind='audio_playback_start',sequence=1,clip='clip.wav',audioVolume=.7),
                  dict(kind='audio_playback_start',sequence=2,clip='clip.wav',audioVolume=2),
                  dict(kind='audio_playback_start',sequence=3,clip=False)]
            report=inspect_audio_assets(root/'recording.jsonl',rows)
            self.assertEqual([r['code'] for r in report['issues']],['invalid_recorded_audio_volume','invalid_audio_asset_reference'])

    def test_fingerprint_callback_runs_before_header_parse(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);path=root/'clip.wav';make_wav(path)
            def mutate(file): file.write_bytes(b'bad')
            report=inspect_audio_assets(root/'recording.jsonl',[dict(kind='audio_playback_start',sequence=1,clip='clip.wav')],on_file=mutate)
            self.assertEqual(report['issues'][0]['code'],'invalid_replay_audio_file')
