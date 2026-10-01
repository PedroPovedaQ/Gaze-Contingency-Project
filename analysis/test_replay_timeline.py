import copy
import csv
import json
import subprocess
import sys
from pathlib import Path
import tempfile
import unittest

from replay_timeline import prepare, fingerprint


def fixture():
    def row(kind, time, **extra):
        return dict(schema=2, kind=kind, time=time, participantCode='SYNTHETIC',
                    trialId='trial_001', sourceTrialId='SYNTHETIC_run001_r00', runNumber=1,
                    round=0, block=0, voice='neutral', practice=False, **extra)
    v = dict(x=0, y=1, z=2)
    head = dict(headAvailable=True, headTracked=1, headPosition=v,
                gazeAvailable=True, gazeTracked=1, gazeOrigin=v, gazeDirection=dict(x=0,y=0,z=1),
                gazeRecordingId='raw-a', gazeObservationSequence=5, gazeDeliveryAge=.01,
                rightController=dict(available=True, tracked=1, rayAvailable=True,
                                     triggerHeld=0, rayOrigin=v, rayEnd=dict(x=0,y=1,z=4)))
    rows = [dict(schema=2, kind='header', time=0, recordingId='replay-a', sourceType='synthetic_demo', utc='2026-10-01T00:00:00Z'),
            row('transition', 1),
            row('trial', 1.1, targetId='target', absoluteTheta=45,
                objects=[dict(id='target',color='Blue',shape='Cube',position=dict(x=0,y=1,z=4))]),
            row('sample', 1.2, frame=10, searchActive=False, visible=False, searchSeconds=0, **head),
            row('search_start', 2),
            row('sample', 2.01, frame=11, searchActive=True, visible=True, searchSeconds=.01, **head),
            row('sample', 2.02, frame=12, searchActive=True, visible=True, searchSeconds=.02,
                changes=[dict(id='target', position=dict(x=1,y=2,z=4))], **head),
            row('search_paused', 2.05), row('search_resumed',3),
            row('selection', 3.01, frame=20, objectId='target', correct=True, searchActive=True,
                visible=True, searchSeconds=.06, **head),
            row('end', 3.5, complete=True)]
    for i, r in enumerate(rows):
        r['sequence'] = i
    return rows


