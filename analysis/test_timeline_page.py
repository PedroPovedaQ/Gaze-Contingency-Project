import json
import re
import tempfile
import unittest
from pathlib import Path

from timeline_page import build


class TimelinePageTests(unittest.TestCase):
    def test_embedded_data_cannot_escape_script_and_source_is_unchanged(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "data.json"
            original = json.dumps(
                {
                    "participant": "</script><img onerror=alert(1)>__VIDEO_CODE__",
                    "utc": "2026-09-27T14:59:28Z",
                    "trials": [{"id": "one", "start": 10, "end": 20}],
                }
            )
            source.write_text(original)
            output = Path(temp) / "timeline.html"
            build(source, output)
            page = output.read_text()
            self.assertNotIn("</script><img", page)
            self.assertIn("p20-video-file", page)
            self.assertIn("media-src blob:", page)
            embedded = re.search(
                r'<script id="p20-records" type="application/json">(.*?)</script>',
                page,
                re.DOTALL,
            )
            self.assertEqual(json.loads(embedded[1]), json.loads(original))
            self.assertEqual(source.read_text(), original)
            with self.assertRaises(FileExistsError):
                build(source, output)

    def test_empty_trials_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "data.json"
            source.write_text('{"participant":"P001","utc":"now","trials":[]}')
            with self.assertRaisesRegex(ValueError, "trial"):
                build(source, Path(temp) / "page.html")
