import unittest
from guidance_paired_pilots import summarize, stats
from guidance_jev_set_pilot import eligible

class PairedPilotTests(unittest.TestCase):
    def event(self,time,event='observation',action='ReturnTarget',controller=True,gaze=False,detail=''):
        return dict(time=time,event=event,action=action,gaze_valid=True,controller_on_target=controller,gaze_on_target=gaze,detail=detail)
    def test_contiguous_conflict_is_one_episode(self):
        events=[self.event(t) for t in [0,.05,.1,.15]]
        events += [self.event(.2,action='None'),self.event(.25)]
        self.assertEqual(2,summarize(events)['controller_conflict_eligibility_episodes'])
    def test_requests_are_not_audio(self):
        events=[self.event(0,'scheduled',detail='ReturnTarget')]
        self.assertEqual(0,summarize(events)['controller_conflicting_simulated_starts'])
    def test_qualification_retries_count_once(self):
        events=[self.event(0,'qualification_controller'),self.event(.2,'qualification_controller'),
                self.event(.4,'simulated_start',detail='ConfirmPointing')]
        self.assertEqual({'count':1,'median':.4,'p95':.4,'maximum':.4,'over_threshold':0},summarize(events)['simulated_controller_start_delay'])
    def test_invalid_gaze_starts_new_episode(self):
        invalid=self.event(.5,gaze=True);invalid['gaze_valid']=False
        events=[self.event(0,'qualification_controller',gaze=True),invalid,
                self.event(1,'qualification_controller',gaze=True),self.event(1.2,'simulated_start',gaze=True,detail='ConfirmPointing')]
        self.assertEqual(.2,summarize(events)['simulated_controller_start_delay']['maximum'])
    def test_nearby_gaze_departure_clears_controller_confirmation(self):
        events=[self.event(0,'qualification_controller',gaze=True),self.event(.5,gaze=False),
                self.event(1,'qualification_controller',gaze=True),self.event(1.1,'simulated_start',gaze=True,detail='ConfirmPointing')]
        self.assertEqual(.1,summarize(events)['simulated_controller_start_delay']['maximum'])
    def test_pause_clears_qualified_episode(self):
        events=[self.event(0,'qualification_gaze',gaze=True),self.event(.5,'pause',gaze=True),
                self.event(2,'qualification_gaze',gaze=True),self.event(2.1,'simulated_start',gaze=True,detail='ConfirmGaze')]
        self.assertEqual(.1,summarize(events)['simulated_gaze_start_delay']['maximum'])
    def test_nearest_rank_and_strict_threshold(self):
        self.assertEqual(2,stats([0,1,1.1,2])['over_threshold'])
        self.assertIsNone(stats([])['p95'])
    def test_half_open_catalog_bounds(self):
        phrases=[dict(id='left',text='left',constraints=dict(min_angle=-90,max_angle=0,min_distance_meters=0,max_distance_meters=2)),
                 dict(id='right',text='right',constraints=dict(min_angle=0,max_angle=180,min_distance_meters=2,max_distance_meters=4))]
        self.assertEqual(['right'],[p['text'] for p in eligible(phrases,0,2)])
        self.assertEqual(['right'],[p['text'] for p in eligible(phrases,180,3)])
        self.assertEqual([],eligible(phrases,0,4))

if __name__=='__main__':unittest.main()
