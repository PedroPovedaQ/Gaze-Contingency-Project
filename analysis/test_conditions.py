"""Condition compatibility checks; run with unittest discovery in analysis/."""

import json
from pathlib import Path
import tempfile
import unittest
import pandas as pd

from load_data import _condition_for_round, _normalize_tlx_condition, load_all_summaries, load_nasa_tlx, load_event_logs
from stats import _ttest_paired_within


class ConditionTests(unittest.TestCase):
    def test_new_voice_runs_remain_aware_for_every_round(self):
        for voice in ("generic", "neutral-male", "neutral-female", "selfsimilar"):
            with self.subTest(voice=voice), tempfile.TemporaryDirectory() as folder:
                label = f"gaze_aware_voice-{voice}"
                summary = {
                    "participant_id": "P001", "run_number": 1,
                    "condition": label, "rounds_per_block": 14,
                    "round_schedule": "always_gaze_aware",
                    "objectives": [{"index": i, "completed": True} for i in range(14)],
                }
                Path(folder, "trial_summary.json").write_text(json.dumps(summary))
                rows = load_all_summaries(Path(folder))
                self.assertEqual(len(rows), 14)
                self.assertEqual(set(rows["condition"]), {"gaze_aware"})
                self.assertEqual(set(rows["run_condition"]), {label})
                self.assertEqual(set(rows["round_schedule"]), {"always_gaze_aware"})
                self.assertEqual(len(set(rows["block"])), 1)
                self.assertEqual(_normalize_tlx_condition(label), "gaze_aware")

    def test_historical_alternation_is_preserved(self):
        label = "alternating_gaze_unaware_then_gaze_aware_voice-selfsimilar"
        self.assertEqual(
            [_condition_for_round(i, label, 7) for i in range(4)],
            ["gaze_unaware", "gaze_aware", "gaze_unaware", "gaze_aware"],
        )
        self.assertEqual(_normalize_tlx_condition(label), "overall")

    def test_historical_single_unaware_and_unknown_labels(self):
        self.assertEqual(_condition_for_round(0, "gaze_unaware_voice-generic", 7), "gaze_unaware")
        self.assertEqual(_condition_for_round(0, "custom", 7), "custom")

    def test_workload_export_retains_voice_identity(self):
        with tempfile.TemporaryDirectory() as folder:
            Path(folder, "nasa_tlx.csv").write_text(
                "participant_id,condition,mental,physical,temporal,performance,effort,frustration\n"
                "P001,gaze_aware_voice-generic,10,10,10,10,10,10\n"
                "P001,gaze_aware_voice-selfsimilar,20,20,20,20,20,20\n"
            )
            rows = load_nasa_tlx(Path(folder))
            self.assertEqual(list(rows["condition"]), ["gaze_aware", "gaze_aware"])
            self.assertEqual(list(rows["run_condition"]), [
                "gaze_aware_voice-generic", "gaze_aware_voice-selfsimilar",
            ])

    def test_event_log_labels_use_sibling_summary(self):
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder, "P001", "run_003_gaze_aware_voice-generic_2026-09-10_10-00-00")
            run.mkdir(parents=True)
            (run / "trial_summary.json").write_text(json.dumps({
                "condition": "gaze_aware_voice-generic", "rounds_per_block": 14,
            }))
            (run / "trial_events.csv").write_text("objective_index,event_type\n0,capture_correct\n13,capture_correct\n")
            rows = load_event_logs(Path(folder))
            self.assertEqual(list(rows["condition"]), ["gaze_aware", "gaze_aware"])

    def test_mixed_design_data_cannot_produce_awareness_test(self):
        rows = pd.DataFrame([
            {"participant_id": p, "condition": c, "time_to_find": t, "round_schedule": schedule}
            for p in ("P001", "P002")
            for c, t, schedule in (
                ("gaze_unaware", 30, "alternating_gaze_unaware_gaze_aware"),
                ("gaze_aware", 20, "alternating_gaze_unaware_gaze_aware"),
                ("gaze_aware", 5, "always_gaze_aware"),
            )
        ])
        result = _ttest_paired_within(rows, "time_to_find")
        self.assertIsNone(result["p"])
        self.assertIn("always", result["reason"])


if __name__ == "__main__":
    unittest.main()
