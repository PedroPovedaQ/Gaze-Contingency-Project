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

    def test_reassurance_training_covers_unfinished_cues_and_unknown_history(self):
        rng=random.Random(41); statuses=set(); unknown=False; repeated=False
        for i in range(4000):
            row=data.example(rng,i,'train'); req=row['payload']
            if row['label']!='encourage': continue
            e=req['state']['runtime']['evidence']
            unknown |= 'targetAcknowledged' not in e
            cues=req['state']['behavior']['interventions']
            if cues:
                statuses.add(cues[-1]['response_status'])
                repeated |= cues[-1]['strategy']=='encourage'
        self.assertEqual(statuses,{'pending','observed_not_causal','unavailable_overlapping_intervention'})
        self.assertTrue(unknown and repeated)

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
        req['answers']={'assistance':dict(choice='observe_1s')}
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

    def test_unresolved_mixed_progress_gets_help_after_long_silence(self):
        req=self.request
        e=req['state']['runtime']['evidence']
        e.update(valid=True,geometryAvailable=True,controllerOnTarget=False,targetGazeQualified=False,
                 gazeWithinTargetVicinity=False,targetReturnPending=False,insideTargetWall=True)
        e['guidanceState'].update(newGuidanceAllowed=True,allowedAction='Direction')
        runtime=req['state']['runtime']
        runtime.update(locally_qualified=True,playback=dict(is_busy=False,is_speaking=False),seconds_since_speech_ended=1)
        req['questions']['assistance']['criteria']={k:v for k,v in data.contract()[0].items()
            if k in data.LABELS[:1]+['refine_brief','refine_explicit']}
        req['state']['behavior']['recent']['samples']=[dict(age_seconds=(7-i)*.5,angular_error_degrees=x)
            for i,x in enumerate([42,44,45,43,32,34,38,32])]
        recent=data.text(req)
        legacy=data.text(req, data.LEGACY_FEATURE_VERSION)
        self.assertTrue(data.reference(req).startswith('observe'))
        runtime['seconds_since_speech_ended']=8
        self.assertNotEqual(data.text(req),recent, 'Elapsed silence must reach the learned model')
        self.assertEqual(data.text(req, data.LEGACY_FEATURE_VERSION),legacy,
                         'Preserved checkpoints must retain their exact input representation')
        self.assertFalse(data.reference(req).startswith('observe'), 'Unresolved mixed progress must not cause indefinite silence')
        req['state']['behavior']['recent']['samples']=[dict(age_seconds=i*.5,angular_error_degrees=30+i*2)
            for i in range(8,-1,-1)]
        runtime['seconds_since_speech_ended']=1
        self.assertEqual(data.reference(req),'observe_1s', 'Clear progress still gets independent exploration time')
        runtime['seconds_since_speech_ended']=8
        self.assertFalse(data.reference(req).startswith('observe'), 'An unresolved search can receive help after prolonged progress')
        for field in ['controllerOnTarget','targetGazeQualified','gazeWithinTargetVicinity']:
            e[field]=True
            self.assertEqual(data.reference(req),'observe_1s', 'Long silence cannot bypass target gates')
            e[field]=False
        req['state']['behavior']['recent']['samples']=req['state']['behavior']['recent']['samples'][-1:]
        self.assertEqual(data.reference(req),'observe_1s', 'Silence alone cannot override insufficient evidence')
        e['targetGazeQualified']=True
        self.assertEqual(data.reference(req),'observe_1s', 'Long silence cannot bypass target confirmation')

    def test_progress_reassurance_and_repeat_spacing(self):
        req=copy.deepcopy(self.request)
        e=req['state']['runtime']['evidence']
        e.update(valid=True,geometryAvailable=True,controllerOnTarget=False,targetGazeQualified=False,
                 gazeWithinTargetVicinity=False,targetReturnPending=False)
        e['guidanceState'].update(newGuidanceAllowed=True,allowedAction='Direction')
        runtime=req['state']['runtime']
        runtime.update(locally_qualified=True,playback=dict(is_busy=False,is_speaking=False),seconds_since_speech_ended=5)
        req['questions']['assistance']['criteria']=data.contract()[0]
        b=req['state']['behavior']; b['interventions']=[]
        for rate in [.65,1.2,4]:
            b['recent']['samples']=[dict(age_seconds=i*.5,angular_error_degrees=30+i*.5*rate,direction='right') for i in range(8,-1,-1)]
            self.assertEqual(data.reference(req),'encourage')
        b['interventions']=[dict(strategy='encourage')]
        runtime['seconds_since_speech_ended']=1
        self.assertEqual(data.reference(req),'observe_1s')
        runtime['seconds_since_speech_ended']=5
        self.assertEqual(data.reference(req),'encourage', 'Previous encouragement must not suppress all future reassurance')
        b['recent']['samples'][-1]['angular_error_degrees']=40
        self.assertNotEqual(data.reference(req),'encourage', 'Do not reassure a current reversal')
        for retired in ['observe_2s','observe_4s']:
            bad=copy.deepcopy(req);bad['questions']['assistance']['criteria'][retired]='retired'
            with self.assertRaises(ValueError): data.unpack(bad)

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
                return dict(raw_choice='correct',answers=dict(assistance=dict(type='choice',choice='observe_1s',probabilities={'observe_1s':1})))
        policy=Stub()
        with tempfile.TemporaryDirectory() as d:
            source=Path(d)/'input'; source.mkdir()
            shadow=Shadow(source,Path(d)/'out',policy,'hash',from_start=True)
            try:
                self.request['state']['runtime']['evidence']['valid']=False
                shadow.observe('recording',trace('response',status='ok',elapsed_ms=150,answers=dict(assistance=dict(type='choice',choice='observe_1s'))))
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
        # Highest logit is unavailable correct; masking must return offered observe_1s.
        policy.model=lambda **kw: SimpleNamespace(logits=torch.tensor([[3.,0.,0.,0.,0.,8.,0.]]))
        result=policy.predict(req,guard=False); answer=result['answers']['assistance']
        self.assertEqual(result['raw_choice'],'correct'); self.assertEqual(answer['choice'],'observe_1s')
        self.assertNotIn('correct',answer['probabilities']); self.assertAlmostEqual(sum(answer['probabilities'].values()),1,places=6)
        req['state']['runtime']['evidence']['valid']=False
        policy.model=lambda **kw: self.fail('Closed gate must not invoke the network')
        self.assertEqual(policy.predict(req)['probability_semantics'],'deterministic_guard')
        req['state']['runtime']['evidence']['valid']=True
        policy.tokenizer=lambda *a,**kw: {'input_ids':torch.zeros((1,385),dtype=torch.long)}
        with self.assertRaises(ValueError): policy.predict(req)


if __name__=='__main__': unittest.main()
