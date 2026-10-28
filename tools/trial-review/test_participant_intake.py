import io
import math
import struct
import tempfile
import unittest
import uuid
import wave
from pathlib import Path
from participant_intake import (create, load, answer, clone_answers, patch, ticket, validate_fit, INTERVIEW_VOICE,
    record_consent, authorize_recording, review_eligibility, ELIGIBILITY, QUESTIONS, sample_quality, finish_audio_intake)
from live_bridge import validate_control

class Provider:
    def __init__(self): self.clones=[]; self.prompts=[]; self.followups=0; self.fail_speech=False
    def speech(self, text):
        self.prompts.append(text)
        if self.fail_speech: raise RuntimeError('speech unavailable')
        return b'prompt mp3'
    def transcribe(self, data): return 'I use VR weekly. I enjoy exploring different environments and listening to spoken instructions when learning how the controls work.'
    def followup(self, topic, text):
        if topic in ('vr', 'experience'): self.followups+=1; return 'What do you use it for?'
        return ''
    def clone(self, code, data): self.clones.append((code,data)); return 'fixture_voice_id'

class IntakeTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.folder=Path(self.temp.name)/uuid.uuid4().hex
        create(self.folder,'P101','A'); self.provider=Provider()
        record_consent(self.folder,'TEST','development synthetic consent',True,True)
        patch(self.folder,phase='interview',selectedVoiceConfirmed=True)
    def recording(self, value=123):
        name='answer-'+uuid.uuid4().hex+'.wav'
        authorize_recording(self.folder,name)
        with wave.open(str(self.folder/name),'wb') as out:
            out.setnchannels(1);out.setsampwidth(2);out.setframerate(24000)
            out.writeframes(b''.join(struct.pack('<h',int(5000*math.sin(i*2*math.pi*220/24000))) for i in range(24000*4)))
        return name
    def eligibility(self):
        for _ in QUESTIONS: answer(self.folder,self.provider,self.recording())
        review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
    def completed(self):
        self.eligibility()
        clone_answers(self.folder,self.provider)
        return patch(self.folder,cacheState='transferred',cacheManifestSha256='a'*64)
    def test_answers_only_clone_snapshot_and_duplicate_prevention(self):
        self.completed()
        (self.folder/'prompt-extra.mp3').write_bytes(b'not participant audio')
        clone_answers(self.folder,self.provider); clone_answers(self.folder,self.provider)
        self.assertEqual(len(self.provider.clones),1)
        with wave.open(io.BytesIO(self.provider.clones[0][1])) as wav: self.assertEqual(wav.getnframes(),24000*4*len(QUESTIONS))
        self.assertEqual(load(self.folder)['cloneAnswerCount'],len(QUESTIONS))
    def test_one_followup_per_topic_and_minimal_handoff(self):
        self.completed()
        state=load(self.folder);self.assertEqual(state['phase'],'interview_complete')
        self.assertEqual(self.provider.followups,0)
        clone_answers(self.folder,self.provider); assignment=ticket(load(self.folder))
        self.assertEqual(set(assignment),{'schema','id','participantCode','configuration','neutralGender','neutralOption','cloneVoiceId','fit','consentId','intakeProtocol','eligibilityReviewed','cacheManifestSha256','development'})
        self.assertNotEqual(INTERVIEW_VOICE,assignment['cloneVoiceId'])
    def test_prompt_failure_retry_does_not_advance_answer_twice(self):
        patch(self.folder,promptRevision=4,promptAudio='previous-question.mp3')
        name=self.recording();self.provider.fail_speech=True
        with self.assertRaises(RuntimeError): answer(self.folder,self.provider,name)
        self.assertEqual(len(load(self.folder)['answers']),1)
        self.assertGreater(load(self.folder)['promptRevision'],4)
        self.assertEqual(load(self.folder)['promptAudio'],'')
        self.provider.fail_speech=False;answer(self.folder,self.provider,name)
        self.assertEqual(len(load(self.folder)['answers']),1)
        self.assertEqual(load(self.folder)['questionIndex'],1)
    def test_skip_and_incomplete_gate(self):
        answer(self.folder,self.provider,skip=True)
        self.assertEqual(load(self.folder)['recordedSeconds'],0)
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
        with self.assertRaises(ValueError): ticket(load(self.folder))
    def test_uncertain_clone_is_not_automatically_repeated(self):
        self.eligibility()
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

    def test_ipd_persists_with_provenance_and_separate_lens_setting(self):
        fit = dict(ipdRecorded=True, participantIpdMm=63.5, ipdSource='pupillometer', headsetIpdRecorded=True, headsetIpdMm=64)
        folder = self.folder / uuid.uuid4().hex
        create(folder, 'P102', 'B', fit=fit)
        record_consent(folder,'TEST','synthetic consent',True,True)
        state = patch(folder, phase='interview_complete', eligible=True, selectedVoiceConfirmed=True, cloneState='ready', cloneVoiceId='fixture_voice_id',cacheState='transferred',cacheManifestSha256='a'*64)
        self.assertEqual(load(folder)['fit'], fit)
        from live_bridge import validate_assignment
        self.assertEqual(validate_assignment(ticket(state))['fit'], fit)
        self.assertFalse(load(self.folder)['fit']['ipdRecorded'])
        del state['fit']
        self.assertEqual(ticket(state)['fit']['ipdSource'], 'not_recorded')

    def test_ipd_rejects_nonfinite_units_missing_source_and_private_fields(self):
        base = validate_fit(None)
        for value in (0, -1, 630, float('nan'), float('inf'), '63', True):
            with self.subTest(value=value), self.assertRaises(ValueError):
                validate_fit(dict(base, ipdRecorded=True, participantIpdMm=value, ipdSource='ruler'))
        for fit in (dict(base, participantIpdMm=63), dict(base, ipdRecorded=True, participantIpdMm=63),
                    dict(base, name='private'), dict(base, headsetIpdRecorded=True, headsetIpdMm=0)):
            with self.assertRaises(ValueError): validate_fit(fit)

    def test_no_capture_transcription_clone_or_ticket_before_consent(self):
        folder=self.folder.parent/uuid.uuid4().hex; create(folder,'P102','B')
        with self.assertRaises(ValueError): authorize_recording(folder,'answer-'+uuid.uuid4().hex+'.wav')
        with self.assertRaises(ValueError): answer(folder,self.provider,skip=True)
        with self.assertRaises(ValueError): clone_answers(folder,self.provider)
        with self.assertRaises(ValueError): ticket(load(folder))

    def test_consent_cannot_be_backdated_or_replaced(self):
        with self.assertRaises(ValueError): record_consent(self.folder,'TEST','replacement',True,True)
        self.assertEqual(len(list(self.folder.glob('consent-*.json'))),1)

    def test_consent_bound_to_exact_participant(self):
        patch(self.folder,participantCode='P999')
        with self.assertRaises(ValueError): self.recording()

    def test_withdrawal_blocks_new_capture_and_clone(self):
        patch(self.folder,withdrawnUtc=1)
        with self.assertRaises(ValueError): self.recording()
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)

    def test_unclear_eligibility_after_nine_answers_stops_screening(self):
        for _ in QUESTIONS: answer(self.folder,self.provider,self.recording())
        review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'unclear'),'TEST')
        self.assertEqual(load(self.folder)['phase'],'screening_stopped')
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
        self.assertEqual(len(load(self.folder)['answers']),9)
        self.assertEqual(load(self.folder)['cacheState'],'not_started')
        self.assertEqual(self.provider.clones,[])

    def test_skipped_eligibility_cannot_be_overridden(self):
        for _ in QUESTIONS: answer(self.folder,self.provider,skip=True)
        finish_audio_intake(self.folder, use_fallback=False)
        review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        self.assertFalse(load(self.folder)['eligible'])

    def test_all_nine_answers_precede_review_and_clone(self):
        for index in range(9):
            state=answer(self.folder,self.provider,self.recording())
            self.assertEqual(state['phase'],'interview' if index < 8 else 'eligibility_review')
            with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
            if index < 8:
                with self.assertRaises(ValueError): review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        self.assertEqual(self.provider.prompts,[prompt for _,prompt in QUESTIONS[1:]])
        self.assertEqual(self.provider.clones,[])
        state=review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        self.assertEqual(state['phase'],'interview_complete')
        self.assertEqual(len(state['eligibility']['answerIds']),9)
        with self.assertRaises(ValueError): review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        clone_answers(self.folder,self.provider)
        self.assertEqual(len(self.provider.clones),1)
        self.assertEqual(load(self.folder)['cloneAnswerCount'],9)

    def test_negative_review_retains_all_nine_answers_without_clone_or_cache(self):
        for _ in QUESTIONS: answer(self.folder,self.provider,self.recording())
        before=load(self.folder)['answers']
        values=dict.fromkeys(ELIGIBILITY,'yes');values['adult']='no'
        state=review_eligibility(self.folder,values,'TEST')
        self.assertEqual(state['phase'],'screening_stopped')
        self.assertFalse(state['eligible'])
        self.assertEqual(state['answers'],before)
        self.assertTrue(all((self.folder/item['audio']).exists() for item in before))
        for action in (lambda: clone_answers(self.folder,self.provider), lambda: self.recording(),
                       lambda: answer(self.folder,self.provider,skip=True), lambda: ticket(load(self.folder))):
            with self.assertRaises(ValueError): action()
        self.assertEqual(state['cloneState'],'not_started')
        self.assertEqual(state['cacheState'],'not_started')
        self.assertEqual(self.provider.clones,[])

    def test_optional_fallback_finishes_before_single_review(self):
        for _ in QUESTIONS: answer(self.folder,self.provider,self.recording())
        # Simulate quieter answers falling short of the same speech gate.
        patch(self.folder,phase='fallback_available',speechSeconds=28,sampleWords=49)
        with self.assertRaises(ValueError): review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        heard_revision=load(self.folder)['promptRevision']
        state=finish_audio_intake(self.folder,use_fallback=True)
        self.assertGreater(state['promptRevision'],heard_revision)
        self.assertEqual(state['promptAudio'],'')
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
        state=answer(self.folder,self.provider,self.recording())
        self.assertEqual(state['phase'],'eligibility_review')
        self.assertEqual(state['answers'][-1]['topic'],'reading_fallback')
        self.assertEqual(len(state['answers']),10)
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
        review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        clone_answers(self.folder,self.provider)
        self.assertEqual(load(self.folder)['cloneAnswerCount'],10)
        with self.assertRaises(ValueError): finish_audio_intake(self.folder,use_fallback=True)

    def test_insufficient_speech_offers_fallback_before_review(self):
        for _ in range(4): answer(self.folder,self.provider,self.recording())
        for _ in QUESTIONS[4:]: answer(self.folder,self.provider,skip=True)
        self.assertEqual(load(self.folder)['phase'],'fallback_available')
        finish_audio_intake(self.folder,use_fallback=True)
        answer(self.folder,self.provider,skip=True)
        self.assertEqual(load(self.folder)['phase'],'fallback_available')
        finish_audio_intake(self.folder,use_fallback=False)
        review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST')
        self.assertTrue(load(self.folder)['eligible'])
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)
        self.assertEqual(self.provider.clones,[])

    def test_withdrawal_blocks_every_intake_phase(self):
        for phase in ('interview','fallback_available','fallback','eligibility_review','interview_complete'):
            with self.subTest(phase=phase):
                patch(self.folder,phase=phase,withdrawnUtc=1)
                for action in (lambda: self.recording(), lambda: answer(self.folder,self.provider,skip=True),
                               lambda: finish_audio_intake(self.folder,use_fallback=True),
                               lambda: review_eligibility(self.folder,dict.fromkeys(ELIGIBILITY,'yes'),'TEST'),
                               lambda: clone_answers(self.folder,self.provider)):
                    with self.assertRaises(ValueError): action()
        self.assertEqual(self.provider.clones,[])

    def test_duplicate_participant_reservation_rejected(self):
        with self.assertRaises(ValueError): create(self.folder.parent/uuid.uuid4().hex,'P101','A')

    def test_duration_without_speech_does_not_qualify(self):
        name=self.recording()
        with wave.open(str(self.folder/name),'wb') as out:
            out.setnchannels(1);out.setsampwidth(2);out.setframerate(24000);out.writeframes(b'\0\0'*24000*31)
        answer(self.folder,self.provider,name)
        self.assertEqual(load(self.folder)['speechSeconds'],0)
        with self.assertRaises(ValueError): clone_answers(self.folder,self.provider)

    def test_cache_transfer_required_for_ticket(self):
        self.completed();patch(self.folder,cacheState='ready')
        with self.assertRaises(ValueError): ticket(load(self.folder))

    def test_recording_authorization_is_question_specific(self):
        name=self.recording(); answer(self.folder,self.provider,skip=True)
        with self.assertRaises(ValueError): answer(self.folder,self.provider,name)

if __name__=='__main__': unittest.main()
