import unittest
from tlx_scoring import score_tlx


class TLXTests(unittest.TestCase):
    def response(self, **changes):
        value = dict(scale_min=0, scale_max=100, performance_direction="high_is_poor",
                     mental=10, physical=20, temporal=30, performance=40, effort=50, frustration=60)
        value.update(changes)
        return value

    def test_raw_mean_with_explicit_direction(self):
        self.assertEqual(score_tlx(self.response())["nasa_tlx_score"], 35)
        self.assertAlmostEqual(score_tlx(self.response(performance_direction="high_is_good"))["nasa_tlx_score"], 230/6)

    def test_unknown_direction_never_guessed(self):
        self.assertIsNone(score_tlx(self.response(performance_direction=""))["nasa_tlx_score"])
        self.assertIsNone(score_tlx(self.response(scale_min=None))["nasa_tlx_score"])

    def test_missing_invalid_and_unsupported_scales_do_not_become_zeros(self):
        for changes in (dict(mental=""), dict(mental=float("nan")), dict(effort=101), dict(scale_max=20)):
            with self.subTest(changes=changes):
                self.assertIsNone(score_tlx(self.response(**changes))["nasa_tlx_score"])

    def test_zero_is_a_valid_score(self):
        self.assertEqual(score_tlx(self.response(mental=0, physical=0, temporal=0, performance=0, effort=0, frustration=0))["nasa_tlx_score"], 0)


if __name__ == "__main__":
    unittest.main()
