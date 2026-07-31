"""Small standard-library OpenAI client, used only when explicitly configured."""

from __future__ import annotations

import json
import os
import urllib.error
import urllib.request

from .errors import ScriptGenerationError, SpeechSynthesisError


def _post(path: str, payload: dict[str, object], *, accept: str) -> bytes:
    api_key = os.environ.get("OPENAI_API_KEY", "").strip()
    if not api_key:
        raise ScriptGenerationError("OPENAI_API_KEY is not set")
    base_url = os.environ.get("P2P_OPENAI_BASE_URL", "https://api.openai.com/v1").rstrip("/")
    request = urllib.request.Request(
        f"{base_url}/{path.lstrip('/')}",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            "Accept": accept,
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=180) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")[:800]
        raise RuntimeError(f"OpenAI returned HTTP {error.code}: {body}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"OpenAI request failed: {error.reason}") from error


def generate_text(prompt: str, *, instructions: str) -> str:
    model = os.environ.get("P2P_OPENAI_MODEL", "gpt-5-mini")
    try:
        raw = _post(
            "responses",
            {"model": model, "instructions": instructions, "input": prompt},
            accept="application/json",
        )
        payload = json.loads(raw)
        direct = payload.get("output_text")
        if isinstance(direct, str) and direct.strip():
            return direct.strip()
        texts: list[str] = []
        for item in payload.get("output", []):
            for content in item.get("content", []):
                text = content.get("text")
                if isinstance(text, str):
                    texts.append(text)
        result = "\n".join(texts).strip()
        if not result:
            raise ValueError("response contained no output text")
        return result
    except Exception as error:
        if isinstance(error, ScriptGenerationError):
            raise
        raise ScriptGenerationError(str(error)) from error


def synthesize_mp3(text: str, voice: str) -> bytes:
    model = os.environ.get("P2P_OPENAI_TTS_MODEL", "gpt-4o-mini-tts")
    try:
        return _post(
            "audio/speech",
            {
                "model": model,
                "voice": voice,
                "input": text,
                "response_format": "mp3",
                "instructions": "Speak as a clear, thoughtful academic podcast host.",
            },
            accept="audio/mpeg",
        )
    except Exception as error:
        raise SpeechSynthesisError(str(error)) from error
