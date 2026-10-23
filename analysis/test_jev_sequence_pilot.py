import unittest
from jev_sequence_pilot import Bridge, simulate, scenarios, make_payload, evidence_flags

class SequencePilotTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.bridge=Bridge()
    @classmethod
    def tearDownClass(cls): cls.bridge.close()
    def oracle(self, body):
        c=body['state']['local_action']
        if c not in body['questions']['action']['criteria']:c='wait'
        return dict(status='ok',seconds=.2,action=c,delivery='now')
    def test_local_confirmations_separate(self):
        r=simulate(self.bridge,'hold',scenarios()['controller_before_gaze'],'restricted',self.oracle)
        starts=[e for e in r['events'] if e['kind']=='simulated_start']
        pointing=[e for e in starts if e['action']=='controller_confirm']
        self.assertTrue(pointing)
        self.assertGreaterEqual(pointing[0]['time'],1.6)
        self.assertTrue(all(not e['flags'] for e in starts))
        self.assertTrue(any(e['action']=='success' and e['time']>=6.5 for e in starts))
    def test_replies_never_count_as_speech(self):
        def waiter(body):return dict(status='ok',seconds=.2,action='wait',delivery='now')
        r=simulate(self.bridge,'silence',(2,[(0,dict(yaw=90))]),'all_jev',waiter)
        self.assertFalse(any(e['kind']=='simulated_start' for e in r['events']))
        self.assertTrue(all(not q['payload']['state']['history']['speech'] for q in r['requests']))
    def test_actual_simulated_starts_feed_history(self):
        r=simulate(self.bridge,'turn',scenarios()['ignored_direction_silence'],'eligible_actions',self.oracle)
        starts=[e for e in r['events'] if e['kind']=='simulated_start']
        histories=[x for q in r['requests'] for x in q['payload']['state']['history']['speech']]
        self.assertTrue(histories)
        self.assertTrue(all(any(e['text']==h['text'] for e in starts) for h in histories))
    def test_late_and_opposing_proposals_blocked(self):
        def bad(body):return dict(status='ok',seconds=.3,action='orient_left',delivery='now')
        r=simulate(self.bridge,'bad',(2,[(0,dict(yaw=60))]),'jev_overrides',bad)
        self.assertFalse(any(e['kind']=='simulated_start' for e in r['events']))
        self.assertTrue(any(e['kind']=='blocked_stale_or_invalid' for e in r['events']))
        def late(body):return dict(status='ok',seconds=.8,action='orient_right',delivery='now')
        r=simulate(self.bridge,'late',(2,[(0,dict(yaw=60))]),'restricted',late)
        self.assertFalse(any(e['kind']=='simulated_start' for e in r['events']))
        self.assertTrue(any(e['kind']=='request_deadline_or_failure' for e in r['events']))
    def test_queued_direction_invalidated_on_controller_entry(self):
        r=simulate(self.bridge,'entry',(2,[(0,dict(yaw=60)),(.4,dict(pointing=True))]),'restricted',self.oracle)
        self.assertFalse(any(e['kind']=='simulated_start' for e in r['events']))
    def test_unprotected_errors_retained(self):
        def bad(body):return dict(status='ok',seconds=.2,action='controller_confirm',delivery='now')
        r=simulate(self.bridge,'raw',(1,[(0,dict(yaw=60))]),'all_jev',bad)
        self.assertTrue(any('unqualified_controller_confirmation' in e.get('flags',[]) for e in r['events']))
    def test_pause_cancels_pending(self):
        r=simulate(self.bridge,'pause',(2,[(0,dict(yaw=60)),(.4,dict(paused=True))]),'restricted',self.oracle)
        self.assertFalse(any(e['kind']=='simulated_start' for e in r['events']))
    def test_useful_partial_not_wrong(self):
        s=dict(paused=False,valid=True,pointing=False,controllerValid=True,looking=False,yaw=12,pitch=-8,selected=False)
        self.assertEqual([],evidence_flags(s,{},'refine_right'))
    def test_early_gate_reduces_requests_without_losing_starts(self):
        scene=scenarios()['ignored_direction_silence']
        before=simulate(self.bridge,'repeat',scene,'jev_overrides',self.oracle)
        after=simulate(self.bridge,'repeat',scene,'jev_overrides',self.oracle,skip_known_repeats=True)
        self.assertLess(len(after['requests']),len(before['requests']))
        self.assertEqual(sum(e['kind']=='simulated_start' for e in before['events']),sum(e['kind']=='simulated_start' for e in after['events']))
    def test_event_wake_does_not_make_local_choices(self):
        scene=(2,[(0,dict(inside=True,yaw=0,looking=True))])
        before=simulate(self.bridge,'wake',scene,'all_jev',self.oracle)
        after=simulate(self.bridge,'wake',scene,'all_jev',self.oracle,wake_on_state_change=True)
        self.assertLess(after['episodes'][0]['latency'],before['episodes'][0]['latency'])
        self.assertTrue(all(e['source']=='jev' for e in after['events'] if e['kind']=='simulated_start'))
    def test_wait_attribution_matches_elapsed_episode(self):
        r=simulate(self.bridge,'timing',scenarios()['speech_then_confirm'],'restricted',self.oracle)
        for e in r['episodes']:
            if e['latency'] is not None:self.assertAlmostEqual(e['latency'],sum(e['waiting'].values()),places=5)
    def test_success_is_not_pointing(self):
        def chooser(body):return dict(status='ok',seconds=.2,action=body['state']['local_action'],delivery='now')
        r=simulate(self.bridge,'end',(2,[(0,dict(selected=True))]),'all_jev',chooser)
        self.assertEqual(1,sum(e['kind']=='simulated_start' and e['action']=='success' for e in r['events']))

if __name__=='__main__':unittest.main()
