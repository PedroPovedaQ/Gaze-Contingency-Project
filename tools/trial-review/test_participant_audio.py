import hashlib
import json
from pathlib import Path
import struct
import tarfile
import tempfile
import time
import unittest
from unittest.mock import patch
import uuid

import participant_audio as audio
from participant_intake import create, record_consent, load, patch as update, ticket, ELIGIBILITY


class ParticipantAudioTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name)/uuid.uuid4().hex
        create(self.folder,'P999','A',development=True)
        record_consent(self.folder,'TEST','synthetic consent fixture',True,True)
        update(self.folder,answers=[{} for _ in range(9)],eligibility=dict(id='fixture-review',values=dict.fromkeys(ELIGIBILITY,'yes')),speechSeconds=30,sampleWords=50,eligible=True,selectedVoiceConfirmed=True,cloneState='ready',cloneVoiceId='synthetic_clone',phase='interview_complete')
        self.template=dict(schema=1,model=audio.MODEL,contentVersion='matched-voice-v3',
            keyPrefix='matched-voice-v3|perspective-v9-shared-imperative|External|eleven_turbo_v2_5|',
            voices=[dict(gender='Male',option=0,voiceId='synthetic_selected',name='Fixture')],
            phrases=[dict(request=json.dumps(dict(text='Look left.',model_id=audio.MODEL)))])
        outer=self
        class Remote:
            root='/fixture/files/'; endpoint='development-fake'
            files={}; waiting=True
            def headset_state(self): return dict(intakeAvailable=self.waiting,updatedUtc=time.time())
            def command(self,*args,**kw):
                if args[0]=='exec-out': return json.dumps(outer.template).encode()
                if args[0]=='push':
                    if args[1].endswith('.tar'):
                        with tarfile.open(args[1]) as archive:
                            for member in archive: self.files.setdefault(member.name,archive.extractfile(member).read())
                    else: self.files['manifest']=Path(args[1]).read_bytes()
                if args[0]=='shell' and ' && sha256sum ' in args[1]:
                    return '\n'.join(hashlib.sha256(self.files[n]).hexdigest()+'  '+n for n in args[1].split(' && sha256sum ')[1].split()).encode()
                if args[0]=='shell' and args[1].startswith('sha256sum '):
                    return (hashlib.sha256(self.files['manifest']).hexdigest()+'  manifest').encode()
                return b''
        self.remote=Remote()

    def prepare(self):
        def synthesize(job,source,keys,voice):
            source.write_bytes(('fixture provider '+voice).encode())
        def decode(source,ffmpeg):
            pcm=struct.pack('<4410f',*([.1]*4410))
            source.with_name(source.name+audio.SUFFIX).write_bytes(struct.pack('<4siii',b'NPC1',1,44100,4410)+
                hashlib.sha256(source.read_bytes()).digest()+hashlib.sha256(pcm).digest()+pcm)
        with patch.object(audio,'synthesize',side_effect=synthesize),patch.object(audio,'decode',side_effect=decode):
            return audio.prepare(self.folder,self.remote,Path('unused'),'unused')

    def test_cache_requires_completed_positive_review_and_speech_thresholds(self):
        original=load(self.folder)
        cases=[dict(phase='interview'),dict(phase='eligibility_review'),dict(phase='fallback'),
               dict(phase='screening_stopped',eligible=False),dict(eligibility=None),
               dict(answers=[{} for _ in range(4)]),dict(speechSeconds=29.9),dict(sampleWords=49)]
        for change in cases:
            with self.subTest(change=change):
                update(self.folder,**dict(original,**change))
                with patch.object(self.remote,'command',side_effect=AssertionError('No remote work before review')):
                    with self.assertRaises(ValueError): self.prepare()
                self.assertEqual(load(self.folder)['cacheState'],'not_started')
                self.assertFalse((self.folder/'audio-cache').exists())

    def test_keys_separate_voices_and_match_runtime_contract(self):
        jobs=audio.jobs_for(self.template,load(self.folder))
        self.assertEqual(len(jobs),2); self.assertNotEqual(jobs[0]['file'],jobs[1]['file'])
        key=self.template['keyPrefix']+'el-clone-synthetic_clone|Look left.'
        self.assertEqual(jobs[1]['file'],'tts_'+hashlib.sha256(key.encode()).hexdigest()+'.mp3')

    def test_end_to_end_fake_provider_receipts_and_transfer(self):
        manifest=self.prepare(); self.assertEqual(manifest['participantCode'],'P999')
        self.assertTrue(manifest['development'])
        audio.transfer(self.folder,self.remote)
        state=load(self.folder); self.assertEqual(state['cacheState'],'transferred')
        self.assertEqual(ticket(state)['cacheManifestSha256'],audio.digest(self.folder/state['cacheManifest']))
        self.assertEqual(len(self.remote.files),5)

    def test_changed_template_blocks_transfer(self):
        self.prepare();self.template['contentVersion']='changed'
        with self.assertRaisesRegex(ValueError,'library changed'): audio.transfer(self.folder,self.remote)

    def test_active_headset_blocks_transfer(self):
        self.prepare();self.remote.waiting=False
        with self.assertRaisesRegex(ValueError,'waiting'): audio.transfer(self.folder,self.remote)

    def test_wrong_participant_manifest_rejected(self):
        self.prepare();update(self.folder,cloneVoiceId='another_voice')
        with self.assertRaisesRegex(ValueError,'identity'): audio.transfer(self.folder,self.remote)

    def test_modified_local_audio_blocks_transfer(self):
        manifest=self.prepare();(self.folder/'audio-cache'/manifest['entries'][0]['file']).write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'changed'): audio.transfer(self.folder,self.remote)

    def test_remote_conflict_never_overwritten_or_accepted(self):
        manifest=self.prepare();name=manifest['entries'][0]['file'];self.remote.files[name]=b'prior recording'
        with self.assertRaisesRegex(ValueError,'hashes differ'): audio.transfer(self.folder,self.remote)
        self.assertEqual(self.remote.files[name],b'prior recording')
        self.assertEqual(load(self.folder)['cacheState'],'ready')

    def test_withdrawal_blocks_preparation(self):
        update(self.folder,withdrawnUtc=time.time())
        with self.assertRaises(ValueError): self.prepare()

    def test_interview_voice_cannot_be_study_voice(self):
        self.template['voices'][0]['voiceId']=audio.INTERVIEW_VOICE
        with self.assertRaises(ValueError): audio.jobs_for(self.template,load(self.folder))


if __name__=='__main__': unittest.main()