class ReplayTimelineTests(unittest.TestCase):
    def write(self, root, rows):
        path = root/'recording.jsonl'
        path.write_text(''.join(json.dumps(r)+'\n' for r in rows))
        return path

    def test_preserves_exact_samples_changes_clocks_pauses_and_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);rows=fixture();path=self.write(root,rows);before=path.read_bytes()
            result=prepare(path);t=result['trials'][0]
            self.assertEqual(result['source']['sha256'],fingerprint(path))
            self.assertEqual(t['sourceId'],'SYNTHETIC_run001_r00')
            self.assertEqual(t['searchSegments'],[[2,2.05],[3,3.01]])
            self.assertEqual(t['duration'],.06)
            self.assertEqual(t['samples'][1][15],rows[5])
            self.assertEqual(t['samples'][1][16],6)
            self.assertEqual(t['objectChanges'][0]['changes'],rows[6]['changes'])
            self.assertFalse(t['stateChanges'][-1]['visible'])
            self.assertIn([2.12,3.01],t['gaps'])
            self.assertEqual(path.read_bytes(),before)

    def test_invalid_tracking_is_not_fabricated_as_valid_zero(self):
        with tempfile.TemporaryDirectory() as directory:
            rows=fixture();rows[5].update(headTracked=0,gazeTracked=0,gazeOrigin=None)
            result=prepare(self.write(Path(directory),rows));s=result['trials'][0]['samples'][1]
            self.assertFalse(s[13]);self.assertFalse(s[14]);self.assertIsNone(s[5])
            self.assertEqual(s[15]['gazeObservationSequence'],5)

    def test_interrupted_tail_and_unstarted_trial_are_explicit(self):
        with tempfile.TemporaryDirectory() as directory:
            path=self.write(Path(directory),fixture()[:4]);path.write_text(path.read_text()+'{"kind":')
            result=prepare(path)
            self.assertFalse(result['complete']);self.assertTrue(result['warnings'])
            self.assertIsNone(result['trials'][0]['searchStart'])
            self.assertIsNone(result['trials'][0]['completeTime'])

    def test_rejects_context_identity_and_nonfinite_corruption(self):
        with tempfile.TemporaryDirectory() as directory:
            for field,value in [('time',True),('sequence',5.5),('participantCode','other'),('voice','other'),('gazeDeliveryAge',float('nan'))]:
                rows=fixture();rows[5][field]=value
                with self.subTest(field=field), self.assertRaises(ValueError):
                    prepare(self.write(Path(directory),rows))
            rows=fixture();rows[2]['objects'][0]['position']=None
            with self.assertRaises(ValueError):prepare(self.write(Path(directory),rows))

    def test_summary_requires_exact_replay_hash_and_recording_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);path=self.write(root,fixture());package=root/'export';package.mkdir()
            manifest=package/'manifest.json'
            manifest.write_text(json.dumps(dict(schema='trial-performance-subjective-v1', inputs=[dict(sha256=fingerprint(path))])))
            summary=dict(trial_id='SYNTHETIC_run001_r00',participant_id='SYNTHETIC',
                         replay_recording_id='replay-a',survey_scope='block',survey_value='42')
            with (package/'trials.csv').open('w',newline='') as handle:
                writer=csv.DictWriter(handle,fieldnames=list(summary));writer.writeheader();writer.writerow(summary)
            result=prepare(path,package)
            self.assertEqual(result['trials'][0]['summary'],summary)
            self.assertIn('not independent',result['summaryProvenance']['scope'])
            manifest.write_text('{"schema":"trial-performance-subjective-v1","inputs":[]}')
            with self.assertRaisesRegex(ValueError,'exact replay'):prepare(path,package)

    def test_practice_and_measured_trial_ids_are_not_collapsed(self):
        with tempfile.TemporaryDirectory() as directory:
            rows=fixture();other=copy.deepcopy(rows[1:])
            for r in rows[1:]:r.update(practice=True,trialId='practice_001',sourceTrialId='',runNumber=0)
            rows=rows[:-1]
            for r in other:r['time']+=4;r['sequence']+=20
            rows.extend(other)
            result=prepare(self.write(Path(directory),rows))
            self.assertEqual(result['trials'][0]['sourceId'],'replay-a:practice_001')
            self.assertTrue(result['trials'][0]['practice'])
            self.assertFalse(result['trials'][1]['practice'])

    def test_eighty_trials_keep_boundaries_and_distinct_context(self):
        with tempfile.TemporaryDirectory() as directory:
            rows=[fixture()[0]]
            for trial in range(80):
                for r in copy.deepcopy(fixture()[1:-1]):
                    r.update(trialId=f'trial_{trial+1:03d}',sourceTrialId=f'SYNTHETIC_run001_r{trial:02d}',
                             round=trial,block=trial//40,voice='neutral' if trial<40 else 'selfsimilar')
                    r['time']+=trial*4; r['sequence']=len(rows);rows.append(r)
            rows.append(dict(schema=2,kind='end',time=320,sequence=len(rows),complete=True))
            result=prepare(self.write(Path(directory),rows))
            self.assertEqual(len(result['trials']),80)
            self.assertEqual(result['trials'][39]['samples'][0][15]['voice'],'neutral')
            self.assertEqual(result['trials'][40]['samples'][0][15]['voice'],'selfsimilar')
            self.assertEqual(result['trials'][39]['end'],result['trials'][40]['start'])

    def test_one_shot_audio_does_not_cut_off_or_extend_spoken_cue(self):
        with tempfile.TemporaryDirectory() as directory:
            rows=fixture()
            base=rows[5]
            rows.extend([dict(base,kind='audio_playback_start',time=2.021,text='Keep looking'),
                         dict(base,kind='audio_playback_start',time=2.022,text='Chime',detail='success_effect;software_one_shot_start;duration=0.01'),
                         dict(base,kind='audio_playback_end',time=2.04)])
            rows.sort(key=lambda r:r['time'])
            for i,r in enumerate(rows):r['sequence']=i
            speech=prepare(self.write(Path(directory),rows))['trials'][0]['speech']
            self.assertEqual(speech[0]['end'],2.04)
            self.assertAlmostEqual(speech[1]['end'],2.032)

    def test_cli_creates_portable_page_and_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=self.write(root,fixture());output=root/'timeline.json';html=root/'timeline.html'
            command=[sys.executable,str(Path(__file__).with_name('replay_timeline.py')),str(source),
                     '--output',str(output),'--html',str(html)]
            result=subprocess.run(command,capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            self.assertIn('replay-timeline-v1',html.read_text())
            before=(source.read_bytes(),output.read_bytes(),html.read_bytes())
            self.assertNotEqual(subprocess.run(command,capture_output=True).returncode,0)
            self.assertEqual(before,(source.read_bytes(),output.read_bytes(),html.read_bytes()))


if __name__=='__main__':unittest.main()
