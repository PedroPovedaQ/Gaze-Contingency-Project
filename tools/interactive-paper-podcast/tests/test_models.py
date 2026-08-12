import json
import unittest
from unittest.mock import MagicMock, patch

from interactive_podcast.models import OllamaModel


class ModelTests(unittest.TestCase):
    @patch("interactive_podcast.models.urllib.request.urlopen")
    def test_recovers_complete_answer_from_truncated_json(self, urlopen):
        response = MagicMock()
        response.__enter__.return_value.read.return_value = json.dumps({
            "message": {"content": '{"answer":"Grounded answer.","discussion_summary":"unfinished'}
        }).encode()
        urlopen.return_value = response

        result = OllamaModel().complete_json("system", "prompt")

        self.assertEqual("Grounded answer.", result["answer"])
        self.assertIn("Prefer", result["memory_update"])
