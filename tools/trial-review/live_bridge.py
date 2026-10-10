#!/usr/bin/env python3
"""Wireless ADB receiver. Copies evidence; never starts trials or changes source files."""
import argparse
import fcntl
import hashlib
import ipaddress
import json
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import shutil
import subprocess
import time

ROOTS = ('GazeReplays', 'EyeMeasurements', 'GazeData', 'PreparationLogs')
CHUNK = 2 * 1024 * 1024


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

    def inventory(self):
        paths = ' '.join(shlex.quote(self.root + p) for p in ROOTS)
        # Missing optional folders are normal. The package directory itself must exist.
        self.command('shell', 'test -d ' + shlex.quote(self.root))
        raw = self.command('shell', "find " + paths + " -type f -exec stat -c '%s|%Y|%n' {} + 2>/dev/null; true")
        result = {}
        for line in raw.decode().splitlines():
            size, modified, path = line.split('|', 2)
            name = safe_relative(path.removeprefix(self.root))
            if name.endswith(('.tmp', '.receiving')): continue
            result[name] = (int(size), int(modified))
        return result

    def read(self, name, offset, count):
        path = shlex.quote(self.root + safe_relative(name))
        return self.command('exec-out', f'dd if={path} bs=65536 skip={offset} count={count} iflag=skip_bytes,count_bytes status=none')

    def digest(self, name):
        result = self.command('shell', 'sha256sum ' + shlex.quote(self.root + safe_relative(name)), timeout=45).decode().split()[0]
        if not re.fullmatch(r'[0-9a-f]{64}', result): raise OSError('Invalid remote digest')
        return result


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
        self.parsed = {}; self.tails = {}; self.stable = {}; self.revision = 0; self.audio_revision = 0
        self.last_data = 0; self.latest = ''
        # Activity is source growth observed across inventories, not archive catch-up
        # or an unrelated CSV/WAV arriving. Re-establish it after every restart.
        self.replay_signature = None; self.replay_updated = 0; self.replay_pending = 0

    def select_links(self, name, inventory):
        path = self.mirror.path(name)
        offset = self.parsed.get(name, 0)
        with path.open('rb') as f:
            f.seek(offset); data = f.read()
        self.parsed[name] = offset + len(data)
        data = self.tails.get(name, b'') + data
        lines = data.split(b'\n'); self.tails[name] = lines.pop()
        eye_folders = {p.split('/')[1].split('_', 1)[-1]: '/'.join(p.split('/')[:2])
                       for p in inventory if p.startswith('EyeMeasurements/')}
        for line in lines:
            try: record = json.loads(line)
            except (ValueError, UnicodeDecodeError): raise ValueError('Malformed complete replay line: ' + name)
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

    def cycle(self):
        inventory = self.remote.inventory()
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
        ordered += [p for p in inventory if p not in ordered]
        copied = 0
        for name in ordered:
            if not any(name.startswith(d + '/') for d in self.selected): continue
            size, modified = inventory[name]
            if self.mirror.copy(name, size, modified):
                self.revision += 1; copied += 1; self.last_data = time.time()
                if name.endswith('.wav'): self.audio_revision += 1
            if name.endswith('/recording.jsonl'): self.select_links(name, inventory)
            signature = (size, modified)
            old = self.stable.get(name)
            if old is None or old[0] != signature: self.stable[name] = (signature, now)
        if selected_replays:
            name = selected_replays[-1] + '/recording.jsonl'
            self.latest = str(self.mirror.path(name))
            self.replay_pending = max(0, inventory[name][0] - self.mirror.entries.get(name, {}).get('received', 0))
        # One stable file per tick: hashing full raw streams must not stall live ingest.
        for name, (signature, since) in self.stable.items():
            # Full-file hashes can block for seconds. Finish verification when live
            # source growth stops; all received bytes are still persisted each pass.
            if self.replay_updated and time.time() - self.replay_updated < 2: break
            entry = self.mirror.entries.get(name, {})
            if now - since >= 3 and not entry.get('verified') and entry.get('received') == signature[0]:
                if not self.mirror.verify(name, *signature):
                    # Source may still be growing; only declare corruption once its stat is unchanged.
                    if self.remote.inventory().get(name) == signature:
                        raise ValueError('Backup hash mismatch; preserve source and local copy: ' + name)
                break
        self.mirror.save()
        atomic_json(self.state_path, {'schema': 2, 'selected': sorted(self.selected), 'baseline': sorted(self.baseline)})
        self.status('connected', f'Wi-Fi connected · {len(selected_replays)} run packages selected')
        return copied

    def status(self, state, message):
        entries = list(self.mirror.entries.values())
        atomic_json(self.output / 'live-status.json', {
            'schema': 1, 'state': state, 'message': message, 'endpoint': self.remote.endpoint,
            'updatedUtc': time.time(), 'lastDataUtc': self.last_data, 'revision': self.revision, 'audioRevision': self.audio_revision,
            'replayUpdatedUtc': self.replay_updated, 'replayPendingBytes': self.replay_pending,
            'recording': self.latest, 'archive': str(self.mirror.directory),
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
        receiver.mirror.save()
        if not failed:
            receiver.status('stopped', 'Receiver stopped; saved files retained. Reconnect to resume.')
        lock.close()


if __name__ == '__main__': main()
