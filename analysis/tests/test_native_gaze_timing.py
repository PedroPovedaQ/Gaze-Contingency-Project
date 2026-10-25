"""Deterministic diagnostic fixtures; these are not measured headset timings."""
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from native_gaze_timing import analyze


def record(sequence, *, pose=None, receipt=None, **changes):
    row=dict(sequence=sequence,epoch=1,session=10,space=20,baseSpace=30,locationFlags=15,
             requestedTime=1_000_000_000+sequence*10_000_000,
             runtimePoseTime=1_000_000_000+sequence*10_000_000 if pose is None else pose,
             receiptMonotonicNs=9_000_000_000+sequence*10_000_000 if receipt is None else receipt,
             result=0,timestampChained=1)
    row.update(changes)
    return row


def batch(records, **changes):
    row=dict(schema=1,kind="native_gaze_batch",featureAvailable=True,focusedAtDrain=True,
             status=dict(epoch=1,records=max([r['sequence'] for r in records],default=0),dropped=0),records=records)
    row.update(changes)
    return row


class NativeTimingTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)

    def write(self,batches, *, end=True, accepted=None):
        path=self.root/'native_gaze_observations.jsonl'
        path.write_text(''.join(json.dumps(b)+'\n' for b in batches))
        (self.root/'native_gaze_capabilities.json').write_text(json.dumps(dict(sensorCaptureSemanticsVerified=False)))
        if end:
            count=sum(len(b['records']) for b in batches)
            (self.root/'native_gaze_end_status.json').write_text(json.dumps(dict(complete=True,acceptedRecords=count if accepted is None else accepted,error='',nativeStatus=batches[-1]['status'])))
        return path

    def test_separate_clock_domains_and_no_sensor_claim(self):
        self.write([batch([record(i) for i in range(1,102)])])
        result=analyze(self.root,sustained_seconds=.5)
        group=result['groups'][0]
        self.assertEqual(group['query_completion_hz'],100)
        self.assertEqual(group['distinct_reported_pose_time_hz'],100)
        self.assertEqual(group['positive_receipt_intervals_ms']['median'],10)
        self.assertEqual(group['pose_time_equals_requested_time'],101)
        self.assertFalse(result['sensor_120hz_established'])
        self.assertIsNone(result['sensor_acquisition_hz'])
        self.assertEqual(result['recording_integrity']['status'],'complete_as_recorded')
        self.assertEqual(len(group['sustained_valid_focused_query_runs']),1)
        self.assertNotIn('latency',group)

    def test_zero_repeated_regressing_future_pose_times(self):
        rows=[record(1,pose=0),record(2,pose=1_020_000_000),record(3,pose=1_020_000_000),
              record(4,pose=1_010_000_000),record(5,pose=2_000_000_000),record(6,pose=-1)]
        self.write([batch(rows)])
        group=analyze(self.root)['groups'][0]
        self.assertEqual(group['zero_pose_times_on_success'],1)
        self.assertEqual(group['negative_pose_times_on_success'],1)
        self.assertEqual(group['repeated_reported_pose_times'],1)
        self.assertEqual(group['regressing_reported_pose_times'],1)
        self.assertEqual(group['distinct_reported_pose_times'],3)
        self.assertEqual(group['pose_time_after_requested_time'],1)

    def test_positive_success_code_invalid_flags_failure_focus(self):
        rows=[record(1,result=1),record(2,result=-1),record(3,locationFlags=3),record(4,locationFlags=1)]
        self.write([batch(rows),batch([record(5)],focusedAtDrain=False),batch([record(6)],focusedAtDrain=None)])
        group=analyze(self.root)['groups'][0]
        self.assertEqual(group['successful_queries'],5)
        self.assertEqual(group['failed_queries'],1)
        self.assertEqual(group['valid_position_and_orientation_queries'],4)
        self.assertEqual(group['tracked_position_and_orientation_queries'],3)
        self.assertEqual(group['unfocused_at_drain_queries'],1)
        self.assertEqual(group['unknown_focus_queries'],1)
        self.assertEqual(group['longest_valid_focused_query_run']['queries'],1)

    def test_sequence_integrity_global_per_epoch_not_space(self):
        rows=[record(1),record(2,space=21),record(3),record(4,space=21)]
        self.write([batch(rows)])
        report=analyze(self.root)
        self.assertEqual(len(report['groups']),2)
        self.assertEqual(report['epochs'][0]['missing_sequences_before_last_observed'],0)
        self.assertEqual(report['recording_integrity']['status'],'complete_as_recorded')

    def test_drops_gaps_missing_tail_and_duplicate_are_not_added_together(self):
        self.write([batch([record(3),record(5),record(5)],status=dict(epoch=1,records=8,dropped=2))])
        report=analyze(self.root)
        epoch=report['epochs'][0]
        self.assertEqual(epoch['missing_sequences_before_last_observed'],3)
        self.assertEqual(epoch['duplicate_sequences'],1)
        self.assertEqual(epoch['native_buffer_drops_reported'],2)
        self.assertEqual(epoch['reported_records_not_observed_after_last'],3)
        self.assertEqual(report['recording_integrity']['status'],'unknown_or_incomplete')

    def test_epochs_and_base_spaces_never_share_timestamp_deltas(self):
        self.write([batch([record(1),record(2,baseSpace=31)]),
                    batch([record(1,epoch=2,pose=1,receipt=1)],status=dict(epoch=2,records=1,dropped=0))])
        report=analyze(self.root)
        self.assertEqual(len(report['epochs']),2)
        self.assertEqual(len(report['groups']),3)
        self.assertTrue(all(g['query_completion_hz'] is None for g in report['groups']))

    def test_partial_live_recording_unknown_not_success(self):
        path=self.write([batch([record(1)])],end=False)
        with path.open('a') as out:
            out.write('{"kind":')
        report=analyze(self.root)
        self.assertEqual(report['recording_integrity']['malformed_lines'],1)
        self.assertIn('end_status_missing_or_invalid',report['recording_integrity']['reasons'])
        self.assertEqual(report['recording_integrity']['status'],'unknown_or_incomplete')

    def test_missing_everything_does_not_report_zero_hz(self):
        report=analyze(self.root)
        self.assertEqual(report['groups'],[])
        self.assertIsNone(report['sensor_acquisition_hz'])
        self.assertEqual(report['recording_integrity']['status'],'unknown_or_incomplete')

    def test_unknown_schema_missing_fields_and_wrong_count(self):
        bad=record(2)
        del bad['locationFlags']
        self.write([batch([record(1),bad]),batch([record(3)],schema=2)],accepted=8)
        report=analyze(self.root)
        integrity=report['recording_integrity']
        self.assertEqual(integrity['invalid_records'],1)
        self.assertEqual(integrity['unsupported_lines'],1)
        self.assertIn('accepted_record_count_unverified_or_mismatched',integrity['reasons'])

    def test_sustained_run_breaks_on_blinks_gaps_and_receipt_regression(self):
        rows=[record(1,receipt=1_000_000_000),record(2,receipt=1_010_000_000),
              record(3,receipt=1_020_000_000,locationFlags=0),record(4,receipt=1_030_000_000),
              record(5,receipt=1_200_000_000),record(6,receipt=1_190_000_000),record(7,receipt=1_200_000_000)]
        self.write([batch(rows)])
        group=analyze(self.root,sustained_seconds=.02)['groups'][0]
        self.assertEqual(group['receipt_gaps_over_threshold'],1)
        self.assertEqual(group['nonincreasing_receipt_intervals'],1)
        self.assertEqual(group['sustained_valid_focused_query_runs'],[])
        self.assertEqual(group['longest_valid_focused_query_run']['duration_seconds'],.01)

    def test_missing_native_status_does_not_certify_integrity(self):
        self.write([batch([record(1)],status={})])
        report=analyze(self.root)
        self.assertIn('native_status_coverage_unknown',report['recording_integrity']['reasons'])
        self.assertIsNone(report['epochs'][0]['native_buffer_drops_reported'])

    def test_reordered_sequences_invalidate_integrity(self):
        self.write([batch([record(2),record(1)])])
        report=analyze(self.root)
        self.assertIn('nonincreasing_enqueue_sequences', report['recording_integrity']['reasons'])
        self.assertEqual(report['recording_integrity']['status'], 'unknown_or_incomplete')

    def test_final_counter_behind_observed_invalidates_even_with_higher_batch_counter(self):
        self.write([batch([record(1),record(2)])])
        end_path=self.root/'native_gaze_end_status.json'
        end=json.loads(end_path.read_text())
        end['nativeStatus']['records']=1
        end_path.write_text(json.dumps(end))
        report=analyze(self.root)
        self.assertIn('reported_counter_behind_observed_sequence',report['recording_integrity']['reasons'])
        self.assertTrue(report['epochs'][0]['reported_counter_behind_observed_sequence'])

    def test_global_missing_sequence_breaks_sustained_run(self):
        self.write([batch([record(1),record(3)])])
        group=analyze(self.root,sustained_seconds=.015)['groups'][0]
        self.assertEqual(group['sustained_valid_focused_query_runs'],[])
        self.assertEqual(group['longest_valid_focused_query_run']['queries'],1)

    def test_interleaved_other_space_does_not_break_sustained_run(self):
        self.write([batch([record(1),record(2,space=21),record(3)])])
        group=analyze(self.root,sustained_seconds=.015)['groups'][0]
        self.assertEqual(len(group['sustained_valid_focused_query_runs']),1)
        self.assertEqual(group['longest_valid_focused_query_run']['queries'],2)

    def test_global_gap_in_other_space_also_breaks_sustained_run(self):
        self.write([batch([record(1),record(3,space=21),record(4)])])
        group=analyze(self.root,sustained_seconds=.015)['groups'][0]
        self.assertEqual(group['sustained_valid_focused_query_runs'],[])
        self.assertEqual(group['longest_valid_focused_query_run']['queries'],1)

    def test_invalid_parameters_rejected(self):
        with self.assertRaises(ValueError):
            analyze(self.root,gap_ms=0)
        with self.assertRaises(ValueError):
            analyze(self.root,sustained_seconds=float("nan"))


if __name__=='__main__':
    unittest.main()
