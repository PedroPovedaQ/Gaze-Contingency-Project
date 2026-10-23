import tempfile
import unittest
from pathlib import Path
from prepare import ROOT, prepare, replace_once


class PrepareChecks(unittest.TestCase):
    def test_refuse_study_or_unrelated_directory(self):
        with self.assertRaises(ValueError):
            prepare(ROOT / 'should-not-create')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'existing.txt').write_text('preserve')
            with self.assertRaises(ValueError):
                prepare(root)
            self.assertEqual((root / 'existing.txt').read_text(), 'preserve')

    def test_adapter_fails_closed_on_source_drift(self):
        with self.assertRaises(ValueError):
            replace_once('two two', 'two', 'one')
        with self.assertRaises(ValueError):
            replace_once('none', 'missing', 'value')

    def test_generated_project_has_no_study_bootstrap_or_editor_runtime(self):
        with tempfile.TemporaryDirectory() as directory:
            root = prepare(Path(directory))
            self.assertTrue((root / 'Assets/SharedReplay/GuidanceStateSnapshot.cs').exists())
            self.assertTrue((root / 'Assets/Runtime/TrialReviewGuidanceState.cs').exists())
            self.assertFalse((root / 'Assets/StreamingAssets/api_keys.json').exists())
            self.assertFalse((root / 'Assets/Scenes/GazeContingencyStudyScene.unity').exists())
            for source in (root / 'Assets/SharedReplay').glob('*.cs'):
                text = source.read_text()
                self.assertNotIn('using UnityEditor', text, source.name)
                self.assertNotIn('RuntimeInitializeOnLoadMethod', text, source.name)
            self.assertNotIn('openxr', (root / 'Packages/manifest.json').read_text().lower())
            self.assertTrue((root / 'shared-source-sha256.json').exists())


if __name__ == '__main__':
    unittest.main()
