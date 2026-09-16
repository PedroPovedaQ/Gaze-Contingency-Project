#!/usr/bin/env python3
"""Silent Android screen + separate macOS microphone, finalized on disconnect."""
import argparse
import datetime
import fcntl
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time

BASE = Path.home() / 'Movies/GazeRecordings'
LATEST = BASE / 'latest-session.txt'


def binary(name):
    found = shutil.which(name)
    if not found and name == 'adb':
        matches = sorted(Path('/Applications/Unity/Hub/Editor').glob('*/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'))
        found = str(matches[-1]) if matches else None
    if not found:
        raise RuntimeError(f'{name} is required but not installed.')
    return found


def save(path, data):
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(data, indent=2))
    temporary.replace(path)


def state(folder, value, **details):
    save(folder / 'status.json', dict(state=value, updated_unix=time.time(), **details))


def read(path):
    return json.loads(path.read_text())


def devices():
    result = subprocess.run([binary('ffmpeg'), '-hide_banner', '-f', 'avfoundation', '-list_devices', 'true', '-i', ''], capture_output=True, text=True)
    audio = result.stderr.partition('AVFoundation audio devices:')[2]
    return [(int(i), name.strip()) for i, name in re.findall(r'\[(\d+)\] ([^\n]+)', audio)]


def connected(serial):
    try:
        result = subprocess.run([binary('adb'), 'devices'], capture_output=True, text=True, timeout=8)
        return f'{serial}\tdevice' in result.stdout
    except subprocess.TimeoutExpired:
        return False


