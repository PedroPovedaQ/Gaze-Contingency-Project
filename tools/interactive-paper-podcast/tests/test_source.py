from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from interactive_podcast.source import chunks, extract_pdf


class SourceTests(unittest.TestCase):
    def test_rejects_non_pdf_before_extraction(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "paper.pdf"
            source.write_text("not a pdf")
            with self.assertRaisesRegex(ValueError, "not a PDF"):
                extract_pdf(source)

    def test_reports_missing_pdftotext(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "paper.pdf"
            source.write_bytes(b"%PDF-" + b"placeholder")
            with patch("interactive_podcast.source.subprocess.run", side_effect=FileNotFoundError):
                with self.assertRaisesRegex(ValueError, "pdftotext is required"):
                    extract_pdf(source)

    def test_reports_extraction_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "paper.pdf"
            source.write_bytes(b"%PDF-" + b"placeholder")
            error = subprocess.CalledProcessError(1, ["pdftotext"], stderr="damaged PDF")
            with patch("interactive_podcast.source.subprocess.run", side_effect=error):
                with self.assertRaisesRegex(ValueError, "damaged PDF"):
                    extract_pdf(source)

    def test_chunking_preserves_the_full_source(self):
        source = "\n\n".join(["A" * 17, "B" * 17, "C" * 17])
        result = chunks(source, max_chars=20)
        self.assertEqual(source.replace("\n\n", ""), "".join(result).replace("\n\n", ""))
        self.assertTrue(all(len(part) <= 20 for part in result))
