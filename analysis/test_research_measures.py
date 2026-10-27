"""Invented fixtures exercise reporting rules, not scientific validity."""
import unittest
import json
import subprocess
import sys
from pathlib import Path
import tempfile

from research_measures import build_trials, score_surveys
from test_eye_outcomes import sample


def summary():
    return dict(participant_id='fixture', run_number=1, session_mode='study',
        session_outcome='completed', objectives=[dict(trial_id='t1', block=0,
            voice_condition='neutral-male', search_started_at=1.,
            search_exposure_seconds=10., completed=True, outcome='completed')])


def metric(eye, count, seconds, **kwargs):
    return dict(recording_id='raw1', participant='fixture', run=1, trial_id='t1',
        eye=eye, voice='neutral-male', practice=False, fixation_count=count,
        fixation_eligible_seconds=seconds, saccade_count=count,
        saccade_eligible_seconds=seconds, **kwargs)


class ResearchMeasureTests(unittest.TestCase):
    def test_two_eyes_are_one_descriptive_trial_not_two_participants(self):
        rows = build_trials([summary()], [metric('left', 4, 4), metric('right', 8, 8)])
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]['candidate_fixation_count_mean_per_eye'], 6)
        self.assertEqual(rows[0]['candidate_saccade_hz_mean_per_eye'], 1)
        self.assertEqual(rows[0]['fixation_coverage_min_eye'], .4)
        self.assertIsNone(rows[0]['last_target_fixation_s'])
        self.assertFalse(rows[0]['eye_research_eligible'])

    def test_missing_eye_not_zero_and_observed_zero_is_retained(self):
        row = build_trials([summary()], [metric('left', 0, 8)])[0]
        self.assertIsNone(row['candidate_fixation_count_mean_per_eye'])
        row = build_trials([summary()], [metric('left', 0, 8), metric('right', 0, 8)])[0]
        self.assertEqual(row['candidate_fixation_count_mean_per_eye'], 0)

    def test_duplicates_and_identity_mismatches_are_not_pooled(self):
        for changed in (dict(voice='selfsimilar'), dict(participant='other'), dict(practice=True)):
            m = metric('left', 1, 1); m.update(changed)
            with self.assertRaises(ValueError): build_trials([summary()], [m])
        m = metric('left', 1, 1)
        with self.assertRaises(ValueError): build_trials([summary()], [m, m])
        with self.assertRaises(ValueError): build_trials([summary(), summary()], [])

    def test_incomplete_and_practice_do_not_become_plotted_trials(self):
        s = summary(); s['objectives'][0].update(completed=False, outcome='abandoned')
        self.assertFalse(build_trials([s], [metric('left', 1, 1)])[0]['plot_eligible'])
        s['objectives'][0]['is_practice'] = True
        self.assertEqual(build_trials([s], []), [])

    def test_survey_requires_six_dimensions_and_unique_block_identity(self):
        trials = build_trials([summary()], [])
        response = dict(participant_id='fixture', run_number=1, block=0,
            voice_condition='neutral-male', response_id='r', scale_min=0, scale_max=10,
            performance_direction='high_is_good', mental=1, physical=2, temporal=3,
            performance=8, effort=5, frustration=6)
        scores = score_surveys([response, dict(response)], trials)
        self.assertEqual(len(scores), 1)
        self.assertAlmostEqual(scores[0]['nasa_tlx_score_0_100'], 190/6)
        with self.assertRaises(ValueError): score_surveys([response, dict(response, mental=2)], trials)
        bad = dict(response); del bad['performance_direction']
        self.assertIsNone(score_surveys([bad], trials)[0]['nasa_tlx_score_0_100'])
        bad = dict(response, block=1)
        self.assertEqual(score_surveys([bad], trials)[0]['join_status'], 'no_matching_block')
        self.assertFalse(score_surveys([bad], trials)[0]['plot_eligible'])

    def test_cli_records_incomplete_sources_and_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root/'gaze_observations.jsonl'
            rows = [sample(1+i*.01, sourceTrialId='t1', voice='neutral-male',
                schema=1, kind='gaze_observation', sequence=i) for i in range(120)]
            source.write_text(''.join(json.dumps(r)+'\n' for r in rows))
            summary_path = root/'trial_summary.json'
            summary_path.write_text(json.dumps(summary()))
            before = source.read_bytes()
            command = [sys.executable, str(Path(__file__).with_name('research_measures.py')),
                '--summary', str(summary_path), '--eye', str(source), '--out', str(root/'out')]
            subprocess.run(command, check=True, capture_output=True)
            report = json.loads((root/'out/measurement-status.json').read_text())
            self.assertFalse(report['eye_research_ready'])
            self.assertFalse(report['recordings'][0]['integrity']['complete'])
            self.assertEqual(report['tlx_scored_blocks'], 0)
            self.assertEqual(source.read_bytes(), before)
            self.assertGreater((root/'out/research-measures.pdf').stat().st_size, 1000)
            self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)


if __name__ == '__main__': unittest.main()
