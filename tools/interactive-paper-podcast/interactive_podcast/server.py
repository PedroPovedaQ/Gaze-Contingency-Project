"""Dependency-free HTTP API and local player."""

from __future__ import annotations

from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
from pathlib import Path
from urllib.parse import urlparse

from .engine import ConversationEngine
from .store import SessionStore


class App:
    def __init__(self, engine: ConversationEngine, store: SessionStore, web_root: Path):
        self.engine = engine
        self.store = store
        self.web_root = web_root.resolve()

    def handler(self):
        app = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, format, *args):
                pass

            def json_response(self, payload, status=HTTPStatus.OK):
                body = json.dumps(payload).encode()
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(body)

            def read_json(self):
                length = int(self.headers.get("Content-Length", "0"))
                if length < 0 or length > 65_536:
                    raise ValueError("Request body exceeds the 64 KiB limit")
                return json.loads(self.rfile.read(length) or b"{}")

            def do_GET(self):
                path = urlparse(self.path).path
                try:
                    if path == "/api/health":
                        return self.json_response({"ok": True, "model": {"provider": app.engine.model.provider, "name": app.engine.model.model}})
                    if path == "/api/sessions":
                        return self.json_response({"sessions": app.store.list()})
                    if path.startswith("/api/sessions/"):
                        parts = path.strip("/").split("/")
                        if len(parts) not in (3, 4) or parts[:2] != ["api", "sessions"]:
                            return self.json_response({"error": "Unknown endpoint"}, HTTPStatus.NOT_FOUND)
                        session_id = parts[2]
                        session = app.store.load(session_id)
                        if len(parts) == 4 and parts[3] == "audio":
                            return self.serve_audio(session)
                        if len(parts) != 3:
                            return self.json_response({"error": "Unknown endpoint"}, HTTPStatus.NOT_FOUND)
                        safe = dict(session)
                        safe.pop("source_chunks", None)
                        safe["has_audio"] = bool(safe.pop("audio_path", None))
                        return self.json_response(safe)
                    return self.serve_static(path)
                except (ValueError, FileNotFoundError) as error:
                    self.json_response({"error": str(error)}, HTTPStatus.NOT_FOUND)

            def do_POST(self):
                path = urlparse(self.path).path
                try:
                    payload = self.read_json()
                    parts = path.strip("/").split("/")
                    if len(parts) == 4 and parts[:2] == ["api", "sessions"] and parts[3] == "ask":
                        session_id = parts[2]
                        result = app.engine.ask(session_id, str(payload.get("question", "")), float(payload.get("position_seconds", 0)))
                        return self.json_response(result)
                    if len(parts) == 4 and parts[:2] == ["api", "sessions"] and parts[3] == "finish":
                        session_id = parts[2]
                        return self.json_response(app.engine.finish_run(session_id))
                    self.json_response({"error": "Unknown endpoint"}, HTTPStatus.NOT_FOUND)
                except (ValueError, FileNotFoundError, json.JSONDecodeError) as error:
                    self.json_response({"error": str(error)}, HTTPStatus.BAD_REQUEST)

            def serve_audio(self, session):
                path = session.get("audio_path")
                if not path or not Path(path).is_file():
                    return self.json_response({"error": "No audio is attached"}, HTTPStatus.NOT_FOUND)
                source = Path(path)
                size = source.stat().st_size
                start, end = 0, size - 1
                range_header = self.headers.get("Range")
                if range_header and range_header.startswith("bytes="):
                    range_value = range_header[6:]
                    if "," in range_value or "-" not in range_value:
                        return self.unsatisfied_range(size)
                    first, last = range_value.split("-", 1)
                    if not first:
                        suffix = int(last)
                        if suffix <= 0:
                            return self.unsatisfied_range(size)
                        start = max(0, size - suffix)
                    else:
                        start = int(first)
                        end = min(int(last) if last else end, size - 1)
                    if start > end or start >= size:
                        return self.unsatisfied_range(size)
                length = end - start + 1
                self.send_response(HTTPStatus.PARTIAL_CONTENT if range_header else HTTPStatus.OK)
                self.send_header("Content-Type", mimetypes.guess_type(source.name)[0] or "audio/mpeg")
                self.send_header("Content-Length", str(length))
                self.send_header("Accept-Ranges", "bytes")
                if range_header:
                    self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
                self.end_headers()
                with source.open("rb") as stream:
                    stream.seek(start)
                    remaining = length
                    try:
                        while remaining and (block := stream.read(min(1024 * 128, remaining))):
                            self.wfile.write(block)
                            remaining -= len(block)
                    except (BrokenPipeError, ConnectionResetError):
                        # Browsers routinely cancel a media request after probing metadata.
                        return

            def unsatisfied_range(self, size):
                self.send_response(HTTPStatus.REQUESTED_RANGE_NOT_SATISFIABLE)
                self.send_header("Content-Range", f"bytes */{size}")
                self.end_headers()

            def serve_static(self, path):
                relative = "index.html" if path == "/" else path.lstrip("/")
                target = (app.web_root / relative).resolve()
                if app.web_root not in target.parents and target != app.web_root:
                    return self.json_response({"error": "Invalid path"}, HTTPStatus.BAD_REQUEST)
                if not target.is_file():
                    target = app.web_root / "index.html"
                body = target.read_bytes()
                self.send_response(HTTPStatus.OK)
                self.send_header("Content-Type", mimetypes.guess_type(target.name)[0] or "application/octet-stream")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

        return Handler


def build_server(host: str, port: int, app: App) -> ThreadingHTTPServer:
    return ThreadingHTTPServer((host, port), app.handler())
