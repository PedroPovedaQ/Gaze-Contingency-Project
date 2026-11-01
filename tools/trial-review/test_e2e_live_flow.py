"""Exercise live-mode orchestration offline, with real workers/detector and synthetic audio."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from e2e_live import LiveHarness, LiveProvider, FFMPEG
import participant_audio as audio


class LiveFlowChecks(unittest.TestCase):
    def test_happy_and_negative_checkpoint_use_one_clone(self):
        with tempfile.TemporaryDirectory(prefix='FMV-DEVELOPMENT-LIVE-FAKES-') as directory:
            root=Path(directory)
            clip=root/'fake-provider.mp3'
            subprocess.run([FFMPEG,'-v','error','-f','lavfi','-i','sine=frequency=440:duration=0.2',str(clip)],check=True,capture_output=True)
            mp3=clip.read_bytes()
            provider=LiveProvider(root/'receipt.json')
            clones=set()
            def fake_call(url,data=None,content_type=None,method=None):
                if '/text-to-speech/' in url:return mp3
                if url.endswith('/speech-to-text'):return json.dumps({'text':provider.text}).encode()
                if url.endswith('/voices/add'):
                    self.assertIn(b'DEV-E2E-',data)
                    clones.add('development_clone_123')
                    return b'{"voice_id":"development_clone_123"}'
                if method=='DELETE':
                    clones.remove(url.rsplit('/',1)[1]);return b'{"status":"ok"}'
                raise AssertionError('Unexpected fake provider request '+url)
            def fake_synthesize(job,source,keys,voice):
                if not source.exists():source.write_bytes(mp3)
            # No credential reads, HTTP, microphone, headset, or OS audio playback.
            original=subprocess.run
            with patch.object(provider,'call',side_effect=fake_call),patch.object(audio,'synthesize',side_effect=fake_synthesize),patch('subprocess.run',wraps=subprocess.run) as process:
                def without_playback(args,**kwargs):
                    if args[0]=='/usr/bin/afplay':return subprocess.CompletedProcess(args,0)
                    return original(args,**kwargs)
                process.side_effect=without_playback
                harness=LiveHarness(root,provider)
                harness.live_happy()
                self.assertEqual(len(clones),1)
                harness.live_negative()
            self.assertEqual(clones,set())
            self.assertEqual(provider.result['cloneAttempts'],1)
            self.assertEqual(len(provider.result['promptGenerations']),9)
            self.assertTrue(provider.result['happyEvidence']['cacheDuringReview'])
            self.assertEqual(provider.result['happyEvidence']['cacheClips'],20)
            self.assertEqual(len(provider.result['playbacks']),20)


if __name__=='__main__':unittest.main()
