"""Synthetic regression fixtures, not validation of the eye tracker."""
import json
from pathlib import Path
import tempfile
import unittest

import pandas as pd

from load_data import load_all_summaries, compute_last_fixation_durations


class LegacyMeasurementTests(unittest.TestCase):
    def test_proxy_values_preserved_but_never_exposed_as_physiology(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder, 'trial_summary.json')
            for provenance in ('ray_intersection_proxy_not_validated_fixation', None, 'validated'):
                summary = dict(participant_id='fixture', run_number=1, objectives=[dict(
                    index=0, fixation_count_total=8, saccade_frequency_hz=2.5)])
                if provenance:
                    summary['eye_event_measurement'] = provenance
                source.write_text(json.dumps(summary))
                row = load_all_summaries(Path(folder)).iloc[0]
                self.assertTrue(pd.isna(row.fixation_count_total))
                self.assertTrue(pd.isna(row.saccade_frequency_hz))
                self.assertEqual(row.proxy_fixation_count_total, 8)
                self.assertTrue(pd.isna(row.proxy_avg_fixation_duration))

    def test_last_target_visit_is_not_last_fixation(self):
        events = pd.DataFrame([dict(participant_id='fixture', run_folder='run_1',
            objective_index=0, condition='neutral', timestamp=2, event_type='fixation_end',
            is_target=1, dwell_duration=.4)])
        row = compute_last_fixation_durations(events).iloc[0]
        self.assertTrue(pd.isna(row.last_fix_on_target_s))
        self.assertEqual(row.last_target_visit_s, .4)


if __name__ == '__main__':
    unittest.main()
