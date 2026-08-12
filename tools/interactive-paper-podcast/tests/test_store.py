from pathlib import Path
import tempfile
import unittest

from interactive_podcast.store import SessionStore


class StoreTests(unittest.TestCase):
    def test_round_trip_and_path_validation(self):
        with tempfile.TemporaryDirectory() as temporary:
            store = SessionStore(Path(temporary))
            store.save({"id": "safe-id", "title": "Paper"})
            self.assertEqual("Paper", store.load("safe-id")["title"])
            with self.assertRaises(ValueError):
                store.load("../secret")
