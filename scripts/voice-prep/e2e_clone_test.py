#!/usr/bin/env python3
"""End-to-end contract test for the self-similar voice path, mirroring the exact
Voxtral calls in Assets/VoxtralClient.cs — no mic / no Unity needed.

Steps:
  1. Generate a ~1-min speech sample via ElevenLabs (stands in for the mic recording).
  2. Clone it via Mistral Voxtral  POST /v1/audio/voices        -> voiceId
  3. Synthesize a study line from the clone POST /v1/audio/speech -> mp3
  4. Delete the test clone            DELETE /v1/audio/voices/{id}

Keys are read from Assets/StreamingAssets/api_keys.json and never printed.
"""
import base64, json, os, ssl, subprocess, urllib.request, urllib.error
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
KEYS = ROOT / "Assets/StreamingAssets/api_keys.json"
OUT = Path("/tmp/selfsim_test")
OUT.mkdir(exist_ok=True)
CTX = ssl.create_default_context()

SAMPLE_TEXT = (
    "Hello, my name is the study participant. I am reading this short passage so the "
    "system can learn the sound of my voice. I enjoy quiet mornings, strong coffee, and "
    "long walks when the weather is clear. The quick brown fox jumps over the lazy dog, "
    "and she sells sea shells by the sea shore. Thank you for listening to this recording."
)
TEST_LINE = "Round one. Find the blue pyramid."


def post(url, headers, body_bytes):
    req = urllib.request.Request(url, data=body_bytes, headers=headers, method="POST")
    with urllib.request.urlopen(req, context=CTX, timeout=90) as r:
        return r.status, r.read(), r.headers.get("Content-Type", "")


def main():
    keys = json.loads(KEYS.read_text())
    mistral_key = keys.get("mistral_key", "")
    if not mistral_key:
        raise SystemExit("no mistral_key in api_keys.json")

    # 1) sample via macOS `say` (stands in for the participant mic recording; no API)
    print("1) generating voice sample via macOS 'say' -> WAV...")
    aiff, sample = OUT / "sample.aiff", OUT / "sample.wav"
    subprocess.run(["say", "-o", str(aiff), SAMPLE_TEXT], check=True)
    subprocess.run(["afconvert", "-f", "WAVE", "-d", "LEI16@16000", "-c", "1",
                    str(aiff), str(sample)], check=True)
    audio = sample.read_bytes()
    print(f"   sample.wav = {len(audio)} bytes")

    # 2) clone via Voxtral (exact shape from VoxtralClient.CloneVoice)
    print("2) cloning via Voxtral POST /v1/audio/voices ...")
    body = json.dumps({
        "name": "selfsim_contract_test",
        "sample_audio": base64.b64encode(audio).decode(),
        "sample_filename": "sample.wav",
    }).encode()
    try:
        st, resp, _ = post("https://api.mistral.ai/v1/audio/voices",
                           {"Authorization": f"Bearer {mistral_key}", "Content-Type": "application/json"}, body)
    except urllib.error.HTTPError as e:
        raise SystemExit(f"   clone failed: HTTP {e.code} {e.read()[:400]}")
    voice_id = json.loads(resp).get("id")
    print(f"   voiceId = {voice_id}")
    if not voice_id:
        raise SystemExit(f"   no id in clone response: {resp[:300]}")

    # 3) TTS from the clone (exact shape from VoxtralClient.Synthesize)
    print("3) synthesizing from clone POST /v1/audio/speech ...")
    body = json.dumps({
        "model": "voxtral-mini-tts-2603", "input": TEST_LINE,
        "voice_id": voice_id, "response_format": "mp3",
    }).encode()
    try:
        st, resp, ctype = post("https://api.mistral.ai/v1/audio/speech",
                               {"Authorization": f"Bearer {mistral_key}", "Content-Type": "application/json"}, body)
    except urllib.error.HTTPError as e:
        raise SystemExit(f"   tts failed: HTTP {e.code} {e.read()[:400]}")
    if "json" in ctype.lower() or resp[:1] == b"{":
        out_audio = base64.b64decode(json.loads(resp)["audio_data"])
    else:
        out_audio = resp
    (OUT / "clone_out.mp3").write_bytes(out_audio)
    print(f"   clone_out.mp3 = {len(out_audio)} bytes")

    # 4) cleanup — delete the test clone from Mistral
    print("4) deleting test clone ...")
    try:
        req = urllib.request.Request(f"https://api.mistral.ai/v1/audio/voices/{voice_id}",
                                     headers={"Authorization": f"Bearer {mistral_key}"}, method="DELETE")
        with urllib.request.urlopen(req, context=CTX, timeout=30) as r:
            print(f"   delete HTTP {r.status}")
    except urllib.error.HTTPError as e:
        print(f"   delete warning: HTTP {e.code} (clone {voice_id} may persist)")

    print(f"\nOK — clone + TTS contract verified. Listen: open {OUT/'clone_out.mp3'}")


if __name__ == "__main__":
    main()
