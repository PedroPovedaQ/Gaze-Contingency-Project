"""Direction features remain words, causal, versioned and subordinate to gates."""
import copy
import random
import unittest
import adaptive_data as a
import phrase_data as p

class DirectionChecks(unittest.TestCase):
    def request(self):
        r=a.example(random.Random(8),0,'train')['payload']
        e=r['state']['runtime']['evidence']
        e.update(valid=True,geometryAvailable=True,controllerOnTarget=False,targetGazeQualified=False,
                 gazeWithinTargetVicinity=False,targetReturnPending=False,insideTargetWall=False,vicinityErrorDegrees=174)
        e['guidanceState'].update(newGuidanceAllowed=True,allowedAction='Direction')
        r['state']['runtime'].update(locally_qualified=True,playback=dict(is_busy=False,is_speaking=False),seconds_since_speech_ended=2)
        r['state']['behavior']['interventions']=[]
        r['state']['behavior']['recent']['samples']=[dict(age_seconds=i*.5,angular_error_degrees=174-i,direction='right') for i in range(8,-1,-1)]
        r['questions']['assistance']['criteria']={k:v for k,v in a.contract()[0].items() if k in ['observe_1s','orient_brief','orient_explicit']}
        return r

    def test_versioned_features_and_no_leakage(self):
        r=self.request(); old=a.text(r,a.PROGRESS_FEATURE_VERSION)
        r['state']['direction']={'schema':'adaptive-direction-v1','needed':'right','moving':'right away','started':'large turn right'}
        self.assertEqual(old,a.text(r,a.PROGRESS_FEATURE_VERSION))
        self.assertIn('needed: right; moving: right away; started: large turn right',a.text(r))
        before=a.text(r); r['state'].update(target='red cube',answers='correct',future='left')
        self.assertEqual(before,a.text(r))
        r['state']['direction']['needed']='secret identity'
        with self.assertRaises(ValueError): a.text(r)

    def test_long_way_and_wrong_way_are_separable_without_gate_changes(self):
        r=self.request()
        r['state']['direction']={'schema':'adaptive-direction-v1','needed':'right','moving':'right away','started':'large turn right'}
        self.assertEqual(a.reference(r),'observe_1s')
        self.assertEqual(a.reference(r,a.LEGACY_POLICY),'orient_explicit')
        r['state']['direction']['moving']='left away'
        self.assertEqual(a.reference(r),'orient_explicit')
        r['state']['runtime']['evidence']['valid']=False
        self.assertEqual(a.reference(r),'observe_1s')
        del r['state']['direction']
        self.assertEqual(a.direction_words(r),('unknown','unknown','unknown'))

    def test_long_way_generator_matches_unity_rear_seam(self):
        rng=random.Random(19); count=0
        for i in range(1000):
            row=a.example(rng,i,'test')
            if ':long_way' not in row['family']: continue
            count+=1; e=row['payload']['state']['runtime']['evidence']
            self.assertFalse(e['insideTargetWall'])
            self.assertGreaterEqual(abs(e['yawErrorDegrees']),150)
            self.assertLessEqual(abs(e['yawErrorDegrees']),180)
        self.assertGreater(count,0)

    def test_training_motion_uses_latest_two_seconds(self):
        rng=random.Random(22); reversals=0
        for i in range(2000):
            row=a.example(rng,i,'test');req=row['payload'];moving=req['state']['direction']['moving']
            samples=[s for s in req['state']['behavior']['recent']['samples'] if s['age_seconds']<=2]
            if moving.endswith(' away'):
                self.assertLess(samples[0]['angular_error_degrees']-samples[-1]['angular_error_degrees'],0)
                reversals+=a.measurements(req)['trend']=='approaching'
        self.assertGreater(reversals,0)

    def test_phrase_direction_comes_from_unity_menu_not_right_area_words(self):
        rows=p.examples([dict(action='refine_explicit',needed='up',
                             phrases=['Right area. Look higher.','This area. Look higher.'])],4,12,set())
        for row in rows:
            self.assertEqual(row['payload']['state']['direction']['needed'],'up')
            self.assertIn('needed: up;',p.features(row['payload'])[0])

    def test_phrase_legacy_and_new_features(self):
        r=self.request(); r['state'].pop('direction',None); r['state']['selected_action']='orient_explicit'
        r['questions']={'assistance_phrase':{'type':'choice','criteria':{'Look right.':'Look right.'}}}
        self.assertIn('needed: unknown',p.features(r)[0])
        self.assertNotIn('needed:',p.features(r,p.LEGACY_VERSION)[0])

if __name__=='__main__': unittest.main()
