"""The compile-check child must not inherit the parent repository's Git context."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'unity-compile-check.sh'
GIT_VARS = (
    'GIT_DIR', 'GIT_INDEX_FILE', 'GIT_WORK_TREE', 'GIT_COMMON_DIR', 'GIT_PREFIX',
    'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_NAMESPACE',
    'GIT_CONFIG', 'GIT_CONFIG_PARAMETERS', 'GIT_CONFIG_COUNT',
)


class UnityCompileHookTests(unittest.TestCase):
    def run_check(self, child_exit=0):
        with tempfile.TemporaryDirectory(prefix='unity-hook-test-') as tmp:
            root = Path(tmp)
            (root / 'scripts').mkdir()
            script = root / 'scripts' / SCRIPT.name
            shutil.copy2(SCRIPT, script)
            fake = root / 'fake-unity'
            fake.write_text(
                '#!' + sys.executable + '\n'
                'import json, os, pathlib, sys\n'
                'pathlib.Path(os.environ["HOOK_TEST_CAPTURE"]).write_text(json.dumps({k: os.environ[k] for k in '
                + repr(GIT_VARS) + ' if k in os.environ}))\n'
                'pathlib.Path(sys.argv[sys.argv.index("-logFile") + 1]).write_text("Compilation completed.\\n")\n'
                'sys.exit(int(os.environ["HOOK_TEST_EXIT"]))\n'
            )
            fake.chmod(0o755)
            capture = root / 'child-git-env.json'
            env = dict(os.environ)
            env.update({name: '/fake/parent-repository' for name in GIT_VARS})
            env.update(UNITY_BIN=str(fake), HOOK_TEST_CAPTURE=str(capture),
                       HOOK_TEST_EXIT=str(child_exit), UNITY_LINT_LOG_FILE=str(root / 'compile.log'))
            result = subprocess.run(['bash', str(script)], env=env, capture_output=True, text=True)
            self.assertTrue(capture.exists(), result.stdout + result.stderr)
            self.assertEqual(json.loads(capture.read_text()), {}, 'Unity received parent Git context')
            self.assertTrue(all(env[name] == '/fake/parent-repository' for name in GIT_VARS))
            return result

    def test_child_git_context_is_cleared(self):
        result = self.run_check()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('Unity compile check passed.', result.stdout)

    def test_unity_failure_still_fails_the_hook(self):
        result = self.run_check(17)
        self.assertEqual(result.returncode, 17, result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
