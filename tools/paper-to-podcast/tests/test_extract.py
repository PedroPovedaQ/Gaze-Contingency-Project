from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from paper_to_podcast.errors import PdfExtractionError
from paper_to_podcast.extract import extract_pdf_text


class PdfExtractionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.pdf = Path(self.temp.name) / "source.pdf"
        self.pdf.write_bytes(b"%PDF-1.5\nfake test document")

    def tearDown(self):
        self.temp.cleanup()

    @patch("paper_to_podcast.extract.subprocess.run")
    def test_uses_explicit_absolute_pdf_and_stdout(self, run):
        run.return_value = subprocess.CompletedProcess(
            args=[], returncode=0, stdout="Academic paper text. " * 20, stderr=""
        )

        text = extract_pdf_text(self.pdf)

        self.assertGreater(len(text), 120)
        arguments = run.call_args.args[0]
        self.assertEqual(str(self.pdf.resolve()), arguments[-2])
        self.assertEqual("-", arguments[-1])
        self.assertNotIn("05-versions-space.pdf", arguments)

    def test_missing_pdf_has_stable_error(self):
        missing = Path(self.temp.name) / "missing.pdf"
        with self.assertRaisesRegex(PdfExtractionError, "does not exist"):
            extract_pdf_text(missing)

    def test_rejects_non_pdf_header(self):
        self.pdf.write_bytes(b"not a pdf")
        with self.assertRaisesRegex(PdfExtractionError, "PDF header"):
            extract_pdf_text(self.pdf)

    @patch("paper_to_podcast.extract.subprocess.run")
    def test_propagates_extractor_failure(self, run):
        run.side_effect = subprocess.CalledProcessError(
            1, ["pdftotext"], stderr="Syntax Error"
        )
        with self.assertRaisesRegex(PdfExtractionError, "Syntax Error"):
            extract_pdf_text(self.pdf)

    @patch("paper_to_podcast.extract.subprocess.run")
    def test_rejects_image_only_or_empty_pdf(self, run):
        run.return_value = subprocess.CompletedProcess(
            args=[], returncode=0, stdout=" \n ", stderr=""
        )
        with self.assertRaisesRegex(PdfExtractionError, "too little"):
            extract_pdf_text(self.pdf)


if __name__ == "__main__":
    unittest.main()
