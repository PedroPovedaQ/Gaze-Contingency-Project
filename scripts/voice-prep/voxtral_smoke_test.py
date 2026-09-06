#!/usr/bin/env python3
"""Standalone smoke test for the Mistral Voxtral clone + TTS contract used by the
Unity VoxtralClient. Verifies the exact endpoints/JSON shapes without Unity.

Usage:
    export MISTRAL_API_KEY=...            # your key
    python3 voxtral_smoke_test.py path/to/sample.wav "Round 1. Find the blue pyramid." out.mp3

It will: POST /v1/audio/voices (clone) -> voiceId, then POST /v1/audio/speech
(voxtral-mini-tts-2603, voice_id) -> write the returned audio to out.mp3.

Only needs `requests`:  pip install requests
"""
import base64
import json
import os
import sys

import requests  # type: ignore

BASE = "https://api.mistral.ai/v1"
TTS_MODEL = "voxtral-mini-tts-2603"


def clone(api_key: str, wav_path: str) -> str:
    with open(wav_path, "rb") as f:
        sample_b64 = base64.b64encode(f.read()).decode()
    r = requests.post(
        f"{BASE}/audio/voices",
        headers={"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
        json={"name": "smoke_test", "sample_audio": sample_b64, "sample_filename": os.path.basename(wav_path)},
        timeout=60,
    )
    print(f"clone -> HTTP {r.status_code}")
    r.raise_for_status()
    voice_id = r.json().get("id")
    print(f"voiceId = {voice_id}")
    if not voice_id:
        raise SystemExit(f"no voice id in response: {r.text[:400]}")
    return voice_id


def synth(api_key: str, voice_id: str, text: str, out_path: str) -> None:
    r = requests.post(
        f"{BASE}/audio/speech",
        headers={"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
        json={"model": TTS_MODEL, "input": text, "voice_id": voice_id, "response_format": "mp3"},
        timeout=30,
    )
    print(f"speech -> HTTP {r.status_code}")
    r.raise_for_status()
    # Voxtral returns JSON with base64 in `audio_data` (fall back to raw bytes).
    audio = None
    ctype = r.headers.get("content-type", "")
    if "application/json" in ctype or r.text.strip().startswith("{"):
        audio = base64.b64decode(r.json()["audio_data"])
    else:
        audio = r.content
    with open(out_path, "wb") as f:
        f.write(audio)
    print(f"wrote {out_path} ({len(audio)} bytes)")


def main() -> None:
    key = os.environ.get("MISTRAL_API_KEY")
    if not key:
        raise SystemExit("set MISTRAL_API_KEY")
    if len(sys.argv) < 4:
        raise SystemExit("usage: voxtral_smoke_test.py <sample.wav> <text> <out.mp3>")
    wav_path, text, out_path = sys.argv[1], sys.argv[2], sys.argv[3]
    vid = clone(key, wav_path)
    synth(key, vid, text, out_path)
    print("OK — clone + TTS contract verified.")


if __name__ == "__main__":
    main()
