import copy
import hashlib
import json
from pathlib import Path
import random
import subprocess
import tempfile
import unittest
from data import LABELS, PHRASE_SOURCE, blocked, direction_words, generate, reference, synthetic, text, unpack


class DataChecks(unittest.TestCase):
    def setUp(self):
        rng=random.Random(12)
        while True:
            self.row=synthetic(rng,1,'train')
            if not blocked(self.row['payload']): break

    def test_safety_gates_close_on_missing_invalid_or_busy(self):
        for key,value in [('valid',False),('geometryAvailable',False),('controllerOnTarget',True),('targetGazeQualified',True)]:
            payload=copy.deepcopy(self.row['payload'])
            payload['state']['runtime']['evidence'][key]=value
            self.assertTrue(blocked(payload));self.assertEqual(reference(payload),'wait')
        for field in ('is_speaking','is_busy'):
            payload=copy.deepcopy(self.row['payload']);payload['state']['runtime']['playback'][field]=True
            self.assertTrue(blocked(payload))
        payload=copy.deepcopy(self.row['payload']);del payload['state']['runtime']['evidence']['guidanceState']['newGuidanceAllowed']
        self.assertTrue(blocked(payload))

    def test_output_labels_cannot_escape_candidates(self):
        for split in ('train','valid','test'):
            rng=random.Random(8)
            for i in range(100):
                row=synthetic(rng,i,split)
                self.assertIn(row['label'],unpack(row['payload'])[-1])
                self.assertTrue(set(row['acceptable']) <= set(unpack(row['payload'])[-1]))

    def test_direction_paraphrases_and_repeat_boundary(self):
        self.assertEqual(direction_words('Look higher and left.'), direction_words('Keep looking up and left.'))
        payload=copy.deepcopy(self.row['payload'])
        payload['questions']['navigation_action']['criteria']={'refine':{'text':'Look to your left.','locally_eligible':True},'wait':{'text':None}}
        runtime=payload['state']['runtime']
        runtime['evidence']['guidanceState']['allowedAction']='Direction'
        runtime['playback']['last_spoken_phrase']='Search to your left.'
        for silence,expected in [(0,'wait'),(2.999,'wait'),(3,'wait'),(3.001,'refine')]:
            runtime['seconds_since_speech_ended']=silence
            self.assertEqual(reference(payload),expected)
        runtime['seconds_since_speech_ended']=1
        runtime['playback']['last_spoken_phrase']='Look right.'
        self.assertEqual(reference(payload),'refine')

    def test_synthetic_phrases_exist_in_compiled_production_library(self):
        mono=Path('/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge')
        if not (mono/'bin/mono').exists(): self.skipTest('Unity Mono required for independent production phrase check')
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=root/'Dump.cs';exe=root/'dump.exe'
            source.write_text('class Dump { static void Main() { foreach (var p in DirectionalHintPolicy.AllPhrases()) System.Console.WriteLine(p); } }')
            subprocess.run([str(mono/'bin/mono'),str(mono/'lib/mono/4.5/csc.exe'),'-nologo',f'-out:{exe}',str(PHRASE_SOURCE),str(source)],check=True,capture_output=True)
            library=set(subprocess.check_output([str(mono/'bin/mono'),str(exe)],text=True).splitlines())
        for split in ('train','valid','test'):
            rng=random.Random(35)
            for i in range(300):
                payload=synthetic(rng,i,split)['payload']
                for key,value in unpack(payload)[-1].items():
                    if key!='wait': self.assertIn(value['text'],library)
                self.assertIn(payload['state']['runtime']['playback']['last_spoken_phrase'],library)

    def test_model_text_ignores_labels_identity_and_provider_result(self):
        payload=copy.deepcopy(self.row['payload']);before=text(payload)
        payload['answers']={'navigation_action':{'choice':'correct'}}
        payload['state'].update(decision_id='private-id',participant='person',expected_label='correct',elapsed_ms=123)
        self.assertEqual(text(payload),before)
        choices=payload['questions']['navigation_action']['criteria']
        payload['questions']['navigation_action']['criteria']=dict(reversed(list(choices.items())))
        self.assertEqual(text(payload),before)

    def test_reject_unoffered_ineligible_and_nonfinite_contracts(self):
        for key,value in [('model_name','invented')]:
            payload=copy.deepcopy(self.row['payload']);payload['questions']={key:value}
            with self.assertRaises(ValueError):unpack(payload)
        for silence in (None, float('nan'),float('inf'),-1,True):
            payload=copy.deepcopy(self.row['payload']);payload['state']['runtime']['seconds_since_speech_ended']=silence
            with self.assertRaises(ValueError):unpack(payload)
        payload=copy.deepcopy(self.row['payload'])
        next(v for k,v in unpack(payload)[-1].items() if k!='wait')['locally_eligible']=False
        with self.assertRaises(ValueError):unpack(payload)

    def test_data_split_is_disjoint_and_reproducible(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);one=generate(root/'one');two=generate(root/'two')
            self.assertEqual(one,two)
            sets={}
            for split in ('train','valid','test','production'):
                rows=[json.loads(line) for line in (root/'one'/f'{split}.jsonl').read_text().splitlines()]
                sets[split]={hashlib.sha256(text(r['payload']).encode()).hexdigest() for r in rows}
                self.assertEqual(len(sets[split]),len(rows))
            for split in ('valid','test','production'):self.assertFalse(sets['train'] & sets[split])
            self.assertFalse(sets['valid'] & sets['test'])
            self.assertEqual(one['files']['train']['count'],1200)
            with self.assertRaises(FileExistsError):generate(root/'one')


if __name__=='__main__':unittest.main()
