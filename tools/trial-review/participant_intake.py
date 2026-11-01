#!/usr/bin/env python3
"""Private local interview workspace. Only a minimal assignment leaves for the headset."""
import argparse
import array
import csv
import hashlib
from contextlib import contextmanager
import fcntl
import io
import json
import math
import os
from pathlib import Path
import re
import time
import urllib.error
import urllib.request
import uuid
import wave
import participant_codes

INTERVIEW_VOICE = 'SAz9YHcvj6GT2YYXdXww'  # River, separate from the study catalog.
INTERVIEW_NAME = 'River'
LEGACY_QUESTIONS = [
    ('adult', 'Are you 18 years of age or older? Please answer yes or no after the tone.'),
    ('hearing', 'Can you hear and understand this spoken guidance at a comfortable volume? Please answer yes or no after the tone.'),
    ('vision', 'With your usual glasses or contact lenses if needed, can you see the scene clearly? Please answer yes, no, or not sure after the tone.'),
    ('seated_controller', 'Can you safely remain seated, turn in the chair, and point and press the trigger with the right controller? Please answer yes, no, or not sure after the tone. You do not need to give medical details.'),
    ('vr', 'How often have you used virtual reality: never, less than monthly, monthly, weekly, or daily? After the tone, describe what you have tried and how comfortable you felt using it.'),
    ('voice_assistant', 'How often do you use voice assistants: never, less than monthly, monthly, weekly, or daily? After the tone, describe what you use them for and your experience following spoken instructions.'),
    ('eye_tracking', 'Have you used eye tracking before? After the tone, describe your experience, or say that you have not used it.'),
    ('handedness', 'Which hand do you usually prefer for everyday tasks: right, left, or both equally? Answer after the tone.'),
    ('visual_correction', 'Are you using no visual correction, glasses, contact lenses, or another correction for this session? Answer after the tone. No prescription details are needed.'),
]
QUESTIONS = [
    ('adult', 'Are you 18 years of age or older? After the tone, please answer in a full sentence.'),
    ('hearing', 'Can you hear and understand this voice at a comfortable volume? After the tone, please answer in a full sentence.'),
    ('vision', 'With your usual glasses or contact lenses if needed, can you see clearly enough to read and to find small objects? After the tone, please answer in a full sentence.'),
    ('seated_controller', 'Can you safely stay seated, turn in a swivel chair, and use a controller in your right hand? After the tone, please answer in a full sentence. You do not need to give medical details.'),
    ('vr', 'Tell me about any experience you have had with virtual reality. What have you tried, how often, and how did it feel? If you have never tried it, tell me what you imagine it is like.'),
    ('voice_assistant', 'Tell me how you use voice assistants or spoken directions in everyday life, such as on your phone or while driving. What works well for you, and what is frustrating?'),
    ('eye_tracking', 'Have you used eye tracking before? If you have, describe it. If not, how would you feel about a system that knows where you are looking?'),
    ('handedness', 'Which hand do you prefer for everyday tasks, and what is something you always do with that hand?'),
    ('visual_correction', 'Are you wearing glasses, contact lenses, or no visual correction today? Tell me how they usually feel when you are looking at screens. No prescription details are needed.'),
    ('directional_speech', 'Imagine you are helping a friend find something they lost in a room. Talk them through it for about thirty seconds, telling them where to look, which way to turn, and when they are getting close.'),
]
PROTOCOL = 'audio-intake-v3-20261101'
# Engineering settings, shared with TrialReviewVoiceActivity; not a validated speech detector.
CAPTURE_SETTINGS = dict(method='20ms_rms_minus_dc_0.01_v1', frameMs=20, rmsThreshold=.01,
                        speechOnsetSeconds=.2, eligibilitySilenceSeconds=3, openSilenceSeconds=5,
                        noSpeechSeconds=10, noSpeechRepeats=1, maximumSeconds=120)
BACKGROUND_CATEGORIES = {
    'vr': ('never', 'less_than_monthly', 'monthly', 'weekly', 'daily', 'unclear'),
    'voice_assistant': ('never', 'less_than_monthly', 'monthly', 'weekly', 'daily', 'unclear'),
    'eye_tracking': ('yes', 'no', 'unclear'),
    'handedness': ('right', 'left', 'both_equally', 'unclear'),
    'visual_correction': ('none', 'glasses', 'contact_lenses', 'other', 'unclear'),
}
ELIGIBILITY = tuple(x[0] for x in QUESTIONS[:4])
READING_PASSAGE = ('Locate the red sphere. Look toward the upper shelf. Move your gaze slowly to the right. '
    'Check the blue cube near the center. Compare each object\'s color and shape. Scan the lower row from left to right. '
    'Look beside the green cylinder. Point the controller ray at the target and check its highlight. '
    'Press the trigger to select the matching object. Take a short pause. Get ready for the next round.')
