#!/usr/bin/env python3
"""Wireless ADB receiver. Copies evidence and sends session-bound wall/participant assignments. Never starts timed trials."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import fcntl
import gzip
import zlib
import hashlib
import ipaddress
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import shutil
import subprocess
import time

ROOTS = ('GazeReplays', 'EyeMeasurements', 'GazeData', 'PreparationLogs')
CHUNK = 2 * 1024 * 1024
LINK_SCAN_CHUNK = 128 * 1024


def safe_relative(name):
    parts = PurePosixPath(name).parts
    if (not parts or parts[0] not in ROOTS or name.startswith('/') or
            any(p in ('.', '..') for p in parts) or
            not re.fullmatch(r'[A-Za-z0-9_./ -]+', name)):
        raise ValueError('Unsupported archive path')
    return name


def atomic_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    pending = path.with_name(path.name + '.tmp')
    with pending.open('w') as out:
        json.dump(value, out, indent=2)
        out.flush(); os.fsync(out.fileno())
    os.replace(pending, path)


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(CHUNK), b''): h.update(block)
    return h.hexdigest()


class Mirror:
    def __init__(self, directory, remote):
        self.directory, self.remote = directory.resolve(), remote
        self.directory.mkdir(parents=True, exist_ok=True)
        self.manifest_path = self.directory / 'wireless-manifest.json'
        self.entries = json.loads(self.manifest_path.read_text()).get('files', {}) if self.manifest_path.exists() else {}

    def path(self, name):
        path = self.directory / safe_relative(name)
        current = self.directory
        for part in PurePosixPath(name).parts:
            current = current / part
            if current.is_symlink(): raise ValueError('Archive symlinks are not supported')
        if self.directory not in path.resolve().parents: raise ValueError('Archive path escapes destination')
        return path

    def save(self):
        atomic_json(self.manifest_path, {'schema': 'wireless-archive-v1', 'updatedUtc': time.time(), 'files': self.entries})

    def copy(self, name, size, modified):
        path = self.path(name)
        old = self.entries.get(name, {})
        append = path.suffix in ('.jsonl', '.csv')
        offset = path.stat().st_size if append and path.exists() else 0
        if append and offset > size: raise ValueError('Source shrank; preserving local evidence: ' + name)
        if old.get('size') == size and old.get('modified') == modified and path.exists() and path.stat().st_size == size:
            return False
        # A same-size rewrite of an append log is not silently accepted.
        if append and offset == size and old and old.get('modified') != modified:
            if self.remote.digest(name) != sha(path): raise ValueError('Source log was rewritten: ' + name)
        count = min(CHUNK, size - offset) if append else size
        if not append and count > 128 * 1024 * 1024: raise ValueError('Oversized non-stream file')
        data = self.remote.read(name, offset, count) if count else b''
        if len(data) != count: raise OSError('Short transfer; retrying without advancing the saved offset')
        path.parent.mkdir(parents=True, exist_ok=True)
        if append:
            with path.open('ab') as out:
                out.write(data); out.flush(); os.fsync(out.fileno())
        else:
            pending = path.with_name(path.name + '.receiving')
            with pending.open('wb') as out:
                out.write(data); out.flush(); os.fsync(out.fileno())
            os.replace(pending, path)
        self.entries[name] = {'size': size, 'modified': modified, 'received': path.stat().st_size,
                              'sha256': None, 'verified': False}
        return True

    def verify(self, name, size, modified):
        path = self.path(name)
        if not path.exists() or path.stat().st_size != size: return False
        digest = self.remote.digest(name)
        ok = digest == sha(path)
        self.entries[name] = {'size': size, 'modified': modified, 'received': size,
                              'sha256': digest if ok else None, 'verified': ok}
        return ok


class Adb:
    def __init__(self, executable, endpoint, package):
        self.executable, self.endpoint = executable, endpoint
        self.compress_text = False
        if not re.fullmatch(r'[A-Za-z0-9_.]+', package): raise ValueError('Invalid application package')
        self.root = '/sdcard/Android/data/' + package + '/files/'

    def command(self, *args, timeout=20):
        result = subprocess.run([self.executable, '-s', self.endpoint, *args], capture_output=True, timeout=timeout)
        if result.returncode: raise OSError(result.stderr.decode(errors='replace').strip() or 'ADB request failed')
        return result.stdout

    def connect(self):
        result = subprocess.run([self.executable, 'connect', self.endpoint], capture_output=True, timeout=12)
        if result.returncode: raise OSError('Cannot connect to wireless ADB')
        if self.command('get-state').strip() != b'device': raise OSError('Headset not authorized or offline')
        try:
            self.compress_text = gzip.decompress(self.command('exec-out', 'printf probe | gzip -1')) == b'probe'
        except (OSError, EOFError, zlib.error):
            self.compress_text = False

    def inventory(self):
        paths = ' '.join(shlex.quote(self.root + p) for p in ROOTS)
        # Missing optional folders are normal. The package directory itself must exist.
        self.command('shell', 'test -d ' + shlex.quote(self.root))
        raw = self.command('shell', "find " + paths + " -type f -exec stat -c '%s|%Y|%n' {} + 2>/dev/null; true")
        return self.parse_inventory(raw)

    def replay_inventory(self):
        # A small stat response discovers new sessions and current replay growth
        # without walking thousands of WAVs and archived raw-eye files each tick.
        path = shlex.quote(self.root + 'GazeReplays')
        raw = self.command('shell', "find " + path +
                           " -mindepth 2 -maxdepth 2 -type f -name recording.jsonl"
                           " -exec stat -c '%s|%Y|%n' {} + 2>/dev/null; true")
        return self.parse_inventory(raw)

    def parse_inventory(self, raw):
        result = {}
        for line in raw.decode().splitlines():
            size, modified, path = line.split('|', 2)
            name = safe_relative(path.removeprefix(self.root))
            if name.endswith(('.tmp', '.receiving')) or name in ('PreparationLogs/live-headset-state.json', 'PreparationLogs/live-headset-command.json'): continue
            result[name] = (int(size), int(modified))
        return result

    def read(self, name, offset, count):
        path = shlex.quote(self.root + safe_relative(name))
        command = f'dd if={path} bs=65536 skip={offset} count={count} iflag=skip_bytes,count_bytes status=none'
        compressed = self.compress_text and count >= 1024 * 1024 and name.endswith(('.jsonl', '.csv'))
        data = self.command('exec-out', command + (' | gzip -1' if compressed else ''))
        if not compressed: return data
        try:
            return gzip.decompress(data)
        except (OSError, EOFError, zlib.error) as error:
            raise OSError('Incomplete compressed transfer; retry without advancing offset') from error

    def headset_state(self):
        # Optional atomic heartbeat, independent of replay creation and archive scans.
        path = shlex.quote(self.root + 'PreparationLogs/live-headset-state.json')
        raw = self.command('exec-out', 'if test -f ' + path + '; then head -c 4097 ' + path + '; fi', timeout=2)
        if not raw: return None
        if len(raw) > 4096: raise ValueError('Oversized headset state')
        return validate_headset_state(json.loads(raw))

    def send_control(self, command):
        state = self.headset_state()
        validate_control(command, state, time.time())
        path = shlex.quote(self.root + 'PreparationLogs/live-headset-command.json')
        pending = shlex.quote(self.root + 'PreparationLogs/live-headset-command.json.tmp')
        payload = shlex.quote(json.dumps(command, separators=(',', ':')))
        self.command('shell', "printf '%s' " + payload + ' > ' + pending + ' && mv ' + pending + ' ' + path, timeout=2)

    def digest(self, name):
        result = self.command('shell', 'sha256sum ' + shlex.quote(self.root + safe_relative(name)), timeout=45).decode().split()[0]
        if not re.fullmatch(r'[0-9a-f]{64}', result): raise OSError('Invalid remote digest')
        return result


def validate_assistance_status(value):
    """Optional diagnostic data; malformed status never becomes a confident provider label."""
    if not isinstance(value, dict): return None
    mode, provider = value.get('mode'), value.get('provider')
    if (mode, provider) not in (('adaptive', 'jev'), ('adaptive', 'local_minilm'), ('baseline', 'baseline_rules')): return None
    if value.get('connection') not in ('not_checked', 'not_required', 'checking', 'ready', 'unavailable', 'configured', 'not_configured'): return None
    result = {key: value[key] for key in ('mode', 'provider', 'connection')}
    for key in ('modelVersion', 'policy'):
        field = value.get(key)
        if field is not None and (not isinstance(field, str) or not re.fullmatch(r'[A-Za-z0-9_.:/-]{1,128}', field)): return None
        result[key] = field
    captured = value.get('capturedAtSeconds')
    if type(captured) not in (int, float) or not math.isfinite(captured) or captured < 0: return None
    result['capturedAtSeconds'] = captured
    decision = value.get('lastDecisionId')
    if decision:
        if mode != 'adaptive' or not isinstance(decision, str) or not re.fullmatch(r'[0-9a-f]{32}', decision): return None
        if value.get('lastDecisionProvider') != provider: return None
        if value.get('lastStrategy') not in ('observe_1s', 'observe_2s', 'observe_4s', 'orient_brief', 'orient_explicit', 'refine_brief', 'refine_explicit', 'correct', 'encourage'): return None
        if value.get('lastOutcome') not in ('observing', 'queued'): return None
        at = value.get('lastDecisionAtSeconds')
        if type(at) not in (int, float) or not math.isfinite(at) or not 0 <= at <= captured: return None
        for key in ('lastDecisionId', 'lastDecisionProvider', 'lastStrategy', 'lastOutcome', 'lastDecisionAtSeconds'):
            result[key] = value[key]
    return result


def validate_headset_state(value):
    if not isinstance(value, dict) or value.get('schema') != 1:
        raise ValueError('Unsupported headset state')
    if value.get('phase') not in ('preparing', 'ready', 'trial', 'paused', 'error'):
        raise ValueError('Unknown headset phase')
    if not isinstance(value.get('sessionId'), str) or len(value['sessionId']) > 128:
        raise ValueError('Invalid headset session')
    for key in ('updatedUtc', 'progress01'):
        if not isinstance(value.get(key), (float, int)) or not math.isfinite(value[key]):
            raise ValueError('Invalid headset state number')
    if not 0 <= value['progress01'] <= 1 or not isinstance(value.get('progressKnown'), bool):
        raise ValueError('Invalid headset progress')
    result = {key: value.get(key) for key in
              ('schema', 'sessionId', 'revision', 'phase', 'progressKnown', 'progress01', 'updatedUtc')}
    result.update(wallControlAvailable=value.get('wallControlAvailable') is True,
                  wallsVisible=value.get('wallsVisible') is True,
                  commandId=value.get('commandId') if isinstance(value.get('commandId'), str) else '',
                  commandResult=value.get('commandResult') if value.get('commandResult') in ('applied', 'rejected') else '')
    result['assistance'] = validate_assistance_status(value.get('assistance'))
    result.update(intakeAvailable=value.get('intakeAvailable') is True)
    for key in ('participantSessionId', 'participantCode', 'configuration', 'assignmentState', 'nextParticipantCode'):
        result[key] = value.get(key, '') if isinstance(value.get(key, ''), str) else ''
    return result


def validate_assignment(value):
    if not isinstance(value, dict) or value.get('schema') != 1:
        raise ValueError('Invalid participant assignment')
    for key, pattern in [('id', r'[0-9a-f]{32}'), ('participantCode', r'P[0-9]{3,12}'), ('cloneVoiceId', r'[A-Za-z0-9_-]{8,128}')]:
        if not isinstance(value.get(key), str) or not re.fullmatch(pattern, value[key]):
            raise ValueError('Invalid participant assignment field: '+key)
    if value.get('configuration') not in ('A','B') or value.get('neutralGender') not in ('Male','Female') or type(value.get('neutralOption')) is not int or not 0 <= value['neutralOption'] < 6:
        raise ValueError('Invalid condition or neutral voice choice')
    if set(value) != {'schema','id','participantCode','configuration','neutralGender','neutralOption','cloneVoiceId'}:
        raise ValueError('Assignment must not contain interview data')
    return value


def validate_control(command, state, now):
    if not isinstance(command, dict) or command.get('schema') != 1 or command.get('action') not in ('set_walls', 'assign_session'):
        raise ValueError('Unsupported headset control')
    if not isinstance(command.get('id'), str) or not re.fullmatch(r'[0-9a-f]{32}', command['id']):
        raise ValueError('Invalid control ID')
    created = command.get('createdUtc')
    if type(created) not in (float, int) or not math.isfinite(created) or not 0 <= now-created <= 10:
        raise ValueError('Headset control expired; send again while connected')
    observed = command.get('observedUtc')
    if type(observed) not in (float, int) or not math.isfinite(observed):
        raise ValueError('Invalid headset observation time')
    revision = command.get('observedRevision')
    if (not state or state.get('phase') == 'paused' or
            command.get('sessionId') != state.get('sessionId') or
            type(revision) is not int or type(state.get('revision')) is not int or
            revision <= 0 or not 0 <= state['revision']-revision <= 20):
        raise ValueError('Headset session unavailable or changed')
    if command['action'] == 'set_walls':
        if state.get('wallControlAvailable') is not True or type(command.get('wallsVisible')) is not bool:
            raise ValueError('Walls unavailable')
    else:
        assignment = validate_assignment(command.get('assignment'))
        if state.get('intakeAvailable') is not True and state.get('participantSessionId') != assignment['id']:
            raise ValueError('Headset is busy with another session')
    return command


def wireless_endpoint(adb, requested):
    if requested:
        host, sep, port = requested.partition(':')
        ipaddress.IPv4Address(host)
        port = int(port) if sep else 5555
        if not 1 <= port <= 65535: raise ValueError('Invalid port')
        return f'{host}:{port}'
    devices = subprocess.check_output([adb, 'devices'], timeout=10).decode().splitlines()[1:]
    serials = [line.split()[0] for line in devices if len(line.split()) > 1 and line.split()[1] == 'device']
    wireless = [s for s in serials if re.fullmatch(r'\d+\.\d+\.\d+\.\d+:\d+', s)]
    if len(wireless) == 1: return wireless[0]
    if len(wireless) > 1: raise ValueError('Multiple wireless headsets: enter its IP address')
    if len(serials) != 1: raise ValueError('Connect one headset by USB once, or enter its Wi-Fi IP address')
    serial = serials[0]
    raw = subprocess.check_output([adb, '-s', serial, 'shell', 'ip -4 addr show wlan0'], timeout=10).decode()
    match = re.search(r'inet (\d+\.\d+\.\d+\.\d+)/', raw)
    if not match: raise ValueError('Headset has no Wi-Fi address')
    subprocess.run([adb, '-s', serial, 'tcpip', '5555'], check=True, capture_output=True, timeout=10)
    time.sleep(2)
    return match[1] + ':5555'


class Receiver:
    def __init__(self, output, remote):
        self.output, self.remote = output, remote
        self.mirror = Mirror(output / 'data', remote)
        self.state_path = output / 'receiver-state.json'
        self.state = json.loads(self.state_path.read_text()) if self.state_path.exists() else {}
        self.selected = set(self.state.get('selected', []))
        self.baseline = set(self.state.get('baseline', []))
        self.initialized = self.state.get('schema') == 2
        self.parsed = {safe_relative(name): offset for name, offset in self.state.get('parsed', {}).items()
                       if isinstance(offset, int) and offset >= 0}
        self.spoken_audio = {}; self.audio_seeded = set()
        self.tails = {}; self.stable = {}; self.revision = 0; self.audio_revision = 0
        self.last_data = 0; self.latest = ''
        # Activity is source growth observed across inventories, not archive catch-up
        # or an unrelated CSV/WAV arriving. Re-establish it after every restart.
        self.replay_signature = None; self.replay_updated = 0; self.replay_pending = 0
        self.inventory_cache = None; self.inventory_at = float('-inf')
        self.saved_at = float('-inf')
        self.verifier = None; self.verification = None
        self.inventory_worker = None; self.inventory_future = None
        self.checked_files = {}; self.checked_at = float('-inf')
        self.headset_state = None; self.headset_polled = float('-inf')
        self.headset_observed = 0; self.headset_worker = None; self.headset_future = None
        self.control_id = ''; self.control_result = ''; self.control_future = None

    def close(self):
        if self.headset_worker is not None: self.headset_worker.shutdown(wait=True)
        if self.verifier is not None: self.verifier.shutdown(wait=True)
        if self.inventory_worker is not None: self.inventory_worker.shutdown(wait=True)

    def current_inventory(self):
        now = time.monotonic()
        # Periodically recheck local files even if the headset copies are unchanged.
        if now - self.checked_at >= 30:
            self.checked_files.clear(); self.checked_at = now
        full = self.inventory_cache is None
        if full:
            self.inventory_cache = self.remote.inventory()
            self.inventory_at = now
        else:
            if self.inventory_future is not None and self.inventory_future.done():
                future = self.inventory_future; self.inventory_future = None
                self.inventory_at = now
                # Surface connection errors through the existing reconnect path.
                self.inventory_cache = future.result(); full = True
            if self.inventory_future is None and now - self.inventory_at >= 3:
                if self.inventory_worker is None:
                    self.inventory_worker = ThreadPoolExecutor(max_workers=1)
                self.inventory_future = self.inventory_worker.submit(self.remote.inventory)
            # Fresh replay stats override the background archive snapshot, including
            # removals. An expensive archive scan never delays current replay polling.
            latest = self.remote.replay_inventory()
            self.inventory_cache = {p: v for p, v in self.inventory_cache.items()
                                    if not (p.startswith('GazeReplays/') and p.endswith('/recording.jsonl'))}
            self.inventory_cache.update(latest)
        return self.inventory_cache, full

    def finish_verification(self):
        if self.verification is None: return
        name, signature, future = self.verification
        if not future.done(): return
        self.verification = None
        entry = self.mirror.entries.get(name, {})
        if (entry.get('size'), entry.get('modified')) != signature: return
        try:
            remote_digest, local_digest = future.result()
        except (OSError, subprocess.SubprocessError):
            # A slow/failed background hash must not take down a healthy live stream.
            self.stable[name] = (signature, time.monotonic())
            return
        if remote_digest != local_digest:
            if self.remote.inventory().get(name) == signature:
                raise ValueError('Backup hash mismatch; preserve source and local copy: ' + name)
            return
        entry.update(sha256=remote_digest, verified=True)

    def start_verification(self, name, signature):
        if self.verifier is None: self.verifier = ThreadPoolExecutor(max_workers=1)
        path = self.mirror.path(name)
        # Workers only read. Manifest mutations stay on the receiver thread.
        def digests(): return self.remote.digest(name), sha(path)
        self.verification = (name, signature, self.verifier.submit(digests))

    def remember_audio(self, name, record):
        if record.get('kind') != 'audio_playback_start': return
        clip = record.get('clip', '')
        # Only recorded WAVs inside this replay package can be promoted.
        if not isinstance(clip, str) or not re.fullmatch(r'audio/[A-Za-z0-9_-]+\.wav', clip): return
        path = str(PurePosixPath(name).parent / clip)
        recent = self.spoken_audio.setdefault(name, [])
        if path in recent: recent.remove(path)
        recent.append(path)
        del recent[:-64]

    def seed_recent_audio(self, name):
        if name in self.audio_seeded: return
        self.audio_seeded.add(name)
        path = self.mirror.path(name)
        if not path.is_file(): return
        with path.open('rb') as source:
            offset = max(0, path.stat().st_size - 1024 * 1024)
            source.seek(offset)
            if offset: source.readline()  # skip a potentially partial first line
            lines = source.read().split(b'\n')[:-1]
        for line in lines:
            try: self.remember_audio(name, json.loads(line))
            except (ValueError, UnicodeDecodeError): continue

    def copy_spoken_audio(self, name, inventory):
        # One urgent atomic WAV may exceed the normal tick budget. Without this
        # reservation, a continuously growing replay can starve its own audio.
        for clip in reversed(self.spoken_audio.get(name, [])):
            if clip not in inventory: continue
            size, modified = inventory[clip]
            if self.mirror.copy(clip, size, modified):
                self.revision += 1; self.audio_revision += 1; self.last_data = time.time()
                self.checked_files[clip] = (size, modified)
                self.stable[clip] = ((size, modified), time.monotonic())
                return

    def select_links(self, name, inventory):
        path = self.mirror.path(name)
        offset = self.parsed.get(name, 0)
        if offset > path.stat().st_size:
            offset = 0; self.tails.pop(name, None)
        with path.open('rb') as f:
            f.seek(offset); data = f.read(LINK_SCAN_CHUNK)
        self.parsed[name] = offset + len(data)
        data = self.tails.get(name, b'') + data
        lines = data.split(b'\n'); self.tails[name] = lines.pop()
        eye_folders = {p.split('/')[1].split('_', 1)[-1]: '/'.join(p.split('/')[:2])
                       for p in inventory if p.startswith('EyeMeasurements/')}
        for line in lines:
            try: record = json.loads(line)
            except (ValueError, UnicodeDecodeError): raise ValueError('Malformed complete replay line: ' + name)
            self.remember_audio(name, record)
            participant, run = record.get('participantCode'), record.get('sourceRunFolder')
            if participant and run:
                folder = safe_relative(f'GazeData/{participant}/{run}')
                self.selected.add(folder)
            eye_path = record.get('gazeRecordingPath', '')
            if '/EyeMeasurements/' in eye_path:
                self.selected.add(safe_relative('EyeMeasurements/' + eye_path.split('/EyeMeasurements/', 1)[1].split('/')[0]))
            eye_id = record.get('gazeRecordingId')
            if eye_id in eye_folders:
                self.selected.add(eye_folders[eye_id])

    def poll_headset_state(self):
        if self.headset_future is not None and self.headset_future.done():
            future = self.headset_future; self.headset_future = None
            try:
                value = future.result()
                if value != self.headset_state:
                    self.headset_observed = time.time() if value is not None and self.headset_state is not None else 0
                self.headset_state = value
            except (OSError, ValueError, subprocess.SubprocessError):
                # Optional telemetry failure must not interrupt replay transfer.
                self.headset_state = None; self.headset_observed = 0
        if self.headset_future is None and time.monotonic() - self.headset_polled >= 1:
            reader = getattr(self.remote, 'headset_state', None)
            if reader is None: return
            if self.headset_worker is None: self.headset_worker = ThreadPoolExecutor(max_workers=1)
            self.headset_polled = time.monotonic()
            self.headset_future = self.headset_worker.submit(reader)

    def poll_control(self):
        if self.control_future is not None:
            if not self.control_future.done(): return
            try:
                self.control_future.result()
                self.control_result = 'sent'
            except (OSError, ValueError, subprocess.SubprocessError):
                self.control_result = 'failed'
            self.control_future = None
        path = self.output / 'headset-command.json'
        if not path.is_file(): return
        try:
            if path.stat().st_size > 4096: return
            command = json.loads(path.read_text())
            if not isinstance(command, dict) or command.get('id') == self.control_id: return
            self.control_id = command.get('id', '')
            self.control_result = 'rejected'
            if not 0 < time.time() - self.headset_observed < 5: return
            validate_control(command, self.headset_state, time.time())
            if self.headset_worker is None: self.headset_worker = ThreadPoolExecutor(max_workers=1)
            self.control_result = 'sending'
            self.control_future = self.headset_worker.submit(self.remote.send_control, command)
        except (OSError, ValueError):
            self.control_result = 'rejected'

    def cycle(self):
        self.poll_headset_state()
        self.poll_control()
        self.finish_verification()
        inventory, full_inventory = self.current_inventory()
        replay_dirs = sorted({str(PurePosixPath(p).parent) for p in inventory if p.startswith('GazeReplays/') and p.endswith('/recording.jsonl')})
        folders = {}
        for name, (_, modified) in inventory.items():
            parts = name.split('/')
            length = 3 if parts[0] == 'GazeData' else 2
            if len(parts) <= length: continue
            directory = '/'.join(parts[:length])
            folders[directory] = max(modified, folders.get(directory, 0))
        # Include the most recent package when joining, then every newly appearing run.
        if not self.initialized:
            self.baseline = set(folders)
            if replay_dirs: self.selected.add(replay_dirs[-1])
            for root in ROOTS[1:]:
                candidates = [d for d in folders if d.startswith(root + '/')]
                if candidates: self.selected.add(max(candidates, key=lambda d: (folders[d], d)))
            self.initialized = True
        self.selected.update(set(folders) - self.baseline)
        self.baseline.update(folders)
        selected_replays = [d for d in replay_dirs if d in self.selected]
        now = time.monotonic()
        if selected_replays:
            name = selected_replays[-1] + '/recording.jsonl'
            signature = (name, inventory[name][0])
            previous = self.replay_signature
            if previous is not None and previous[0] == name and signature[1] > previous[1]:
                self.replay_updated = time.time()
            elif previous is None or previous[0] != name:
                self.replay_updated = 0
            self.replay_signature = signature
        # Replay first, newest first: live preview is never held behind raw-eye catch-up.
        ordered = [d + '/recording.jsonl' for d in reversed(selected_replays)]
        newest_replay = ordered[0] if ordered else None
        if newest_replay: self.seed_recent_audio(newest_replay)
        # Prepared, normalized WAVs are exported during Loading, before playback.
        # Copy them ahead of bulk telemetry; playback events later reuse these files.
        ordered += [p for d in reversed(selected_replays) for p in inventory
                    if p.startswith(d + '/audio/') and p.endswith('.wav')]
        priority = set(ordered)
        ordered += [p for p in inventory if p not in priority]
        copied = 0
        transfer_started = time.monotonic()
        for name in ordered:
            if not any(name.startswith(d + '/') for d in self.selected): continue
            size, modified = inventory[name]
            entry = self.mirror.entries.get(name, {})
            if self.checked_files.get(name) == (size, modified) and \
                    (entry.get('size'), entry.get('modified'), entry.get('received')) == (size, modified, size):
                if name.endswith('/recording.jsonl') and self.parsed.get(name, 0) < size:
                    self.select_links(name, inventory)
                if name == newest_replay: self.copy_spoken_audio(name, inventory)
                if time.monotonic() - transfer_started >= .25: break
                continue
            if self.mirror.copy(name, size, modified):
                self.revision += 1; copied += 1; self.last_data = time.time()
                if name.endswith('.wav'): self.audio_revision += 1
            self.checked_files[name] = (size, modified)
            if name.endswith('/recording.jsonl'): self.select_links(name, inventory)
            if name == newest_replay: self.copy_spoken_audio(name, inventory)
            signature = (size, modified)
            old = self.stable.get(name)
            if old is None or old[0] != signature: self.stable[name] = (signature, now)
            # Revisit playback events frequently even while a whole library is
            # arriving. Finish each atomic file, then yield after a bounded batch.
            if time.monotonic() - transfer_started >= .25: break
        if selected_replays:
            name = selected_replays[-1] + '/recording.jsonl'
            self.latest = str(self.mirror.path(name))
            self.replay_pending = max(0, inventory[name][0] - self.mirror.entries.get(name, {}).get('received', 0))
        # One stable file per tick: hashing full raw streams must not stall live ingest.
        for name, (signature, since) in self.stable.items():
            # Full-file hashes can block for seconds. Finish verification when live
            # source growth stops; all received bytes are still persisted each pass.
            if self.verification is not None or self.replay_updated and time.time() - self.replay_updated < 2: break
            entry = self.mirror.entries.get(name, {})
            if now - since >= 3 and not entry.get('verified') and entry.get('received') == signature[0]:
                self.start_verification(name, signature)
                break
        # Data bytes are fsynced on transfer. Checkpoint the larger manifest once
        # per second rather than rewriting it for every low-latency replay poll.
        if time.monotonic() - self.saved_at >= 1 or full_inventory:
            self.mirror.save()
            atomic_json(self.state_path, {'schema': 2, 'selected': sorted(self.selected), 'baseline': sorted(self.baseline),
                'parsed': {name: offset - len(self.tails.get(name, b'')) for name, offset in self.parsed.items()}})
            self.saved_at = time.monotonic()
        self.status('connected', f'Wi-Fi connected · {len(selected_replays)} run packages selected')
        return copied

    def status(self, state, message):
        entries = list(self.mirror.entries.values())
        atomic_json(self.output / 'live-status.json', {
            'schema': 1, 'state': state, 'message': message, 'endpoint': self.remote.endpoint,
            'updatedUtc': time.time(), 'lastDataUtc': self.last_data, 'revision': self.revision, 'audioRevision': self.audio_revision,
            'replayUpdatedUtc': self.replay_updated, 'replayPendingBytes': self.replay_pending,
            'recording': self.latest, 'archive': str(self.mirror.directory),
            'headsetState': self.headset_state, 'headsetStateObservedUtc': self.headset_observed,
            'controlId': self.control_id, 'controlResult': self.control_result,
            'files': len(entries), 'verifiedFiles': sum(bool(x.get('verified')) for x in entries),
            'receivedBytes': sum(x.get('received', 0) for x in entries),
            'pendingBytes': sum(max(0, x.get('size', 0)-x.get('received', 0)) for x in entries),
        })


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--endpoint', default='')
    parser.add_argument('--adb', default=shutil.which('adb') or str(Path.home()/'Library/Android/sdk/platform-tools/adb'))
    parser.add_argument('--package', default='com.DefaultCompany.MixedRealityTemplate')
    parser.add_argument('--parent', type=int)
    parser.add_argument('--once', action='store_true')
    args = parser.parse_args()
    args.output = args.output.expanduser().resolve(); args.output.mkdir(parents=True, exist_ok=True)
    lock = (args.output / 'receiver.lock').open('w')
    try: fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError: raise SystemExit('A receiver already owns this archive')
    endpoint = wireless_endpoint(args.adb, args.endpoint)
    remote = Adb(args.adb, endpoint, args.package)
    marker = args.output / 'headset.json'
    receiver = Receiver(args.output, remote)
    stop = args.output / 'stop-request'
    stop.unlink(missing_ok=True)
    receiver.status('connecting', 'Starting wireless transfer')
    failed = False
    connected = False
    try:
        while not stop.exists():
            if args.parent:
                try: os.kill(args.parent, 0)
                except ProcessLookupError: break
            try:
                if not connected:
                    remote.connect()
                    # Validate identity after every connection, before copying. An
                    # initially sleeping headset should wait/retry, not kill the receiver.
                    identity = remote.command('shell', 'getprop ro.serialno').decode().strip()
                    identity_data = {'serial': identity, 'package': args.package}
                    if not identity: raise ValueError('Headset identity unavailable')
                    if marker.exists() and json.loads(marker.read_text()) != identity_data:
                        raise ValueError('This archive belongs to another headset/application')
                    atomic_json(marker, identity_data)
                    receiver.headset_state = None; receiver.headset_observed = 0
                    receiver.inventory_cache = None
                    receiver.inventory_future = None
                    receiver.checked_files.clear()
                    receiver.replay_signature = None; receiver.replay_updated = 0
                    connected = True
                receiver.cycle()
            except (OSError, subprocess.SubprocessError) as error:
                connected = False
                receiver.status('disconnected', 'Connection interrupted; local recording continues. ' + str(error))
                if args.once: raise
                time.sleep(2)
            if args.once: break
            time.sleep(.1)
    except Exception as error:
        failed = True
        receiver.status('error', str(error)); raise
    finally:
        receiver.close()
        receiver.mirror.save()
        if not failed:
            receiver.status('stopped', 'Receiver stopped; saved files retained. Reconnect to resume.')
        lock.close()


if __name__ == '__main__': main()
