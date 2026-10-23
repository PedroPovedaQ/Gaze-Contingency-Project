"""Run the local study, recording-link, guidance and eye-quality checks together.

This reads explicitly supplied copied files. It never pulls from a device,
uploads data, changes recordings or certifies hardware/research readiness.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys

from recording_links import fingerprint


ROOT = Path(__file__).resolve().parents[1]
VERSION = 'recording-audit-bundle-v1'
LIMITS = ('Recording consistency only. Hardware accuracy, native 120 Hz, eye-event validity, '
          'survey completeness, voice quality and video/acoustic synchronization remain unverified.')


def report_status(name, directory, returncode):
    """A successful process is insufficient when its report says evidence is missing."""
    if name == 'session':
        return 'consistent' if returncode == 0 else 'issues_found'
    if name == 'links':
        report = json.loads((directory/'recording_links.json').read_text())
        status = report['status']
        expected = {'consistent': 0, 'evidence_incomplete': 2, 'issues_found': 1}
        if status not in expected or returncode != expected[status]:
            raise ValueError('Recording-link status and exit code disagree')
        return status
    if returncode != 0:
        return 'issues_found'
    if name == 'guidance':
        report = json.loads((directory/'guidance_audit.json').read_text())
        replays = report['replays']
        if any(r.get('issues') or r.get('status') == 'issues_found' for r in replays):
            return 'issues_found'
        complete = (replays and not report['warnings'] and report['eye_end_status'] and
                    all(r.get('status') == 'consistent' and r.get('replay_complete') is True for r in replays) and
                    all(r.get('complete') is True for r in report['eye_end_status']))
    else:
        report = json.loads((directory/'quality_report.json').read_text())
        complete = not report['recording_warnings'] and report['recording_end_status'].get('complete') is True
    return 'consistent' if complete else 'evidence_incomplete'


def run_audit(run, eyes, replays, output, *, allow_incomplete=False, no_plots=False):
    """Keep each validator's report/logs even when another validator fails."""
    run, output = Path(run).resolve(), Path(output).resolve()
    eyes, replays = [Path(p).resolve() for p in eyes], [Path(p).resolve() for p in replays]
    if not run.is_dir() or not eyes or not replays:
        raise ValueError('Supply one run directory and at least one eye file and replay file')
    if output.exists():
        raise ValueError('Output exists; choose a new directory')
    if len(set(eyes+replays)) != len(eyes+replays):
        raise ValueError('Duplicate input files are not allowed')
    # Never create reports in a source recording directory (including symlink aliases).
    if any(output == parent or parent in output.parents for parent in [run, *[p.parent for p in eyes+replays]]):
        raise ValueError('Output must be outside the supplied source directories')
    paths = [run/name for name in ('trial_summary.json', 'trial_events.csv', 'object_manifest.csv',
                                  'trial_schedule.csv', 'voice-library-manifest.json')]
    paths += eyes+replays
    paths += [p.parent/name for p in eyes for name in ('capabilities.json', 'end_status.json')]
    required = [run/'trial_summary.json', *eyes, *replays]
    if any(not p.is_file() for p in required):
        raise ValueError('A required source file is missing or is not a regular file')
    # Include missing sidecars so appearing/disappearing metadata is detected too.
    def inventory():
        return [fingerprint(p) if p.is_file() else dict(path=str(p), missing=True)
                for p in dict.fromkeys(paths)]
    sources = inventory()
    summary = json.loads((run/'trial_summary.json').read_text(encoding='utf-8-sig'))
    if not isinstance(summary, dict):
        raise ValueError('Invalid trial summary')
    output.mkdir(parents=True, exist_ok=False)
    nominal = summary.get('nominal_total_rounds')
    full_session = (summary.get('session_outcome') == 'completed' and
                    summary.get('total_rounds') == nominal and summary.get('debug_round_override') == 0
                    if type(nominal) is int and nominal > 0 else None)
    report = dict(version=VERSION, started_at=datetime.now(timezone.utc).isoformat(), status='running',
                  participant_id=summary.get('participant_id'), run_number=summary.get('run_number'),
                  session_outcome=summary.get('session_outcome'), allow_incomplete=allow_incomplete,
                  nominal_total_rounds=nominal, configured_total_rounds=summary.get('total_rounds'),
                  declared_full_nominal_session=full_session,
                  hardware_verified=False, limits=LIMITS, sources=sources, checks=[])
    report_path = output/'audit_report.json'
    def save():
        temporary = output/'audit_report.json.tmp'
        temporary.write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
        temporary.replace(report_path)
    save()
    arguments = [argument for flag, files in (('--eye', eyes), ('--replay', replays))
                 for path in files for argument in (flag, str(path))]
    jobs = [('session', [ROOT/'scripts/verify-study-session.py', run] +
             (['--allow-incomplete'] if allow_incomplete else [])),
            ('links', [ROOT/'analysis/recording_links.py', run, *arguments, '--out', output/'links']),
            ('guidance', [ROOT/'analysis/guidance_audit.py', *arguments, '--out', output/'guidance'])]
    jobs += [(f'eye-{i:03}', [ROOT/'analysis/eye_quality.py', path, '--out', output/f'eye-{i:03}'] +
              (['--no-plots'] if no_plots else [])) for i, path in enumerate(eyes, 1)]
    for name, arguments in jobs:
        command = [sys.executable, *map(str, arguments)]
        check = dict(name=name, command=command, status='running')
        report['checks'].append(check); save()
        try:
            with (output/f'{name}.stdout.log').open('w') as stdout, (output/f'{name}.stderr.log').open('w') as stderr:
                result = subprocess.run(command, cwd=ROOT, stdout=stdout, stderr=stderr, check=False)
            check['exit_code'] = result.returncode
            check['status'] = report_status(name, output/name, result.returncode)
        except (OSError, ValueError, KeyError, TypeError) as error:
            check.update(status='check_failed', error=str(error))
        save()
    try:
        report['sources_unchanged'] = inventory() == sources
    except OSError as error:
        report.update(sources_unchanged=False, source_check_error=str(error))
    statuses = [c['status'] for c in report['checks']]
    if not report['sources_unchanged'] or any(s in ('check_failed', 'issues_found') for s in statuses):
        report['status'] = 'issues_found'
    elif summary.get('session_outcome') != 'completed' or 'evidence_incomplete' in statuses:
        report['status'] = 'evidence_incomplete'
    else:
        report['status'] = 'consistent'
    report['finished_at'] = datetime.now(timezone.utc).isoformat()
    save()
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    parser.add_argument('--eye', type=Path, action='append', required=True)
    parser.add_argument('--replay', type=Path, action='append', required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--allow-incomplete', action='store_true', help='Inspect a partial run; never label it complete')
    parser.add_argument('--no-plots', action='store_true', help='Generate eye tables without matplotlib figures')
    args = parser.parse_args()
    report = run_audit(args.run, args.eye, args.replay, args.out,
                       allow_incomplete=args.allow_incomplete, no_plots=args.no_plots)
    print(f"{report['status']}: {args.out/'audit_report.json'}")
    print(LIMITS)
    return {'consistent': 0, 'issues_found': 1, 'evidence_incomplete': 2}[report['status']]


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise SystemExit(str(error)) from None
