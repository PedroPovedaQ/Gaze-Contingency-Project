"""Numerical and design checks for the current trial-level power simulation."""
import csv
from dataclasses import replace
from pathlib import Path
import unittest
from unittest.mock import patch
import warnings

import numpy as np
from scipy.linalg import block_diag
from scipy.optimize._numdiff import approx_derivative

import current_design_power as power


class CurrentPowerTests(unittest.TestCase):
    def setUp(self):
        self.x, self.y, self.obs, self.diag = power.generate(
            np.random.default_rng(123), 12, .90, power.MAIN[1], 32)

    def test_schedule_matches_production_allocation_and_groups(self):
        path = Path(__file__).resolve().parents[1]/"work_log/receipts/2026-10-30-schedule-v8/synthetic-trial-schedule.csv"
        with path.open() as stream:
            rows = list(csv.DictReader(stream))
        expected = sorted(int(r["signed_theta_deg"]) for r in rows[:32])
        for block in power.schedule(np.random.default_rng(9), 100, 32).reshape(-1, 32):
            self.assertEqual(sorted(block), expected)
            for group in block.reshape(4, 8):
                self.assertEqual(sorted(group), sorted(power.STEPS*45))
        for offset in range(0, 64, 8):
            self.assertEqual(sorted(int(r["signed_theta_deg"]) for r in rows[offset:offset+8]),
                             sorted(power.STEPS*45))

    def test_counterbalance_and_missing_denominators(self):
        self.assertEqual(np.sum(self.x[:, 0, 1] > 0), 6)
        self.assertTrue(np.all(self.x[:, :32, 1] == -self.x[:, 32:, 1]))
        d = self.diag
        self.assertEqual(d["exposed_trials"], 64*12-32*d["dropped_participants"])
        self.assertEqual(d["successful_trials"]+d["timeout_neutral"]+d["timeout_self"]+
                         d["incomplete_trials"], d["exposed_trials"])
        self.assertTrue(np.all(self.y[self.obs] < np.log(25)))
        self.assertEqual(self.obs.sum(), d["successful_trials"])

    def test_96_trial_schedule_and_denominators(self):
        from collections import Counter
        for block in power.schedule(np.random.default_rng(9), 100).reshape(-1, 48):
            self.assertEqual(Counter(block), Counter({a: 6 for a in power.STEPS * 45}))
            for group in block.reshape(6, 8):
                self.assertEqual(sorted(group), sorted(power.STEPS * 45))
        x, y, obs, d = power.generate(np.random.default_rng(123), 20, .9, power.MAIN[1])
        self.assertEqual(x.shape, (20, 96, 7))
        self.assertEqual(np.sum(x[:, 0, 1] > 0), 10)
        self.assertTrue(np.all(x[:, :48, 1] == -x[:, 48:, 1]))
        self.assertEqual(d["exposed_trials"], 96*20-48*d["dropped_participants"])
        self.assertEqual(d["successful_trials"]+d["timeout_neutral"]+d["timeout_self"]+
                         d["incomplete_trials"], d["exposed_trials"])
        self.assertTrue(np.all(y[obs] < np.log(25)))

    def test_v9_simulation_matches_exported_production_schedule(self):
        path = Path(__file__).resolve().parents[1]/"work_log/receipts/2026-11-01-schedule-v9/synthetic-trial-schedule.csv"
        with path.open() as stream:
            rows = list(csv.DictReader(stream))
        self.assertEqual(len(rows), 96)
        expected = sorted(int(r["signed_theta_deg"]) for r in rows[:48])
        for block in power.schedule(np.random.default_rng(9), 30).reshape(-1, 48):
            self.assertEqual(sorted(block), expected)

    def test_dense_likelihood_and_gradient(self):
        q, m = power.sufficient(self.x, self.y, self.obs)
        lam = np.array([.3, .2])
        v = block_diag(*[np.eye(o.sum())+(x[o, :2]*lam) @ x[o, :2].T
                         for x, o in zip(self.x, self.obs)])
        x = self.x[self.obs]
        y = self.y[self.obs]
        inv = np.linalg.inv(v)
        h = x.T @ inv @ x
        b = np.linalg.solve(h, x.T @ inv @ y)
        rss = (y-x@b) @ inv @ (y-x@b)
        logdet_v = 2*np.log(np.diag(np.linalg.cholesky(v))).sum()
        logdet_h = 2*np.log(np.diag(np.linalg.cholesky(h))).sum()
        expected = .5*(logdet_v+logdet_h+(m-7)*np.log(rss/(m-7)))
        value, gradient = power.reml(lam, q, m)
        self.assertAlmostEqual(value, expected, places=8)
        np.testing.assert_allclose(gradient, approx_derivative(lambda l: power.reml(l, q, m)[0], lam), rtol=2e-5)
        beta, cov, scale, _ = power.reml(lam, q, m, details=True)
        np.testing.assert_allclose(beta, b, atol=1e-9)
        np.testing.assert_allclose(cov, np.linalg.inv(h)*scale, atol=1e-9)
        # Boundary likelihood must remain finite (zero slope = intercept-only).
        self.assertTrue(np.isfinite(power.reml([.3, 0], q, m)[0]))

    def test_fixed_effect_translation(self):
        f = power.fit(self.x, self.y, self.obs, self.diag["paired_participants"])
        g = power.fit(self.x, self.y+.2*self.x[:, :, 1], self.obs, self.diag["paired_participants"])
        self.assertTrue(f["ok"] and g["ok"])
        self.assertAlmostEqual(g["estimate"]-f["estimate"], .2, places=5)
        self.assertAlmostEqual(g["se"], f["se"], places=5)

    def test_failure_not_removed_from_power_denominator(self):
        with patch.object(power, "fit", return_value=dict(ok=False, error="forced", retry=False)):
            r = power.run_cell((power.MAIN[1], 12, .9, 3, 123))
        self.assertEqual(r["failed_fits"], 3)
        self.assertEqual(r["power"], 0.)
        self.assertNotIn("power_given_fit", r)

    def test_fixed_seed(self):
        t = (power.MAIN[1], 12, .9, 3, 123)
        self.assertEqual(power.run_cell(t), power.run_cell(t))

    def test_no_censoring_no_loss(self):
        s = replace(power.MAIN[1], deadline=1e8, trial_loss=0., dropout=0.)
        _, _, obs, diag = power.generate(np.random.default_rng(11), 20, 1., s)
        self.assertTrue(obs.all())
        self.assertEqual(diag["paired_participants"], 20)
        with self.assertRaises(ValueError):
            power.generate(np.random.default_rng(11), 13, 1., s)

    def test_effect_changes_timeout_selection_in_expected_directions(self):
        s = replace(power.MAIN[1], trial_loss=0., dropout=0.)
        x, _, null_obs, _ = power.generate(np.random.default_rng(83), 60, 1., s)
        _, _, effect_obs, _ = power.generate(np.random.default_rng(83), 60, .85, s)
        self_mask = x[:, :, 1] > 0
        # Same random draws: centered positive contrast speeds self-similar and
        # slows neutral. No timeout is ever inserted as an observed time.
        self.assertTrue(np.all(effect_obs[self_mask] >= null_obs[self_mask]))
        self.assertTrue(np.all(effect_obs[~self_mask] <= null_obs[~self_mask]))
        self.assertGreater(effect_obs[self_mask].sum(), null_obs[self_mask].sum())

    def test_total_second_block_dropout_is_explicit_fit_failure(self):
        x, y, obs, d = power.generate(np.random.default_rng(5), 12, .9,
                                       replace(power.MAIN[1], dropout=1.))
        self.assertFalse(obs[:, 48:].any())
        self.assertEqual(d["paired_participants"], 0)
        self.assertFalse(power.fit(x, y, obs, d["paired_participants"])["ok"])

    def test_against_statsmodels(self):
        try:
            from statsmodels.regression.mixed_linear_model import MixedLM, MixedLMParams
        except ImportError:
            self.skipTest("statsmodels optional validation dependency; see pinned power requirements")
        for seed in [8, 18, 38]:
            x, y, obs, d = power.generate(np.random.default_rng(seed), 40, .9, power.MAIN[1])
            f = power.fit(x, y, obs, d["paired_participants"])
            xx, yy = x[obs], y[obs]
            groups = np.broadcast_to(np.arange(40)[:, None], obs.shape)[obs]
            model = MixedLM(yy, xx, groups, exog_re=xx[:, :2])
            free = MixedLMParams.from_components(fe_params=np.ones(7), cov_re=np.eye(2))
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                ref = model.fit(reml=True, free=free, method="lbfgs", gtol=1e-9, maxiter=500)
            self.assertTrue(f["ok"] and ref.converged)
            self.assertAlmostEqual(f["estimate"], ref.fe_params[1], places=4)
            # Use GLS covariance at estimated variances; statsmodels' full Hessian
            # bse includes cross-derivatives and is not the same approximation.
            q, m = power.sufficient(x, y, obs)
            b, c, scale, v = power.reml(np.diag(ref.cov_re)/ref.scale, q, m, details=True)
            self.assertAlmostEqual(f["se"], np.sqrt(c[1, 1]), places=4)
            self.assertAlmostEqual(f["slope_sd"], np.sqrt(v[1]), places=3)


if __name__ == "__main__":
    unittest.main()
