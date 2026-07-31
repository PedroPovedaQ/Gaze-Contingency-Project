from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from paper_to_podcast.audio import synthesize, validate_mp3
from paper_to_podcast.errors import AudioValidationError, SpeechSynthesisError


class AudioTests(unittest.TestCase):
    @patch("paper_to_podcast.audio._synthesize_macos")
    @patch("paper_to_podcast.audio._synthesize_openai")
    def test_external_tts_failure_falls_back_to_macos(self, openai, macos):
        openai.side_effect = SpeechSynthesisError("provider unavailable")
        with tempfile.TemporaryDirectory() as temp:
            transcript = Path(temp) / "transcript.txt"
            output = Path(temp) / "output.mp3"
            transcript.write_text("A sufficiently useful transcript.")

            provider, fallback_reason, details = synthesize(
                transcript, output, provider="openai", allow_fallback=True
            )

        self.assertEqual("macos:Samantha", provider)
        self.assertIn("provider unavailable", fallback_reason)
        self.assertTrue(details["system_voice_fallback_used"])
        macos.assert_called_once()

    def test_labels_and_parses_multi_host_transcript(self):
        from paper_to_podcast.audio import (
            label_transcript_for_hosts,
            parse_speaker_segments,
        )

        labeled = label_transcript_for_hosts("First paragraph.\n\nSecond paragraph.", 2)
        self.assertEqual(
            [("host", "First paragraph."), ("cohost", "Second paragraph.")],
            parse_speaker_segments(labeled),
        )

    def test_validation_rejects_missing_or_tiny_audio(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "tiny.mp3"
            output.write_bytes(b"not audio")
            with self.assertRaisesRegex(AudioValidationError, "too small"):
                validate_mp3(output)


if __name__ == "__main__":
    unittest.main()
