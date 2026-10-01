import csv
import json
import tempfile
import unittest
from pathlib import Path

from gaze_timing import capabilities, timing, write_outputs


def sample(sequence, time, **fields):
    row = dict(sequence=sequence, frame=sequence, receiptTime=time, inputEventTime=time,
               recordingId='fixture', participant='p1', run=1, phase='search', practice=False,
               deviceId=7, deviceAvailable=True, newInputUpdate=True, valid=True,
               worldDirection=dict(x=0, y=0, z=1))
    row.update(fields)
    return row


class GazeTimingTests(unittest.TestCase):
    def test_120_saved_rows_per_second_can_contain_only_60_input_deliveries(self):
        rows = [sample(i, 10+i/120, inputEventTime=10+(i//2)/60, newInputUpdate=i%2 == 0)
                for i in range(121)]
        result = timing(rows)
        phase = result['phases'][0]
        self.assertEqual(phase['recorded_rows'], 121)
        self.assertEqual(phase['distinct_known_input_deliveries'], 61)
        self.assertEqual(phase['heartbeat_rows'], 60)
        self.assertEqual(phase['delivery_interval_pairs'], 60)
        self.assertAlmostEqual(phase['delivery_interval_hz'], 60)
        self.assertAlmostEqual(phase['recorded_row_interval_hz'], 120)
        self.assertIsNone(phase['native_samples'])
        self.assertEqual(result['sensor_rate_conclusion'], 'not_established')
        # Every direction is identical: legitimate deliveries are not deduplicated by pose.

    def test_phase_return_does_not_bridge_the_pause(self):
        rows = [sample(0, 1), sample(1, 1.01), sample(2, 2, phase='paused'),
                sample(3, 10), sample(4, 10.01)]
        result = timing(rows)
        search, paused = result['phases']
        self.assertEqual(search['delivery_interval_pairs'], 2)
        self.assertAlmostEqual(search['delivery_interval_hz'], 100)
        self.assertIsNone(paused['delivery_interval_hz'])
        self.assertEqual(len(result['intervals']), 2)

    def test_device_participant_run_and_practice_changes_split_pairs(self):
        for changed in ({'deviceId': 8}, {'participant': 'p2'}, {'run': 2}, {'practice': True}):
            with self.subTest(changed=changed):
                result = timing([sample(0, 1), sample(1, 1.01, **changed)])
                self.assertEqual(len(result['phases']), 2)
                self.assertEqual(result['intervals'], [])

    def test_duplicate_update_claims_are_visible_not_extra_samples(self):
        result = timing([sample(0, 1), sample(1, 1.005, inputEventTime=1), sample(2, 1.01)])
        phase = result['phases'][0]
        self.assertEqual(phase['input_update_flags'], 3)
        self.assertEqual(phase['distinct_known_input_deliveries'], 2)
        self.assertEqual(phase['duplicate_update_flags'], 1)
        self.assertEqual(phase['nonincreasing_delivery_times'], 1)
        self.assertEqual(phase['delivery_interval_pairs'], 1)

    def test_unknown_timestamp_and_invalid_deliveries_are_distinct(self):
        result = timing([sample(0, 1, inputEventTime=0, valid=False),
                         sample(1, 1.01, valid=False), sample(2, 1.02)])
        phase = result['phases'][0]
        self.assertEqual(phase['unknown_delivery_times'], 1)
        self.assertEqual(phase['distinct_known_input_deliveries'], 2)
        self.assertEqual(phase['valid_input_update_flags'], 1)
        self.assertAlmostEqual(phase['delivery_interval_hz'], 100)

    def test_application_sequence_gap_does_not_become_sensor_loss(self):
        result = timing([sample(4, 1), sample(7, 1.1)])
        self.assertEqual(result['missing_application_row_sequences'], 2)
        self.assertIsNone(result['native_drops'])

    def test_copied_htc_fields_do_not_inflate_query_counts(self):
        flags = dict(htcGazeAvailable=True, leftValid=True, rightValid=False,
                     pupilAvailable=True, leftPupilValid=True, rightPupilValid=False,
                     opennessAvailable=False, leftOpennessValid=False, rightOpennessValid=False)
        rows = [sample(0, 1, htcQueryReceiptTime=1, **flags),
                sample(1, 1.005, htcQueryReceiptTime=1, **flags),
                sample(2, 1.01, htcQueryReceiptTime=1.01, **flags)]
        signals = {r['signal']: r for r in capabilities(rows)}
        self.assertEqual(signals['combined_gaze']['observation_count'], 3)
        self.assertEqual(signals['left_eye_gaze']['observation_count'], 2)
        self.assertEqual(signals['left_pupil']['valid_observations'], 2)
        self.assertEqual(signals['right_pupil']['evidence'], 'available_no_valid_observation')
        self.assertEqual(signals['left_openness']['evidence'], 'not_observed_or_unavailable')
        self.assertIsNone(signals['left_pupil']['native_rate_hz'])

    def test_missing_query_clock_cannot_establish_capability_rate(self):
        rows = [sample(0, 1, htcGazeAvailable=True, leftValid=True)]
        signals = {r['signal']: r for r in capabilities(rows)}
        self.assertEqual(signals['left_eye_gaze']['evidence'], 'no_timed_query_observed')
        self.assertEqual(signals['left_eye_gaze']['observation_count'], 0)

    def test_tables_preserve_unknown_metadata_and_have_clock_dictionary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            result = write_outputs(root, [sample(0, 1), sample(1, 1.01)], root/'raw.jsonl',
                                   {'complete': False}, plot=False)
            self.assertIsNone(result['recording_metadata'])
            self.assertFalse(result['recording_end_status']['complete'])
            with (root/'signal_capabilities.csv').open(newline='') as stream:
                signals = list(csv.DictReader(stream))
            self.assertEqual(signals[0]['native_rate_hz'], '')
            report = json.loads((root/'timing_report.json').read_text())
            capture = next(r for r in report['clocks_and_frames'] if r['field'] == 'nativeCaptureTime')
            self.assertFalse(capture['known'])
            self.assertTrue((root/'delivery_intervals.csv').exists())
            self.assertFalse((root/'delivery_timing.png').exists())


if __name__ == '__main__':
    unittest.main()
