import copy
import csv
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from recording_links import audit, CONTRACT


class RecordingLinkTests(unittest.TestCase):
    def fixture(self, root):
        run = root/'run'; run.mkdir()
        eye_dir = root/'eye'; eye_dir.mkdir()
        eye_file, replay_file = eye_dir/'gaze_observations.jsonl', root/'recording.jsonl'
        tid = 'P1_run001_r00'
        summary = dict(participant_id='P1', run_number=1, objectives=[dict(trial_id=tid,index=0,block=0,
                       voice_condition='neutral-male', completed=True, search_started_at=10)])
        (run/'trial_summary.json').write_text(json.dumps(summary))
        with (run/'object_manifest.csv').open('w',newline='') as file:
            writer=csv.DictWriter(file,fieldnames=['trial_id','object_id','shape','color','is_target'])
            writer.writeheader();writer.writerow(dict(trial_id=tid,object_id='target',shape='Cube',color='Red',is_target='1'))
        eye = dict(kind='gaze_observation',schema=1,recordingId='eye-a',sequence=7,receiptTime=100,
                   inputEventTime=99.99,participant='P1',run=1,sourceTrialId=tid,phase='search',
                   source='EXT',valid=True,round=0,block=0,voice='neutral-male',worldOrigin=dict(x=11,y=2,z=3),worldDirection=dict(x=1,y=0,z=0))
        # A 90-degree Y origin rotation converts local forward into world right.
        header=dict(kind='header',schema=2,recordingId='replay-a',sequence=0,time=0,gazeLinkVersion=CONTRACT,
                    origin=dict(x=10,y=2,z=3),originRotation=dict(x=0,y=2**-.5,z=0,w=2**-.5))
        context=dict(schema=2,participantCode='P1',runNumber=1,sourceTrialId=tid,round=0,block=0,
                     voice='neutral-male',practice=False)
        trial=dict(context,kind='trial',sequence=1,time=1,targetId='target',objects=[dict(id='target',shape='Cube',color='Red',target=True)])
        sample=dict(context,kind='sample',sequence=2,time=2,gazeRecordingId='eye-a',gazeObservationSequence=7,
                    gazeReceiptTime=100,gazeInputEventTime=99.99,gazeSource='EXT',gazeTracked=1,
                    gazeOrigin=dict(x=0,y=0,z=1),gazeDirection=dict(x=0,y=0,z=1),searchActive=True)
        selection=dict(sample,kind='selection',sequence=3,time=3,objectId='target',correct=True)
        end=dict(context,kind='end',sequence=4,time=4,complete=True)
        rows=[header,trial,sample,selection,end]
        self.write(eye_file,[eye]);self.write(replay_file,rows)
        (eye_dir/'end_status.json').write_text(json.dumps(dict(complete=True,acceptedRows=1)))
        return run,eye_file,replay_file,eye,rows

    def write(self,path,rows):
        path.write_text(''.join(json.dumps(r)+'\n' for r in rows))

    def test_exact_identity_and_coordinate_transform_with_input_fingerprints(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,_,_=self.fixture(Path(directory))
            report=audit(run,[eye],[replay])
            self.assertEqual(report['status'],'consistent',report)
            self.assertEqual(report['trials'][0]['linked_samples'],2)
            self.assertEqual(report['trials'][0]['raw_search_rows'],1)
            self.assertTrue(all(len(source['sha256'])==64 for source in report['sources']))

    def test_eye_restart_requires_sample_specific_recording_id(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);run,eye,replay,row,rows=self.fixture(root)
            second=root/'eye2';second.mkdir();eye2=second/'gaze_observations.jsonl'
            row2=dict(row,recordingId='eye-b') # Repeated sequence is valid in a different recording.
            self.write(eye2,[row2]);(second/'end_status.json').write_text(json.dumps(dict(complete=True,acceptedRows=1)))
            rows[3]['gazeRecordingId']='eye-b';self.write(replay,rows)
            self.assertEqual(audit(run,[eye,eye2],[replay])['status'],'consistent')
            report=audit(run,[eye],[replay])
            self.assertIn('missing_exact_raw_observation',report['issue_counts'])
            rows[3]['gazeRecordingId']='unrelated';self.write(replay,rows)
            self.assertIn('missing_exact_raw_observation',audit(run,[eye,eye2],[replay])['issue_counts'])

    def test_wrong_context_timestamp_validity_and_coordinates_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,row,rows=self.fixture(Path(directory))
            for field,value,code in [('runNumber',2,'replay_trial_context_mismatch'),
                                      ('voice','selfsimilar','replay_trial_context_mismatch'),
                                      ('gazeReceiptTime',101,'raw_timestamp_mismatch'),
                                      ('gazeSource','head_fallback','raw_source_mismatch'),
                                      ('gazeOrigin',dict(x=1,y=0,z=0),'gaze_origin_transform_mismatch'),
                                      ('gazeDirection',dict(x=1,y=0,z=0),'gaze_direction_transform_mismatch')]:
                changed=copy.deepcopy(rows);changed[2][field]=value;self.write(replay,changed)
                self.assertIn(code,audit(run,[eye],[replay])['issue_counts'])
            self.write(replay,rows);row['valid']=False;self.write(eye,[row])
            self.assertIn('tracked_replay_from_invalid_raw',audit(run,[eye],[replay])['issue_counts'])

    def test_manifest_selection_and_duplicate_replays_are_checked(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);run,eye,replay,_,rows=self.fixture(root)
            changed=copy.deepcopy(rows);changed[1]['objects'][0]['color']='Blue';self.write(replay,changed)
            self.assertIn('replay_manifest_object_metadata_mismatch',audit(run,[eye],[replay])['issue_counts'])
            self.write(replay,rows);other=root/'second.jsonl';changed=copy.deepcopy(rows);changed[0]['recordingId']='replay-b';self.write(other,changed)
            self.assertIn('trial_in_multiple_replays',audit(run,[eye],[replay,other])['issue_counts'])
            changed=copy.deepcopy(rows);changed[3]['objectId']='other';self.write(replay,changed)
            self.assertIn('correct_selection_is_not_manifest_target',audit(run,[eye],[replay])['issue_counts'])

    def test_legacy_incomplete_and_unstarted_trials_cannot_imply_complete_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,_,rows=self.fixture(Path(directory))
            rows[0].pop('gazeLinkVersion');self.write(replay,rows)
            report=audit(run,[eye],[replay]);self.assertEqual(report['status'],'evidence_incomplete')
            (eye.parent/'end_status.json').unlink()
            rows[-1]['complete']=False;self.write(replay,rows)
            codes={i['code'] for i in audit(run,[eye],[replay])['incomplete']}
            self.assertIn('raw_recording_not_cleanly_closed',codes);self.assertIn('replay_not_cleanly_completed',codes)
            self.assertEqual(audit(run,[],[])['status'],'evidence_incomplete')
            summary=json.loads((run/'trial_summary.json').read_text())
            summary['objectives'][0].update(completed=False,search_started_at=-1)
            (run/'trial_summary.json').write_text(json.dumps(summary))
            self.assertEqual(audit(run,[],[])['status'],'evidence_incomplete')

    def test_invalid_observation_can_be_linked_without_fabricated_pose(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,row,rows=self.fixture(Path(directory))
            row['valid']=False;self.write(eye,[row])
            for r in rows[2:4]: r.update(gazeTracked=0,gazeOrigin=dict(x=0,y=0,z=0),gazeDirection=dict(x=0,y=0,z=0))
            self.write(replay,rows)
            self.assertEqual(audit(run,[eye],[replay])['status'],'consistent')
            # No observation is allowed only when not claiming tracked gaze.
            rows[2]['gazeObservationSequence']=-1;self.write(replay,rows)
            self.assertEqual(audit(run,[eye],[replay])['trials'][0]['unavailable_samples'],1)
            rows[2]['gazeTracked']=1;self.write(replay,rows)
            self.assertIn('tracked_gaze_without_observation',audit(run,[eye],[replay])['issue_counts'])

    def test_duplicate_inputs_and_corrupt_middle_rows_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,row,rows=self.fixture(Path(directory))
            with self.assertRaises(ValueError):audit(run,[eye,eye],[replay])
            eye.write_text(json.dumps(row)+'\n{bad}\n'+json.dumps(dict(row,sequence=8))+'\n')
            with self.assertRaises(ValueError):audit(run,[eye],[replay])

    def test_active_cross_trial_links_fail_but_transition_lookback_is_explicit(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,row,rows=self.fixture(Path(directory))
            row['sourceTrialId']='previous_trial';self.write(eye,[row])
            self.assertIn('active_sample_cross_trial_observation',audit(run,[eye],[replay])['issue_counts'])
            rows[2]['searchActive']=False
            # Selection still requires same-trial provenance, even if its flag is false.
            rows[3]['searchActive']=False;self.write(replay,rows)
            report=audit(run,[eye],[replay])
            self.assertEqual(report['issue_counts']['active_sample_cross_trial_observation'],1)
            self.assertTrue(any('transition observation' in note for note in report['notes']))

    def test_clean_receipt_requires_explicit_row_count_and_legacy_cli_is_incomplete(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);run,eye,replay,_,rows=self.fixture(root)
            (eye.parent/'end_status.json').write_text('{"complete":true}')
            self.assertEqual(audit(run,[eye],[replay])['status'],'evidence_incomplete')
            rows[0].pop('gazeLinkVersion');self.write(replay,rows)
            result=subprocess.run([sys.executable,str(Path(__file__).with_name('recording_links.py')),str(run),
                                   '--eye',str(eye),'--replay',str(replay),'--out',str(root/'legacy-report')],
                                  capture_output=True,text=True)
            self.assertEqual(result.returncode,2,result.stderr)
            self.assertEqual(result.stdout.strip(),'evidence_incomplete')

    def test_cli_outputs_and_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);run,eye,replay,_,rows=self.fixture(root);out=root/'report'
            command=[sys.executable,str(Path(__file__).with_name('recording_links.py')),str(run),'--eye',str(eye),'--replay',str(replay),'--out',str(out)]
            result=subprocess.run(command,capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            self.assertEqual(json.loads((out/'recording_links.json').read_text())['status'],'consistent')
            self.assertTrue((out/'trials.csv').exists())
            before=(out/'recording_links.json').read_bytes()
            self.assertNotEqual(subprocess.run(command,capture_output=True).returncode,0)
            self.assertEqual((out/'recording_links.json').read_bytes(),before)
            rows[2]['gazeReceiptTime']=999;self.write(replay,rows);command[-1]=str(root/'bad-report')
            self.assertEqual(subprocess.run(command,capture_output=True).returncode,1)

    def test_malformed_nested_records_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,raw,rows=self.fixture(Path(directory))
            for value in ([], None, 7):
                self.write(eye,[value])
                with self.assertRaises(ValueError): audit(run,[eye],[replay])
            self.write(eye,[raw])
            (eye.parent/'end_status.json').write_text('[]')
            with self.assertRaises(ValueError): audit(run,[eye],[replay])
            (eye.parent/'end_status.json').write_text('{"complete":true,"acceptedRows":1}')
            rows[1]['objects']=[None];self.write(replay,rows)
            with self.assertRaises(ValueError): audit(run,[eye],[replay])

    def test_header_and_unavailable_tracking_flags_are_typed(self):
        with tempfile.TemporaryDirectory() as directory:
            run,eye,replay,_,rows=self.fixture(Path(directory))
            for field,value in [('sequence',0.5),('time',False)]:
                changed=copy.deepcopy(rows);changed[0][field]=value;self.write(replay,changed)
                with self.assertRaises(ValueError): audit(run,[eye],[replay])
            for value in [None,False,2,'0']:
                changed=copy.deepcopy(rows)
                changed[2].update(gazeObservationSequence=-1,gazeTracked=value)
                self.write(replay,changed)
                self.assertIn('invalid_replay_tracking_flag',audit(run,[eye],[replay])['issue_counts'])
