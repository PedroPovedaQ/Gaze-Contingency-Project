import csv
import json
import unittest
from unittest.mock import patch

import export_run
from test_trial_csv import TrialCSVTests as Fixture


class DesktopRunExportTests(unittest.TestCase):
    def setUp(self):
        self.fixture = Fixture()
        self.fixture.setUp()
        self.addCleanup(self.fixture.tearDown)
        self.recording = self.fixture.package / "recording.jsonl"
        self.output = self.fixture.root / "exports"

    def test_names_rounds_combined_cache_and_unchanged_ids(self):
        original = self.recording.read_bytes()
        folder = export_run.generate(self.recording, self.output, 'Alex, "A"')
        with (folder / "trials.csv").open(newline="") as stream:
            rows = list(csv.DictReader(stream))
        self.assertEqual(len(rows), 2)
        self.assertTrue(all(row["participant_id"] == "P001" for row in rows))
        self.assertTrue(all(row["participant_name"] == 'Alex, "A"' for row in rows))
        self.assertEqual(len(list(folder.glob("trial_*_performance_subjective.csv"))), 2)
        self.assertEqual(export_run.generate(self.recording, self.output, 'Alex, "A"'), folder)
        changed = export_run.generate(self.recording, self.output, "Alexandra")
        self.assertNotEqual(changed, folder)
        self.assertEqual(self.recording.read_bytes(), original)
        manifest = json.loads((folder / "manifest.json").read_text())
        self.assertEqual(manifest["schema"], "trial-performance-subjective-v2")
        self.assertEqual(rows[1]["completed"], "0")

    def test_missing_wrong_identity_and_traversal_rejected(self):
        with self.assertRaisesRegex(ValueError, "0 matches"):
            export_run.resolve_run(self.recording, self.output)
        summary = self.fixture.run / "trial_summary.json"
        data = json.loads(summary.read_text()); data["participant_id"] = "OTHER"
        summary.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "0 matches"):
            export_run.resolve_run(self.recording)
        with self.assertRaises(ValueError):
            export_run.component("../participant")

    def test_modified_export_rejected(self):
        folder = export_run.generate(self.recording, self.output)
        (folder / "trials.csv").write_text("modified")
        with self.assertRaisesRegex(ValueError, "modified"):
            export_run.generate(self.recording, self.output)

    def test_guidance_evaluation_is_linked_and_does_not_invent_ratings(self):
        folder = export_run.generate(self.recording, self.output)
        report_path = folder / "guidance-evaluation.json"
        report = json.loads(report_path.read_text())
        self.assertEqual(report["provenance"]["recording_sha256"],
                         export_run.fingerprint(self.recording)["sha256"])
        self.assertEqual(report["summary"]["central_state_requests"], 0)
        with (folder / "guidance-human-ratings.csv").open(newline="") as stream:
            ratings = list(csv.DictReader(stream))
        self.assertTrue(all(not row["appropriateness_1_5"] for row in ratings))
        receipt = json.loads((folder / "desktop-export.json").read_text())
        files = {row["file"]: row["sha256"] for row in receipt["files"]}
        for name in ("guidance-evaluation.json", "guidance-human-ratings.csv", "guidance-benchmark.jsonl"):
            self.assertEqual(files[name], export_run.fingerprint(folder / name)["sha256"])
        report_path.write_text("{}")
        with self.assertRaisesRegex(ValueError, "modified"):
            export_run.generate(self.recording, self.output)

    def test_condensed_projection_missing_values_and_receipt(self):
        original = self.recording.read_bytes()
        folder = export_run.generate(self.recording, self.output, assigned_id="P020")
        with (folder / "trials.csv").open(newline="", encoding="utf-8") as stream:
            full = list(csv.DictReader(stream))
        compact_path = folder / export_run.CONDENSED_FILE
        with compact_path.open(newline="", encoding="utf-8-sig") as stream:
            reader = csv.DictReader(stream)
            self.assertEqual(reader.fieldnames, list(export_run.CONDENSED_FIELDS))
            compact = list(reader)
        self.assertEqual(len(compact), len(full))
        for source, row in zip(full, compact):
            for field in export_run.CONDENSED_FIELDS:
                self.assertEqual(row[field], "" if field == "subjective_performance_rating" else source.get(field, ""))
        self.assertEqual(compact[0]["participant_id"], "P001")
        self.assertEqual(compact[1]["time_to_final_selection_seconds"], "")
        self.assertEqual(compact[1]["nasa_tlx_score"], "")
        metadata = json.loads((folder / "manifest.json").read_text())["condensed_export"]
        self.assertEqual(metadata["row_count"], len(compact))
        self.assertEqual(list(metadata["columns"]), list(export_run.CONDENSED_FIELDS))
        receipt = json.loads((folder / "desktop-export.json").read_text())
        entry = next(f for f in receipt["files"] if f["file"] == export_run.CONDENSED_FILE)
        self.assertEqual(entry["sha256"], export_run.fingerprint(compact_path)["sha256"])
        self.assertEqual(self.recording.read_bytes(), original)
        compact_path.write_text("modified")
        with self.assertRaisesRegex(ValueError, "modified"):
            export_run.generate(self.recording, self.output, assigned_id="P020")

    def test_condensed_preserves_signed_zero_and_quoted_values(self):
        folder = self.fixture.root / "projection"
        folder.mkdir()
        supplied = {field: "" for field in export_run.CONDENSED_FIELDS}
        supplied.update(participant_id='P, "quoted"', trial_id="run_r00",
                        signed_theta_deg="-135", absolute_theta_deg="135",
                        target_minus_head_height_m="-0.25", instruction_count="0",
                        time_to_first_target_gaze_seconds="0", nasa_tlx_score="0",
                        subjective_performance_rating="99")
        with (folder / "trials.csv").open("w", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=export_run.CONDENSED_FIELDS)
            writer.writeheader(); writer.writerow(supplied)
        export_run.write_condensed(folder)
        with (folder / export_run.CONDENSED_FILE).open(newline="", encoding="utf-8-sig") as stream:
            row = next(csv.DictReader(stream))
        expected = dict(supplied, subjective_performance_rating="")
        self.assertEqual(row, expected)

    def test_dataset_assignment_preserves_source_identity(self):
        original = self.recording.read_bytes()
        folder = export_run.generate(self.recording, self.output, "Pilot 20",
                                     assigned_id="P020", dataset_id="dataset-20", dataset_name="Morning run")
        with (folder / "trials.csv").open(newline="") as stream:
            rows = list(csv.DictReader(stream))
        self.assertTrue(all(r["participant_id"] == "P001" and r["assigned_participant_id"] == "P020" for r in rows))
        self.assertTrue(all(r["dataset_name"] == "Morning run" and r["dataset_id"] == "dataset-20" for r in rows))
        receipt = json.loads((folder / "desktop-export.json").read_text())
        self.assertEqual(receipt["study_data_path"], str(self.fixture.run.resolve()))
        self.assertEqual(self.recording.read_bytes(), original)
        changed = export_run.generate(self.recording, self.output, "Pilot 20",
                                      assigned_id="P020", dataset_id="dataset-20", dataset_name="Afternoon run")
        self.assertNotEqual(changed, folder)

    def test_source_change_during_export_rejected(self):
        original = export_run.export
        def change(*args, **kwargs):
            result = original(*args, **kwargs)
            self.recording.write_text(self.recording.read_text()+" ")
            return result
        with patch.object(export_run, "export", side_effect=change):
            with self.assertRaisesRegex(ValueError, "changed"):
                export_run.generate(self.recording, self.output)
        self.assertFalse(list(self.output.glob("P001_*")))


if __name__ == "__main__":
    unittest.main()
