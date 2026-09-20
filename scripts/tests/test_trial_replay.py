import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


analysis = load("replay_analysis", "analyze-trial-replay.py")
encoder = load("replay_encoder", "encode-trial-replay.py")


def fixture():
    items = [dict(kind="header", recordingId="synthetic-test", sourceType="synthetic_demo", time=0),
             dict(kind="trial", trialId="r1", targetId="b", objects=[dict(id="a"), dict(id="b")], time=0),
             dict(kind="search_start", time=1),
             dict(kind="sample", trialId="r1", time=1, searchActive=True, gazeAvailable=True, gazeTracked=1, hoveredId="a"),
             dict(kind="sample", trialId="r1", time=1.05, searchActive=True, gazeAvailable=True, gazeTracked=-1, hoveredId="a"),
             dict(kind="selection", trialId="r1", objectId="a", correct=False, time=2),
             dict(kind="search_paused", time=3), dict(kind="search_resumed", time=5),
             dict(kind="sample", trialId="r1", time=5, searchActive=True, gazeAvailable=True, gazeTracked=0),
             dict(kind="sample", trialId="r1", time=6, searchActive=True, gazeAvailable=True, gazeTracked=1, hoveredId="b"),
             dict(kind="selection", trialId="r1", objectId="b", correct=True, time=7),
             dict(kind="end", complete=True, time=8)]
    return [dict(r, schema=1, sequence=i) for i, r in enumerate(items)]


class ReplayAnalysisTests(unittest.TestCase):
    def test_pause_selection_validity_and_gaps(self):
        row = analysis.metrics(fixture())[0]
        self.assertEqual(row["search_seconds"], 4)
        self.assertEqual(row["wrong_selections"], 1)
        self.assertEqual(row["correct_selections"], 1)
        self.assertEqual(row["gaze_unknown_samples"], 1)
        self.assertEqual(row["gaze_invalid_samples"], 1)
        self.assertEqual(row["sample_gap_seconds"], 1)
        self.assertEqual(row["sampled_hover_other_seconds"], 0.05)

    def test_incomplete_does_not_extrapolate(self):
        row = analysis.metrics(fixture()[:6])[0]
        self.assertEqual(row["search_seconds"], 1)
        self.assertEqual(row["outcome"], "incomplete")

    def test_recorded_pause_aware_clock_wins_over_wall_time(self):
        records = fixture()
        records[3]["searchSeconds"] = 0
        records[4]["searchSeconds"] = 0.025
        records[5]["searchSeconds"] = 0.5
        records[8]["searchSeconds"] = 1
        records[9]["searchSeconds"] = 1.5
        records[10]["searchSeconds"] = 2
        row = analysis.metrics(records)[0]
        self.assertEqual(row["search_seconds"], 2)
        self.assertEqual(row["search_clock"], "recorded_search_clock")

    def test_wrong_identity_or_correctness_rejected(self):
        for field, value in [("objectId", "unknown"), ("correct", True), ("trialId", "other")]:
            records = fixture()
            records[5][field] = value
            with self.assertRaises(ValueError):
                analysis.metrics(records)

    def test_practice_separate(self):
        records = fixture()
        records[1]["practice"] = True
        self.assertTrue(analysis.metrics(records)[0]["practice"])

    def test_prefix_recovery_and_corrupt_middle(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "recording.jsonl"
            lines = [json.dumps(r) for r in fixture()]
            path.write_text("\n".join(lines[:-1]) + '\n{"kind":')
            records, warning = analysis.read_records(path)
            self.assertEqual(len(records), len(lines) - 1)
            self.assertIn("Incomplete", warning)
            path.write_text("\n".join([lines[0], "bad-json", *lines[1:]]))
            with self.assertRaises(ValueError):
                analysis.read_records(path)

    def test_order_and_schema_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "recording.jsonl"
            for key, value in [("schema", 99), ("sequence", 0), ("time", -1)]:
                records = fixture()
                records[3][key] = value
                path.write_text("\n".join(json.dumps(r) for r in records))
                with self.assertRaises(ValueError):
                    analysis.read_records(path)

    def test_output_provenance_and_source_immutability(self):
        with tempfile.TemporaryDirectory() as temp:
            parent = Path(temp)
            source_dir = parent / "source"
            source_dir.mkdir()
            source = source_dir / "recording.jsonl"
            source.write_text("\n".join(json.dumps(r) for r in fixture()))
            before = source.read_bytes()
            out = parent / "derived"
            analysis.analyze(source, out)
            meta = json.loads((out / "provenance.json").read_text())
            self.assertEqual(meta["source_recording_id"], "synthetic-test")
            self.assertEqual(source.read_bytes(), before)
            for bad in [source_dir, source_dir / "derived", out]:
                with self.assertRaises((OSError, ValueError)):
                    analysis.analyze(source, bad)


class EncoderTests(unittest.TestCase):
    def test_incomplete_export_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            (folder / "export.json").write_text(json.dumps(dict(schema=1, complete=False)))
            with self.assertRaises(ValueError):
                encoder.encode(folder)

    def test_safe_arguments_label_and_no_overwrite(self):
        with tempfile.TemporaryDirectory(prefix="replay test ") as temp:
            folder = Path(temp)
            (folder / "export.json").write_text(json.dumps(dict(schema=1, complete=True, frames=1, fps=30, sourceType="synthetic_demo")))
            (folder / "frame_000000.png").touch()
            (folder / "audio.wav").touch()
            with patch.object(encoder.shutil, "which", return_value="/usr/bin/ffmpeg"), patch.object(encoder.subprocess, "run") as run:
                encoder.encode(folder)
                args = run.call_args.args[0]
                self.assertIn("-n", args)
                self.assertIn(str(folder.resolve() / "frame_%06d.png"), args)
                self.assertIn("SYNTHETIC DEMO", args[args.index("-vf") + 1])
            (folder / "demo.mp4").touch()
            with self.assertRaises(ValueError):
                encoder.encode(folder)


if __name__ == "__main__":
    unittest.main()
