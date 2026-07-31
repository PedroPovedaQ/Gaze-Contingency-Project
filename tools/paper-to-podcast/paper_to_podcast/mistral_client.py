"""Mistral Voxtral speech client compatible with the WebKasa provider contract."""

from __future__ import annotations

import base64
import json
import os
import urllib.error
import urllib.request

from .errors import SpeechSynthesisError

VOXTRAL_MODEL = "voxtral-mini-tts-2603"
VOICE_ALIASES = {
    "alloy": "gb_jane_neutral",
    "echo": "gb_oliver_neutral",
    "fable": "en_paul_cheerful",
    "onyx": "en_paul_confident",
    "nova": "gb_jane_curious",
    "shimmer": "gb_jane_confident",
    "petro": "fd636fe5-7677-4d7a-9f21-c2a10a43c8e1",
}


def resolve_voice_id(voice_id: str) -> str:
    normalized = voice_id.strip()
    if not normalized:
        raise SpeechSynthesisError("Voxtral voice ID cannot be empty")
    return VOICE_ALIASES.get(normalized, normalized)


def synthesize_voxtral_wav(text: str, voice_id: str) -> bytes:
    api_key = os.environ.get("MISTRAL_API_KEY", "").strip()
    if not api_key:
        raise SpeechSynthesisError("MISTRAL_API_KEY is not set")
    model = os.environ.get("P2P_VOXTRAL_MODEL", VOXTRAL_MODEL)
    base_url = os.environ.get(
        "P2P_MISTRAL_BASE_URL", "https://api.mistral.ai/v1"
    ).rstrip("/")
    request = urllib.request.Request(
        f"{base_url}/audio/speech",
        data=json.dumps(
            {
                "model": model,
                "input": text,
                "voice_id": resolve_voice_id(voice_id),
                "response_format": "wav",
            }
        ).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=240) as response:
            payload = json.loads(response.read())
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")[:800]
        if error.code == 429:
            message = "Mistral Voxtral rate limit reached"
        elif error.code in {401, 403}:
            message = "Mistral Voxtral authentication failed"
        else:
            message = f"Mistral Voxtral returned HTTP {error.code}: {detail}"
        raise SpeechSynthesisError(message) from error
    except urllib.error.URLError as error:
        raise SpeechSynthesisError(
            f"Mistral Voxtral request failed: {error.reason}"
        ) from error
    except (json.JSONDecodeError, TypeError) as error:
        raise SpeechSynthesisError(
            "Mistral Voxtral returned an invalid JSON response"
        ) from error

    audio_data = payload.get("audio_data")
    if not isinstance(audio_data, str) or not audio_data:
        raise SpeechSynthesisError("Mistral Voxtral response did not contain audio_data")
    try:
        audio = base64.b64decode(audio_data, validate=True)
    except ValueError as error:
        raise SpeechSynthesisError(
            "Mistral Voxtral returned invalid base64 audio"
        ) from error
    if not audio.startswith(b"RIFF") or audio[8:12] != b"WAVE":
        raise SpeechSynthesisError("Mistral Voxtral response was not WAV audio")
    return audio
