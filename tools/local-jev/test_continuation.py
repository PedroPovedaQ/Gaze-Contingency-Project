import copy
import json
from pathlib import Path
import random
import subprocess
import tempfile
import unittest
from continuation_data import FEATURE_VERSION, example, generate_continuation, model_text, wording
from data import PHRASE_SOURCE, text, unpack


class ContinuationChecks(unittest.TestCase):
    def test_legacy_input_and_whitelist_remain_stable(self):
        payload = example(random.Random(44), 1, 'train')['payload']
        self.assertEqual(model_text(payload), text(payload))
        compact = model_text(payload, FEATURE_VERSION)
        altered = copy.deepcopy(payload)
        altered['answers'] = {'navigation_action': {'choice':'wait'}}
        altered['state'].update(expected_label='correct', participant='private', decision_id='private')
        self.assertEqual(compact, model_text(altered, FEATURE_VERSION))
        with self.assertRaises(ValueError): model_text(payload, 'unknown-future-version')

    def test_family_split_and_all_action_coverage(self):
        directions = ['left','right','up','down','up left','up right','down left','down right']
        groups = {split:{p for d in directions for p in wording(d, split)} for split in ('train','valid','fresh')}
        self.assertFalse(groups['train'] & groups['valid'])
        self.assertFalse(groups['train'] & groups['fresh'])
        self.assertFalse(groups['valid'] & groups['fresh'])
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); manifest=generate_continuation(root/'one')
            self.assertEqual(manifest, generate_continuation(root/'two'))
            seen=set()
            for split in ('train','valid','fresh'):
                rows=[json.loads(line) for line in (root/'one'/f'{split}.jsonl').read_text().splitlines()]
                self.assertEqual(len(set(r['label'] for r in rows)), 5)
                values={model_text(r['payload'], FEATURE_VERSION) for r in rows}
                self.assertEqual(len(values), len(rows)); self.assertFalse(seen & values); seen |= values
                for row in rows:
                    choices=unpack(row['payload'])[-1]
                    key='refine' if 'refine' in choices else 'orient'
                    self.assertIn(choices[key]['text'], groups[split])

    def test_new_wording_is_in_production_prepared_phrase_library(self):
        mono=Path('/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge')
        if not (mono/'bin/mono').exists(): self.skipTest('Unity Mono unavailable')
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); source=root/'Dump.cs'; exe=root/'dump.exe'
            source.write_text('class Dump { static void Main() { foreach (var p in DirectionalHintPolicy.AllPhrases()) System.Console.WriteLine(p); } }')
            subprocess.run([str(mono/'bin/mono'),str(mono/'lib/mono/4.5/csc.exe'),'-nologo',f'-out:{exe}',str(PHRASE_SOURCE),str(source)],check=True,capture_output=True)
            phrases=set(subprocess.check_output([str(mono/'bin/mono'),str(exe)],text=True).splitlines())
        for split in ('train','valid','fresh'):
            for direction in ('left','right','up','down','up left','up right','down left','down right'):
                self.assertTrue(set(wording(direction, split)) <= phrases)


if __name__=='__main__': unittest.main()
