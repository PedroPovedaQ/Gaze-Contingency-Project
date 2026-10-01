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

    def test_raw_replay_payloads_are_separate_lossless_and_escaped(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);source=root/'data.json';output=root/'page.html'
            trials=[dict(id=str(i),start=i+1,end=i+2,samples=[[i,'</script>__TRIAL_PAYLOADS__']],
                         targetName='__VIDEO_CODE__',practice=False) for i in range(3)]
            original=dict(schema='replay-timeline-v1',participant='SYNTHETIC',utc='now',trials=trials)
            source.write_text(json.dumps(original));build(source,output);page=output.read_text()
            main=json.loads(re.search(r'<script id="p20-records" type="application/json">(.*?)</script>',page,re.S)[1])
            self.assertNotIn('samples',main['trials'][0])
            for i,trial in enumerate(trials):
                block=re.search(rf'<script id="p20-trial-payload-{i}" type="application/json">(.*?)</script>',page,re.S)[1]
                self.assertEqual(json.loads(block),trial)
                self.assertNotIn('</script>',block)
            self.assertEqual(json.loads(source.read_text()),original)

    def test_bad_trial_payload_does_not_publish_partial_html(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);source=root/'data.json';output=root/'page.html'
            source.write_text(json.dumps(dict(schema='replay-timeline-v1',participant='S',utc='now',
                              trials=[dict(start=1,end=2,samples=[float('nan')])])) )
            with self.assertRaises(ValueError):build(source,output)
            self.assertFalse(output.exists())
            self.assertEqual(list(root.iterdir()),[source])

    def test_boolean_and_infinite_intervals_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);source=root/'data.json'
            for start,end in [(True,2),(1,float('inf')),(1,1)]:
                source.write_text(json.dumps(dict(participant='S',utc='now',trials=[dict(start=start,end=end)])))
                with self.assertRaises(ValueError):build(source,root/'page.html')
