"""Check that descriptive development spread never invents participant variance."""
import math
import unittest

from power_development_audit import summarize


class DevelopmentPowerAuditTests(unittest.TestCase):
    def test_unfinished_trials_are_not_zero_times_or_participants(self):
        rows = [dict(run=1, completed=True, search_seconds=10.),
                dict(run=1, completed=True, search_seconds=20.),
                dict(run=2, completed=True, search_seconds=30.),
                dict(run=2, completed=False, search_seconds=None)]
        result = summarize(rows)
        self.assertEqual(result["started"], 4)
        self.assertEqual(result["completed"], 3)
        self.assertEqual(result["unfinished"], 1)
        self.assertEqual(result["median_seconds"], 20.)
        self.assertEqual(result["completed_at_or_above_25"], 1)
        self.assertEqual(result["within_run_residual_df"], 1)
        self.assertAlmostEqual(result["within_run_log_sd"], math.log(2)/math.sqrt(2))
        self.assertIsNone(result["between_participant_sd"])
        self.assertIsNone(result["voice_slope_sd"])


if __name__ == "__main__":
    unittest.main()
