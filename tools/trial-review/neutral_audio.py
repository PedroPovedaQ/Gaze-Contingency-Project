#!/usr/bin/env python3
"""Prepare the headset's Eric quick-test catalog on the Mac. Never starts a trial."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import fcntl
import hashlib
import io
import json
from pathlib import Path
import re
import shlex
import shutil
import struct
import subprocess
import tarfile
import tempfile
import time
import urllib.error
import urllib.request
from live_bridge import Adb, atomic_json, wireless_endpoint

SUFFIX = '.decoded-v1'
VOICE = 'cjVigY5qzO86Huf0OWal'  # Eric, the existing neutral quick-test voice.
MODEL = 'eleven_turbo_v2_5'


def catalog_jobs(catalog):
    if catalog.get('schema') != 1 or catalog.get('voiceId') != VOICE or catalog.get('model') != MODEL:
        raise ValueError('Unsupported neutral catalog. Update the headset/viewer together.')
    jobs = catalog.get('phrases')
    if not isinstance(jobs, list) or not 1 <= len(jobs) <= 3000:
        raise ValueError('Invalid neutral catalog size')
    seen = set()
    for job in jobs:
        name = job.get('file', '')
        if not re.fullmatch(r'tts_[a-f0-9]{64}\.mp3', name) or name in seen:
            raise ValueError('Invalid or duplicate neutral cache key')
        body = json.loads(job['request'])
        if set(body) != {'text', 'model_id'} or body['model_id'] != MODEL or not isinstance(body['text'], str) or not 1 <= len(body['text']) <= 5000:
            raise ValueError('Invalid neutral synthesis request')
        seen.add(name)
    return jobs


def decoded_valid(path, source):
    try:
        data = path.read_bytes()
        magic, channels, hz, count = struct.unpack('<4siii', data[:16])
        return (magic == b'NPC1' and channels == 1 and hz == 44100 and 4410 <= count <= 44100*120 and
                len(data) == 80 + count*4 and data[16:48] == hashlib.sha256(source.read_bytes()).digest() and
                data[48:80] == hashlib.sha256(data[80:]).digest())
    except (OSError, struct.error):
        return False


def decode(source, ffmpeg):
    target = source.with_name(source.name + SUFFIX)
    if decoded_valid(target, source): return False
    pcm = subprocess.run([ffmpeg, '-v', 'error', '-i', str(source), '-f', 'f32le', '-ac', '1', '-ar', '44100', '-'],
                         capture_output=True, check=True, timeout=60).stdout
    if not 4410*4 <= len(pcm) <= 44100*120*4 or len(pcm) % 4:
        raise ValueError('Neutral audio is empty or too long')
    data = (struct.pack('<4siii', b'NPC1', 1, 44100, len(pcm)//4) + hashlib.sha256(source.read_bytes()).digest() +
            hashlib.sha256(pcm).digest() + pcm)
    pending = target.with_name(target.name + '.tmp'); pending.write_bytes(data); pending.replace(target)
    return True


def synthesize(job, source, keys):
    if source.exists(): return False
    key = json.loads(keys.read_text()).get('elevenlabs_key')
    if not key: raise ValueError('Add the ElevenLabs key to ~/.config/follow-my-voice/api_keys.json')
    for attempt in range(3):
        request = urllib.request.Request('https://api.elevenlabs.io/v1/text-to-speech/' + VOICE,
            data=job['request'].encode(), headers={'xi-api-key': key, 'Content-Type': 'application/json', 'Accept': 'audio/mpeg'})
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                if not response.headers.get('Content-Type', '').startswith('audio/'):
                    raise ValueError('Neutral provider returned non-audio data')
                data = response.read(8 * 1024 * 1024 + 1)
            if not 100 <= len(data) <= 8*1024*1024: raise ValueError('Invalid neutral audio size')
            pending = source.with_name(source.name + '.tmp'); pending.write_bytes(data); pending.replace(source)
            return True
        except urllib.error.HTTPError as error:
            if attempt == 2 or error.code not in (429, 500, 502, 503, 504):
                raise RuntimeError('Neutral synthesis failed: HTTP ' + str(error.code)) from None
            time.sleep(2 ** attempt)


def prepare(remote, cache, keys, ffmpeg, report):
    catalog = json.loads(remote.command('exec-out', 'cat ' + shlex.quote(remote.root + 'PreparationLogs/neutral-audio-catalog.json')))
    jobs = catalog_jobs(catalog)
    state = remote.headset_state()
    if state and not state.get('intakeAvailable'):
        raise ValueError('Finish or cancel the current headset session before preparing neutral audio.')
    report('Reading existing headset cache', 0, len(jobs))
    # Preserve provider recordings already on the headset; do not regenerate warm audio.
    existing = set(remote.command('shell', 'ls ' + shlex.quote(remote.root + 'tts_cache')).decode().splitlines())
    wanted = [j['file'] + suffix for j in jobs for suffix in ('', SUFFIX) if j['file'] + suffix in existing]
    digests = {}
    for offset in range(0, len(wanted), 100):
        response = remote.command('shell', 'cd ' + shlex.quote(remote.root + 'tts_cache') +
                                  ' && sha256sum ' + ' '.join(wanted[offset:offset+100]), timeout=60)
        for line in response.decode().splitlines():
            parts = line.split()
            if len(parts) == 2 and re.fullmatch('[a-f0-9]{64}', parts[0]) and parts[1] in wanted:
                digests[parts[1]] = parts[0]
    def already_present(name):
        path = cache / name
        return path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == digests.get(name)
    names = [j['file'] for j in jobs if j['file'] in existing and not already_present(j['file'])]
    if names:
        raw = remote.command('exec-out', 'tar -C ' + shlex.quote(remote.root + 'tts_cache') + ' -cf - ' + ' '.join(names), timeout=180)
        with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
            for member in archive:
                if member.name not in names or not member.isfile() or member.size > 8*1024*1024:
                    raise ValueError('Unexpected neutral cache archive entry')
                pending = cache / (member.name + '.tmp')
                pending.write_bytes(archive.extractfile(member).read()); pending.replace(cache / member.name)
    generated = decoded = 0
    def work(job):
        source = cache / job['file']
        generated = synthesize(job, source, keys)
        return generated, decode(source, ffmpeg)
    with ThreadPoolExecutor(max_workers=3) as pool:
        futures = [pool.submit(work, job) for job in jobs]
        for count, future in enumerate(as_completed(futures), 1):
            made, converted = future.result(); generated += made; decoded += converted
            report('Preparing neutral audio on Mac', count, len(jobs))
    publish = []
    for job in jobs:
        name = job['file']
        if name not in existing: publish.append(name)
        if not already_present(name + SUFFIX): publish.append(name + SUFFIX)
    if not publish: return dict(total=len(jobs), generated=generated, decoded=decoded, transferred=0)
    report('Copying prepared audio to headset', len(jobs), len(jobs))
    with tempfile.TemporaryDirectory(prefix='neutral-audio-') as temporary:
        bundle = Path(temporary) / 'cache.tar'
        with tarfile.open(bundle, 'w') as archive:
            for name in publish: archive.add(cache / name, arcname=name)
        stage = remote.root + 'neutral-cache-staging'
        remote.command('shell', 'mkdir -p ' + shlex.quote(stage))
        remote.command('push', str(bundle), stage + '/cache.tar', timeout=180)
        # Each file is published by rename on the same filesystem. Never replace an existing MP3.
        command = ('cd ' + shlex.quote(stage) + ' && tar -xf cache.tar && for f in tts_*.mp3; do '
                   '[ -f "$f" ] || continue; [ -e ../tts_cache/"$f" ] || mv "$f" ../tts_cache/; done && '
                   'for f in tts_*.decoded-v1; do [ -f "$f" ] || continue; mv "$f" ../tts_cache/; done && rm -f cache.tar tts_*.mp3')
        remote.command('shell', command, timeout=180)
    return dict(total=len(jobs), generated=generated, decoded=decoded, transferred=len(publish))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--endpoint', default='')
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--adb', default=shutil.which('adb') or str(Path.home()/'Library/Android/sdk/platform-tools/adb'))
    args = parser.parse_args()
    cache = Path.home()/'.local/share/follow-my-voice-neutral-audio'; cache.mkdir(parents=True, exist_ok=True)
    lock = (cache/'worker.lock').open('w')
    try: fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError: raise SystemExit('Neutral preparation is already running')
    started = time.monotonic()
    def report(stage, completed=0, total=0, **extra):
        atomic_json(args.output, dict(stage=stage, completed=completed, total=total, seconds=round(time.monotonic()-started, 2), **extra))
    try:
        report('Connecting to headset')
        ffmpeg = shutil.which('ffmpeg') or '/opt/homebrew/bin/ffmpeg'
        if not Path(ffmpeg).is_file(): raise ValueError('Install ffmpeg on the Mac to prepare neutral audio.')
        remote = Adb(args.adb, wireless_endpoint(args.adb, args.endpoint), 'com.DefaultCompany.MixedRealityTemplate'); remote.connect()
        result = prepare(remote, cache, Path.home()/'.config/follow-my-voice/api_keys.json', ffmpeg, report)
        report('Neutral audio ready on headset', result['total'], **result, ok=True)
    except Exception as error:
        report('Neutral preparation failed', error=str(error) if isinstance(error, (ValueError, RuntimeError)) else type(error).__name__, ok=False)
        raise SystemExit(1)


if __name__ == '__main__': main()
