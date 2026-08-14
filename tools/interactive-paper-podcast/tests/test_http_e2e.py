from pathlib import Path
import json
import tempfile
import threading
import unittest
import urllib.request

from interactive_podcast.engine import ConversationEngine
from interactive_podcast.server import App, build_server
from interactive_podcast.store import SessionStore
from test_engine import FakeModel


class HttpEndToEndTests(unittest.TestCase):
    def request(self, path, payload=None):
        data = None if payload is None else json.dumps(payload).encode()
        request = urllib.request.Request(
            f"http://127.0.0.1:{self.port}{path}", data=data,
            headers={"Content-Type": "application/json"} if data else {},
            method="POST" if data is not None else "GET",
        )
        with urllib.request.urlopen(request, timeout=4) as response:
            return response.status, json.loads(response.read())

    def test_prepare_interrupt_answer_reload_and_finish(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            web = root / "web"
            web.mkdir()
            (web / "index.html").write_text("interactive player")
            store = SessionStore(root / "data")
            engine = ConversationEngine(store, FakeModel())
            audio = root / "episode.mp3"
            audio.write_bytes(b"0123456789")
            session = engine.create("Test paper", ["NASA TLX is the workload measure."], "Transcript", str(audio))
            server = build_server("127.0.0.1", 0, App(engine, store, web))
            self.port = server.server_port
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                status, health = self.request("/api/health")
                self.assertEqual(200, status)
                self.assertEqual("fake-local", health["model"]["provider"])
                range_request = urllib.request.Request(
                    f"http://127.0.0.1:{self.port}/api/sessions/{session['id']}/audio",
                    headers={"Range": "bytes=2-5"},
                )
                with urllib.request.urlopen(range_request, timeout=4) as response:
                    self.assertEqual(206, response.status)
                    self.assertEqual("bytes 2-5/10", response.headers["Content-Range"])
                    self.assertEqual(b"2345", response.read())
                suffix_request = urllib.request.Request(
                    f"http://127.0.0.1:{self.port}/api/sessions/{session['id']}/audio",
                    headers={"Range": "bytes=-3"},
                )
                with urllib.request.urlopen(suffix_request, timeout=4) as response:
                    self.assertEqual("bytes 7-9/10", response.headers["Content-Range"])
                    self.assertEqual(b"789", response.read())
                _, answer = self.request(
                    f"/api/sessions/{session['id']}/ask",
                    {"question": "How was workload measured?", "position_seconds": 17.25},
                )
                self.assertEqual(17.25, answer["turn"]["position_seconds"])
                _, reloaded = self.request(f"/api/sessions/{session['id']}")
                self.assertEqual(1, len(reloaded["turns"]))
                self.assertNotIn("source_chunks", reloaded)
                self.assertNotIn("audio_path", reloaded)
                self.assertTrue(reloaded["has_audio"])
                _, finished = self.request(f"/api/sessions/{session['id']}/finish", {})
                self.assertEqual(1, finished["turn_count"])
                self.assertTrue(store.load(session["id"])["run_summaries"])
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=2)