MIN_SPEECH_SECONDS = 30
MIN_WORDS = 50


def require_consent(state):
    consent = state.get('consent') or {}
    if (state.get('protocol') != PROTOCOL or consent.get('participantCode') != state['participantCode'] or
            consent.get('sessionId') != state['id'] or consent.get('study') is not True or
            consent.get('voiceCloud') is not True or state.get('withdrawnUtc')):
        raise ValueError('Record study consent and explicit cloud recording/cloning authorization for this session first.')
    return consent


def record_consent(folder, operator, reference, study, voice_cloud):
    if not operator.strip() or not reference.strip() or study is not True or voice_cloud is not True:
        raise ValueError('Enter researcher initials and the signed consent document/version reference, and confirm both permissions.')
    with locked(folder):
        state = load(folder)
        if state.get('consent') or state.get('answers') or state.get('withdrawnUtc'):
            raise ValueError('Consent is immutable. Resume the consented session or create a new session; never backdate consent.')
        consent = dict(id=uuid.uuid4().hex, sessionId=state['id'], participantCode=state['participantCode'],
                       recordedUtc=time.time(), operator=operator.strip(), reference=reference.strip(),
                       study=True, voiceCloud=True, method='researcher_attestation_of_signed_consent')
        atomic(folder / ('consent-' + consent['id'] + '.json'), consent)
        state.update(consent=consent, phase='select_voice', selectedVoiceConfirmed=False)
        atomic(folder / 'session.json', state)
    return state


def authorize_recording(folder, filename):
    with locked(folder):
        state = load(folder); consent = require_consent(state)
        if state['phase'] not in ('interview', 'fallback') or not re.fullmatch(r'answer-[0-9a-f]{32}\.wav', filename):
            raise ValueError('No answer recording is available at this step.')
        path = folder / (filename + '.capture.json')
        if path.exists() or (folder / filename).exists(): raise ValueError('Recording ID has already been used.')
        atomic(path, dict(consentId=consent['id'], sessionId=state['id'], questionIndex=state['questionIndex'],
                         prompt=state['prompt'], authorizedUtc=time.time(), microphone='operator_selected',
                         cue='880Hz_180ms_before_microphone_v1', settings=CAPTURE_SETTINGS))


def review_eligibility(folder, values, operator):
    with locked(folder):
        state = load(folder); require_consent(state)
        if (state['phase'] != 'eligibility_review' or len(state['answers']) < len(QUESTIONS) or
                state.get('eligibility') or set(values) != set(ELIGIBILITY) or not operator.strip()):
            raise ValueError('Review all four eligibility answers and enter researcher initials.')
        if any(v not in ('yes', 'no', 'unclear') for v in values.values()): raise ValueError('Invalid eligibility response.')
        review = dict(id=uuid.uuid4().hex, values=values, operator=operator.strip(), recordedUtc=time.time(),
                      source='researcher_review_of_audio_and_transcript', answerIds=[x['id'] for x in state['answers']])
        atomic(folder / ('eligibility-' + review['id'] + '.json'), review)
        eligible = all(v == 'yes' for v in values.values()) and all(not x['skipped'] for x in state['answers'][:4])
        state.update(eligibility=review, eligible=eligible, phase='interview_complete' if eligible else 'screening_stopped')
        atomic(folder / 'session.json', state)
    return state


def sample_quality(data, transcript):
    with wave.open(io.BytesIO(data)) as wav:
        if wav.getnchannels() != 1 or wav.getsampwidth() != 2 or wav.getframerate() != 24000:
            raise ValueError('Use mono 24 kHz 16-bit PCM answer recordings.')
        pcm = array.array('h', wav.readframes(wav.getnframes()))
    if not 2400 <= len(pcm) <= 24000 * 120: raise ValueError('Answer must last 0.1–120 seconds.')
    active = 0
    for offset in range(0, len(pcm)-479, 480):
        frame = pcm[offset:offset+480]; mean = sum(frame)/480
        rms = math.sqrt(sum((v-mean)**2 for v in frame)/480)/32768
        if rms >= .01: active += 480
    clipped = sum(abs(v) >= 32700 for v in pcm)/len(pcm)
    words = len(re.findall(r"\b[\w']+\b", transcript))
    usable = words >= 1 and clipped < .01
    return dict(method='20ms_rms_minus_dc_0.01_v1', seconds=len(pcm)/24000,
                speechSeconds=active/24000 if usable else 0, words=words if usable else 0,
                clippedFraction=clipped, usable=usable)


