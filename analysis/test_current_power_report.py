"""Check exact paired-score power, retention mixture and threshold reporting."""
import unittest

from scipy import stats

from current_power_report import detectable, enrolled_paired_power, paired_power, validate_design_inputs


class CurrentPowerReportTests(unittest.TestCase):
    def test_null_size(self):
        for n in [12, 30, 60]:
            for alpha in [.025, .05]:
                self.assertAlmostEqual(paired_power(n, 0., alpha), alpha, places=10)

    def test_retention_and_holm_bounds(self):
        for dz in [.2, .5, .8]:
            self.assertAlmostEqual(enrolled_paired_power(30, dz, .05, 1.), paired_power(30, dz, .05))
            lower = enrolled_paired_power(60, dz, .025, .855)
            upper = enrolled_paired_power(60, dz, .05, .855)
            self.assertLess(lower, upper)
            self.assertLess(upper, paired_power(60, dz, .05))
        self.assertEqual(enrolled_paired_power(60, .5, .05, 0.), 0.)
        self.assertLess(paired_power(60, .2, .05), paired_power(60, .5, .05))

    def test_independent_reference(self):
        # Compare the two-survival-function implementation against direct CDF tails.
        cutoff = stats.t.isf(.025, 29)
        expected = stats.nct.cdf(-cutoff, 29, .5*30**.5)+stats.nct.sf(cutoff, 29, .5*30**.5)
        self.assertAlmostEqual(paired_power(30, .5, .05), expected, places=10)

    def test_report_rejects_mixed_schedule_or_under_refined_comparison(self):
        import copy
        rows = [dict(scenario=s, n_enrolled=20, ratio=r, repetitions=10000, trials_per_block=48)
                for s in ["optimistic", "middle", "pessimistic"] for r in [1., .97, .95, .93, .9, .85]]
        baseline = [dict(r, trials_per_block=32) for r in rows]
        validate_design_inputs({"n20": rows}, baseline)
        for field, value in [("trials_per_block", 32), ("repetitions", 1000), ("n_enrolled", 60)]:
            bad = copy.deepcopy(rows)
            bad[0][field] = value
            with self.assertRaises(ValueError):
                validate_design_inputs({"n20": bad}, baseline)
        with self.assertRaises(ValueError):
            validate_design_inputs({"n20": rows}, rows)
        with self.assertRaises(ValueError):
            validate_design_inputs({"n20": rows}, baseline[:-1])

    def test_threshold_does_not_extrapolate(self):
        rows = [dict(scenario="x", ratio=1., power=.05), dict(scenario="x", ratio=.9, power=.8)]
        self.assertAlmostEqual(detectable(rows, "x", .8)["reduction"], 10.)
        self.assertIsNone(detectable(rows, "x", .9)["reduction"])


if __name__ == "__main__":
    unittest.main()
