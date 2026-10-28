#!/usr/bin/env python3
"""Participant-bound preparation and verified transfer. Never starts study trials."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import fcntl
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import tarfile
import time
import uuid

from participant_intake import load, patch, atomic, require_consent, INTERVIEW_VOICE
from neutral_audio import synthesize, decode, decoded_valid, MODEL, SUFFIX
from live_bridge import Adb, wireless_endpoint


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def seed_existing(remote, cache, jobs):
    """Reuse exact voice/text MP3s already on this headset before spending provider quota."""
    listing = remote.command('shell', 'if test -d '+shlex.quote(remote.root+'tts_cache')+'; then ls '+shlex.quote(remote.root+'tts_cache')+'; fi')
    existing = set(listing.decode().splitlines())
    wanted = [j['file'] for j in jobs if j['file'] in existing and not (cache/j['file']).exists()]
    for offset in range(0,len(wanted),80):
        names = wanted[offset:offset+80]
        raw = remote.command('exec-out','tar -C '+shlex.quote(remote.root+'tts_cache')+' -cf - '+' '.join(names),timeout=120)
        with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
            for entry in archive:
                if entry.name not in names or not entry.isfile() or not 100 <= entry.size <= 8*1024*1024:
                    raise ValueError('Unexpected headset audio archive entry.')
                with (cache/entry.name).open('xb') as output: output.write(archive.extractfile(entry).read())
                os.chmod(cache/entry.name,0o600)


def jobs_for(template, state):
    if (template.get('schema') != 1 or template.get('model') != MODEL or
            not re.fullmatch(r'[\w-]+\|[\w-]+\|External\|eleven_turbo_v2_5\|', template.get('keyPrefix', ''))):
        raise ValueError('Unsupported headset phrase template; update headset and viewer together.')
    voices = template.get('voices', [])
    selected = [v for v in voices if v['gender'] == state['neutralGender'] and v['option'] == state['neutralOption']]
    if len(selected) != 1: raise ValueError('Selected Voice is absent from the headset catalog.')
    neutral = selected[0]['voiceId']; clone = state['cloneVoiceId']
    if any(not re.fullmatch(r'[A-Za-z0-9_-]{8,128}', v) for v in (neutral, clone)) or clone == neutral or INTERVIEW_VOICE in (neutral, clone):
        raise ValueError('Study voices must be distinct from each other and the intake voice.')
    phrases = template.get('phrases', [])
    if not 1 <= len(phrases) <= 3000: raise ValueError('Invalid study phrase count.')
    jobs = []; seen = set()
    for condition, voice, scope in [('selected', neutral, 'el-'), ('selfsimilar', clone, 'el-clone-')]:
        for phrase in phrases:
            body = json.loads(phrase['request'])
            if set(body) != {'text','model_id'} or body['model_id'] != MODEL or not isinstance(body['text'], str) or not 1 <= len(body['text']) <= 5000:
                raise ValueError('Invalid study phrase request.')
            key = template['keyPrefix'] + scope + voice + '|' + body['text']
            name = 'tts_' + hashlib.sha256(key.encode()).hexdigest() + '.mp3'
            if name in seen: continue
            seen.add(name)
            jobs.append(dict(file=name, request=phrase['request'], voiceId=voice, condition=condition))
    return jobs


def prepare(folder, remote, keys, ffmpeg):
    state = load(folder); consent = require_consent(state)
    if not state.get('eligible') or not state.get('selectedVoiceConfirmed') or state['cloneState'] != 'ready': raise ValueError('Eligibility, Selected Voice and clone must be ready.')
    raw = remote.command('exec-out', 'cat ' + shlex.quote(remote.root+'PreparationLogs/study-audio-template.json'))
    template = json.loads(raw); jobs = jobs_for(template, state)
    cache = folder/'audio-cache'; cache.mkdir(exist_ok=True); os.chmod(cache, 0o700)
    patch(folder, cacheState='preparing', cacheCompleted=0, cacheTotal=len(jobs), cacheError='')
    seed_existing(remote,cache,jobs)
    def work(job):
        require_consent(load(folder))
        source = cache/job['file']
        synthesize(job, source, keys, job['voiceId'])
        decode(source, ffmpeg)
        os.chmod(source, 0o600); os.chmod(cache/(job['file']+SUFFIX), 0o600)
        return {**job, 'sha256':digest(source), 'decodedSha256':digest(cache/(job['file']+SUFFIX))}
    entries = []
    with ThreadPoolExecutor(max_workers=2) as pool:
        for count, future in enumerate(as_completed([pool.submit(work, job) for job in jobs]), 1):
            entries.append(future.result()); patch(folder, cacheCompleted=count)
    require_consent(load(folder))
    manifest = dict(schema=1, id=state['id'], participantCode=state['participantCode'], consentId=consent['id'],
                    cloneVoiceId=state['cloneVoiceId'], neutralGender=state['neutralGender'], neutralOption=state['neutralOption'],
                    development=state['development'], templateSha256=hashlib.sha256(raw).hexdigest(), model=MODEL,
                    generatedUtc=time.time(), entries=sorted(entries,key=lambda e:e['file']))
    path = folder/('audio-manifest-'+uuid.uuid4().hex+'.json'); atomic(path, manifest)
    patch(folder, cacheState='ready', cacheManifest=path.name, cacheManifestSha256=digest(path))
    return manifest


def transfer(folder, remote):
    state = load(folder); require_consent(state)
    if state.get('cacheState') not in ('ready','transferred'): raise ValueError('Prepare the entire study audio library first.')
    patch(folder,cacheState='ready',cacheError='')
    manifest_path = folder/state['cacheManifest']; manifest = json.loads(manifest_path.read_text())
    if (digest(manifest_path) != state['cacheManifestSha256'] or manifest['id'] != state['id'] or
            manifest['cloneVoiceId'] != state['cloneVoiceId']): raise ValueError('Audio manifest identity mismatch.')
    template = remote.command('exec-out','cat '+shlex.quote(remote.root+'PreparationLogs/study-audio-template.json'))
    if hashlib.sha256(template).hexdigest() != manifest['templateSha256']:
        raise ValueError('Headset phrase library changed. Prepare audio again before transfer.')
    status = remote.headset_state()
    if not status or not status.get('intakeAvailable') or not 0 <= time.time()-status.get('updatedUtc',0) <= 15:
        raise ValueError('Headset must be connected and waiting for a participant before transfer.')
    cache = folder/'audio-cache'; expected = {}
    for entry in manifest['entries']:
        for suffix, field in [('', 'sha256'), (SUFFIX, 'decodedSha256')]:
            name = entry['file']+suffix
            if not re.fullmatch(r'tts_[a-f0-9]{64}\.mp3(?:\.decoded-v1)?',name) or digest(cache/name) != entry[field]:
                raise ValueError('Cached audio changed; preparation must be repeated.')
            expected[name] = entry[field]
        if not decoded_valid(cache/(entry['file']+SUFFIX),cache/entry['file']): raise ValueError('Invalid decoded audio.')
    # Never replace a prior recording/cache. A conflict fails the verification below.
    stage = remote.root+'intake-staging-'+state['id']+'-'+uuid.uuid4().hex
    bundle = folder/('transfer-'+uuid.uuid4().hex+'.tar')
    try:
        with tarfile.open(bundle,'w') as archive:
            for name in expected: archive.add(cache/name,arcname=name)
        remote.command('shell','mkdir -p '+shlex.quote(stage)+' '+shlex.quote(remote.root+'tts_cache'))
        remote.command('push',str(bundle),stage+'/cache.tar',timeout=240)
        remote.command('shell','cd '+shlex.quote(stage)+' && tar -xf cache.tar && for f in tts_*.mp3; do '
                       '[ -f "$f" ] || continue; [ -e ../tts_cache/"$f" ] || mv "$f" ../tts_cache/; done && '
                       'for f in tts_*.decoded-v1; do [ -f "$f" ] || continue; mv "$f" ../tts_cache/; done',timeout=240)
        names = list(expected); actual = {}
        for offset in range(0,len(names),80):
            output = remote.command('shell','cd '+shlex.quote(remote.root+'tts_cache')+' && sha256sum '+' '.join(names[offset:offset+80]),timeout=90)
            for line in output.decode().splitlines():
                parts = line.split()
                if len(parts)==2: actual[parts[1]] = parts[0]
        if actual != expected: raise ValueError('Headset audio hashes differ. Existing files were preserved; no assignment is authorized.')
        require_consent(load(folder))
        target = remote.root+'PreparationLogs/intake-audio-'+state['id']+'.json'
        remote.command('push',str(manifest_path),target+'.tmp')
        remote.command('shell','mv '+shlex.quote(target+'.tmp')+' '+shlex.quote(target))
        if remote.command('shell','sha256sum '+shlex.quote(target)).decode().split()[0] != state['cacheManifestSha256']:
            raise ValueError('Headset manifest verification failed.')
        patch(folder,cacheState='transferred',transferredUtc=time.time(),transferEndpoint=remote.endpoint,cacheError='')
    finally:
        # Only our disposable transfer bundle; answer audio/manifests are retained.
        bundle.unlink(missing_ok=True)


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--folder',required=True,type=Path)
    parser.add_argument('--endpoint',default=''); parser.add_argument('--transfer',action='store_true')
    parser.add_argument('--preview',action='store_true'); parser.add_argument('--gender',choices=['Male','Female']); parser.add_argument('--option',type=int)
    args=parser.parse_args(); os.umask(0o077)
    lock=(args.folder/'audio-worker.lock').open('a')
    try: fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    except BlockingIOError: raise SystemExit('Participant audio worker already running.')
    try:
        adb=shutil.which('adb') or str(Path.home()/'Library/Android/sdk/platform-tools/adb')
        remote=Adb(adb,wireless_endpoint(adb,args.endpoint),'com.DefaultCompany.MixedRealityTemplate'); remote.connect()
        if args.preview:
            state=load(args.folder);require_consent(state)
            if state['phase']!='select_voice': raise ValueError('Voice selection is closed for this session.')
            template=json.loads(remote.command('exec-out','cat '+shlex.quote(remote.root+'PreparationLogs/study-audio-template.json')))
            voices=[v for v in template['voices'] if v['gender']==args.gender and v['option']==args.option and v['voiceId']!=INTERVIEW_VOICE]
            if len(voices)!=1: raise ValueError('Choose a study catalog voice.')
            name='audition-'+uuid.uuid4().hex+'.mp3'
            synthesize(dict(request=json.dumps(dict(text='Hello. I will guide you during the search. Look to your left, then look a little higher.',model_id=MODEL))),
                       args.folder/name,Path.home()/'.config/follow-my-voice/api_keys.json',voices[0]['voiceId'])
            patch(args.folder,neutralGender=args.gender,neutralOption=args.option,previewAudio=name,previewRevision=time.time(),cacheError='')
        elif args.transfer: transfer(args.folder,remote)
        else: prepare(args.folder,remote,Path.home()/'.config/follow-my-voice/api_keys.json',shutil.which('ffmpeg') or '/opt/homebrew/bin/ffmpeg')
    except Exception as error:
        patch(args.folder,cacheError=str(error) if isinstance(error,(ValueError,RuntimeError)) else type(error).__name__)
        raise SystemExit(1)


if __name__=='__main__': main()