def possible_identifiers(transcript):
    """Local, conservative review flag only; never rewrite ASR or claim de-identification."""
    if re.search(r'\b(my name|named|called|name is|mr\.?|mrs\.?|dr\.?)\b|@', transcript, re.I):
        return True
    common = {'I', 'Yes', 'No', 'The', 'It', 'My', 'We', 'They', 'You', 'After', 'Before', 'When',
              'If', 'For', 'With', 'In', 'On', 'At', 'And', 'But', 'Not', 'Sometimes', 'Never',
              'Weekly', 'Daily', 'Monthly', 'Right', 'Left', 'Both', 'None', 'Glasses', 'Contact'}
    return any(word not in common for word in re.findall(r'\b[A-Z][a-z]+\b', transcript))


def export_transcripts(folder, state):
    """Private participant data; also serves as the local intake data export."""
    rows = []
    fields = ('participantCode', 'protocol', 'questionIndex', 'id', 'topic', 'question', 'audio',
              'transcript', 'machineTranscript', 'transcriptionProvider', 'transcriptionModel',
              'recordedUtc', 'transcriptionStatus', 'transcriptionError', 'researcherCorrected',
              'possibleIdentifiers', 'structuredValue', 'skipped')
    for index, answer_record in enumerate(state.get('answers', [])):
        row = {key: answer_record.get(key, '') for key in fields}
        row.update(participantCode=state['participantCode'], protocol=answer_record.get('protocol', state.get('protocol', '')),
                   questionIndex=answer_record.get('questionIndex', index+1))
        rows.append(row)
    atomic(folder/'intake_transcripts.json', dict(schema='intake-transcripts-v1',
           participantCode=state['participantCode'], sessionId=state['id'], protocol=state['protocol'],
           withdrawnUtc=state.get('withdrawnUtc'), answers=state.get('answers', [])))
    temp = folder/('transcripts-'+uuid.uuid4().hex+'.tmp')
    with temp.open('w', newline='', encoding='utf-8-sig') as stream:
        os.chmod(temp, 0o600)
        writer = csv.DictWriter(stream, fieldnames=fields); writer.writeheader(); writer.writerows(rows)
    os.replace(temp, folder/'intake_transcripts.csv')


def save_answer_update(folder, state, entry):
    atomic(folder/('answer-'+entry['id']+'.json'), entry)
    state['speechSeconds'] = sum(x['quality']['speechSeconds'] for x in state['answers'])
    state['sampleWords'] = sum(x['quality']['words'] for x in state['answers'])
    atomic(folder/'session.json', state)
    export_transcripts(folder, state)
    return state


def editable_answer(state, answer_id):
    require_consent(state)
    if state.get('eligibility') or state.get('cloneState') != 'not_started':
        raise ValueError('Correct transcripts before saving the eligibility review and freezing clone inputs.')
    entry = next((x for x in state['answers'] if x['id'] == answer_id), None)
    if entry is None: raise ValueError('Answer not found in this participant session.')
    return entry


def retry_transcript(folder, provider, answer_id):
    state = load(folder); entry = editable_answer(state, answer_id)
    if entry['skipped'] or not entry['audio']: raise ValueError('There is no audio to transcribe for this answer.')
    data = (folder/entry['audio']).read_bytes()
    if hashlib.sha256(data).hexdigest() != entry['audioSha256']: raise ValueError('Answer audio has changed.')
    require_consent(load(folder))
    try: transcript, error = provider.transcribe(data), ''
    except Exception as failure:
        transcript, error = '', str(failure) if isinstance(failure, RuntimeError) else type(failure).__name__
    with locked(folder):
        state = load(folder)
        # Do not discard an in-flight result after withdrawal; preserve it as an attempt only.
        entry = next(x for x in state['answers'] if x['id'] == answer_id)
        entry['transcriptionAttempts'].append(dict(transcript=transcript, error=error, recordedUtc=time.time(),
                                                   provider='ElevenLabs', model='scribe_v2'))
        entry.update(transcriptionStatus='failed' if error else 'complete', transcriptionError=error)
        if not error:
            if not entry['machineTranscript']: entry['machineTranscript'] = transcript
            if not entry['researcherCorrected']: entry['transcript'] = transcript
            entry['possibleIdentifiers'] = possible_identifiers(entry['transcript'])
            entry['quality'] = sample_quality(data, entry['transcript'])
        return save_answer_update(folder, state, entry)


