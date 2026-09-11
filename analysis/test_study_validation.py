import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("study_validator", Path(__file__).parents[1] / "scripts/verify-study-session.py")
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


class StudyValidationTests(unittest.TestCase):
    def test_valid_session_and_wrong_voice_or_timing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            records, events, objects = [], [], []
            for i in range(14):
                trial = f"P001_run001_r{i:02}"
                records.append(dict(index=i, trial_id=trial, completed=True, objects_ready_at=9,
                                    voice_condition="neutral-male" if i < 7 else "selfsimilar", time_to_find_seconds=8))
                for kind, time, detail in (("audio_playback_start", 1, f"context=round;clip_id={'neutral' if i < 7 else 'self'};"),
                                           ("audio_playback_end", 8, "context=round;"),
                                           ("search_start", 10, ""), ("capture_correct", 18, "")):
                    events.append(dict(trial_id=trial, timestamp=time, event_type=kind, detail=detail))
                for j in range(56):
                    objects.append(dict(trial_id=trial, object_id=f"r{i}_o{j}", is_target=int(j == 0)))
            for name, rows in (("trial_events.csv", events), ("object_manifest.csv", objects)):
                with (root / name).open("w", newline="") as file:
                    writer = csv.DictWriter(file, fieldnames=rows[0].keys())
                    writer.writeheader(); writer.writerows(rows)
            summary = dict(schema_version=2, session_outcome="completed", voice_order="neutral_then_selfsimilar",
                           neutral_voice_profile="male", objectives=records)
            (root / "trial_summary.json").write_text(json.dumps(summary))
            (root / "voice-library-manifest.json").write_text(json.dumps(dict(clips=[
                dict(clip_id="neutral", voice_id="cjVigY5qzO86Huf0OWal", provider="elevenlabs", text="Find."),
                dict(clip_id="self", voice_id="test-clone", provider="mistral", text="Find."),
            ])))
            self.assertEqual(validator.validate(root), [])
            records[0]["time_to_find_seconds"] = 18
            records[1]["voice_condition"] = "selfsimilar"
            (root / "trial_summary.json").write_text(json.dumps(summary))
            errors = validator.validate(root)
            self.assertTrue(any("duration" in error for error in errors))
            self.assertTrue(any("assignment" in error for error in errors))
