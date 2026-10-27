"""Start/reuse the adaptive model and tunnel headset loopback through authorized ADB.

No LAN listener, APK install, trial start, cloud credentials or implicit provider fallback.
Rerun after reconnecting ADB or restarting the Mac. Keep the Mac awake while testing.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request

VERSION = '20261026-minilm-adaptive-v2'
ROOT = Path.home() / '.local/share/follow-my-voice-models'
ADB = Path('/Applications/Unity/Hub/Editor/6000.3.10f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb')


def health():
    try:
        with urllib.request.urlopen('http://127.0.0.1:8766/health', timeout=2) as response:
            return json.load(response)
    except (urllib.error.URLError, TimeoutError, ConnectionError):
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', help='Authorized adb serial; required if multiple devices are connected')
    args = parser.parse_args()
    devices = subprocess.check_output([str(ADB), 'devices'], text=True)
    connected = [line.split()[0] for line in devices.splitlines()[1:] if len(line.split()) == 2 and line.split()[1] == 'device']
    serial = args.serial
    if serial is None:
        if len(connected) != 1:
            raise SystemExit('Connect one authorized headset or provide --serial; existing ADB server is left intact.')
        serial = connected[0]
    if serial not in connected:
        raise SystemExit('Selected headset is not authorized/connected.')
    model = ROOT / VERSION / 'model'
    python = ROOT / 'venv/bin/python'
    if not model.is_dir() or not python.is_file():
        raise SystemExit('Adaptive v2 checkpoint or local Python environment is missing.')
    state = health()
    log_dir = ROOT / 'headset-service'
    log_dir.mkdir(mode=0o700, parents=True, exist_ok=True)
    if state is None:
        log_path = log_dir / ('service-' + time.strftime('%Y%m%d-%H%M%S') + '.log')
        with log_path.open('xb') as log:
            os.chmod(log_path, 0o600)
            process = subprocess.Popen([str(python), str(Path(__file__).with_name('serve.py')), '--model', str(model), '--device', 'cpu'],
                                       stdin=subprocess.DEVNULL, stdout=log, stderr=log, start_new_session=True)
        (log_dir / 'launcher.pid').write_text(str(process.pid) + '\n')
        for _ in range(60):
            state = health()
            if state is not None:
                break
            if process.poll() is not None:
                raise SystemExit(f'Local service exited; inspect {log_path}')
            time.sleep(.5)
        if state is None:
            raise SystemExit(f'Local service is still loading or unavailable; inspect {log_path}')
    if state.get('model_version') != VERSION or state.get('scope') != 'assistance' or state.get('status') != 'ready':
        raise SystemExit('Port 8766 is serving a different model/contract. It was not replaced. Stop that service explicitly before retrying.')
    subprocess.run([str(ADB), '-s', serial, 'reverse', 'tcp:8766', 'tcp:8766'], check=True, stdout=subprocess.DEVNULL)
    tunnels = subprocess.check_output([str(ADB), '-s', serial, 'reverse', '--list'], text=True)
    if 'tcp:8766 tcp:8766' not in tunnels:
        raise SystemExit('ADB did not confirm the model tunnel.')
    print(json.dumps(dict(status='ready', serial=serial, model_version=VERSION, transport='adb_reverse_loopback',
                          log_directory=str(log_dir), note='Choose Adaptive, then Model: Local MiniLM in quick-test Settings. Keep the Mac awake.')))


if __name__ == '__main__':
    main()
