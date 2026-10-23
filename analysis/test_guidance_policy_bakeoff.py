import copy
import unittest
from guidance_policy_bakeoff import (CATALOG, ARMS, scripted_sequences, request_payload,
    local_override, eligible, assess, local_delivery, safe_state, summarize)

class PolicyBakeoffTests(unittest.TestCase):
    def state(self, name):
        return copy.deepcopy(next(c['state'] for c in scripted_sequences() if c['id']==name))

    def test_all_jev_receives_opposite_and_unqualified_choices_even_during_blink(self):
        s=self.state('overlap_blink_pause_recovery_1')
        p=request_payload(s,'all_jev')
        self.assertEqual(set(p['questions']['action']['criteria']),set(CATALOG))
        for a in ('orient_left','orient_right','controller_confirm','success'):
            self.assertIn(a,p['questions']['action']['criteria'])
        self.assertNotIn('proposed_phrase',p['state'])
        self.assertNotIn('eligible_actions',p['state'])
        self.assertIn('invalid_evidence_speech',assess(s,'orient_right','now'))

    def test_manual_overrides_keep_gaze_pointing_and_selection_distinct(self):
        self.assertEqual(local_override(self.state('gaze_then_point_0')),'wait')
        self.assertEqual(local_override(self.state('gaze_then_point_1')),'gaze_confirm')
        self.assertEqual(local_override(self.state('controller_entry_hold_exit_0')),'wait')
        self.assertEqual(local_override(self.state('controller_entry_hold_exit_1')),'controller_confirm')
        self.assertEqual(local_override(self.state('selection_not_pointing_1')),'success')
        self.assertIn('unqualified_controller_confirmation',assess(self.state('gaze_then_point_1'),'controller_confirm','now'))

    def test_full_menu_but_final_guard_rejects_wrong_direction(self):
        s=self.state('large_turn_entry_2')
        self.assertIn('orient_left',request_payload(s,'jev_overrides')['questions']['action']['criteria'])
        self.assertNotIn('orient_left',eligible(s))
        self.assertIn('counterproductive_direction',assess(s,'orient_left','now'))

    def test_helpful_alternatives_are_not_wrong_for_differing_from_policy(self):
        s=self.state('controller_entry_hold_exit_1')
        self.assertEqual(assess(s,'trigger_reminder','now'), [])
        s=self.state('gaze_then_point_1')
        self.assertEqual(assess(s,'aim_reminder','now'), [])
        s=self.state('overshoot_return_0')
        self.assertEqual(assess(s,'refine_left','now'), [])
        self.assertIn('false_completion',assess(s,'success','now'))

    def test_near_zero_axis_is_unknown_not_wrong(self):
        s=self.state('large_turn_entry_0'); s['yaw']=1
        self.assertEqual(assess(s,'orient_right','now'), ['direction_helpfulness_unknown'])

    def test_controller_hit_blocks_directions_before_hold(self):
        self.assertIn('controller_conflicting_direction',assess(self.state('controller_entry_hold_exit_0'),'correct_down_left','now'))

    def test_short_endings_and_interruption_cooldown_protected(self):
        for name in ('short_speech_protection_0','short_speech_protection_1','correction_interrupt_1'):
            self.assertEqual(local_delivery(self.state(name),'correct_down_left'),'after_current')
        self.assertEqual(local_delivery(self.state('correction_interrupt_0'),'correct_down_left'),'interrupt')

    def test_no_unknown_hold_is_turned_into_confirmation(self):
        s=self.state('controller_entry_hold_exit_0');s.update(controller_hold=None,gaze_hold=None,gaze_winner=True)
        self.assertEqual(local_override(safe_state(s)),'wait')

    def test_all_eligible_actions_exist_and_inputs_unchanged(self):
        for c in scripted_sequences():
            s=c['state'];before=copy.deepcopy(s)
            self.assertTrue(set(eligible(s)) <= set(CATALOG),c['id'])
            for arm in ARMS:request_payload(s,arm)
            self.assertEqual(s,before)

    def test_local_cases_not_zero_latency_network_successes(self):
        row=dict(arm='current',local_only=True,status='ok',seconds=0,basis='scripted',flags=[],stale_at_response=False)
        r=summarize([row])['current']
        self.assertEqual(r['network_requests'],0)
        self.assertEqual(r['http_seconds'],{'n':0})

    def test_recorded_unknown_cases_not_scored(self):
        row=dict(arm='all_jev',local_only=False,status='ok',seconds=.2,basis='recorded_unknown_qualification',flags=[],stale_at_response=None)
        r=summarize([row])['all_jev']
        self.assertEqual(r['scripted_scored'],0)
        self.assertEqual(r['stale_checks_known'],0)

if __name__=='__main__':unittest.main()