def correct_transcript(folder, answer_id, transcript, operator, structured_value=None):
    if not operator.strip(): raise ValueError('Enter reviewing researcher initials.')
    with locked(folder):
        state = load(folder); entry = editable_answer(state, answer_id)
        if structured_value is not None:
            if structured_value not in BACKGROUND_CATEGORIES.get(entry['topic'], ()):
                raise ValueError('Invalid background coding category.')
            entry['structuredValue'] = structured_value
        entry['corrections'].append(dict(previousTranscript=entry['transcript'], transcript=transcript,
                                         operator=operator.strip(), recordedUtc=time.time(), structuredValue=entry['structuredValue']))
        entry.update(transcript=transcript, researcherCorrected=True, possibleIdentifiers=possible_identifiers(transcript))
        if entry['audio'] and not entry['skipped']:
            data = (folder/entry['audio']).read_bytes()
            if hashlib.sha256(data).hexdigest() != entry['audioSha256']: raise ValueError('Answer audio has changed.')
            entry['quality'] = sample_quality(data, transcript)
        return save_answer_update(folder, state, entry)


def withdraw(folder):
    with locked(folder):
        state = load(folder)
        state.update(withdrawnUtc=state.get('withdrawnUtc') or time.time(), phase='withdrawn')
        atomic(folder/'session.json', state); export_transcripts(folder, state)
    return state


def enough_speech(state):
    return state.get('speechSeconds', 0) >= MIN_SPEECH_SECONDS and state.get('sampleWords', 0) >= MIN_WORDS


def require_reviewed_eligibility(state):
    review = state.get('eligibility') or {}
    if (state['phase'] != 'interview_complete' or not state.get('eligible') or not review.get('id') or
            len(state['answers']) < len(QUESTIONS) or
            any(review.get('values', {}).get(key) != 'yes' for key in ELIGIBILITY)):
        raise ValueError('Complete all ten intake questions and the researcher eligibility review first.')


def finish_audio_intake(folder, use_fallback):
    with locked(folder):
        state = load(folder); require_consent(state)
        if state['phase'] != 'fallback_available' or state.get('eligibility'):
            raise ValueError('Finish all ten questions before choosing the optional reading fallback.')
        if use_fallback:
            state.update(phase='fallback', questionIndex=len(QUESTIONS),
                         promptAudio='', promptRevision=state['promptRevision']+1,
                         prompt='Read the following passage in your natural voice after the tone. '+READING_PASSAGE)
        else:
            state.update(phase='eligibility_review')
        atomic(folder / 'session.json', state)
    return state


