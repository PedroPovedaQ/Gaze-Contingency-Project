#!/usr/bin/env python3
"""Build the Mac trial viewer in an isolated generated Unity project."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
from prepare import prepare


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path, help='New .app destination (never overwritten)')
    parser.add_argument('--project', type=Path, help='Isolated generated project; defaults to a new temporary directory')
    parser.add_argument('--unity', type=Path, default=Path('/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity'))
    parser.add_argument('--verify-recording', type=Path, help='Optional copied real recording to test before building')
    parser.add_argument('--verify-audio-recording', type=Path, help='Run deterministic batched-transport audio coverage on a saved recording')
    parser.add_argument('--verify-export', type=Path, help='Optional matching CSV export to verify')
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists() or output.suffix != '.app':
        parser.error('--output must name a new .app')
    if not args.unity.is_file():
        parser.error('Unity executable not found')
    project = prepare(args.project or Path(tempfile.mkdtemp(prefix='trial-review-unity-')))
    output.parent.mkdir(parents=True, exist_ok=True)
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_') and not k.startswith('TRIAL_REVIEW_')}
    env['TRIAL_REVIEW_OUTPUT'] = str(output)
    if args.verify_recording:
        env['TRIAL_REVIEW_VERIFY_RECORDING'] = str(args.verify_recording.resolve())
    if args.verify_audio_recording:
        env['TRIAL_REVIEW_AUDIO_RECORDING'] = str(args.verify_audio_recording.resolve())
    if args.verify_export:
        env['TRIAL_REVIEW_VERIFY_EXPORT'] = str(args.verify_export.resolve())
    log = project / 'build.log'
    command = [str(args.unity), '-batchmode', '-nographics', '-quit', '-projectPath', str(project),
               '-buildTarget', 'StandaloneOSX', '-executeMethod', 'TrialReviewBuild.Build', '-logFile', str(log)]
    print(f'Generated project: {project}\nBuild log: {log}', flush=True)
    result = subprocess.run(command, env=env)
    if result.returncode != 0 or not output.exists() or 'TRIAL_REVIEW_BUILD_PASS' not in log.read_text():
        raise SystemExit(f'Build did not succeed. Inspect {log}')
    print(f'Built {output}')


if __name__ == '__main__':
    main()
