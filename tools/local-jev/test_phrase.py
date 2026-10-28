"""The phrase stage receives round playback, excludes unoffered phrases, and uses learned scores."""
import copy
import random
import unittest
from types import SimpleNamespace
import torch
import adaptive_data
import phrase_data
from phrase_model import PhrasePolicy


def request():
    state=adaptive_data.example(random.Random(8),0,'train')['payload']['state']
    state['selected_action']='refine_explicit'
    state['runtime'].update(locally_qualified=True,playback=dict(is_busy=False,is_speaking=False))
    e=state['runtime']['evidence']
    e.update(valid=True,geometryAvailable=True,controllerOnTarget=False,targetGazeQualified=False,gazeWithinTargetVicinity=False)
    e['guidanceState']['newGuidanceAllowed']=True
    state['behavior']['phrase_history']=[dict(phrase='Right area. Look higher.',count=2,last_started_age_seconds=7)]
    return dict(state=state,questions={phrase_data.SCOPE:dict(type='choice',criteria={
        p:p for p in ['Right area. Look higher.','This area. Look higher.']})})


class PhraseChecks(unittest.TestCase):
    def test_arrival_override_only_opens_playback_gate_for_verified_arrival(self):
        r=request(); state=r['state']; state['override_reason']='first_target_wall_entry'
        state['runtime']['playback'].update(is_busy=True,is_speaking=True)
        e=state['runtime']['evidence']; e.update(insideTargetWall=True,wallEntryQualified=True)
        e['guidanceState']['allowedAction']='Arrival'
        r['questions']['assistance_phrase']['criteria']={p:p for p in phrase_data.ARRIVAL_PHRASES}
        p=PhrasePolicy.__new__(PhrasePolicy);p.device='cpu'
        p.tokenizer=lambda *a,**k:{'input_ids':torch.zeros((4,160),dtype=torch.long)}
        p.model=lambda **k:SimpleNamespace(logits=torch.tensor([[0.],[1.],[3.],[2.]]))
        self.assertEqual(p.predict(r)['answers']['assistance_phrase']['choice'],phrase_data.ARRIVAL_PHRASES[2])
        for mutation in (lambda x:x['state'].pop('override_reason'),
                         lambda x:x['state']['runtime']['evidence'].update(valid=False),
                         lambda x:x['state']['runtime']['evidence'].update(controllerOnTarget=True),
                         lambda x:x['state']['runtime']['evidence'].update(targetGazeQualified=True),
                         lambda x:x['state']['runtime']['evidence'].update(insideTargetWall=False),
                         lambda x:x['state']['runtime']['evidence'].update(wallEntryQualified=False),
                         lambda x:x['state']['runtime']['evidence']['guidanceState'].update(allowedAction='Direction'),
                         lambda x:x['questions']['assistance_phrase']['criteria'].update({'Look left.':'Look left.'})):
            bad=copy.deepcopy(r);mutation(bad)
            with self.assertRaises(ValueError):p.predict(bad)
    def test_history_changes_features_without_leaking_identity(self):
        req=request(); texts=phrase_data.features(req)
        self.assertIn('played this round: yes; repeat count: 2',texts[0])
        self.assertIn('played this round: no; repeat count: 0',texts[1])
        req['state']['decision_id']='private'; req['state']['participant']='private'
        self.assertEqual(phrase_data.features(req),texts)
        req['state']['behavior']['phrase_history']=[]
        self.assertNotEqual(phrase_data.features(req),texts)

    def test_inference_uses_candidate_scores_and_offered_probabilities(self):
        p=PhrasePolicy.__new__(PhrasePolicy); p.device='cpu'
        p.tokenizer=lambda *a,**k: {'input_ids':torch.zeros((2,160),dtype=torch.long)}
        p.model=lambda **k: SimpleNamespace(logits=torch.tensor([[-2.],[2.]]))
        result=p.predict(request()); answer=result['answers']['assistance_phrase']
        self.assertEqual(answer['choice'],'This area. Look higher.')
        self.assertEqual(set(answer['probabilities']),set(request()['questions']['assistance_phrase']['criteria']))
        self.assertAlmostEqual(sum(answer['probabilities'].values()),1,places=6)
        self.assertEqual(result['source'],'phrase_model')

    def test_unknown_action_invalid_history_and_closed_gates_rejected(self):
        for mutation in [lambda r:r['state'].update(selected_action='observe_1s'),
                         lambda r:r['state']['behavior']['phrase_history'][0].update(count=-1),
                         lambda r:r['state']['behavior']['phrase_history'][0].update(last_started_age_seconds=float('nan'))]:
            r=request(); mutation(r)
            with self.assertRaises(ValueError): phrase_data.unpack(r)
        p=PhrasePolicy.__new__(PhrasePolicy); p.device='cpu'
        r=request(); r['state']['runtime']['playback']['is_busy']=True
        with self.assertRaises(ValueError): p.predict(r)

    def test_permuting_choices_keeps_each_candidates_features(self):
        a=request(); b=copy.deepcopy(a)
        b['questions']['assistance_phrase']['criteria']=dict(reversed(list(b['questions']['assistance_phrase']['criteria'].items())))
        def by_phrase(r): return dict(zip(r['questions']['assistance_phrase']['criteria'],phrase_data.features(r)))
        self.assertEqual(by_phrase(a),by_phrase(b))


if __name__=='__main__': unittest.main()
