"""Local and OpenAI-compatible chat model adapters."""

from __future__ import annotations

import json
import os
import re
import urllib.error
import urllib.request


class ModelError(RuntimeError):
    pass


class ChatModel:
    provider = "unknown"
    model = "unknown"

    def complete_json(self, system: str, prompt: str) -> dict[str, object]:
        raise NotImplementedError


class OllamaModel(ChatModel):
    provider = "ollama"

    def __init__(self, model: str | None = None, base_url: str | None = None):
        self.model = model or os.environ.get("IPP_OLLAMA_MODEL", "llama3.2:1b")
        self.base_url = (base_url or os.environ.get("IPP_OLLAMA_URL", "http://127.0.0.1:11434")).rstrip("/")

    def complete_json(self, system: str, prompt: str) -> dict[str, object]:
        request = urllib.request.Request(
            f"{self.base_url}/api/chat",
            data=json.dumps({
                "model": self.model,
                "stream": False,
                "format": "json",
                "messages": [
                    {"role": "system", "content": system},
                    {"role": "user", "content": prompt},
                ],
                "options": {"temperature": 0.25, "num_predict": 180, "num_ctx": 4096},
            }).encode(),
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=90) as response:
                payload = json.loads(response.read())
            content = payload["message"]["content"]
            try:
                result = json.loads(content)
            except json.JSONDecodeError:
                answer_match = re.search(r'"answer"\s*:\s*"((?:\\.|[^"\\])*)"', content)
                if not answer_match:
                    raise
                answer = json.loads(f'"{answer_match.group(1)}"')
                result = {
                    "answer": answer,
                    "discussion_summary": answer[:220],
                    "memory_update": "Prefer concise answers before adding methodological caveats.",
                }
            if not isinstance(result, dict):
                raise ValueError("model JSON was not an object")
            return result
        except (urllib.error.URLError, KeyError, ValueError, json.JSONDecodeError) as error:
            raise ModelError(f"Ollama generation failed: {error}") from error


class OpenAICompatibleModel(ChatModel):
    provider = "openai-compatible"

    def __init__(self, model: str | None = None, base_url: str | None = None, api_key: str | None = None):
        self.model = model or os.environ.get("IPP_MODEL", "gpt-5-mini")
        self.base_url = (base_url or os.environ.get("IPP_MODEL_URL", "https://api.openai.com/v1")).rstrip("/")
        self.api_key = api_key or os.environ.get("OPENAI_API_KEY", "")

    def complete_json(self, system: str, prompt: str) -> dict[str, object]:
        if not self.api_key:
            raise ModelError("OPENAI_API_KEY is not configured")
        request = urllib.request.Request(
            f"{self.base_url}/chat/completions",
            data=json.dumps({
                "model": self.model,
                "response_format": {"type": "json_object"},
                "messages": [
                    {"role": "system", "content": system},
                    {"role": "user", "content": prompt},
                ],
                "temperature": 0.35,
            }).encode(),
            headers={"Authorization": f"Bearer {self.api_key}", "Content-Type": "application/json"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=90) as response:
                payload = json.loads(response.read())
            result = json.loads(payload["choices"][0]["message"]["content"])
            if not isinstance(result, dict):
                raise ValueError("model JSON was not an object")
            return result
        except (urllib.error.URLError, KeyError, ValueError, json.JSONDecodeError) as error:
            raise ModelError(f"OpenAI-compatible generation failed: {error}") from error


def configured_model(provider: str = "auto") -> ChatModel:
    selected = provider.lower()
    if selected == "auto":
        selected = "openai" if os.environ.get("IPP_MODEL_URL") or os.environ.get("OPENAI_API_KEY") else "ollama"
    if selected == "ollama":
        return OllamaModel()
    if selected in {"openai", "openai-compatible"}:
        return OpenAICompatibleModel()
    raise ValueError(f"Unknown model provider: {provider}")
