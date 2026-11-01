#!/usr/bin/env python3
"""Offline development E2E: real intake/audio workers, Piper WAVs, real C# activity detector.

No microphone or network. Provider and headset boundaries are fakes. This harness and
its explicit WAV input are not packaged by prepare.py into study applications.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tarfile
import tempfile
import time
from unittest.mock import patch
import uuid
import wave

import participant_intake as intake
import participant_audio as audio
import participant_codes
from live_bridge import validate_assignment, validate_headset_state

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
MONO = Path('/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge')
ANSWERS = [
    'Yes, I am over eighteen years old and I can take part in this experiment.',
    'Yes, I can hear and understand this voice clearly at a comfortable volume.',
    'Yes, I can see clearly enough to read and find small objects with my usual glasses.',
    'Yes, I can safely stay seated and turn in a swivel chair while using the controller in my right hand.',
    'I have tried virtual reality a few times. I explored a quiet virtual room and played a simple game. I use it less than monthly and I prefer seated experiences.',
    'I use spoken directions on my phone every week. Clear instructions help me find the next turn. Long directions can be frustrating when I need to remember several things at once.',
    'I have not used eye tracking before. I would be curious to try a system that follows where I look, as long as the researcher explains what is recorded.',
    'I am wearing glasses today.',
    'Look toward the chair on your left. Turn slowly and check beside the table. Now look a little higher. You are getting close. Look behind the blue box and then check the shelf above it. Move your gaze back toward the center and look near the edge. Point at the object when you find it. Take your time and check the shape before making your selection. If it is not there, turn a little farther to the right and look near the lamp.',
]


class FakeProvider:
    def __init__(self):
        self.text = ''; self.clones = set(); self.prompts = []; self.fail = ''
    def speech(self, text):
        if self.fail == 'prompt': raise RuntimeError('Synthetic River failure')
        self.prompts.append(text); return b'synthetic prompt boundary'
    def transcribe(self, data):
        if self.fail == 'transcript': raise RuntimeError('Synthetic ASR failure')
        return self.text
    def clone(self, code, data):
        if self.fail == 'clone': raise RuntimeError('Synthetic clone failure')
        voice = 'synthetic_'+uuid.uuid4().hex; self.clones.add(voice); return voice
    def delete_voice(self, voice): self.clones.discard(voice)


class FakeHeadset:
    root = '/development/files/'
    endpoint = 'fake-only'
    def __init__(self):
        self.files = {}
        self.template = dict(schema=1, model=audio.MODEL,
            keyPrefix='development|fixture|External|eleven_turbo_v2_5|',
            voices=[dict(gender='Male', option=0, voiceId='synthetic_selected')],
            phrases=[dict(request=json.dumps(dict(text=f'Look toward position {i}.',model_id=audio.MODEL))) for i in range(10)])
    def headset_state(self): return dict(intakeAvailable=True, updatedUtc=time.time())
    def command(self, *args, **kwargs):
        if args[0] == 'exec-out': return json.dumps(self.template).encode()
        if args[0] == 'push':
            if args[1].endswith('.tar'):
                with tarfile.open(args[1]) as archive:
                    for member in archive: self.files.setdefault(member.name, archive.extractfile(member).read())
            else: self.files['manifest'] = Path(args[1]).read_bytes()
        if args[0] == 'shell' and ' && sha256sum ' in args[1]:
            return '\n'.join(hashlib.sha256(self.files[n]).hexdigest()+'  '+n for n in args[1].split(' && sha256sum ')[1].split()).encode()
        if args[0] == 'shell' and args[1].startswith('sha256sum '):
            return (hashlib.sha256(self.files['manifest']).hexdigest()+'  manifest').encode()
        return b''


def fake_synthesize(job, source, keys, voice): source.write_bytes(('synthetic '+voice+job['request']).encode())
def fake_decode(source, ffmpeg):
    pcm = struct.pack('<4410f', *([.1]*4410))
    source.with_name(source.name+audio.SUFFIX).write_bytes(struct.pack('<4siii',b'NPC1',1,44100,4410)+
        hashlib.sha256(source.read_bytes()).digest()+hashlib.sha256(pcm).digest()+pcm)


def denied(action):
    try: action()
    except (ValueError, RuntimeError): return
    raise AssertionError('Expected operation to be blocked')


class Harness:
    def __init__(self, root):
        self.root = root; self.provider = FakeProvider(); self.receipts = []; self.captures = []
        self.exe = root/'activity.exe'
        subprocess.run([str(MONO/'bin/mono'),str(MONO/'lib/mono/4.5/csc.exe'),'-nologo','-out:'+str(self.exe),
            str(HERE/'Runtime/TrialReviewVoiceActivity.cs'),str(HERE/'e2e_support/IntakeWavInput.cs')],check=True,capture_output=True)
        self.wavs=[]
        for i,text in enumerate(ANSWERS+[intake.READING_PASSAGE]):
            source=root/f'piper-{i}.wav'; output=root/f'prepared-{i}.wav'
            subprocess.run(['/opt/homebrew/Caskroom/miniconda/base/bin/piper','-m',str(Path.home()/'.local/share/piper/en_US-ryan-high.onnx'),'-f',str(source)],input=text,text=True,check=True,capture_output=True)
            subprocess.run(['/opt/homebrew/bin/ffmpeg','-v','error','-y','-i',str(source),'-af','apad=pad_dur=6','-ac','1','-ar','24000','-c:a','pcm_s16le',str(output)],check=True,capture_output=True)
            self.wavs.append(output)
        self.silent=root/'silent.wav'
        with wave.open(str(self.silent),'wb') as out:
            out.setnchannels(1);out.setsampwidth(2);out.setframerate(24000);out.writeframes(b'\0\0'*24000*11)

    def reserve(self):
        used,dev=participant_codes.known_codes(self.root/'sessions')
        code=participant_codes.suggest(used,dev,True)
        folder=self.root/'sessions'/uuid.uuid4().hex
        intake.create(folder,code,'A',development=True)
        intake.record_consent(folder,'E2E','synthetic development consent; no person',True,True)
        # Audition boundary is fake; selection and the real worker state are retained.
        denied(lambda:intake.confirm_selected_voice(folder))
        intake.patch(folder,previewAudio='synthetic-audition.mp3')
        intake.confirm_selected_voice(folder)
        return folder

    def capture(self, folder, index, source=None):
        state=intake.load(folder)
        if not state['development'] or participant_codes.number(state['participantCode']) < participant_codes.DEV_BASE:
            raise ValueError('Prepared audio is restricted to development sessions and codes')
        intake.speak_prompt(folder,self.provider)
        result=json.loads(subprocess.check_output([str(MONO/'bin/mono'),str(self.exe),'--development-audio',str(source or self.wavs[index]),'open' if index>=4 else 'eligibility']))
        self.captures.append(result)
        if result['reason']=='no_speech': return None
        assert result['reason']=='sustained_silence' and result['speechDetected'],result
        name='answer-'+uuid.uuid4().hex+'.wav';intake.authorize_recording(folder,name)
        with wave.open(str(source or self.wavs[index])) as inp, wave.open(str(folder/name),'wb') as out:
            out.setparams(inp.getparams());out.writeframes(inp.readframes(result['samples']))
        intake.atomic(folder/(name+'.end.json'),result)
        self.provider.text=(ANSWERS+[intake.READING_PASSAGE])[index]
        return intake.answer(folder,self.provider,name)

    def complete_audio(self, folder):
        for i in range(9):
            state=self.capture(folder,i)
            assert state['questionIndex']==i+1
        assert state['phase']=='eligibility_review' and intake.enough_speech(state)
        # Same automatic launch criteria as desktop PollIntake, no review gate.
        intake.clone_answers(folder,self.provider)
        assert not intake.load(folder)['eligible']

    def prepare(self, folder, remote):
        with patch.object(audio,'synthesize',side_effect=fake_synthesize),patch.object(audio,'decode',side_effect=fake_decode):
            return audio.prepare(folder,remote,Path('/unused'),'/unused')

    def scenario(self,name,action):
        before=time.monotonic()
        try:
            detail=action();self.receipts.append(dict(scenario=name,status='PASS',seconds=round(time.monotonic()-before,2),detail=detail))
        except Exception as error:
            self.receipts.append(dict(scenario=name,status='FAIL',error=type(error).__name__+': '+str(error)));raise

    def happy(self):
        folder=self.reserve(); self.complete_audio(folder);remote=FakeHeadset()
        manifest=self.prepare(folder,remote);assert len(manifest['entries'])==20
        state=intake.load(folder);entry=state['answers'][4]
        state=intake.review_eligibility(folder,dict.fromkeys(intake.ELIGIBILITY,'yes'),'E2E',
            dict(answerId=entry['id'],transcript=entry['transcript']+' This is my corrected answer.',structuredValue='less_than_monthly'))
        assert state['answers'][4]['machineTranscript']==entry['machineTranscript']
        assert state['answers'][4]['corrections'][0]['operator']=='E2E'
        audio.transfer(folder,remote);assignment=validate_assignment(intake.ticket(intake.load(folder)))
        assert assignment['participantCode']==state['participantCode'] and assignment['configuration']=='A'
        assert json.loads(remote.files['manifest'])['cloneVoiceId']==assignment['cloneVoiceId']
        exported=json.loads((folder/'intake_transcripts.json').read_text())
        rows=list(csv.DictReader((folder/'intake_transcripts.csv').open(encoding='utf-8-sig')))
        assert len(exported['answers'])==len(rows)==9 and 'handedness' not in {r['topic'] for r in rows}
        assert len(list(folder.glob('consent-*.json')))==1
        export=self.root/'development-export';intake.export_transcripts(export,state)
        assert (export/'intake_transcripts.csv').read_bytes()==(folder/'intake_transcripts.csv').read_bytes()
        # Automatic fit is nonblocking; absent sensor data never fabricates an IPD.
        assert not assignment['fit']['ipdRecorded']
        next_folder=self.reserve();assert participant_codes.number(intake.load(next_folder)['participantCode'])==participant_codes.number(state['participantCode'])+1
        intake.withdraw(folder);intake.cleanup_clone(folder,self.provider)
        assert not self.provider.clones
        return dict(protocol=state['protocol'],prompts=9,cacheClips=20,headset='fake receipt only; real headset untouched',automaticIpd='missing remains unavailable; physical device check not run',nextDevelopmentCode=intake.load(next_folder)['participantCode'])

    def fallback(self):
        folder=self.reserve()
        for i in range(9): intake.answer(folder,self.provider,skip=True)
        assert intake.load(folder)['phase']=='fallback_available'
        denied(lambda:intake.clone_answers(folder,self.provider))
        while not intake.enough_speech(intake.load(folder)):
            intake.finish_audio_intake(folder,True);self.capture(folder,9)
        intake.clone_answers(folder,self.provider)
        intake.withdraw(folder);intake.cleanup_clone(folder,self.provider)
        return 'Insufficient answers -> optional reading takes -> threshold -> clone -> cleanup'

    def ineligible(self):
        folder=self.reserve();self.complete_audio(folder);remote=FakeHeadset();self.prepare(folder,remote)
        intake.review_eligibility(folder,dict.fromkeys(intake.ELIGIBILITY,'no'),'E2E');intake.cleanup_clone(folder,self.provider)
        denied(lambda:audio.transfer(folder,remote));denied(lambda:intake.ticket(intake.load(folder)))
        assert not remote.files and intake.load(folder)['cloneState']=='deleted'
        return 'Negative review stops cache, deletes clone, blocks every transfer'

    def withdrawal(self):
        folder=self.reserve();self.capture(folder,0);intake.withdraw(folder)
        denied(lambda:self.capture(folder,1));intake.cleanup_clone(folder,self.provider)
        folder=self.reserve();self.complete_audio(folder);remote=FakeHeadset()
        def stop_during_cache(job,source,keys,voice):
            intake.withdraw(folder);fake_synthesize(job,source,keys,voice)
        with patch.object(audio,'synthesize',side_effect=stop_during_cache),patch.object(audio,'decode',side_effect=fake_decode):
            denied(lambda:audio.prepare(folder,remote,Path('/unused'),'/unused'))
        intake.cleanup_clone(folder,self.provider)
        assert intake.load(folder)['cacheState']=='stopped' and not self.provider.clones
        denied(lambda:audio.transfer(folder,remote));assert not remote.files
        return 'Withdrawal before next capture and during synthesis; no handoff'

    def failures(self):
        folder=self.reserve();self.provider.fail='prompt';denied(lambda:self.capture(folder,0));assert not intake.load(folder)['answers']
        self.provider.fail='';self.provider.text=ANSWERS[0]
        assert self.capture(folder,0,self.silent) is None
        assert self.capture(folder,0,self.silent) is None
        assert intake.load(folder)['questionIndex']==0
        self.provider.fail='transcript';state=self.capture(folder,0);assert state['answers'][0]['transcriptionStatus']=='failed'
        self.provider.fail='';intake.retry_transcript(folder,self.provider,state['answers'][0]['id'])
        # Close/reload while paused: cursor and saved answer remain on disk; no capture occurs.
        before=(folder/'session.json').read_bytes();time.sleep(.02);assert before==(folder/'session.json').read_bytes()
        intake.load(folder)
        for i in range(1,9): self.capture(folder,i)
        self.provider.fail='clone';denied(lambda:intake.clone_answers(folder,self.provider))
        assert intake.load(folder)['cloneState']=='uncertain'
        self.provider.fail='';intake.patch(folder,cloneState='not_started');intake.clone_answers(folder,self.provider)
        intake.withdraw(folder);intake.cleanup_clone(folder,self.provider)
        return 'River/ASR/clone failures, explicit retry, silent repeat input, worker reload/resume; desktop Pause UI covered separately by Unity checks'


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--development-audio',action='store_true',required=True)
    parser.add_argument('--receipts',type=Path,required=True)
    parser.add_argument('--monitor-receipt',type=Path,help='Actual Unity StudySessionMonitorChecks JSON, never participant data')
    args=parser.parse_args();os.umask(0o077)
    result=dict(mode='offline synthetic development',liveProviderCalls=0,providerCharacters=0,providerCredits=0,
                microphoneUsed=False,realHeadsetUsed=False,protocol=intake.PROTOCOL)
    args.receipts.mkdir(parents=True,exist_ok=True)
    if args.monitor_receipt:
        monitor=json.loads(args.monitor_receipt.read_text())
        assert monitor['development'] is True
        screens=[]
        for state in monitor['states']:
            forwarded=validate_headset_state(state)
            assert forwarded['screen']==state['screen'] and forwarded['trialTotal']==96
            screens.append(forwarded['screen'])
        for expected in ('Waiting','Confirm your session','Selected Voice audio','self-similar audio','Calibrating dot 1',
                         'Calibrating dot 2','Calibrating dot 3','accuracy check','activation exercise','Instructions',
                         'Readiness cross','Opening cue','Selection feedback','Timeout feedback','Block break','Block 2',
                         'Finished','paused / sleeping','out of focus'):
            assert any(expected in screen for screen in screens),expected
        for trial in range(1,97): assert any(f'Trial {trial} of 96 · Active search' in screen for screen in screens)
        result['monitor']=dict(status='PASS',source='actual Unity synthetic checks through Python bridge',states=len(screens),transitions=monitor['transitions'])
    with tempfile.TemporaryDirectory(prefix='FMV-DEVELOPMENT-INTAKE-') as temp:
        harness=Harness(Path(temp))
        try:
            for name,action in [('happy_path_offline',harness.happy),('too_little_speech',harness.fallback),('ineligible',harness.ineligible),('withdrawal',harness.withdrawal),('failures_restart',harness.failures)]:
                harness.scenario(name,action)
        finally:
            # Fake provider account must be empty even after an assertion fails.
            for voice in list(harness.provider.clones): harness.provider.delete_voice(voice)
            result.update(scenarios=harness.receipts,captures=harness.captures,testClonesRemaining=len(harness.provider.clones))
            intake.atomic(args.receipts/'e2e-results.json',result)
    print(json.dumps(result,indent=2))


if __name__=='__main__': main()
