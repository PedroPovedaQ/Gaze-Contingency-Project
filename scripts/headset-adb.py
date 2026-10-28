#!/usr/bin/env python3
"""Select the Focus Vision, then optionally install, reconnect MiniLM and launch."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "UserSettings" / "headset-adb-wifi.txt"


def choose_device(devices):
    """Never silently select a phone or choose between multiple headsets."""
    vive = [serial for serial, state, detail in devices
            if state == "device" and "model:VIVE_Focus_Vision" in detail]
    wireless = [serial for serial in vive if ":" in serial or "._adb-tls-connect." in serial]
    candidates = wireless or vive
    if len(candidates) != 1:
        raise RuntimeError("Expected one online Focus Vision. Wake the headset and check Wi-Fi/debugging; "
                           "use --serial to choose explicitly if multiple headsets are connected.")
    return candidates[0]


def parse_devices(output):
    rows = []
    for line in output.splitlines():
        parts = line.split(maxsplit=2)
        if len(parts) >= 2 and parts[1] in ("device", "offline", "unauthorized"):
            rows.append((parts[0], parts[1], parts[2] if len(parts) > 2 else ""))
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default=os.environ.get("HEADSET_ADB_SERIAL"))
    parser.add_argument("--apk", type=Path)
    parser.add_argument("--package")
    args = parser.parse_args()
    if args.apk and (not args.apk.is_file() or not args.package):
        parser.error("--apk must exist and requires --package")
    adb = os.environ.get("ADB") or shutil.which("adb")
    if not adb:
        sdk = Path(os.environ.get("ANDROID_HOME", str(Path.home() / "Library/Android/sdk")))
        adb = str(sdk / "platform-tools/adb")

    def run(*command, timeout=15, check=True):
        result = subprocess.run([adb, *command], capture_output=True, text=True, timeout=timeout)
        if check and result.returncode:
            raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "ADB failed")
        return result.stdout.strip()

    serial = args.serial
    remembered = CACHE.read_text().strip() if CACHE.exists() else ""
    # Reconnect only a known endpoint. Do not scan the network or restart the shared ADB server.
    endpoint = serial or remembered
    if endpoint and (":" in endpoint or "._adb-tls-connect." in endpoint):
        try:
            run("connect", endpoint, timeout=8, check=False)
        except subprocess.TimeoutExpired:
            pass
    devices = parse_devices(run("devices", "-l"))
    if serial:
        if not any(s == serial and state == "device" for s, state, _ in devices):
            raise RuntimeError(f"Headset {serial} is not online/authorized. No other device was selected.")
    else:
        serial = choose_device(devices)
    model = run("-s", serial, "shell", "getprop", "ro.product.model")
    if model != "VIVE Focus Vision":
        raise RuntimeError(f"Refusing to deploy to unexpected model: {model}")
    if ":" in serial or "._adb-tls-connect." in serial:
        CACHE.parent.mkdir(parents=True, exist_ok=True)
        CACHE.write_text(serial + "\n")
    print(serial if not args.apk else f"Selected {model}: {serial}", flush=True)
    if args.apk:
        # Pin the chosen serial throughout; a disappearing headset must not redirect installation.
        print(run("-s", serial, "install", "-r", str(args.apk.resolve()), timeout=240), flush=True)
        # ADB reverse mappings can disappear across reconnects/updates. Reuse the pinned
        # model launcher so every deployment restores the tunnel before the app opens.
        try:
            local = subprocess.run(
                [sys.executable, str(ROOT / "tools/local-jev/headset.py"), "--serial", serial],
                capture_output=True, text=True, timeout=45)
        except subprocess.TimeoutExpired as exc:
            raise RuntimeError("APK installed, but MiniLM reconnection timed out; app not launched. "
                               "Check ~/.local/share/follow-my-voice-models/headset-service/.") from exc
        if local.returncode:
            raise RuntimeError("APK installed, but MiniLM reconnection failed; app not launched. " +
                               (local.stderr.strip() or local.stdout.strip()))
        print("Local MiniLM ready; headset connection restored.", flush=True)
        component = run("-s", serial, "shell", "cmd", "package", "resolve-activity", "--brief",
                        "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER",
                        args.package).splitlines()[-1]
        if not component.startswith(args.package + "/"):
            raise RuntimeError(f"No launcher activity found for {args.package}")
        output = run("-s", serial, "shell", "am", "start", "-W", "-n", component, timeout=30)
        if "Error:" in output or "Status: ok" not in output:
            raise RuntimeError(output)
        print(output, flush=True)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, subprocess.TimeoutExpired) as exc:
        print(f"Headset deployment failed: {exc}", file=sys.stderr)
        sys.exit(1)
