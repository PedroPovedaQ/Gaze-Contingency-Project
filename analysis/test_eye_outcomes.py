"""Artificial unit fixtures are not participant evidence or physiological validation."""
import math
import unittest

import numpy as np
from scipy.spatial.transform import Rotation

from eye_outcomes import Config, analyze, fresh_search_segments, idt, ipa, compare_reviewed_windows


def ray(degrees):
    theta = math.radians(degrees)
    return {'x': math.sin(theta), 'y': 0., 'z': math.cos(theta)}


def sample(t, direction=0., **changes):
    row = dict(recordingId='test', participant='fixture', run=1, sourceTrialId='r0',
               phase='search', practice=False, deviceId=1, voice='test', valid=True,
               focused=True, deviceAvailable=True, htcGazeAvailable=True,
               htcQueryReceiptTime=t, receiptTime=t, headValid=True,
               headRotation={'x':0.,'y':0.,'z':0.,'w':1.}, opennessAvailable=True,
               pupilAvailable=True)
    for side in ('left', 'right'):
        row.update({side+'Valid':True, side+'WorldDirection':ray(direction),
                    side+'PupilValid':True, side+'PupilMm':4.,
                    side+'OpennessValid':True, side+'Openness':1.})
    row.update(changes)
    return row


class EyeOutcomeTests(unittest.TestCase):
    def test_elapsed_time_not_sample_count_and_azimuth_wrap(self):
        c = Config()
        t = np.array([1.,1.02,1.04,1.06,1.08])
        v = np.array([list(ray(0).values())]*len(t))
        self.assertEqual(idt(t,v,c), [])
        t[-1]=1.083
        self.assertEqual(idt(t,v,c), [(0,4)])
        t = np.arange(20)*.01
        v = np.array([list(ray(179.9 if i%2 else -179.9).values()) for i in range(20)])
        self.assertEqual(idt(t,v,c), [(0,19)])

    def test_two_known_fixations(self):
        times = np.arange(60)*.01
        directions = np.array([list(ray(0 if i<30 else 20).values()) for i in range(60)])
        self.assertEqual(idt(times,directions,Config()), [(0,29),(30,59)])

    def test_cached_queries_pause_and_stale_reveal_do_not_create_samples(self):
        rows=[sample(1,phase='ready'), sample(1,receiptTime=1.005),sample(1.01),
              sample(1.01,receiptTime=1.015),sample(1.02),sample(1.03,phase='paused'),sample(1.04),sample(1.05)]
        self.assertEqual([[r['htcQueryReceiptTime'] for r in s] for s in fresh_search_segments(rows,Config())],
                         [[1.01,1.02],[1.04,1.05]])

    def test_invalid_gap_backwards_context_breaks(self):
        rows=[sample(1),sample(1.01),sample(1.02,valid=False),sample(1.03),sample(1.10),
              sample(1.11,sourceTrialId='r1'),sample(1.105,sourceTrialId='r1')]
        self.assertEqual([len(s) for s in fresh_search_segments(rows,Config())],[2,1,1,1])

    def test_unknown_is_not_zero(self):
        metrics,_=analyze([sample(1,valid=False),sample(1.01,valid=False)])
        for m in metrics:
            self.assertIsNone(m['fixation_count'])
            self.assertIsNone(m['saccade_count'])
            self.assertIsNone(m['ipa_candidate_hz'])

    def test_constant_pupil_and_missing_head(self):
        rows=[sample(1+i*.01,headValid=False) for i in range(401)]
        metrics,events=analyze(rows)
        self.assertEqual(len(events),2)
        for m in metrics:
            self.assertEqual(m['fixation_count'],1)
            self.assertIsNone(m['saccade_count'])
            self.assertEqual(m['ipa_candidate_hz'],0.)

    def test_known_saccade_and_head_turn_exclusion(self):
        angles=[0.]*20+[3.,6.,9.]+[9.]*20
        rows=[sample(1+i*.01,direction=d) for i,d in enumerate(angles)]
        metrics,events=analyze(rows)
        self.assertEqual([m['saccade_count'] for m in metrics],[1,1])
        self.assertEqual(len([e for e in events if e['kind']=='candidate_saccade']),2)
        for r,d in zip(rows,angles):
            q=Rotation.from_euler('y',d,degrees=True).as_quat()
            r['headRotation']=dict(zip('xyzw',q))
        metrics,_=analyze(rows)
        self.assertEqual([m['saccade_count'] for m in metrics],[0,0])

    def test_short_or_invalid_pupil_not_imputed(self):
        rows=[sample(1+i*.01) for i in range(301)]
        rows[150]['leftPupilValid']=False
        metrics,_=analyze(rows)
        left,right=metrics
        self.assertIsNone(left['ipa_candidate_hz'])
        self.assertEqual(right['ipa_candidate_hz'],0.)
        self.assertEqual(left['pupil_segments_rejected'],2)

    def test_query_completes_just_after_row_timestamp(self):
        rows=[sample(1+i*.01,receiptTime=1+i*.01-.0003) for i in range(30)]
        metrics,_=analyze(rows)
        self.assertEqual([r['fixation_count'] for r in metrics],[1,1])

    def test_tracking_loss_padding_across_segments(self):
        rows=[sample(1+i*.01) for i in range(501)]
        rows[250]['valid']=False
        metrics,_=analyze(rows)
        # Two 2.29-second pupil pieces after 200-ms padding on either side.
        self.assertTrue(all(4.4 < r['pupil_eligible_seconds'] < 4.7 for r in metrics))

    def test_short_disconnected_runs_are_unavailable(self):
        rows=[sample(1),sample(1.02),sample(2),sample(2.02),sample(3),sample(3.02),sample(4),sample(4.02),sample(5),sample(5.02)]
        metrics,_=analyze(rows)
        self.assertTrue(all(r['fixation_count'] is None for r in metrics))

    def test_no_saccade_from_unbracketed_segment_edge(self):
        rows=[sample(1+i*.01,direction=i*3 if i<4 else 9) for i in range(30)]
        metrics,_=analyze(rows)
        self.assertTrue(all(r['saccade_count']==0 for r in metrics))

    def test_annotation_windows_exclude_unreviewed_candidates(self):
        event=dict(recording_id='test',eye='left',trial_id='r0',kind='candidate_fixation',start=1.,end=2.)
        other=dict(event,start=3.,end=4.)
        reference=[dict(event,kind='fixation')]
        windows=[dict(recording_id='test',eye='left',trial_id='r0',start=.5,end=2.5)]
        result=compare_reviewed_windows([event,other],reference,windows,'test')
        self.assertEqual(result['unscored_candidate_count'],1)
        self.assertEqual(result['by_eye_and_kind']['left_fixation']['true_positive'],1)
        self.assertEqual(result['by_eye_and_kind']['left_fixation']['false_positive'],0)
        with self.assertRaises(ValueError):
            compare_reviewed_windows([event],reference,windows,'another-recording')

    def test_ipa_matches_independent_scalar_reference(self):
        import pywt
        x=np.array([4+.12*math.sin(i*.2)+.05*math.sin(i*.8) for i in range(400)])
        d=pywt.wavedec(x,'sym16',mode='periodization',level=2)[1]/2
        peaks=[]
        for i,value in enumerate(abs(d)):
            lo=abs(d[max(0,i-1)]); hi=abs(d[min(len(d)-1,i+1)])
            peaks.append(float(value) if value>=lo and value>=hi and (value>lo or value>hi) and value>1e-10*max(1,max(abs(x))) else 0.)
        mean=sum(peaks)/len(peaks)
        sd=math.sqrt(sum((v-mean)**2 for v in peaks)/len(peaks))
        threshold=sd*math.sqrt(2*math.log2(len(peaks)))
        expected=sum(v>0 and v>=threshold for v in peaks)
        count, rate=ipa(x,3.99)
        self.assertEqual(count,expected)
        self.assertAlmostEqual(rate,expected/3.99)
        self.assertEqual(ipa(np.full(400,4.),3.99),(0,0.))
        with self.assertRaises(ValueError): ipa([4.]*10,1.)


if __name__ == '__main__': unittest.main()
