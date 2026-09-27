import csv
import json
import tempfile
import unittest
from pathlib import Path

from mega_csv import export


class MegaCSVTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.run = self.root / 'GazeData' / 'P001' / 'run_001_test'
        self.run.mkdir(parents=True)
        self.tid = 'P001_run001_r00'
        (self.run / 'trial_summary.json').write_text(json.dumps({
            'participant_id': 'P001', 'run_number': 1, 'rounds_per_block': 10,
            'neutral_voice_name': 'Sarah', 'session_outcome': 'completed',
            'objectives': [dict(index=0, trial_id=self.tid, block=0, time_to_find_seconds=4.5)]}))
        self.write_csv('gaze_log.csv', [dict(participant_id='P001', run_number=1, trial_id=self.tid,
                                           frame=50, timestamp=123.45, left_eye_open='', is_practice=0)])
        self.write_csv('nasa_tlx.csv', [dict(participant_id='P001', run_number=1, block=0, mental=35)])
        self.write_csv('trial_events.csv', [dict(trial_id=self.tid, detail='He said, "look left"\nThen paused.', timestamp=123.45)])
        self.write_csv('object_manifest.csv', [dict(trial_id=self.tid, object_id='o1', x=3, is_target=1)])
        self.package = self.root / 'GazeReplays' / 'recording'
        self.package.mkdir(parents=True)
        self.records = [
            dict(kind='header', recordingId='fixture', origin={'x': 1, 'y': 2, 'z': 3}),
            dict(kind='trial', objects=[dict(id='o1', position={'x': 2}, target=True)],
                 meshes=[dict(id='m1', vertices=[dict(x=0,y=0,z=0)], triangles=[0,0,0])]),
            dict(kind='sample', frame=50, headPosition={'x': 2}, leftController={'triggerHeld': 1}, time=2.1),
            dict(kind='selection', objectId='o1', correct=True, frame=50, time=2.1),
            dict(kind='end', complete=True, time=3),
        ]
        self.save_replay()

    def tearDown(self):
        self.temp.cleanup()

    def write_csv(self, name, rows):
        with (self.run / name).open('w', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)

    def save_replay(self, tail=''):
        with (self.package / 'recording.jsonl').open('w') as f:
            for sequence, source in enumerate(self.records):
                row = dict(schema=2, sequence=sequence, time=sequence / 10,
                           participantCode='P001', runNumber=1, sourceRunFolder=self.run.name,
                           sourceTrialId=self.tid, practice=False)
                row.update(source)
                f.write(json.dumps(row) + '\n')
            f.write(tail)

    def read_export(self, name='mega.csv'):
        output = self.root / name
        report = export(self.run, output, self.root / 'GazeReplays')
        with output.open(newline='') as f: rows = list(csv.DictReader(f))
        return rows, report

    def test_complete_union_exact_join_and_repeated_context(self):
        rows, report = self.read_export()
        samples = [r for r in rows if r['row_type'] == 'sample']
        self.assertEqual(len(samples), 1)
        row = samples[0]
        self.assertEqual(row['gaze__timestamp'], '123.45')
        self.assertEqual(row['replay__time'], '2.1')  # no fabricated clock conversion
        self.assertEqual(row['gaze__left_eye_open'], '')
        self.assertEqual(row['replay__leftController__triggerHeld'], '1')
        self.assertEqual(row['trial__time_to_find_seconds'], '4.5')
        self.assertEqual(row['survey__nasa_tlx__block__mental'], '35')
        self.assertEqual(row['target__x'], '3')
        self.assertEqual(report['rows']['replay_selection'], 1)
        self.assertEqual(report['rows']['mesh_triangles'], 3)
        self.assertEqual(report['rows']['object'], 1)
        self.assertEqual(report['rows']['replay_object'], 1)
        self.assertEqual(next(r for r in rows if r['row_type']=='event')['event__detail'],
                         'He said, "look left"\nThen paused.')
        with self.assertRaises(FileExistsError): self.read_export()

    def test_frame_mismatch_keeps_both_sources(self):
        self.records[2]['frame'] = 51; self.save_replay()
        _, report = self.read_export()
        self.assertEqual(report['rows']['gaze_sample'], 1)
        self.assertEqual(report['rows']['replay_sample'], 1)
        self.assertNotIn('sample', report['rows'])

    def test_wrong_run_replay_not_joined(self):
        for record in self.records: record['sourceRunFolder'] = 'run_999'
        self.save_replay()
        _, report = self.read_export()
        self.assertNotIn('replay_sample', report['rows'])
        self.assertTrue(any('No matching replay' in w for w in report['warnings']))

    def test_duplicate_gaze_frame_is_not_arbitrarily_chosen(self):
        with (self.run/'gaze_log.csv').open() as f: lines=f.readlines()
        with (self.run/'gaze_log.csv').open('a') as f: f.write(lines[-1])
        _, report = self.read_export()
        self.assertEqual(report['rows']['gaze_sample'], 2)
        self.assertNotIn('sample', report['rows'])

    def test_practice_has_no_measured_summary_or_survey(self):
        self.records[1].update(practice=True, sourceTrialId='', trialId='practice_001')
        self.save_replay()
        rows, _ = self.read_export()
        trial = next(r for r in rows if r['row_type']=='replay_trial')
        self.assertEqual(trial['is_practice'], '1')
        self.assertEqual(trial['trial__time_to_find_seconds'], '')
        self.assertEqual(trial['survey__nasa_tlx__block__mental'], '')

    def test_duplicate_survey_preserved_without_broadcast(self):
        self.write_csv('nasa_tlx.csv', [dict(participant_id='P001', run_number=1, block=0, mental=v) for v in [30,40]])
        rows, report = self.read_export()
        self.assertEqual(report['rows']['survey'], 2)
        self.assertNotIn('survey__nasa_tlx__block__mental', rows[0])
        self.assertTrue(any('Multiple nasa_tlx' in w for w in report['warnings']))

    def test_truncated_tail_recoverable_but_interior_corruption_rejected(self):
        self.records.pop(); self.save_replay('{"kind":')
        _, report = self.read_export()
        self.assertTrue(any('Truncated final' in w for w in report['warnings']))
        (self.package/'recording.jsonl').write_text('{oops}\n{}\n')
        with self.assertRaises(ValueError): self.read_export('bad.csv')
        self.assertFalse((self.root/'bad.csv').exists())

    def test_external_trial_survey_does_not_leak_to_other_trials(self):
        path = self.root / 'similarity.csv'
        path.write_text('participant_id,run_number,trial_id,rating\nP001,1,P001_run001_r00,6\nP001,2,P001_run002_r00,2\n')
        output = self.root / 'external.csv'
        export(self.run, output, self.root/'GazeReplays', [path])
        with output.open(newline='') as f: rows=list(csv.DictReader(f))
        self.assertEqual(next(r for r in rows if r['row_type']=='sample')['survey__similarity__trial__rating'], '6')
        self.assertEqual(next(r for r in rows if r['row_type']=='session')['survey__similarity__trial__rating'], '')
        self.assertEqual(len([r for r in rows if r['row_type']=='survey' and r['source_file']==str(path.resolve())]), 1)

    def test_voice_manifest_and_unknown_fields_are_preserved(self):
        (self.run/'voice-library-manifest.json').write_text(json.dumps(dict(matched_rms=.1,
            clips=[dict(clip_id='voice1', voice_id='actual_provider_id', effect_profile='dry', gain=.8)])))
        self.records[2]['futureField'] = {'confidence': 0}
        self.save_replay()
        rows, report = self.read_export()
        self.assertEqual(report['rows']['voice_clip'], 1)
        self.assertEqual(next(r for r in rows if r['row_type']=='sample')['replay__futureField__confidence'], '0')
        self.assertEqual(next(r for r in rows if r['row_type']=='voice_clip')['voice_clip__voice_id'], 'actual_provider_id')

    def test_export_without_replay_preserves_csv_data(self):
        output = self.root/'csv-only.csv'
        report = export(self.run, output)
        self.assertEqual(report['rows']['gaze_sample'], 1)
        self.assertEqual(report['rows']['survey'], 1)
        self.assertTrue(report['warnings'])


if __name__ == '__main__': unittest.main()
