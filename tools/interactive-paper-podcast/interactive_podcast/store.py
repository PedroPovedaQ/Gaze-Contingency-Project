"""Atomic JSON persistence for sessions and evolving characters."""

from __future__ import annotations

import json
from pathlib import Path
import tempfile
import threading


class SessionStore:
    def __init__(self, root: Path):
        self.root = root.expanduser().resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self._lock = threading.RLock()

    def path(self, session_id: str) -> Path:
        if not session_id.replace("-", "").replace("_", "").isalnum():
            raise ValueError("Invalid session ID")
        return self.root / f"{session_id}.json"

    def load(self, session_id: str) -> dict[str, object]:
        with self._lock:
            return json.loads(self.path(session_id).read_text(encoding="utf-8"))

    def save(self, session: dict[str, object]) -> None:
        with self._lock:
            destination = self.path(str(session["id"]))
            with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=self.root, delete=False) as stream:
                json.dump(session, stream, indent=2)
                stream.write("\n")
                temporary = Path(stream.name)
            temporary.replace(destination)

    def list(self) -> list[dict[str, object]]:
        sessions = []
        for path in sorted(self.root.glob("*.json"), key=lambda item: item.stat().st_mtime, reverse=True):
            session = json.loads(path.read_text(encoding="utf-8"))
            sessions.append({key: session.get(key) for key in ("id", "title", "created_at", "updated_at", "run_summary")})
        return sessions
