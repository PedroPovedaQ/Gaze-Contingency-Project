"""Harness-only ElevenLabs boundary; never copied by prepare.py into a study build."""
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
import csv
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import struct
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from unittest.mock import patch
import uuid
import wave

from e2e_intake import Harness, FakeHeadset, denied
import participant_audio as audio
import participant_codes
import participant_intake as intake
from live_bridge import validate_assignment
from neutral_audio import VOICE

API = 'https://api.elevenlabs.io/v1/'
KEYS = Path.home()/'.config/follow-my-voice/api_keys.json'
FFMPEG = '/opt/homebrew/bin/ffmpeg'
CACHE_CLIPS = 20  # Only the harness's fake headset template limits this run; no production knob.


def require_development(folder, root):
    state = intake.load(folder)
    if (not folder.resolve().is_relative_to(root.resolve()) or state.get('development') is not True
            or participant_codes.number(state['participantCode']) < participant_codes.DEV_BASE):
        raise ValueError('Live synthetic audio requires an isolated development session and code')
    intake.require_consent(state)
    return state


class LiveProvider(intake.Provider):
    def __init__(self, receipt):
        super().__init__(KEYS)
        self.path = receipt
        if receipt.exists():
            raise ValueError('Use a new receipt directory; inspect the existing clone/deletion receipt before any rerun')
        self.lock = threading.RLock()
        self.result = dict(mode='live ElevenLabs synthetic development', microphoneUsed=False,
                           realHeadsetUsed=False, protocol=intake.PROTOCOL, calls=[], clones=[],
                           cloneAttempts=0, scenarios=[], promptGenerations=[], playbacks=[],
                           quotaDeltaScope='account-wide; concurrent account activity may contribute')
        self.preflight_ok = False
        self.active_folder = None
        self.root = None
        self.save()

    def save(self):
        with self.lock:
            intake.atomic(self.path, self.result)

    def account(self):
        # Do not retain names or IDs of unrelated voices in the receipt.
        voices = json.loads(self.call(API+'voices'))['voices']
        self.result['lastVoiceList'] = dict(total=len(voices),
            nonPremade=sum(v.get('category') != 'premade' for v in voices), observedUtc=time.time())
        self.save()
        sub = json.loads(self.call(API+'user/subscription'))
        count, limit = sub['character_count'], sub['character_limit']
        # Some subscription API versions omit usage. Count every non-premade voice
        # conservatively (including shared voices), never assume an unknown slot is free.
        if any('category' not in v for v in voices):
            raise RuntimeError('Voice list lacks categories; cannot safely determine clone slots')
        slots_used = sub.get('voice_slots_used', sum(v['category'] != 'premade' for v in voices))
        slots_limit = sub['voice_limit']
        snapshot = dict(characterCount=count, characterLimit=limit, charactersRemaining=limit-count,
                        voiceSlotsUsed=slots_used, voiceLimit=slots_limit,
                        cloneSlotsFree=slots_limit-slots_used, voicesReturned=len(voices),
                        canUseInstantCloning=sub.get('can_use_instant_voice_cloning'),
                        slotCountSource='subscription.voice_slots_used' if 'voice_slots_used' in sub else 'conservative non-premade voice count',
                        # ElevenLabs exposes this allowance as character_count; it is billed credit usage.
                        creditsUsed=count, creditLimit=limit, creditSource='subscription.character_count')
        return snapshot, {v['voice_id'] for v in voices}

    def preflight(self):
        before, _ = self.account()
        self.result['before'] = before
        self.save()
        if before['cloneSlotsFree'] < 1 or before['charactersRemaining'] < 30000:
            raise RuntimeError('Live run stopped: require a free clone slot and at least 30000 remaining characters')
        if before['canUseInstantCloning'] is False:
            raise RuntimeError('Live run stopped: instant voice cloning unavailable')
        self.preflight_ok = True

    @contextmanager
    def network_guard(self):
        original = urllib.request.urlopen

        def guarded(request, *args, **kwargs):
            if not isinstance(request, urllib.request.Request):
                raise ValueError('Only explicit ElevenLabs requests are allowed')
            parsed = urllib.parse.urlsplit(request.full_url)
            method, path = request.get_method(), parsed.path
            if parsed.scheme != 'https' or parsed.netloc != 'api.elevenlabs.io':
                raise ValueError('Only ElevenLabs is authorized')
            readonly = method == 'GET' and path in ('/v1/voices', '/v1/user/subscription')
            if not readonly:
                if not self.preflight_ok:
                    raise ValueError('Voice list and quota preflight must succeed first')
                if method == 'DELETE':
                    voice = path.removeprefix('/v1/voices/')
                    if voice not in {c['voiceId'] for c in self.result['clones']}:
                        raise ValueError('Refusing to delete a voice not created by this harness')
                else:
                    require_development(self.active_folder, self.root)
                    allowed = (path in ('/v1/voices/add', '/v1/speech-to-text') or
                               path.startswith('/v1/text-to-speech/'))
                    if method != 'POST' or not allowed:
                        raise ValueError('Provider mutation outside the synthetic intake path')
            entry = dict(method=method, path=path, startedUtc=time.time())
            if path.startswith('/v1/text-to-speech/'):
                entry['characters'] = len(json.loads(request.data)['text'])
            with self.lock:
                self.result['calls'].append(entry)
                self.save()
            try:
                response = original(request, *args, **kwargs)
            except urllib.error.HTTPError as error:
                with self.lock:
                    entry.update(status=error.code, completedUtc=time.time())
                    self.save()
                raise
            # No credentials or raw provider responses are written to receipts.
            with self.lock:
                entry.update(status=response.status, completedUtc=time.time())
                for header in ('character-cost', 'xi-character-cost'):
                    if response.headers.get(header): entry[header] = response.headers[header]
                self.save()
            return response

        with patch.object(urllib.request, 'urlopen', side_effect=guarded):
            yield

    def speech(self, text):
        self.result['promptGenerations'].append(dict(question=next(i+1 for i, q in enumerate(intake.QUESTIONS) if q[1] == text),
                                                   textSha256=hashlib.sha256(text.encode()).hexdigest()))
        self.save()
        return super().speech(text)

    def clone(self, code, data):
        require_development(self.active_folder, self.root)
        if self.result['cloneAttempts']:
            raise RuntimeError('This harness run permits exactly one clone creation attempt')
        self.result['cloneAttempts'] += 1
        self.save()  # Persist intent before sending: unknown outcomes must never auto-retry.
        name = 'DEV-E2E-'+code+'-'+uuid.uuid4().hex[:8]
        result = self.multipart(API+'voices/add', {'name':name, 'remove_background_noise':'false'},
                                [('files','synthetic-piper-answers.wav',data)])
        voice = result.get('voice_id', '')
        if not re.fullmatch(r'[A-Za-z0-9_-]{8,128}', voice):
            raise RuntimeError('Clone response lacks a valid ID; inspect account before any rerun')
        self.result['clones'].append(dict(voiceId=voice, name=name, deletionVerified=False))
        self.save()  # Record the exact ID even if the application rejects verification requirements.
        if result.get('requires_verification'):
            raise intake.CloneVerificationRequired(voice)
        return voice

    def delete_voice(self, voice):
        if voice not in {c['voiceId'] for c in self.result['clones']}:
            raise ValueError('Not a harness-created clone')
        super().delete_voice(voice)

    def cleanup(self):
        errors = []
        for clone in self.result['clones']:
            try:
                if not clone.get('deletionRequested'):
                    self.delete_voice(clone['voiceId'])
                    clone['deletionRequested'] = True
                    self.save()
                # GET the entire voice list, verifying exact-ID absence after DELETE.
                voices = json.loads(self.call(API+'voices'))['voices']
                clone['deletionVerified'] = clone['voiceId'] not in {v['voice_id'] for v in voices}
                if not clone['deletionVerified']: raise RuntimeError('Clone remains in voice list')
            except Exception as error:
                clone['cleanupError'] = type(error).__name__+': '+str(error)
                errors.append(clone['voiceId'])
            self.save()
        try:
            after, _ = self.account()
            self.result['after'] = after
            if 'before' in self.result:
                self.result['subscriptionCreditDelta'] = after['creditsUsed']-self.result['before']['creditsUsed']
        except Exception as error:
            self.result['afterError'] = type(error).__name__+': '+str(error)
        finally:
            self.result['ttsCharactersSubmitted'] = sum(c.get('characters', 0) for c in self.result['calls'])
            self.save()
        if errors: raise RuntimeError('Clone cleanup NOT verified: '+', '.join(errors))


