import csv
import json
import tempfile
import unittest
from pathlib import Path

from demo_trial_sheets import generate


class DemoTrialSheetsTests(unittest.TestCase):
    def test_generated_examples_use_production_export_and_explicit_missingness(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "demo"
            report = generate(output)
            with (output / "export/trials.csv").open(newline="") as stream:
                rows = list(csv.DictReader(stream))
            self.assertEqual(len(rows), 3)
            self.assertEqual([r["incorrect_selection_count"] for r in rows], ["0", "1", "0"])
            self.assertEqual([r["instruction_count"] for r in rows], ["1", "2", "1"])
            self.assertEqual([r["time_to_final_selection_seconds"] for r in rows], ["2", "3", ""])
            self.assertEqual(rows[2]["interrupted_instruction_count"], "1")
            self.assertEqual(rows[2]["search_exposure_seconds"], "1.5")
            self.assertEqual(rows[2]["outcome"], "interrupted")
            for row, movement, rotation, valid, total in zip(
                rows, (.02, .03, .0145), (45, 90, 26.1), (41, 61, 30), (41, 61, 31)
            ):
                self.assertAlmostEqual(float(row["gross_head_translation_m"]), movement)
                self.assertAlmostEqual(float(row["gross_head_rotation_deg"]), rotation)
                self.assertEqual(int(row["gaze_valid_samples"]), valid)
                self.assertEqual(int(row["gaze_total_samples"]), total)
                self.assertAlmostEqual(float(row["time_to_first_target_gaze_seconds"]), .6)
                self.assertAlmostEqual(float(row["time_to_first_right_controller_target_seconds"]), .9)
            response_ids = {r["survey__nasa_tlx__block__response_id"] for r in rows}
            self.assertEqual(response_ids, {"SYNTHETIC_TLX_BLOCK0"})
            for row in rows:
                self.assertEqual(row["exclude_from_study_analysis"], "1")
                self.assertEqual(row["data_purpose"], "synthetic")
                self.assertAlmostEqual(float(row["nasa_tlx_score"]), 175/6)
                self.assertEqual(row["subjective_scoring_status"], "raw_responses_and_explicit_tlx_mean")
                self.assertEqual(row["unique_zones_visited_count"], "2")
                self.assertNotIn("time_to_final_correct_zone_seconds", row)
                self.assertEqual(row["scheduled_height_reference"], "recorded_readiness_cross_y_v1")
            with (output / "export/motion.csv").open(newline="") as stream:
                motion = list(csv.DictReader(stream))
            self.assertEqual(len(motion), report["motion"]["row_count"])
            self.assertTrue(any(r["replay__gazeTracked"] == "0" for r in motion))
            self.assertTrue(any(r["replay__searchActive"] == "0" for r in motion))
            for entry, row in zip(report["trials"], rows):
                with (output / "export" / entry["file"]).open(newline="") as stream:
                    self.assertEqual(list(csv.DictReader(stream)), [row])
            self.assertTrue(json.loads((output / "export/manifest.json").read_text())["exclude_from_study_analysis"])
            with (output / "trial_column_dictionary.csv").open(newline="") as stream:
                self.assertEqual({r["column"] for r in csv.DictReader(stream)}, set(rows[0]))
            with self.assertRaises(FileExistsError):
                generate(output)


if __name__ == "__main__":
    unittest.main()