def probe(path):
    result = subprocess.run([binary('ffprobe'), '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(f'Invalid media: {path.name}; originals preserved.')
    return json.loads(result.stdout)


def merge(folder, adjustment=0):
    data = read(folder / 'capture.json')
    video = probe(folder / 'screen.mkv')
    video_duration = float(video['format']['duration'])
    duration = max(video_duration, data.get('recording_end_unix', data['video_launch_unix'] + video_duration) - data['video_launch_unix'])
    command = [binary('ffmpeg'), '-hide_banner', '-nostdin', '-i', str(folder / 'screen.mkv')]
    tracks = [('commentary.wav', 'microphone_launch_unix')]
    if 'earlier_microphone_launch_unix' in data:
        tracks.insert(0, ('earlier-mac-microphone.wav', 'earlier_microphone_launch_unix'))
    filters, offsets = [], []
    for index, (name, key) in enumerate(tracks, 1):
        probe(folder / name)
        command += ['-i', str(folder / name)]
        offset = data[key] - data['video_launch_unix'] + adjustment
        offsets.append(offset)
        shift = f'adelay={round(offset * 1000)}:all=1' if offset >= 0 else f'atrim=start={-offset},asetpts=PTS-STARTPTS'
        filters.append(f'[{index}:a]{shift},apad[a{index}]')
    labels = ''.join(f'[a{i}]' for i in range(1, len(tracks) + 1))
    filters.append(labels + f'amix=inputs={len(tracks)}:normalize=0,alimiter=limit=0.95[out]')
    output = folder / ('vr-session.mp4' if not (folder / 'vr-session.mp4').exists() else f'vr-session-{time.time_ns()}.mp4')
    video_options = ['-c:v', 'copy']
    if duration > video_duration + .2:
        video_options = ['-vf', f'tpad=stop_mode=clone:stop_duration={duration - video_duration}', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20']
    command += ['-filter_complex', ';'.join(filters), '-map', '0:v:0', '-map', '[out]', *video_options, '-c:a', 'aac', '-b:a', '192k', '-t', str(duration), '-movflags', '+faststart', str(output)]
    with (folder / 'merge.log').open('w') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError('Merge failed. Original tracks remain; inspect merge.log.')
    streams = probe(output)['streams']
    if not any(s['codec_type'] == 'video' for s in streams) or not any(s['codec_type'] == 'audio' for s in streams):
        raise RuntimeError('Merged output is missing a stream.')
    state(folder, 'complete', output=str(output), duration_seconds=duration, audio_offsets_seconds=offsets,
          synchronization='Approximate process-start alignment; use a spoken/visible cue and merge --audio-offset for refinement.')
    return output


def worker(folder):
    data = read(folder / 'capture.json')
    children = []
    stopping = False
    def request_stop(*unused):
        nonlocal stopping
        stopping = True
    signal.signal(signal.SIGINT, request_stop)
    signal.signal(signal.SIGTERM, request_stop)
    def launch(args, log_name, key):
        with (folder / log_name).open('w') as log:
            stamp = time.time()
            child = subprocess.Popen(args, stdout=log, stderr=subprocess.STDOUT, env={**os.environ, 'ADB': binary('adb')})
        children.append(child)
        data[key + '_pid'] = child.pid
        data[key + '_launch_unix'] = stamp
        save(folder / 'capture.json', data)
        return child
    try:
        launch([binary('scrcpy'), '--serial=' + data['serial'], '--no-audio', '--no-control', '--no-playback', '--no-window', '--no-clipboard-autosync', '--max-size=1920', '--max-fps=30', '--record=' + str(folder / 'screen.mkv')], 'video.log', 'video')
        launch([binary('ffmpeg'), '-hide_banner', '-nostats', '-nostdin', '-f', 'avfoundation', '-i', ':' + str(data['mic_index']), '-vn', '-c:a', 'pcm_s16le', str(folder / 'commentary.wav')], 'microphone.log', 'microphone')
        deadline = time.monotonic() + 20
        while not all((folder / name).exists() and (folder / name).stat().st_size > minimum for name, minimum in [('screen.mkv', 1024), ('commentary.wav', 4096)]):
            if any(p.poll() is not None for p in children) or time.monotonic() > deadline:
                raise RuntimeError('Capture did not start; inspect video.log and microphone.log (including microphone permission).')
            time.sleep(.25)
        state(folder, 'recording', microphone=data['microphone_name'], worker_pid=os.getpid())
        misses = 0
        while not stopping and not (folder / 'stop-recording').exists():
            misses = 0 if connected(data['serial']) else misses + 1
            if misses >= 2:
                break
            if any(p.poll() is not None for p in children) and misses == 0:
                raise RuntimeError('A capture process stopped while the device remained connected.')
            time.sleep(2)
        data['recording_end_unix'] = time.time()
        state(folder, 'finalizing')
    except Exception as error:
        data['capture_error'] = str(error)
        data['recording_end_unix'] = time.time()
    finally:
        for p in children:
            if p.poll() is None:
                p.send_signal(signal.SIGINT)
        for p in children:
            try:
                p.wait(timeout=15)
            except subprocess.TimeoutExpired:
                p.kill()
                p.wait()
                data['capture_error'] = 'Capture failed to finalize in time; raw files preserved.'
        save(folder / 'capture.json', data)
    try:
        merge(folder)
        if data.get('capture_error'):
            result = read(folder / 'status.json')
            result['warning'] = data['capture_error']
            result['state'] = 'failed'
            save(folder / 'status.json', result)
    except Exception as error:
        state(folder, 'failed', error=str(error), capture_error=data.get('capture_error'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['devices', 'start', 'status', 'stop', 'merge', '_worker'])
    parser.add_argument('--session', type=Path)
    parser.add_argument('--serial')
    parser.add_argument('--mic', default='Yeti Stereo Microphone', help='Device name, matched from the current enumeration')
    parser.add_argument('--audio-offset', type=float, default=0, help='Additional audio delay in seconds; negative advances audio')
    args = parser.parse_args()
    BASE.mkdir(parents=True, exist_ok=True)
    if args.command == 'devices':
        print(json.dumps(dict(microphones=devices()), indent=2))
        return
    if args.command == 'start':
        with (BASE / '.start.lock').open('w') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            if LATEST.exists():
                previous = Path(LATEST.read_text().strip()) / 'status.json'
                if previous.exists() and read(previous)['state'] in ('starting', 'recording', 'finalizing'):
                    raise RuntimeError(f'An existing session is active: {previous.parent}. Stop it first.')
            microphones = devices()
            matches = [(i, n) for i, n in microphones if n.casefold() == args.mic.casefold()]
            if not matches:
                raise RuntimeError(f'Microphone {args.mic!r} unavailable. Run devices; no fallback was selected.')
            serial = args.serial
            if not serial:
                result = subprocess.check_output([binary('adb'), 'devices'], text=True)
                serials = re.findall(r'^([^\s]+)\tdevice$', result, re.M)
                if len(serials) != 1:
                    raise RuntimeError('Specify --serial when exactly one authorized device is not connected.')
                serial = serials[0]
            if not connected(serial):
                raise RuntimeError('Headset is not connected and authorized.')
            for name in ['scrcpy', 'ffmpeg', 'ffprobe']:
                binary(name)
            folder = args.session or BASE / datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
            folder = folder.resolve()
            folder.mkdir(parents=True, exist_ok=False)
            save(folder / 'capture.json', dict(serial=serial, mic_index=matches[0][0], microphone_name=matches[0][1]))
            state(folder, 'starting')
            LATEST.write_text(str(folder))
            with (folder / 'worker.log').open('w') as log:
                subprocess.Popen([sys.executable, str(Path(__file__).resolve()), '_worker', '--session', str(folder)], stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        for _ in range(90):
            if read(folder / 'status.json')['state'] != 'starting':
                break
            time.sleep(.25)
    else:
        folder = args.session or (Path(LATEST.read_text().strip()) if LATEST.exists() else None)
        if folder is None:
            raise RuntimeError('No latest session. Specify --session.')
        if args.command == '_worker':
            worker(folder)
            return
        if args.command == 'stop':
            (folder / 'stop-recording').touch()
        elif args.command == 'merge':
            if read(folder / 'status.json')['state'] in ('starting', 'recording', 'finalizing'):
                raise RuntimeError('Stop recording and wait for finalization before merging.')
            merge(folder, args.audio_offset)
    result = read(folder / 'status.json')
    if result['state'] == 'recording' and result.get('worker_pid'):
        try:
            os.kill(result['worker_pid'], 0)
        except ProcessLookupError:
            result.update(state='failed', error='Recording worker is no longer running; inspect raw tracks before recovery.')
    result['files_bytes'] = {name: (folder / name).stat().st_size for name in ['screen.mkv', 'commentary.wav'] if (folder / name).exists()}
    label = 'STOP REQUESTED' if args.command == 'stop' and result['state'] in ('starting', 'recording', 'finalizing') else result['state'].upper()
    print(label + ': ' + str(folder))
    print(json.dumps(dict(folder=str(folder), **result), indent=2))
    if result['state'] == 'failed':
        sys.exit(1)

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(json.dumps(dict(error=str(error))), file=sys.stderr)
        sys.exit(1)
