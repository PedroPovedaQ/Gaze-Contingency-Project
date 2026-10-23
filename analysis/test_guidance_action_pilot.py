import copy
import unittest
import tempfile
import json
from pathlib import Path
from guidance_action_pilot import scripted_cases, options, payload, parse, summarize, holdout_cases, recorded_cases

class ActionPilotTests(unittest.TestCase):
    def test_all_scripted_expectations_are_eligible(self):
        for case in scripted_cases():
            self.assertTrue(set(case['expected_actions']) <= set(options(case['state'])),case['id'])
    def test_no_confirmation_or_completion_claim(self):
        for case in scripted_cases():
            self.assertTrue(set(options(case['state'])) <= {'wait','orient','refine','correct','encourage'})
    def test_progress_requires_measured_history(self):
        s=scripted_cases()[1]['state']; self.assertIn('encourage',options(s))
        s['pilot_history']['measured_progress']=None;self.assertNotIn('encourage',options(s))
    def test_controller_blocks_before_hold(self):
        s=scripted_cases()[0]['state'];s['runtime']['evidence']['controllerOnTarget']=True
        self.assertEqual(list(options(s)),['wait'])
    def test_late_state_revalidation(self):
        s=scripted_cases()[0]['state'];menu=options(s)
        answer={'answers':{'guidance_action':{'type':'choice','choice':'orient'}}}
        self.assertEqual(parse(answer,'autonomous',menu,s)[0],'orient')
        s['runtime']['evidence']['valid']=False
        with self.assertRaises(ValueError):parse(answer,'autonomous',options(s),s)
    def test_payload_does_not_mutate_evidence(self):
        s=scripted_cases()[0]['state'];before=copy.deepcopy(s)
        payload(s,'autonomous',options(s));self.assertEqual(s,before)
    def test_schema_rejects_unoffered_actions(self):
        s=scripted_cases()[0]['state']
        for value in ('complete','confirm_gaze','invented'):
            with self.assertRaises(ValueError):parse({'answers':{'guidance_action':{'type':'choice','choice':value}}},'autonomous',options(s),s)
    def test_mirrored_holdout_actions_remain_eligible(self):
        for c in holdout_cases():
            self.assertTrue(set(c['expected_actions']) <= set(options(c['state'])))
    def test_revision_changes_instructions_not_state_or_menu(self):
        s=scripted_cases()[1]['state'];menu=options(s)
        a=payload(s,'autonomous',menu,'v1');b=payload(s,'autonomous',menu,'v2')
        self.assertEqual(a['state'],b['state'])
        self.assertEqual(a['questions']['guidance_action']['criteria'],b['questions']['guidance_action']['criteria'])
        self.assertNotEqual(a['questions']['guidance_action']['instructions'],b['questions']['guidance_action']['instructions'])
    def test_no_history_leak_across_trials(self):
        s=scripted_cases()[0]['state']
        rows=[{'group':{'trial_group':trial},'source':{'request_sequence':seq,'request_time_seconds':seq},
            'input':{'payload':{'state':s,'questions':{'transmit':{}}}}} for trial,seq in [('old',396),('new',397)]]
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'data.jsonl';p.write_text('\n'.join(json.dumps(r) for r in rows))
            self.assertEqual(recorded_cases(p)[0]['state']['recent_observations'],[])
    def test_local_cases_not_network_latency(self):
        rows=[{'arm':'baseline','local_only':True,'status':'ok','action':'wait','seconds':0,'expected_actions':['wait']}]
        self.assertEqual(summarize(rows)['baseline']['latency_seconds'],{'n':0})

if __name__=='__main__':unittest.main()
