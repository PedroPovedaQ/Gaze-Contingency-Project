"""Exercise real FFmpeg merging with synthetic, non-private media."""
import array
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('record_vr', Path(__file__).resolve().parents[1] / 'record-vr.py')
recorder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(recorder)


@unittest.skipUnless(shutil.which('ffmpeg') and shutil.which('ffprobe'), 'FFmpeg required')
class MergeTests(unittest.TestCase):
    def test_offsets_duration_and_raw_preservation(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            def generate(*args):
                subprocess.run(['ffmpeg', '-v', 'error', *args], check=True)
            generate('-f', 'lavfi', '-i', 'color=c=blue:s=64x64:r=10:d=2', '-c:v', 'libx264', str(folder / 'screen.mkv'))
            generate('-f', 'lavfi', '-i', 'sine=frequency=440:duration=1', '-c:a', 'pcm_s16le', str(folder / 'commentary.wav'))
            recorder.save(folder / 'capture.json', dict(video_launch_unix=100, microphone_launch_unix=100.5, recording_end_unix=103))
            original = (folder / 'commentary.wav').read_bytes()
            output = recorder.merge(folder)
            metadata = recorder.probe(output)
            self.assertAlmostEqual(float(metadata['format']['duration']), 3, delta=.15)
            self.assertEqual({stream['codec_type'] for stream in metadata['streams']}, {'video', 'audio'})
            def samples(path):
                raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(path), '-map', '0:a:0', '-f', 'f32le', '-ac', '1', '-ar', '8000', '-'])
                result = array.array('f'); result.frombytes(raw)
                return result
            audio = samples(output)
            self.assertLess(max(abs(v) for v in audio[:2400]), .001)
            self.assertGreater(max(abs(v) for v in audio[5600:7200]), .01)
            adjusted = recorder.merge(folder, -.75)
            self.assertNotEqual(output, adjusted)
            self.assertTrue(output.exists())
            self.assertGreater(max(abs(v) for v in samples(adjusted)[400:1600]), .01)
            self.assertEqual((folder / 'commentary.wav').read_bytes(), original)


if __name__ == '__main__':
    unittest.main()
