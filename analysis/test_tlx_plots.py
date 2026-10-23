import unittest
import pandas as pd
from plots import aligned_tlx_subscales


class TLXPlotTests(unittest.TestCase):
    def test_equal_performance_meaning_has_equal_chart_value_with_mixed_anchors(self):
        common = dict(condition="overall", scale_min=0, scale_max=100,
                      mental=10, physical=20, temporal=30, effort=40, frustration=50)
        source = pd.DataFrame([
            dict(common, performance=100, performance_direction="high_is_good"),
            dict(common, performance=0, performance_direction="high_is_poor"),
            dict(common, performance=0, performance_direction="unknown"),
        ])
        plotted = aligned_tlx_subscales(source)
        self.assertEqual(list(plotted.performance), [0, 0])
        self.assertEqual(list(source.performance), [100, 0, 0])
        source.loc[0, "scale_max"] = 20
        self.assertEqual(len(aligned_tlx_subscales(source)), 1)

    def test_ten_and_hundred_point_forms_only_mix_after_explicit_rescaling(self):
        common = dict(condition="preferred", scale_min=0, performance_direction="high_is_poor")
        a = dict(common, scale_max=10, mental=1, physical=2, temporal=3, performance=4, effort=5, frustration=6)
        b = dict(common, scale_max=100, mental=10, physical=20, temporal=30, performance=40, effort=50, frustration=60)
        source = pd.DataFrame([a,b])
        plotted = aligned_tlx_subscales(source)
        self.assertEqual(list(plotted.mental), [10,10])
        self.assertEqual(list(plotted.performance), [40,40])
        self.assertEqual(list(source.mental), [1,10])


if __name__ == "__main__":
    unittest.main()
