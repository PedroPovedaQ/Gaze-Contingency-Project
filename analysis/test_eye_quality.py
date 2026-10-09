import unittest
import json
import tempfile
import math
import subprocess
import sys
import csv
from pathlib import Path
from eye_quality import summarize, candidate_events, compare_annotations, evaluate_offset, read_recording, recording_status, flat, trial_quality


def sample(t, direction=None, **extra):
    row = dict(receiptTime=t, inputEventTime=t, sequence=int(t*1000), valid=True,
               newInputUpdate=True, worldDirection=direction or dict(x=0,y=0,z=1),
               rawWorldDirection=direction or dict(x=0,y=0,z=1), phase='search')
    row.update(extra)
    return row


class EyeQualityTests(unittest.TestCase):
    def test_native_fallback_cannot_validate_untracked_ext_measurements(self):
        rows = [sample(1+i*.02, raySource='HTC_binocular_mean', tracked=False,
                       validationTargetPresent=True, validationId='check', validationSet='validation',
                       targetId='center', targetErrorDegrees=0, mirroredOriginErrorDegrees=0,
                       usedTargetErrorDegrees=2) for i in range(20)]
        report = summarize(rows, settle=0)
        self.assertEqual(report['spatial'][0]['samples'], 0)
        self.assertEqual(report['spatial'][0]['valid_seconds'], 0)
        self.assertIsNone(report['spatial'][0]['median_error_deg'])
        self.assertEqual(report['native_fallback_rows'], 20)
        self.assertEqual(candidate_events(rows), [])
        # CSV retains the labeled used ray; invalid target errors never become measured zero.
        self.assertEqual(flat(rows[0])['raySource'], 'HTC_binocular_mean')
        self.assertIsNone(flat(dict(rows[0], valid=False))['usedTargetErrorDegrees'])

    def test_json_reader_rejects_truthy_strings_and_boolean_numbers(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'gaze_observations.jsonl'
            base = sample(1, schema=1, kind='gaze_observation', recordingId='synthetic')
            for field, value in (('valid', 'false'), ('newInputUpdate', 'false'), ('deviceAvailable', 1),
                                 ('leftPupilValid', 'true'), ('validationTargetPresent', None),
                                 ('schema', True), ('sequence', True), ('receiptTime', True),
                                 ('recordingId', 1), ('recordingId', '  '), ('inputEventTime', True),
                                 ('ageSeconds', '.01'), ('maxDeliveryAgeSeconds', False)):
                with self.subTest(field=field, value=value):
                    path.write_text(json.dumps(dict(base, **{field:value}))+'\n')
                    with self.assertRaises(ValueError):
                        read_recording(path)
            # Actual boolean false remains an explicit invalid observation.
            path.write_text(json.dumps(dict(base, valid=False))+'\n')
            rows, warnings = read_recording(path)
            self.assertIs(rows[0]['valid'], False)
            self.assertEqual(warnings, [])

    def test_clean_close_requires_typed_count_and_boolean_completion(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'gaze_observations.jsonl'
            receipt = path.parent/'end_status.json'
            rows = [sample(1)]
            receipt.write_text('{"complete":true}')
            status = recording_status(path, rows, [])
            self.assertIs(status['complete'], False)
            self.assertIn('count is missing', status['reason'])
            for record in ({'complete':'false','acceptedRows':1}, {'complete':1,'acceptedRows':1},
                           {'complete':True,'acceptedRows':True}, {'complete':True,'acceptedRows':1.0},
                           {'complete':True,'acceptedRows':-1}):
                receipt.write_text(json.dumps(record))
                with self.assertRaises(ValueError):
                    recording_status(path, rows, [])
            receipt.write_text('{"complete":true,"acceptedRows":1}')
            self.assertIs(recording_status(path, rows, [])['complete'], True)

    def test_trial_quality_does_not_hide_loss_or_bridge_pauses_and_practice(self):
        rows = [sample(1, sourceTrialId='a'), sample(1.05, sourceTrialId='a',valid=False),
                sample(1.1, sourceTrialId='a',valid=False),
                sample(2, sourceTrialId='a',phase='paused'),
                sample(8, sourceTrialId='a'), sample(8.05, sourceTrialId='a'),
                sample(9, sourceTrialId='b'), sample(9.05, sourceTrialId='b'),
                sample(10, sourceTrialId='b',practice=True)]
        a,b = trial_quality(rows)
        self.assertAlmostEqual(a['observed_search_window_seconds'], .15)
        self.assertAlmostEqual(a['valid_seconds'], .1)
        self.assertAlmostEqual(a['gap_seconds'], .05)
        self.assertAlmostEqual(a['valid_observation_fraction'], 3/5)
        self.assertAlmostEqual(b['valid_time_fraction'], 1)
        self.assertAlmostEqual(a['input_delivery_hz'], 20)  # No 7-second paused delivery interval.
        self.assertIsNone(a['calibration_id'])
        self.assertIsNone(a['exclusion_reason'])
        self.assertEqual(a['exclusion_status'], 'not_adjudicated')

    def test_trial_quality_clips_valid_time_to_remaining_freshness(self):
        rows = [sample(1, sourceTrialId='a',ageSeconds=.08,maxDeliveryAgeSeconds=.1),
                sample(1.2, sourceTrialId='a',valid=False)]
        quality = trial_quality(rows)[0]
        self.assertAlmostEqual(quality['valid_seconds'], .02)
        self.assertAlmostEqual(quality['gap_seconds'], .18)
        self.assertIsNone(trial_quality([rows[0]])[0]['valid_time_fraction'])

    def test_trial_quality_splits_device_and_participant_identity(self):
        rows = [sample(1,participant='p1',sourceTrialId='a',deviceId=1),
                sample(1.05,participant='p1',sourceTrialId='a',deviceId=2),
                sample(2,participant='p2',sourceTrialId='a',deviceId=2)]
        first, second = trial_quality(rows)
        self.assertEqual(first['device_count'],2)
        self.assertIsNone(first['input_delivery_hz'])
        self.assertEqual(second['participant_id'],'p2')
        self.assertEqual(second['observed_search_window_seconds'],0)

    def test_spatial_bias_is_distinct_from_precision_and_missingness(self):
        def direction(yaw):
            return dict(x=math.sin(math.radians(yaw)),y=0,z=math.cos(math.radians(yaw)))
        rows=[sample(t,direction(yaw),validationId='v',targetId='a',validationSet='heldout',
                     validationTargetPresent=True,targetErrorDegrees=abs(yaw),mirroredOriginErrorDegrees=abs(yaw),
                     rawWorldOrigin=dict(x=0,y=0,z=0),validationTarget=dict(x=0,y=0,z=2),valid=valid)
              for t,yaw,valid in [(1,4,True),(1.01,6,True),(1.02,20,False),(1.03,9,True),(1.04,11,True)]]
        row=summarize(rows,settle=0)['spatial'][0]
        self.assertAlmostEqual(row['mean_direction_offset_deg'],7.5)
        self.assertAlmostEqual(row['mean_yaw_bias_deg'],7.5)
        self.assertAlmostEqual(row['mean_pitch_bias_deg'],0)
        self.assertAlmostEqual(row['angular_sd_deg'],math.sqrt(2))
        self.assertAlmostEqual(row['rms_s2s_deg'],2)
        self.assertAlmostEqual(row['invalid_update_fraction'],.2)
        self.assertEqual(row['precision_segments'],2)
        self.assertEqual(row['angular_sd_dof'],2)

    def test_invalid_heartbeat_breaks_spatial_precision(self):
        rows=[sample(t,validationId='v',targetId='a',validationSet='heldout',
                     validationTargetPresent=True,targetErrorDegrees=1,mirroredOriginErrorDegrees=1,
                     valid=valid,newInputUpdate=update)
              for t,valid,update in [(1,True,True),(1.01,False,False),(1.02,True,True)]]
        result=summarize(rows,settle=0)['spatial'][0]
        self.assertEqual(result['precision_pairs'],0)
        self.assertIsNone(result['angular_sd_deg'])
        self.assertEqual(result['recorded_updates'],2)
        self.assertAlmostEqual(result['valid_time_fraction'],.5)

    def test_cli_exports_trial_quality_with_missing_measurements_blank(self):
        with tempfile.TemporaryDirectory() as directory:
            source=Path(directory)/'gaze_observations.jsonl'
            rows=[sample(t,schema=1,kind='gaze_observation',recordingId='fixture',
                         sourceTrialId='p1_run001_r00',participant='p1',run=1)
                  for t in (1,1.05)]
            source.write_text(''.join(json.dumps(r)+'\n' for r in rows))
            output=Path(directory)/'report'
            result=subprocess.run([sys.executable,str(Path(__file__).with_name('eye_quality.py')),
                                   str(source),'--out',str(output)],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            with (output/'trial_eye_quality.csv').open(newline='') as f:
                exported=list(csv.DictReader(f))
            self.assertEqual(len(exported),1)
            self.assertEqual(exported[0]['trial_id'],'p1_run001_r00')
            self.assertEqual(exported[0]['calibration_id'],'')
            self.assertEqual(exported[0]['exclusion_reason'],'')
            report=json.loads((output/'quality_report.json').read_text())
            self.assertEqual(report['report_version'],'eye-quality-v3-source-aware-ext')
            self.assertEqual(report['trials'][0]['valid_time_fraction'],1)
            self.assertFalse(report['recording_end_status']['complete'])
            timing=json.loads((output/'timing_report.json').read_text())
            self.assertEqual(timing['sensor_rate_conclusion'],'not_established')
            self.assertTrue((output/'signal_capabilities.csv').exists())
            self.assertTrue((output/'delivery_timing.svg').exists())

    def test_polling_is_not_acquisition(self):
        rows=[sample(i/120,newInputUpdate=(i%2==0)) for i in range(121)]
        r=summarize(rows)
        self.assertEqual(r['sensor_rate_conclusion'],'not_established')
        self.assertAlmostEqual(r['input_delivery_hz'],60)
        self.assertEqual(r['poll_rows'],121)

    def test_equal_gaze_can_be_distinct_updates(self):
        r=summarize([sample(i/100) for i in range(101)])
        self.assertAlmostEqual(r['input_delivery_hz'],100)

    def test_gap_never_becomes_fixation_or_blink(self):
        rows=[sample(0),sample(.05),sample(.1),sample(.15,valid=False),sample(.8),sample(.85)]
        events=candidate_events(rows)
        self.assertFalse(any(e['start']<.15 and e['end']>.15 for e in events))
        self.assertFalse(any(e['kind']=='candidate_blink' for e in events))

    def test_blinks_need_independent_bilateral_validity(self):
        rows=[sample(t,opennessAvailable=True,leftOpennessValid=True,rightOpennessValid=True,
                     leftOpenness=op,rightOpenness=op) for t,op in [(0,1),(.05,0),(.1,0),(.15,1)]]
        self.assertEqual(len([e for e in candidate_events(rows) if e['kind']=='candidate_blink']),1)
        rows[2]['rightOpennessValid']=False
        self.assertEqual(len([e for e in candidate_events(rows) if e['kind']=='candidate_blink']),0)

    def test_target_precision_excludes_gaps(self):
        rows=[sample(t,validationId='v',targetId='a',validationSet='heldout',validationTargetPresent=True,
                     targetErrorDegrees=2,mirroredOriginErrorDegrees=4) for t in [0,.4,.41,.9]]
        r=summarize(rows,settle=0)
        self.assertEqual(r['spatial'][0]['precision_pairs'],1)
        self.assertEqual(r['spatial'][0]['median_error_deg'],2)

    def test_offset_evaluated_only_on_independent_targets(self):
        import math
        direction=dict(x=math.sin(math.radians(5)),y=0,z=math.cos(math.radians(5)))
        rows=[sample(t,direction,validationId=run,validationSet=kind,targetId='a',validationTargetPresent=True,
                     rawWorldOrigin=dict(x=0,y=0,z=0),validationTarget=dict(x=0,y=0,z=2))
              for t,run,kind in [(1,'fit1','fit'),(2,'held1','heldout')]]
        result=evaluate_offset(rows,'fit1','held1',settle=0)
        self.assertAlmostEqual(result['yaw_offset_deg'],-5)
        self.assertAlmostEqual(result['targets'][0]['corrected_median_deg'],0)
        self.assertFalse(result['applied_to_runtime'])
        with self.assertRaises(ValueError):evaluate_offset(rows,'fit1','fit1',settle=0)

    def test_recording_recovery_and_clean_close_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'gaze_observations.jsonl'
            row=sample(1,schema=1,kind='gaze_observation',recordingId='fixture')
            path.write_text(json.dumps(row)+'\n'+ '{"kind":')
            rows,warnings=read_recording(path)
            self.assertEqual(len(rows),1)
            self.assertTrue(warnings)
            (path.parent/'end_status.json').write_text(json.dumps(dict(complete=True,acceptedRows=1)))
            self.assertFalse(recording_status(path,rows,warnings)['complete'])
            path.write_text(json.dumps(row)+'\n')
            rows,warnings=read_recording(path)
            self.assertTrue(recording_status(path,rows,warnings)['complete'])
            (path.parent/'end_status.json').write_text(json.dumps(dict(complete=True,acceptedRows=2)))
            self.assertFalse(recording_status(path,rows,warnings)['complete'])

    def test_corrupt_middle_and_mixed_recordings_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'gaze_observations.jsonl'
            row=sample(1,schema=1,kind='gaze_observation',recordingId='fixture')
            path.write_text(json.dumps(row)+'\n{broken\n'+json.dumps(row)+'\n')
            with self.assertRaises(ValueError):read_recording(path)
            other=dict(row,sequence=1001,receiptTime=1.1,recordingId='other')
            path.write_text(json.dumps(row)+'\n'+json.dumps(other)+'\n')
            with self.assertRaises(ValueError):read_recording(path)
            path.write_text(json.dumps(row)+'\n'+json.dumps(row)+'\n')
            with self.assertRaises(ValueError):read_recording(path)

    def test_invalid_numeric_placeholders_are_blank_csv_measurements(self):
        row=sample(1,valid=False,poseValid=False,rawLocalOrigin=dict(x=0,y=0,z=0),
                   leftValid=False,leftWorldOrigin=dict(x=0,y=0,z=0),leftPupilValid=False,leftPupilMm=0)
        result=flat(row)
        self.assertIsNone(result['worldDirection_z'])
        self.assertIsNone(result['rawLocalOrigin_x'])
        self.assertIsNone(result['leftWorldOrigin_x'])
        self.assertIsNone(result['leftPupilMm'])

    def test_reference_matching_is_one_to_one(self):
        predicted=[dict(kind='candidate_fixation',start=0,end=.2),dict(kind='candidate_fixation',start=.01,end=.21)]
        reference=[dict(kind='fixation',start=0,end=.2)]
        result=compare_annotations(predicted,reference)
        self.assertEqual(result['true_positive'],1)
        self.assertEqual(result['false_positive'],1)

    def test_reference_matching_accepts_empty_unity_context_without_merging_ids(self):
        # Real trial data has an empty validation ID; validation runs have an
        # empty trial ID. CSV import represents those same missing IDs as None.
        for trial, validation in [('p20_run001_r00', ''), ('', 'validation-1')]:
            predicted=[dict(kind='candidate_fixation',start=1,end=1.2,
                            trial_id=trial,validation_id=validation)]
            reference=[dict(kind='fixation',start=1,end=1.2,
                            trial_id=trial or None,validation_id=validation or None)]
            self.assertEqual(compare_annotations(predicted,reference)['true_positive'],1)
            reference[0]['trial_id']='different-trial'
            self.assertEqual(compare_annotations(predicted,reference)['true_positive'],0)

    def test_device_change_breaks_eye_events_and_precision_pairs(self):
        rows=[sample(t,deviceId=device,validationId='v',targetId='a',validationSet='heldout',
                     validationTargetPresent=True,targetErrorDegrees=2,mirroredOriginErrorDegrees=4,
                     opennessAvailable=True,leftOpennessValid=True,rightOpennessValid=True,
                     leftOpenness=openness,rightOpenness=openness)
              for t,device,openness in [(1,1,1),(1.05,1,0),(1.1,2,1),(1.15,2,1)]]
        self.assertEqual(candidate_events(rows,min_fixation=.1),[])
        self.assertEqual(summarize(rows,settle=0)['spatial'][0]['precision_pairs'],2)

if __name__=='__main__': unittest.main()
