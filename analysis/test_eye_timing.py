"""Artificial timing/AOI fixtures test software, not physiological validity."""
import unittest
from eye_timing import diagnostics, stamp
from eye_outcomes import analyze, Config, fresh_search_segments
from eye_quality import flat, summarize
from test_eye_outcomes import sample, ray


def timing(t, **changes):
    ns=9007199254740993+round(t*1e9)
    value=dict(epoch=1,tracker=10,baseSpace=20,session=1,requestedTime=ns,returnedTime=ns,
               beforeNs=ns,afterNs=ns+1000,result=0,observed=1)
    value.update(changes)
    return {s:dict(value) for s in ('gaze','pupil','geometric')}


class TimingTests(unittest.TestCase):
    def test_exact_integer_flat_export_and_strict_types(self):
        row=sample(1,htcTiming=timing(0))
        self.assertEqual(flat(row)['htcTiming_gaze_returnedTime'],9007199254740993)
        for bad in (True,1.,'1'):
            row['htcTiming']['gaze']['returnedTime']=bad
            with self.assertRaises(ValueError): stamp(row,'gaze')

    def test_duplicate_and_regressing_native_times_break_continuity(self):
        rows=[sample(1+i*.01,htcTiming=timing(t)) for i,t in enumerate((0,.01,.01,.005,.02))]
        # Give repeated poses distinct query brackets (two real calls).
        for i,r in enumerate(rows):
            r['htcTiming']['gaze']['beforeNs']+=i
            r['htcTiming']['gaze']['afterNs']+=i
        self.assertEqual([len(s) for s in fresh_search_segments(rows,Config())],[2,1])
        report=diagnostics(rows)['gaze']
        self.assertEqual(report['repeated_times'],1)
        self.assertEqual(report['regressions'],1)
        self.assertFalse(report['sensor_cadence_validated'])

    def test_invalid_clock_does_not_fall_back_to_query_time(self):
        for changes in ({'returnedTime':0},{'result':-1},{'observed':0}):
            rows=[sample(1+i*.01,htcTiming=timing(i*.01,**changes)) for i in range(20)]
            self.assertEqual(fresh_search_segments(rows,Config()),[])

    def test_pupil_is_not_deduplicated_using_gaze_time(self):
        rows=[]
        for i in range(401):
            r=sample(1+i*.01,htcTiming=timing(i*.01))
            r['htcTiming']['gaze']['returnedTime']=9007199254740993
            rows.append(r)
        metrics,_=analyze(rows)
        self.assertIsNone(metrics[0]['fixation_count'])
        self.assertEqual(metrics[0]['ipa_candidate_hz'],0.)
        self.assertGreater(metrics[0]['pupil_eligible_seconds'],3.9)
        for r in rows:r['htcTiming']['pupil']['returnedTime']=9007199254740993
        metrics,_=analyze(rows)
        self.assertIsNone(metrics[0]['ipa_candidate_hz'])

    def test_epoch_change_and_native_gap_split(self):
        rows=[sample(1+i*.01,htcTiming=timing(t)) for i,t in enumerate((0,.01,.5,.51))]
        self.assertEqual([len(s) for s in fresh_search_segments(rows,Config())],[2,2])
        rows[1]['htcTiming']=timing(.01,epoch=2)
        self.assertEqual([len(s) for s in fresh_search_segments(rows[:2],Config())],[1,1])

    def test_target_event_join_and_censoring(self):
        rows=[sample(1+i*.01,direction=(0 if i<20 else 10 if i<40 else 20),
                     taskAoiAvailable=True,taskTargetId='target',
                     leftHitObjectId='target' if 20<=i<40 else 'other',
                     rightHitObjectId='target' if 20<=i<40 else 'other') for i in range(60)]
        metrics,events=analyze(rows)
        self.assertAlmostEqual(metrics[0]['last_target_candidate_s'],.19)
        target=[e for e in events if e['target_aoi_fraction']==1.]
        self.assertEqual(len(target),2)
        self.assertFalse(target[0]['boundary_censored'])
        metrics,_=analyze(rows[:40])
        self.assertIsNone(metrics[0]['last_target_candidate_s'])
        self.assertEqual(metrics[0]['last_target_candidate_status'],'boundary_censored')
        rows[30]['leftHitObjectId']='other'
        metrics,_=analyze(rows)
        self.assertIsNone(metrics[0]['last_target_candidate_s'])
        self.assertAlmostEqual(metrics[1]['last_target_candidate_s'],.19)

    def test_native_saccade_requires_matching_head_pose(self):
        angles=[0.]*20+[3.,6.,9.]+[9.]*20
        rows=[sample(1+i*.01,direction=d,htcTiming=timing(i*.01)) for i,d in enumerate(angles)]
        metrics,_=analyze(rows)
        self.assertIsNone(metrics[0]['saccade_count'])
        for row in rows:
            row['htcHeadPoseValid']=True
            row['htcHeadWorldRotation']={'x':0.,'y':0.,'z':0.,'w':1.}
        metrics,_=analyze(rows)
        self.assertEqual(metrics[0]['saccade_count'],1)

    def test_reference_error_precision_not_ray_precision(self):
        rows=[sample(1+i*.01,rawWorldDirection=ray(v),inputEventTime=1+i*.01,
                     newInputUpdate=True,validationTargetPresent=True,validationId='v',validationSet='heldout',
                     targetId='t',targetErrorDegrees=1,mirroredOriginErrorDegrees=1) for i,v in enumerate((-1,1,-1,1))]
        report=summarize(rows,settle=0)['spatial'][0]
        self.assertAlmostEqual(report['rms_s2s_deg'],2)
        self.assertEqual(report['target_error_rms_s2s_deg'],0)
        self.assertEqual(report['target_error_sd_population_deg'],0)
