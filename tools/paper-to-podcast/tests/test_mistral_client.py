import base64
import json
import os
import unittest
from unittest.mock import MagicMock, patch

from paper_to_podcast.mistral_client import resolve_voice_id, synthesize_voxtral_wav


class MistralClientTests(unittest.TestCase):
    def test_webkasa_voice_aliases_match_voxtral_ids(self):
        self.assertEqual("gb_jane_neutral", resolve_voice_id("alloy"))
        self.assertEqual("gb_oliver_neutral", resolve_voice_id("echo"))

    @patch.dict(os.environ, {"MISTRAL_API_KEY": "test-key"})
    @patch("paper_to_podcast.mistral_client.urllib.request.urlopen")
    def test_voxtral_request_uses_expected_contract(self, urlopen):
        response = MagicMock()
        response.__enter__.return_value.read.return_value = json.dumps(
            {"audio_data": base64.b64encode(b"RIFF1234WAVEaudio").decode()}
        ).encode()
        urlopen.return_value = response

        audio = synthesize_voxtral_wav("Research text", "alloy")

        self.assertEqual(b"RIFF1234WAVEaudio", audio)
        request = urlopen.call_args.args[0]
        payload = json.loads(request.data)
        self.assertEqual("voxtral-mini-tts-2603", payload["model"])
        self.assertEqual("gb_jane_neutral", payload["voice_id"])
        self.assertEqual("wav", payload["response_format"])
        self.assertNotIn("test-key", json.dumps(payload))
