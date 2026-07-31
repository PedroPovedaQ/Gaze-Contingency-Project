from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from paper_to_podcast.errors import ScriptGenerationError
from paper_to_podcast.script import create_transcript


class ScriptTests(unittest.TestCase):
    def test_offline_provider_summarizes_every_chunk(self):
        source = "\n\n".join(
            f"Section {index}. " + "Evidence sentence. " * 30 for index in range(8)
        )
        transcript, summaries, provider, fallback_reason = create_transcript(
            source,
            title="Test Paper",
            provider="offline",
            max_chunk_chars=500,
        )

        self.assertEqual("offline-extractive", provider)
        self.assertIsNone(fallback_reason)
        self.assertGreater(len(summaries), 8)
        self.assertTrue(all(summary in transcript for summary in summaries))
        self.assertIn("complete extracted paper", transcript)

    def test_reviewed_transcript_is_supported_for_offline_evidence_sensitive_work(self):
        with tempfile.TemporaryDirectory() as temp:
            transcript_path = Path(temp) / "reviewed.txt"
            transcript_path.write_text("Grounded reviewed transcript. " * 20)
            transcript, summaries, provider, fallback_reason = create_transcript(
                "Source paragraph. " * 100,
                title="Test",
                transcript_file=transcript_path,
            )
        self.assertEqual("reviewed-transcript", provider)
        self.assertIsNone(fallback_reason)
        self.assertGreater(len(summaries), 0)
        self.assertTrue(transcript.startswith("Grounded reviewed"))

    def test_rejects_too_short_reviewed_transcript(self):
        with tempfile.TemporaryDirectory() as temp:
            transcript_path = Path(temp) / "short.txt"
            transcript_path.write_text("Too short")
            with self.assertRaisesRegex(ScriptGenerationError, "at least 200"):
                create_transcript(
                    "Source paragraph. " * 100,
                    title="Test",
                    transcript_file=transcript_path,
                )

    @patch.dict("os.environ", {"OPENAI_API_KEY": "test-key"})
    @patch("paper_to_podcast.script.generate_text")
    def test_auto_provider_records_openai_failure_and_falls_back(self, generate):
        generate.side_effect = ScriptGenerationError("provider unavailable")

        transcript, summaries, provider, fallback_reason = create_transcript(
            "Source paragraph. " * 100,
            title="Test",
            provider="auto",
        )

        self.assertEqual("offline-extractive-after-openai-failure", provider)
        self.assertEqual("provider unavailable", fallback_reason)
        self.assertGreater(len(summaries), 0)
        self.assertIn("local fallback", transcript)


if __name__ == "__main__":
    unittest.main()
