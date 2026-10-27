"""Adaptive adapter: no state/answer leakage, correct temporal summaries, strict contracts."""
import copy
import json
from pathlib import Path
import random
import tempfile
import unittest
from types import SimpleNamespace
import torch
import adaptive_data as data
from model import Policy as LearnedPolicy
from shadow import Shadow
from test_shadow import trace


class AdaptiveChecks(unittest.TestCase):
    def setUp(self): self.request=data.example(random.Random(8),0,'train')['payload']

    def test_shared_contract_and_all_classes(self):
        rng=random.Random(4)
        labels={data.example(rng,i,'train')['label'] for i in range(2000)}
        self.assertEqual(labels,set(data.LABELS))
        self.assertEqual(set(data.contract()[0]),labels)

    def test_departures_cross_movement_patterns_in_training(self):
        rng=random.Random(12); trends=set()
        for i in range(2000):
            row=data.example(rng,i,'train'); req=row['payload']
            if ':departure' in row['family'] and not data.blocked(req):
                trends.add(data.measurements(req)['trend'])
                self.assertEqual(row['label'],'correct')
        self.assertEqual(trends,{'approaching','receding','little_change','unknown'})

    def test_identity_answers_demographics_and_old_net_gain_do_not_override_recent_trend(self):
        req=self.request; e=req['state']['runtime']['evidence']
        e.update(valid=True,geometryAvailable=True,controllerOnTarget=False,targetGazeQualified=False,gazeWithinTargetVicinity=False,targetReturnPending=False)
        e['guidanceState'].update(newGuidanceAllowed=True,allowedAction='Direction')
        req['state']['runtime'].update(locally_qualified=True,playback=dict(is_busy=False,is_speaking=False))
        req['state']['behavior']['recent']['samples']=[dict(age_seconds=i*.5,angular_error_degrees=60-i*2) for i in range(8,-1,-1)]
        req['state']['behavior']['recent']['error_reduction_degrees']=100
        self.assertEqual(data.measurements(req)['trend'],'receding')
        self.assertFalse(data.reference(req).startswith('observe'))
        before=data.text(req)
        req['state'].update(decision_id='secret-id',participant='private-name',demographics='private')
        req['answers']={'assistance':dict(choice='observe_4s')}
        self.assertEqual(data.text(req),before)

    def test_departure_after_confirmation_and_current_target_are_separate(self):
        cases=json.loads((data.ROOT/'scripts/tests/adaptive-device-replay.json').read_text())
        case=next(c for c in cases if c['name']=='departed_after_confirmation')
        req=data.payload(case['state'],case['capabilities'])
        self.assertEqual(data.reference(req),'correct')
        for key in ['targetGazeQualified','gazeWithinTargetVicinity','controllerOnTarget']:
            altered=copy.deepcopy(req); altered['state']['runtime']['evidence'][key]=True
            self.assertTrue(data.blocked(altered)); self.assertEqual(data.reference(altered),'observe_1s')

    def test_gaps_and_stale_history_are_not_sustained_progress(self):
        req=self.request
        recent=req['state']['behavior']['recent']
        recent['samples']=[dict(age_seconds=4,angular_error_degrees=60),dict(age_seconds=0,angular_error_degrees=30)]
        self.assertEqual(data.measurements(req)['trend'],'unknown')
        recent['samples']=[dict(age_seconds=x,angular_error_degrees=30+x*3) for x in [4,3,2,1]]
        self.assertEqual(data.measurements(req)['trend'],'unknown')

    def test_malformed_wrong_contract_and_nonfinite_inputs_rejected(self):
        cases=[]
        for value in [None,[],{}]: cases.append(value)
        wrong=copy.deepcopy(self.request); wrong['state']['policy']='jev_adaptive_assistance_v1'; cases.append(wrong)
        wrong=copy.deepcopy(self.request); wrong['questions']['assistance']['criteria']['invented']='text'; cases.append(wrong)
        wrong=copy.deepcopy(self.request); wrong['state']['behavior']['recent']['samples'][0]['angular_error_degrees']=float('nan'); cases.append(wrong)
        wrong=copy.deepcopy(self.request); wrong['state']['behavior']['recent']['samples'].reverse(); cases.append(wrong)
        for case in cases:
            with self.assertRaises(ValueError): data.unpack(case)

    def test_adaptive_shadow_pairs_and_reports_gates_without_playback(self):
        class Stub:
            scope='assistance'
            def predict(self,req,guard=True):
                self.seen=copy.deepcopy(req)
                return dict(raw_choice='correct',answers=dict(assistance=dict(type='choice',choice='observe_2s',probabilities={'observe_2s':1})))
        policy=Stub()
        with tempfile.TemporaryDirectory() as d:
            source=Path(d)/'input'; source.mkdir()
            shadow=Shadow(source,Path(d)/'out',policy,'hash',from_start=True)
            try:
                self.request['state']['runtime']['evidence']['valid']=False
                shadow.observe('recording',trace('response',status='ok',elapsed_ms=150,answers=dict(assistance=dict(type='choice',choice='observe_2s'))))
                shadow.observe('recording',trace('request',payload=self.request))
                report=shadow.report(); self.assertEqual(report['comparable'],1); self.assertEqual(report['masked_matches'],1)
                self.assertEqual(shadow.decisions()[0]['local']['gated_choice'],'observe_1s')
                self.assertEqual(policy.seen,self.request)
            finally: shadow.close()

    def test_inference_mask_probabilities_guard_and_context_limit(self):
        cases=json.loads((data.ROOT/'scripts/tests/adaptive-device-replay.json').read_text())
        case=next(c for c in cases if c['name']=='independent_progress_control')
        req=data.payload(case['state'],case['capabilities'])
        policy=LearnedPolicy.__new__(LearnedPolicy)
        policy.adaptive=True; policy.scope='assistance'; policy.labels=data.LABELS; policy.device='cpu'
        policy.tokenizer=lambda *a,**kw: {'input_ids':torch.zeros((1,180),dtype=torch.long)}
        # Highest logit is unavailable correct; masking must return offered observe_4s.
        policy.model=lambda **kw: SimpleNamespace(logits=torch.tensor([[0.,1.,3.,0.,0.,0.,0.,8.,0.]]))
        result=policy.predict(req,guard=False); answer=result['answers']['assistance']
        self.assertEqual(result['raw_choice'],'correct'); self.assertEqual(answer['choice'],'observe_4s')
        self.assertNotIn('correct',answer['probabilities']); self.assertAlmostEqual(sum(answer['probabilities'].values()),1,places=6)
        req['state']['runtime']['evidence']['valid']=False
        policy.model=lambda **kw: self.fail('Closed gate must not invoke the network')
        self.assertEqual(policy.predict(req)['probability_semantics'],'deterministic_guard')
        req['state']['runtime']['evidence']['valid']=True
        policy.tokenizer=lambda *a,**kw: {'input_ids':torch.zeros((1,385),dtype=torch.long)}
        with self.assertRaises(ValueError): policy.predict(req)


if __name__=='__main__': unittest.main()
