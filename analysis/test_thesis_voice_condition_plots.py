"""Edge-case fixtures test scoring only; they are never used to draw thesis figures."""
import unittest
from thesis_voice_condition_plots import derive, describe, CONDITIONS


class VoiceConditionPlotsTest(unittest.TestCase):
    def summary(self, **changes):
        value = dict(participant_id='TEST', run_number=1, voice_condition='neutral-male',
            voice_order='selfsimilar_then_neutral', objectives=[dict(trial_id='trial',
            search_started_at=10, completed=True, outcome='completed',
            search_exposure_seconds=12, wrong_captures=0)])
        value.update(changes)
        return value

    def test_actual_condition_not_counterbalanced_order(self):
        data = describe(derive(self.summary(), []))
        self.assertEqual(data[CONDITIONS[0]]['accuracy_percent'], 100)
        self.assertIsNone(data[CONDITIONS[1]]['accuracy_percent'])

    def test_unscheduled_and_practice_excluded(self):
        s = self.summary(); row = s['objectives'][0]
        s['objectives'] += [dict(row, trial_id='not_started', search_started_at=-1),
                            dict(row, trial_id='practice', practice=True)]
        self.assertEqual(len(derive(s, [])), 1)

    def test_missing_deadline_not_zero_misses(self):
        self.assertIsNone(derive(self.summary(), [])[0]['missed_target'])

    def test_timeout_not_success_time_and_unresolved_not_miss(self):
        s = self.summary(active_search_limit_seconds=25)
        row = s['objectives'][0]
        s['objectives'] += [dict(row, trial_id='timeout', outcome='timeout', completed=False),
                            dict(row, trial_id='stopped', outcome='technical_stop', completed=False)]
        d = describe(derive(s, []))[CONDITIONS[0]]
        self.assertEqual(d['missed_percent'], 50)
        self.assertEqual(d['completion_seconds'], [12])
        self.assertEqual(d['deadline_resolved_n'], 2)

    def test_two_eyes_one_point_missing_not_zero(self):
        eye = dict(trial_id='trial', eye='left', voice='neutral-male', participant='TEST',
                   run='1', practice='False', ipa_candidate_hz='.4', pupil_eligible_seconds='2')
        rows = derive(self.summary(), [eye, dict(eye, eye='right', ipa_candidate_hz='.8')])
        self.assertAlmostEqual(rows[0]['ipa_candidate_hz'], .6)
        self.assertEqual(rows[0]['ipa_eyes'], 2)
        self.assertIsNone(derive(self.summary(), [dict(eye, ipa_candidate_hz='')])[0]['ipa_candidate_hz'])
        with self.assertRaises(ValueError):
            derive(self.summary(), [eye, eye])
        with self.assertRaises(ValueError):
            derive(self.summary(), [dict(eye, voice='selfsimilar')])

    def test_incorrect_selection_retained(self):
        s = self.summary(); s['objectives'][0]['wrong_captures'] = 2
        self.assertEqual(describe(derive(s, []))[CONDITIONS[0]]['accuracy_percent'], 0)


if __name__ == '__main__':
    unittest.main()