def atomic(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    with temp.open('w') as f:
        os.chmod(temp, 0o600)
        json.dump(value, f, indent=2); f.flush(); os.fsync(f.fileno())
    os.replace(temp, path)


@contextmanager
def locked(folder):
    folder.mkdir(parents=True, exist_ok=True)
    os.chmod(folder, 0o700)
    with (folder / '.lock').open('a') as f:
        fcntl.flock(f, fcntl.LOCK_EX)
        yield


def load(folder):
    return json.loads((folder / 'session.json').read_text())


def patch(folder, **fields):
    with locked(folder):
        state = load(folder); state.update(fields); state['updatedUtc'] = time.time()
        if state.get('withdrawnUtc'): state['phase'] = 'withdrawn'
        atomic(folder / 'session.json', state)
    return state


def validate_fit(fit):
    if fit is None:
        return dict(ipdRecorded=False, participantIpdMm=0, ipdSource='not_recorded', headsetIpdRecorded=False, headsetIpdMm=0)
    legacy = {'ipdRecorded', 'participantIpdMm', 'ipdSource', 'headsetIpdRecorded', 'headsetIpdMm'}
    extra = {'viewSeparation', 'eyeSeparation', 'fitCaptureId', 'fitStartedUtc', 'fitEndedUtc', 'fitWarning',
             'autoIpdStatus', 'separationChange', 'separationChangeUtc', 'separationChangeSource', 'separationChangeMm', 'manualRecordedUtc', 'setupSampleCount'}
    if not isinstance(fit, dict) or not legacy <= set(fit) or set(fit) - legacy - extra:
        raise ValueError('Invalid IPD metadata fields.')
    for flag, number in [('ipdRecorded', 'participantIpdMm'), ('headsetIpdRecorded', 'headsetIpdMm')]:
        value = fit[number]
        if type(fit[flag]) is not bool or type(value) not in (float, int) or not math.isfinite(value):
            raise ValueError('IPD must be a finite number in millimetres.')
        if (fit[flag] and not 0 < value <= 100) or (not fit[flag] and value != 0):
            raise ValueError('Enter IPD in millimetres (greater than 0, at most 100), or leave it blank.')
    if fit['ipdSource'] not in (('pupillometer', 'ruler', 'participant_report') if fit['ipdRecorded'] else ('not_recorded',)):
        raise ValueError('Choose the participant IPD measurement source.')
    for key in ('viewSeparation', 'eyeSeparation'):
        value = fit.get(key)
        if value is None: continue
        if not isinstance(value, dict) or set(value) != {'recorded','millimetres','rangeMm','windowSeconds','samples','source','method','recordedUtc'}:
            raise ValueError('Invalid automatic fit measurement.')
        # Unity's inline serializer may materialize a null class as an inactive object.
        if value['recorded'] is False:
            if any(value[k] != 0 for k in ('millimetres','rangeMm','windowSeconds','samples')):
                raise ValueError('Unrecorded automatic fit must not carry a value.')
            continue
        if value['recorded'] is not True or type(value['samples']) is not int or value['samples'] < 40:
            raise ValueError('Automatic fit measurement needs a stable sample window.')
        for field, lo, hi in [('millimetres',40,85),('rangeMm',0,1),('windowSeconds',2,3)]:
            v = value[field]
            if type(v) not in (float,int) or not math.isfinite(v) or not lo <= v <= hi: raise ValueError('Invalid automatic fit window.')
        if value['method'] != 'median_stable_window_v1' or not all(isinstance(value[k],str) and value[k] for k in ('source','recordedUtc')):
            raise ValueError('Automatic fit provenance is missing.')
    for key in extra - {'viewSeparation','eyeSeparation','separationChangeMm','setupSampleCount'}:
        if fit.get(key) is not None and (not isinstance(fit[key],str) or len(fit[key]) > 512): raise ValueError('Invalid fit text.')
    if 'separationChangeMm' in fit and (type(fit['separationChangeMm']) not in (float,int) or not math.isfinite(fit['separationChangeMm']) or abs(fit['separationChangeMm']) > 45): raise ValueError('Invalid fit change.')
    if 'setupSampleCount' in fit and (type(fit['setupSampleCount']) is not int or fit['setupSampleCount'] < 0): raise ValueError('Invalid fit count.')
    return dict(fit)


def create(folder, participant, configuration, neutral_gender='Male', neutral_option=0, fit=None, development=False, catalog=None, archives=(), known=()):
    fit = validate_fit(fit)
    if not re.fullmatch(r'P[0-9]{3,12}', participant): raise ValueError('Use a participant code such as P001.')
    if configuration not in ('A', 'B'): raise ValueError('Choose configuration A or B.')
    if neutral_gender not in ('Male', 'Female') or not 0 <= neutral_option < 6: raise ValueError('Choose a neutral study voice.')
    with locked(folder.parent):
        used, dev = participant_codes.known_codes(folder.parent, catalog, archives, known)
        if participant_codes.duplicate(participant, used):
            raise ValueError('Duplicate participant code. Choose an unused code; stopped and withdrawn sessions retain theirs.')
        if not development and participant_codes.number(participant) >= participant_codes.DEV_BASE:
            raise ValueError('P900000000001 and above are reserved for development rehearsals.')
        if development and participant_codes.number(participant) < participant_codes.DEV_BASE:
            raise ValueError('Use the separate P900000000001 development sequence for new rehearsals.')
        used.add(participant)
        if development: dev.add(participant)
        atomic(folder.parent / 'participant-codes.json', {'used':sorted(used), 'development':sorted(dev)})
        folder.mkdir(parents=True, exist_ok=True); os.chmod(folder, 0o700)
        if (folder / 'session.json').exists(): raise ValueError('This session already exists; resume it instead.')
        state = dict(schema=2, protocol=PROTOCOL, development=bool(development), id=folder.name, participantCode=participant, configuration=configuration,
                     neutralGender=neutral_gender, neutralOption=neutral_option, phase='awaiting_consent',
                     questionIndex=0, prompt=QUESTIONS[0][1], promptRevision=0,
                     interviewVoiceId=INTERVIEW_VOICE, interviewVoiceName=INTERVIEW_NAME,
                     fit=fit, answers=[], cloneState='not_started', cloneVoiceId='', error='', cloneError='',
                     consent=None, eligible=False, speechSeconds=0, sampleWords=0, cacheState='not_started',
                     recordedSeconds=0, captureSettings=CAPTURE_SETTINGS,
                     updatedUtc=time.time())
        atomic(folder / 'session.json', state)
    return state


def sync_fit(folder, session_id, participant, fit):
    with locked(folder):
        state = load(folder)
        if state['id'] != session_id or state['participantCode'] != participant or state.get('withdrawnUtc'):
            raise ValueError('Fit record does not belong to this active intake.')
        state['fit'] = validate_fit(fit)
        state['updatedUtc'] = time.time()
        atomic(folder / 'session.json', state)


class Provider:
    def __init__(self, keys_path):
        self.keys_path = Path(keys_path).expanduser()

    def call(self, url, data=None, content_type='application/json'):
        headers = {'Content-Type': content_type}
        try: headers['xi-api-key'] = json.loads(self.keys_path.read_text())['elevenlabs_key']
        except (OSError, ValueError, KeyError): raise RuntimeError('ElevenLabs credentials are unavailable; local audio is preserved.') from None
        req = urllib.request.Request(url, data=data, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=90) as response: return response.read()
        except urllib.error.HTTPError as e:
            raise RuntimeError(f'Provider request failed (HTTP {e.code}); check connection, permissions or quota.') from None
        except (urllib.error.URLError, TimeoutError):
            raise RuntimeError('Provider connection timed out or failed. Your local recordings are preserved.') from None

    def multipart(self, url, fields, files):
        boundary = uuid.uuid4().hex; chunks = []
        for name, value in fields.items():
            chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
        for name, filename, data in files:
            chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{filename}"\r\nContent-Type: audio/wav\r\n\r\n'.encode() + data + b'\r\n')
        return json.loads(self.call(url, b''.join(chunks) + f'--{boundary}--\r\n'.encode(), 'multipart/form-data; boundary='+boundary))

    def speech(self, text):
        return self.call('https://api.elevenlabs.io/v1/text-to-speech/'+INTERVIEW_VOICE,
                         json.dumps({'text': text, 'model_id': 'eleven_turbo_v2_5', 'language_code': 'en'}).encode())

    def transcribe(self, data):
        return self.multipart('https://api.elevenlabs.io/v1/speech-to-text', {'model_id':'scribe_v2', 'language_code':'en'},
                              [('file', 'answer.wav', data)]).get('text', '')

    def clone(self, code, data):
        result = self.multipart('https://api.elevenlabs.io/v1/voices/add', {'name':'study_'+code,'remove_background_noise':'false'},
                                [('files','participant-answers.wav',data)])
        if result.get('requires_verification'): raise RuntimeError('ElevenLabs requires account-side voice verification.')
        voice = result.get('voice_id','')
        if not re.fullmatch(r'[A-Za-z0-9_-]{8,128}',voice): raise RuntimeError('Provider returned no usable voice ID.')
        return voice


def speak_prompt(folder, provider):
    state = load(folder)
    require_consent(state)
    if state['phase'] not in ('interview', 'fallback'): return state
    # Exact text + voice + model key prevents reusing old-protocol questions.
    key = hashlib.sha256((INTERVIEW_VOICE+'|eleven_turbo_v2_5|'+state['prompt']).encode()).hexdigest()
    cached = folder / ('river-' + key + '.mp3')
    existing = folder / state.get('promptAudio', '')
    if existing.is_file():
        data = existing.read_bytes()
    elif cached.is_file():
        data = cached.read_bytes()
    else:
        data = provider.speech(state['prompt'])
        cached.write_bytes(data); os.chmod(cached, 0o600)
    require_consent(load(folder))
    revision = state['promptRevision'] + 1
    path = folder / f'prompt-{revision}-{uuid.uuid4().hex}.mp3'
    path.write_bytes(data); os.chmod(path,0o600)
    return patch(folder, promptRevision=revision, promptAudio=path.name, error='')


def answer(folder, provider, filename=None, skip=False):
    state = load(folder)
    consent = require_consent(state)
    if state['phase'] not in ('interview', 'fallback'): raise ValueError('No question is awaiting an answer.')
    seconds = 0; transcript = ''; filename = filename or ''
    if filename and any(x['audio'] == filename for x in state['answers']):
        return speak_prompt(folder, provider)
    if not skip:
        if not re.fullmatch(r'answer-[0-9a-f]{32}\.wav',filename): raise ValueError('Invalid answer recording.')
        capture = json.loads((folder / (filename + '.capture.json')).read_text())
        if (capture['consentId'] != consent['id'] or capture['questionIndex'] != state['questionIndex'] or
                capture['prompt'] != state['prompt'] or capture['authorizedUtc'] < consent['recordedUtc']):
            raise ValueError('Recording does not belong to the current consent and question.')
        data = (folder / filename).read_bytes()
        sample_quality(data, '')  # Validate format before any provider upload.
        require_consent(load(folder))
        try:
            transcript = provider.transcribe(data)
            transcription_error = ''
        except Exception as error:
            # Preserve audio and advance. A later explicit retry is independent of the question cursor.
            transcription_error = str(error) if isinstance(error, RuntimeError) else type(error).__name__
    quality = sample_quality(data, transcript) if not skip else dict(seconds=0, speechSeconds=0, words=0, usable=False)
    seconds = quality['seconds']
    index = state['questionIndex']; topic = QUESTIONS[index][0] if index < len(QUESTIONS) else 'reading_fallback'
    response = dict(id=uuid.uuid4().hex, question=state['prompt'],topic=topic,audio=filename,
                    seconds=seconds,transcript=transcript,skipped=skip, quality=quality, recordedUtc=time.time(),
                    consentId=consent['id'], audioSha256=hashlib.sha256(data).hexdigest() if not skip else '',
                    transcriptSource='elevenlabs_scribe_v2' if not skip else 'skipped',
                    structuredValue='unclear' if topic in BACKGROUND_CATEGORIES else 'unreviewed', protocol=state['protocol'],
                    participantCode=state['participantCode'], questionIndex=index+1,
                    machineTranscript=transcript, researcherCorrected=False,
                    transcriptionProvider='ElevenLabs' if not skip else '', transcriptionModel='scribe_v2' if not skip else '',
                    transcriptionStatus=('failed' if transcription_error else 'complete') if not skip else 'unanswered',
                    transcriptionError=transcription_error if not skip else '',
                    possibleIdentifiers=possible_identifiers(transcript), corrections=[], transcriptionAttempts=[])
    if not skip:
        response['transcriptionAttempts'].append(dict(transcript=transcript, error=transcription_error,
                                                      recordedUtc=response['recordedUtc'], provider='ElevenLabs', model='scribe_v2'))
    response['captureAttempts'] = []
    for receipt in sorted(folder.glob('answer-*.wav.capture.json')):
        captured = json.loads(receipt.read_text())
        if captured.get('questionIndex') == index and captured.get('prompt') == state['prompt']:
            audio = receipt.name.removesuffix('.capture.json')
            end = folder/(audio+'.end.json')
            response['captureAttempts'].append(dict(audio=audio, captureReceipt=receipt.name,
                ending=json.loads(end.read_text()) if end.is_file() else None))
    atomic(folder / ('answer-' + response['id'] + '.json'), response)
    next_index = index + 1
    next_prompt = QUESTIONS[next_index][1] if next_index < len(QUESTIONS) else 'Audio intake complete. Please wait for the researcher eligibility review.'
    totals = dict(speechSeconds=state['speechSeconds']+quality['speechSeconds'], sampleWords=state['sampleWords']+quality['words'])
    phase = ('interview' if next_index < len(QUESTIONS) else
             'fallback_available' if not enough_speech(totals) else 'eligibility_review')
    with locked(folder):
        current = load(folder)
        # A withdrawal can arrive during the provider request. Retain its result without reopening the flow.
        current.update(answers=current['answers']+[response], recordedSeconds=current['recordedSeconds']+seconds,
                       speechSeconds=current['speechSeconds']+quality['speechSeconds'], sampleWords=current['sampleWords']+quality['words'])
        if not current.get('withdrawnUtc'):
            current.update(questionIndex=next_index, prompt=next_prompt, promptAudio='',
                           promptRevision=current['promptRevision']+1, phase=phase, error='')
        atomic(folder/'session.json', current)
        export_transcripts(folder, current)
        state = current
    if state['phase']=='interview': return speak_prompt(folder,provider)
    return state


def clone_answers(folder, provider):
    with locked(folder):
        state=load(folder)
        require_consent(state)
        require_reviewed_eligibility(state)
        if not state.get('eligible') or not enough_speech(state):
            raise ValueError('Confirm eligibility and collect at least 30 seconds of usable speech and 50 transcript words; use the reading fallback if needed.')
        if state['cloneVoiceId'] or state['cloneState'] in ('cloning','uncertain'): return state
        answers=[x for x in state['answers'] if x['audio'] and not x['skipped'] and x['quality']['usable']]
        if not answers: raise ValueError('Record participant answers before cloning.')
        state.update(cloneState='cloning',cloneError='',cloneWorkerPid=os.getpid()); atomic(folder/'session.json',state)
    # Only answer WAVs; never prompt audio or researcher speech. Snapshot inputs once.
    try:
        result=io.BytesIO(); params=None
        with wave.open(result,'wb') as out:
            for entry in answers:
                if hashlib.sha256((folder/entry['audio']).read_bytes()).hexdigest() != entry['audioSha256']:
                    raise ValueError('An answer recording changed after transcription; cloning stopped.')
                with wave.open(str(folder/entry['audio']),'rb') as sample:
                    current=(sample.getnchannels(),sample.getsampwidth(),sample.getframerate())
                    if params is None: params=current; out.setnchannels(params[0]);out.setsampwidth(params[1]);out.setframerate(params[2])
                    if current!=params: raise ValueError('Answer recordings use different audio formats.')
                    out.writeframes(sample.readframes(sample.getnframes()))
        require_consent(load(folder))
        voice=provider.clone(state['participantCode'],result.getvalue())
        return patch(folder,cloneState='ready',cloneVoiceId=voice,cloneAnswerCount=len(answers),
                     cloneAnswerIds=[x['id'] for x in answers], cloneInputSha256=hashlib.sha256(result.getvalue()).hexdigest(),
                     cloneCreatedUtc=time.time(), cloneError='')
    except Exception as e:
        # Do not silently create another provider voice after an uncertain remote outcome.
        patch(folder,cloneState='uncertain',cloneError=str(e))
        raise


def ticket(state):
    consent = require_consent(state)
    if (state['phase']!='interview_complete' or not state.get('eligible') or state['cloneState']!='ready' or
            not state['cloneVoiceId'] or not state.get('selectedVoiceConfirmed') or state.get('cacheState') != 'transferred'):
        raise ValueError('Finish the eligible intake, clone, and verified audio transfer before sending to the headset.')
    return {**{k:state[k] for k in ('schema','id','participantCode','configuration','neutralGender','neutralOption','cloneVoiceId')},
            'consentId':consent['id'], 'intakeProtocol':state['protocol'], 'eligibilityReviewed':True,
            'cacheManifestSha256':state['cacheManifestSha256'], 'development':state['development'],
            'fit': validate_fit(state.get('fit'))}


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--request',type=Path,required=True)
    args=parser.parse_args();req=json.loads(args.request.read_text());folder=Path(req['folder'])
    try:
        action=req['action'];provider=Provider(req['keysPath']) if action in ('prompt','answer','clone','retry_clone','retry_transcript') else None
        if action=='codes':
            used, dev = participant_codes.known_codes(folder, req.get('catalog'), req.get('archives') or (), req.get('knownCodes') or ())
            result = dict(ok=True, suggested=participant_codes.suggest(used,dev,req.get('development',False),req.get('headsetSuggestion')),
                          duplicate=participant_codes.duplicate(req.get('participantCode'),used))
            atomic(args.request.with_suffix('.result.json'),result); return
        elif action=='create': create(folder,req['participantCode'],req['configuration'],req['neutralGender'],req['neutralOption'],req.get('fit'),req.get('development',False),req.get('catalog'),req.get('archives') or (),req.get('knownCodes') or ())
        elif action=='sync_fit':
            sync_fit(folder,req.get('participantSessionId'),req.get('participantCode'),req.get('fit'))
        elif action=='consent': record_consent(folder,req['operator'],req['reference'],req['study'],req['voiceCloud'])
        elif action=='select_voice':
            state=load(folder);require_consent(state)
            if state['phase']!='select_voice' or not state.get('previewAudio'): raise ValueError('Audition the participant-selected voice first.')
            patch(folder,selectedVoiceConfirmed=True,selectedVoiceConfirmedUtc=time.time(),phase='interview')
        elif action=='authorize_recording': authorize_recording(folder,req['audio'])
        elif action=='review': review_eligibility(folder,dict(zip(ELIGIBILITY,req['eligibilityValues'])),req['operator'])
        elif action=='withdraw': withdraw(folder)
        elif action=='retry_transcript': retry_transcript(folder,provider,req['answerId'])
        elif action=='correct_transcript': correct_transcript(folder,req['answerId'],req['transcript'],req['operator'],req.get('structuredValue') or None)
        elif action=='recover_clone':
            state=load(folder);require_consent(state)
            if state['cloneState']!='cloning': raise ValueError('No interrupted clone to recover.')
            try: os.kill(state.get('cloneWorkerPid',0),0)
            except ProcessLookupError: patch(folder,cloneState='uncertain',cloneError='Worker exited without a definitive provider result. Check the provider account before retrying.')
            else: raise ValueError('The original clone worker is still running; wait for it to finish.')
        elif action=='fallback':
            finish_audio_intake(folder, use_fallback=True)
        elif action=='finish_audio': finish_audio_intake(folder, use_fallback=False)
        elif action=='ticket': atomic(folder/'assignment.json',ticket(load(folder)))
        elif action=='prompt': speak_prompt(folder,provider)
        elif action=='answer': answer(folder,provider,req.get('audio'),req.get('skip',False))
        elif action=='clone': clone_answers(folder,provider)
        elif action=='retry_clone':
            if load(folder)['cloneState'] != 'uncertain': raise ValueError('No failed clone to retry.')
            patch(folder, cloneState='not_started')
            clone_answers(folder,provider)
        else: raise ValueError('Unknown intake action.')
        atomic(args.request.with_suffix('.result.json'),{'ok':True})
    except Exception as e:
        atomic(args.request.with_suffix('.result.json'),{'ok':False,'error':str(e) if isinstance(e,(ValueError,RuntimeError)) else type(e).__name__})
        raise SystemExit(1)

if __name__=='__main__': main()
