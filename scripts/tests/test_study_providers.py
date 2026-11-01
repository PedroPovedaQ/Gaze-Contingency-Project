"""Synthetic, offline provider-boundary checks; no credentials or audio loaded."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('study_providers', Path(__file__).parents[1] / 'check-study-providers.py')
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)


class StudyProviderTests(unittest.TestCase):
    def test_study_tree_has_no_retired_provider_integrations(self):
        self.assertEqual(guard.check(), [])

    def test_each_study_root_rejects_endpoints_keys_clients_and_dependencies(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for scope in guard.STUDY_PATHS:
                path = root / scope / 'synthetic.py'
                path.parent.mkdir(parents=True, exist_ok=True)
                for text in ('https://api.mistral.ai/v1/audio/speech', "keys.get('mistral_key')",
                             "os.environ['MISTRAL_API_KEY']", 'VoxtralClient.Synthesize()',
                             'from mistralai import Mistral', 'mistralai>=1'):
                    path.write_text(text)
                    with self.subTest(scope=scope, text=text):
                        self.assertTrue(guard.violations(root, [path.relative_to(root)]))

    def test_historical_labels_remain_readable_and_secret_file_is_never_opened(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'legacy.py'
            source.write_text('provider="mistral"')
            secret = root / 'api_keys.json'
            secret.write_bytes(b'\xff')  # would fail UTF-8 decoding if opened
            self.assertEqual(guard.violations(root, ['legacy.py', 'api_keys.json']), [])


if __name__ == '__main__':
    unittest.main()
