import io
import tempfile
import unittest
import uuid
import wave
from pathlib import Path
from participant_intake import create, load, answer, clone_answers, patch, ticket, INTERVIEW_VOICE
from live_bridge import validate_control

class Provider:
    def __init__(self): self.clones=[]; self.prompts=[]; self.followups=0; self.fail_speech=False
    def speech(self, text):
        self.prompts.append(text)
        if self.fail_speech: raise RuntimeError('speech unavailable')
        return b'prompt mp3'
    def transcribe(self, data): return 'I use VR weekly.'
    def followup(self, topic, text):
        if topic in ('vr', 'experience'): self.followups+=1; return 'What do you use it for?'
        return ''
    def clone(self, code, data): self.clones.append((code,data)); return 'fixture_voice_id'

class IntakeTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name)/uuid.uuid4().hex
        create(self.folder,'P101','A'); self.provider=Provider()
    def recording(self, value=123):
        name='answer-'+uuid.uuid4().hex+'.wav'
        with wave.open(str(self.folder/name),'wb') as out:
            out.setnchannels(1);out.setsampwidth(2);out.setframerate(24000)
            out.writeframes(value.to_bytes(2,'little',signed=True)*24000)
        return name
    def test_answers_only_clone_snapshot_and_duplicate_prevention(self):
        first=self.recording(); answer(self.folder,self.provider,first)
        (self.folder/'prompt-extra.mp3').write_bytes(b'not participant audio')
        clone_answers(self.folder,self.provider); clone_answers(self.folder,self.provider)
        self.assertEqual(len(self.provider.clones),1)
        with wave.open(io.BytesIO(self.provider.clones[0][1])) as wav: self.assertEqual(wav.getnframes(),24000)
        self.assertEqual(load(self.folder)['cloneAnswerCount'],1)
    def test_one_followup_per_topic_and_minimal_handoff(self):
        for _ in range(6): answer(self.folder,self.provider,self.recording())
        state=load(self.folder);self.assertEqual(state['phase'],'interview_complete')
        self.assertEqual(self.provider.followups,2)
        clone_answers(self.folder,self.provider); assignment=ticket(load(self.folder))
        self.assertEqual(set(assignment),{'schema','id','participantCode','configuration','neutralGender','neutralOption','cloneVoiceId'})
        self.assertNotEqual(INTERVIEW_VOICE,assignment['cloneVoiceId'])
    def test_prompt_failure_retry_does_not_advance_answer_twice(self):
        name=self.recording();self.provider.fail_speech=True
        with self.assertRaises(RuntimeError): answer(self.folder,self.provider,name)
        self.assertEqual(len(load(self.folder)['answers']),1)
        self.provider.fail_speech=False;answer(self.folder,self.provider,name)
        self.assertEqual(len(load(self.folder)['answers']),1)
        self.assertEqual(load(self.folder)['questionIndex'],1)
    def test_skip_and_incomplete_gate(self):
        answer(self.folder,self.provider,skip=True)
        self.assertEqual(load(self.folder)['recordedSeconds'],0)
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
        with self.assertRaises(ValueError): ticket(load(self.folder))
    def test_uncertain_clone_is_not_automatically_repeated(self):
        answer(self.folder,self.provider,self.recording())
        calls=[]
        def failure(*args): calls.append(1);raise RuntimeError('lost response')
        self.provider.clone=failure
        with self.assertRaises(RuntimeError): clone_answers(self.folder,self.provider)
        clone_answers(self.folder,self.provider)
        self.assertEqual(calls,[1]);self.assertEqual(load(self.folder)['cloneState'],'uncertain')
    def test_handoff_rejects_active_trial_and_private_fields(self):
        assignment=dict(schema=1,id=uuid.uuid4().hex,participantCode='P101',configuration='A',neutralGender='Male',neutralOption=0,cloneVoiceId='fixture_voice_id')
        state=dict(sessionId='launch',revision=10,phase='ready',intakeAvailable=True)
        command=dict(schema=1,id=uuid.uuid4().hex,sessionId='launch',observedRevision=10,observedUtc=100,createdUtc=100,action='assign_session',assignment=assignment)
        self.assertEqual(validate_control(command,state,101),command)
        with self.assertRaises(ValueError): validate_control(command,dict(state,intakeAvailable=False,phase='trial'),101)
        with self.assertRaises(ValueError): validate_control(dict(command,assignment=dict(assignment,name='private')),state,101)
        with self.assertRaises(ValueError): validate_control(dict(command,assignment=dict(assignment,configuration='C')),state,101)
    def test_interviewer_is_not_a_study_catalog_voice(self):
        catalog = (Path(__file__).resolve().parents[2]/'Assets/SessionConfig.cs').read_text()
        self.assertNotIn(INTERVIEW_VOICE, catalog)
    def test_invalid_code_does_not_create_session(self):
        with self.assertRaises(ValueError): create(self.folder/'other','../private','A')

if __name__=='__main__': unittest.main()