class LiveHarness(Harness):
    def __init__(self, root, provider):
        super().__init__(root)
        self.provider = provider
        provider.root = root
        self.remote = FakeHeadset()
        self.remote.template['voices'][0]['voiceId'] = VOICE
        assert len(self.remote.template['phrases'])*2 == CACHE_CLIPS
        self.tone = root/'development-tone.wav'
        samples = [int(32767*.18*math.sin(2*math.pi*880*i/24000)*math.sin(math.pi*i/4319)) for i in range(4320)]
        with wave.open(str(self.tone),'wb') as output:
            output.setnchannels(1); output.setsampwidth(2); output.setframerate(24000)
            output.writeframes(struct.pack('<4320h',*samples))

    def play(self, path, kind):
        pcm = subprocess.run([FFMPEG,'-v','error','-i',str(path),'-f','f32le','-ac','1','-ar','24000','-'],
                             check=True,capture_output=True).stdout
        assert len(pcm) >= 2400*4
        values = struct.unpack('<'+str(len(pcm)//4)+'f', pcm)
        rms = math.sqrt(sum(v*v for v in values)/len(values))
        assert math.isfinite(rms) and rms > .0001
        subprocess.run(['/usr/bin/afplay',str(path)],check=True,timeout=120,capture_output=True)
        self.provider.result['playbacks'].append(dict(kind=kind, seconds=round(len(values)/24000,3), rms=round(rms,6),
                                                     sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        self.provider.save()

    def reserve(self):
        used, dev = participant_codes.known_codes(self.root/'sessions')
        code = participant_codes.suggest(used,dev,True)
        folder = self.root/'sessions'/uuid.uuid4().hex
        intake.create(folder,code,'A',development=True)
        intake.record_consent(folder,'E2E','SYNTHETIC Piper development test; no human participant or signed human consent',True,True)
        self.provider.active_folder = folder
        require_development(folder,self.root)
        denied(lambda:intake.confirm_selected_voice(folder))
        preview = folder/'synthetic-development-audition.mp3'
        audio.synthesize(dict(request=json.dumps(dict(text='Hello. I will guide you during the search. Look to your left, then look a little higher.',model_id=audio.MODEL))),preview,KEYS,VOICE)
        self.play(preview,'Selected Voice audition')
        intake.patch(folder,previewAudio=preview.name)
        intake.confirm_selected_voice(folder)
        return folder

    def before_capture(self, folder):
        state = require_development(folder,self.root)
        before = len(self.provider.result['promptGenerations'])
        first = (folder/state['promptAudio']).read_bytes()
        # Force the exact-text-cache branch, not the existing promptAudio shortcut.
        intake.patch(folder,promptAudio='')
        state = intake.speak_prompt(folder,self.provider)
        assert len(self.provider.result['promptGenerations']) == before
        assert (folder/state['promptAudio']).read_bytes() == first
        self.play(folder/state['promptAudio'],f'River Q{state["questionIndex"]+1}')
        self.play(self.tone,'880Hz 180ms cue')
        time.sleep(.07)  # Match the desktop's 250 ms cue-to-capture interval.

    def live_happy(self):
        folder = self.reserve()
        self.complete_audio(folder)
        state = intake.load(folder)
        assert state['cloneState'] == 'ready' and state['phase'] == 'eligibility_review' and not state['eligible']
        checkpoint = self.root/'negative-review-checkpoint'
        shutil.copytree(folder,checkpoint)
        started = threading.Event()
        synthesize = audio.synthesize

        def observed(job, source, keys, voice):
            started.set()
            return synthesize(job,source,keys,voice)

        with patch.object(audio,'synthesize',side_effect=observed), ThreadPoolExecutor(max_workers=1) as pool:
            future = pool.submit(audio.prepare,folder,self.remote,KEYS,FFMPEG)
            assert started.wait(15)
            pending = intake.load(folder)
            assert pending['cacheState'] == 'preparing' and pending['phase'] == 'eligibility_review' and not pending['eligible']
            denied(lambda:audio.transfer(folder,self.remote))
            denied(lambda:intake.ticket(pending))
            self.provider.result['reviewOverlap'] = dict(observedUtc=time.time(),cacheState=pending['cacheState'],
                cacheCompleted=pending['cacheCompleted'],phase=pending['phase'],eligible=pending['eligible'])
            entry = pending['answers'][4]
            corrected = intake.review_eligibility(folder,dict.fromkeys(intake.ELIGIBILITY,'yes'),'E2E',
                         dict(answerId=entry['id'],transcript=entry['transcript']+' This is a synthetic correction.',structuredValue='less_than_monthly'))
            assert corrected['answers'][4]['machineTranscript'] == entry['machineTranscript']
            assert corrected['answers'][4]['corrections'][0]['operator'] == 'E2E'
            manifest = future.result(timeout=240)
        assert len(manifest['entries']) == CACHE_CLIPS
        audio.transfer(folder,self.remote)
        state = intake.load(folder)
        assignment = validate_assignment(intake.ticket(state))
        assert json.loads(self.remote.files['manifest'])['cloneVoiceId'] == assignment['cloneVoiceId']
        own = next(e for e in manifest['entries'] if e['condition']=='selfsimilar')
        self.play(folder/'audio-cache'/own['file'],'Real cloned-voice cache clip')
        exported = json.loads((folder/'intake_transcripts.json').read_text())
        with (folder/'intake_transcripts.csv').open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert len(exported['answers']) == len(rows) == 9
        assert all(a['transcriptionStatus']=='complete' and a['transcriptionModel']=='scribe_v2' for a in state['answers'])
        assert len(list(folder.glob('consent-*.json'))) == 1
        intake.require_consent_receipt(folder,state)
        export = self.root/'development-export'; export.mkdir()
        intake.export_transcripts(export,state)
        assert (export/'intake_transcripts.csv').read_bytes() == (folder/'intake_transcripts.csv').read_bytes()
        used,dev = participant_codes.known_codes(self.root/'sessions')
        next_code = participant_codes.suggest(used,dev,True)
        next_folder = self.root/'sessions'/uuid.uuid4().hex
        intake.create(next_folder,next_code,'A',development=True)
        assert participant_codes.number(next_code) == participant_codes.number(state['participantCode'])+1
        self.provider.result['happyEvidence'] = dict(cacheDuringReview=True,cacheClips=len(manifest['entries']),
            consentVerified=True,transcriptRows=len(rows),exportVerified=True,correctionPreservedMachineOriginal=True,
            nextDevelopmentCode=next_code,sessionFiles=[dict(name=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                for p in sorted(folder.iterdir()) if p.is_file()],
            captureDriver='actual C# detector with synthetic WAV; worker orchestration, not desktop GUI',
            promptCache='all nine generated in isolated session; each reused from exact-text cache before playback')
        self.provider.save()
        # Replay the saved pre-review state on the SAME clone. Cached audio is copied,
        # so the negative branch needs no new synthesis, transcription or clone credits.
        shutil.copytree(folder/'audio-cache',checkpoint/'audio-cache')
        self.negative_folder = checkpoint
        return dict(prompts=9,cacheClips=CACHE_CLIPS,headset='fake transfer and assignment only')

    def live_negative(self):
        folder = self.negative_folder
        self.provider.active_folder = folder
        gate, entered = threading.Event(), threading.Event()
        calls_before = len([c for c in self.provider.result['calls'] if c['method']=='POST'])
        synthesize = audio.synthesize

        def cached_only(job, source, keys, voice):
            assert source.exists(), 'Negative path may not generate additional audio'
            entered.set()
            if not gate.wait(20): raise RuntimeError('Negative review barrier timed out')
            return synthesize(job,source,keys,voice)

        with patch.object(audio,'synthesize',side_effect=cached_only), ThreadPoolExecutor(max_workers=1) as pool:
            future = pool.submit(audio.prepare,folder,self.remote,KEYS,FFMPEG)
            try:
                assert entered.wait(10)
                intake.review_eligibility(folder,dict.fromkeys(intake.ELIGIBILITY,'no'),'E2E')
            finally:
                gate.set()
            denied(lambda:future.result(timeout=90))
        intake.cleanup_clone(folder,self.provider)
        self.provider.result['clones'][0]['deletionRequested'] = True
        self.provider.save()
        state = intake.load(folder)
        assert state['cacheState']=='stopped' and state['cloneState']=='deleted'
        denied(lambda:audio.transfer(folder,FakeHeadset()))
        denied(lambda:intake.ticket(state))
        assert calls_before == len([c for c in self.provider.result['calls'] if c['method']=='POST'])
        return 'Saved pre-review checkpoint, same clone; active cache stopped; DELETE succeeded; no additional billable POST calls'


def run(receipts):
    receipts.mkdir(parents=True,exist_ok=True)
    provider = LiveProvider(receipts/'live-results.json')
    with provider.network_guard():
        try:
            provider.preflight()
            print(json.dumps(dict(preflight=provider.result['before'])),flush=True)
            with tempfile.TemporaryDirectory(prefix='FMV-DEVELOPMENT-LIVE-') as temp:
                harness = LiveHarness(Path(temp),provider)
                try:
                    for name, action in [('live_happy_worker_path',harness.live_happy),('live_negative_same_clone',harness.live_negative)]:
                        print('Running '+name,flush=True)
                        harness.scenario(name,action)
                finally:
                    provider.result.update(scenarios=harness.receipts,captures=harness.captures)
                    provider.save()
        except Exception as error:
            provider.result['error'] = type(error).__name__+': '+str(error)
            provider.save()
            raise
        finally:
            try:
                provider.cleanup()
            finally:
                provider.result['temporaryDevelopmentDataRemoved'] = True
                provider.save()
    print(json.dumps(provider.result,indent=2))
