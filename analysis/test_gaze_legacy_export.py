"""Prevent unavailable eye events from becoming measured zeros in legacy exports."""
import csv
from pathlib import Path
import tempfile
import unittest

from load_data import _load_blink_metrics


class LegacyGazeExportTests(unittest.TestCase):
    def metrics(self, rows):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with (root / "gaze_log.csv").open("w", newline="") as file:
                writer = csv.DictWriter(file, fieldnames=rows[0].keys())
                writer.writeheader()
                writer.writerows(rows)
            return _load_blink_metrics(root, 60)

    def test_unvalidated_events_remain_unavailable(self):
        self.assertEqual(self.metrics([dict(left_eye_open="0.8", right_eye_open="0.8", blink="",
            eye_event_measurement="ray_intersection_proxy_not_validated_fixation")]), (-1, -1.0))

    def test_openness_without_events_is_not_measured_zero(self):
        self.assertEqual(self.metrics([dict(left_eye_open="0.8", right_eye_open="0.8", blink="")]), (-1, -1.0))

    def test_historical_blink_column_still_loads(self):
        self.assertEqual(self.metrics([dict(left_eye_open="0.8", right_eye_open="0.8", blink="0"),
            dict(left_eye_open="0.8", right_eye_open="0.8", blink="1")]), (1, 1.0))

    def test_missing_sentinel_is_not_a_negative_count(self):
        self.assertEqual(self.metrics([dict(left_eye_open="0.8", right_eye_open="0.8", blink="-1")]), (-1, -1.0))
