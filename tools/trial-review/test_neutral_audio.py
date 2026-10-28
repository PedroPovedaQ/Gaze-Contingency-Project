import hashlib
import io
import json
from pathlib import Path
import struct
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import neutral_audio as audio


class NeutralAudioTests(unittest.TestCase):
    def catalog(self):
        return dict(schema=1, voiceId=audio.VOICE, model=audio.MODEL,
                    phrases=[dict(file='tts_'+'a'*64+'.mp3', request=json.dumps(dict(text='Look left.', model_id=audio.MODEL)))])

    def test_catalog_identity_and_path(self):
        self.assertEqual(len(audio.catalog_jobs(self.catalog())), 1)
        for change in (dict(voiceId='clone'), dict(model='other'), dict(schema=2)):
            with self.assertRaises(ValueError): audio.catalog_jobs(dict(self.catalog(), **change))
        catalog = self.catalog(); catalog['phrases'][0]['file'] = '../secret'
        with self.assertRaises(ValueError): audio.catalog_jobs(catalog)
        catalog = self.catalog(); catalog['phrases'] *= 2
        with self.assertRaises(ValueError): audio.catalog_jobs(catalog)

    def test_decode_reuse_and_invalidation(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder)/'voice.mp3'; source.write_bytes(b'original')
            pcm = struct.pack('<4410f', *([.1]*4410))
            with patch.object(audio.subprocess, 'run') as process:
                process.return_value.stdout = pcm
                self.assertTrue(audio.decode(source, 'ffmpeg'))
                self.assertFalse(audio.decode(source, 'ffmpeg'))
                self.assertEqual(process.call_count, 1)
                source.write_bytes(b'changed')
                self.assertTrue(audio.decode(source, 'ffmpeg'))
                sidecar = source.with_name(source.name + audio.SUFFIX)
                data = sidecar.read_bytes()
                self.assertEqual(data[:16], struct.pack('<4siii', b'NPC1', 1, 44100, 4410))
                self.assertEqual(data[16:48], hashlib.sha256(b'changed').digest())
                sidecar.write_bytes(data[:-1])
                self.assertTrue(audio.decode(source, 'ffmpeg'))

    def test_existing_provider_file_never_calls_provider(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder)/'voice.mp3'; source.write_bytes(b'original')
            with patch.object(audio.urllib.request, 'urlopen') as request:
                self.assertFalse(audio.synthesize(self.catalog()['phrases'][0], source, Path('missing-key')))
                request.assert_not_called()

    def test_prepare_preserves_existing_mp3_and_atomic_upload(self):
        catalog = self.catalog(); name = catalog['phrases'][0]['file']; calls = []; uploaded = []
        remote_files = {name: b'original'}
        stream = io.BytesIO()
        with tarfile.open(fileobj=stream, mode='w') as archive:
            member = tarfile.TarInfo(name); member.size = 8; archive.addfile(member, io.BytesIO(b'original'))
        class Remote:
            root = '/device/files/'
            def headset_state(self): return dict(intakeAvailable=True)
            def command(self, *args, **kwargs):
                calls.append(args)
                if args[0] == 'push':
                    with tarfile.open(args[1]) as archive:
                        uploaded.extend(archive.getnames())
                        for member in archive: remote_files[member.name] = archive.extractfile(member).read()
                if args[0] == 'exec-out' and args[1].startswith('cat '): return json.dumps(catalog).encode()
                if args[0] == 'exec-out': return stream.getvalue()
                if args[0] == 'shell' and args[1].startswith('ls '): return ('\n'.join(remote_files)+'\n').encode()
                if args[0] == 'shell' and ' && sha256sum ' in args[1]:
                    names = args[1].split(' && sha256sum ')[1].split()
                    return '\n'.join(hashlib.sha256(remote_files[n]).hexdigest()+'  '+n for n in names).encode()
                return b''
        with tempfile.TemporaryDirectory() as folder:
            with patch.object(audio.subprocess, 'run') as process, patch.object(audio.urllib.request, 'urlopen') as provider:
                process.return_value.stdout = struct.pack('<4410f', *([.1]*4410))
                result = audio.prepare(Remote(), Path(folder), Path('missing-key'), 'ffmpeg', lambda *a: None)
                self.assertEqual(result, dict(total=1, generated=0, decoded=1, transferred=1)); provider.assert_not_called()
                self.assertEqual(uploaded, [name + audio.SUFFIX])
                self.assertIn('[ -e ../tts_cache/"$f" ] || mv', calls[-1][1])
                self.assertEqual((Path(folder)/name).read_bytes(), b'original')
                calls.clear(); uploaded.clear(); process.reset_mock()
                result = audio.prepare(Remote(), Path(folder), Path('missing-key'), 'ffmpeg', lambda *a: None)
                self.assertEqual(result, dict(total=1, generated=0, decoded=0, transferred=0))
                process.assert_not_called(); provider.assert_not_called(); self.assertEqual(uploaded, [])

    def test_active_session_does_not_transfer(self):
        catalog = self.catalog()
        class Remote:
            root = '/device/files/'
            def headset_state(self): return dict(intakeAvailable=False)
            def command(self, *args, **kwargs): return json.dumps(catalog).encode()
        with self.assertRaisesRegex(ValueError, 'Finish or cancel'):
            audio.prepare(Remote(), Path('unused'), Path('unused'), 'ffmpeg', lambda *a: None)


if __name__ == '__main__': unittest.main()
