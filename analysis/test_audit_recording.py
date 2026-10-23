import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from audit_recording import run_audit
import test_recording_links
import test_study_validation


class RecordingAuditTests(unittest.TestCase):
    def fixture(self, root):
        # Combine the existing real-file validator fixtures; these are synthetic,
        # not a claim that a one-trial legacy fixture is the 80-trial study.
        links = test_recording_links.RecordingLinkTests()
        run, eye, replay, row, rows = links.fixture(root/'sources')
        study = test_study_validation.StudyValidationTests()
        summary, events = study.completed_fixture(run, count=1, block_size=1, object_count=1)
        summary.update(participant_id='P1', run_number=1)
        summary['objectives'][0]['trial_id'] = 'P1_run001_r00'
        for event in events:
            event['trial_id'] = 'P1_run001_r00'
        study.write_recording(run, summary, events)
        (run/'object_manifest.csv').write_text('trial_id,object_id,shape,color,is_target\nP1_run001_r00,target,Cube,Red,1\n')
        row.update(newInputUpdate=True, deviceAvailable=True, rawWorldDirection=row['worldDirection'],
                   maxDeliveryAgeSeconds=.1, ageSeconds=.01)
        links.write(eye, [row])
        rows[0]['guidanceAuditVersion'] = 'guidance-decision-v1'
        decision = dict(rows[1], kind='guidance_decision', time=3.5, sequence=4,
                        guidanceDecisionId='suspended', guidanceDecision=dict(schema=1, decisionId='suspended',
                        decisionTime=100, stage='suspended', observationAvailable=False, gazeUsable=False,
                        participant='P1', run=1, sourceTrialId='P1_run001_r00', round=0, block=0))
        rows[-1]['sequence'] = 5
        rows.insert(-1, decision)
        links.write(replay, rows)
        return run, eye, replay

    def inputs(self, root):
        (root/'sources').mkdir()
        return self.fixture(root)

    def test_complete_bundle_runs_real_commands_and_keeps_hardware_unknown(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            before = {p:p.read_bytes() for p in (run/'trial_summary.json', eye, replay)}
            report = run_audit(run, [eye], [replay], root/'report', no_plots=True)
            self.assertEqual(report['status'], 'consistent', report)
            self.assertTrue(report['sources_unchanged'])
            self.assertFalse(report['hardware_verified'])
            self.assertEqual(len(report['checks']), 4)
            self.assertTrue(all(c['status'] == 'consistent' for c in report['checks']), report)
            for path, original in before.items():
                self.assertEqual(path.read_bytes(), original)
            self.assertTrue((root/'report/eye-001/clock_frame_dictionary.csv').exists())
            self.assertIsNone(report['declared_full_nominal_session'])

    def test_missing_receipt_and_missing_guidance_cannot_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            (eye.parent/'end_status.json').unlink()
            report = run_audit(run, [eye], [replay], root/'report', no_plots=True)
            self.assertEqual(report['status'], 'evidence_incomplete', report)
            self.assertEqual(report['checks'][-1]['status'], 'evidence_incomplete')
            (eye.parent/'end_status.json').write_text('{"complete":true,"acceptedRows":1}')
            rows = [json.loads(line) for line in replay.read_text().splitlines()]
            rows = [r for r in rows if r['kind'] != 'guidance_decision']
            replay.write_text(''.join(json.dumps(r)+'\n' for r in rows))
            report = run_audit(run, [eye], [replay], root/'no-guidance', no_plots=True)
            self.assertEqual(report['status'], 'evidence_incomplete', report)

    def test_failure_does_not_skip_other_reports(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            (run/'voice-library-manifest.json').unlink()
            report = run_audit(run, [eye], [replay], root/'report', no_plots=True)
            self.assertEqual(report['status'], 'issues_found')
            self.assertEqual(report['checks'][0]['status'], 'issues_found')
            self.assertEqual(report['checks'][-1]['status'], 'consistent')
            self.assertTrue((root/'report/session.stderr.log').read_text() or
                            'FAIL:' in (root/'report/session.stdout.log').read_text())

    def test_source_changes_and_success_without_reports_are_failures(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            actual_run = subprocess.run
            def changing(command, **kwargs):
                result = actual_run(command, **kwargs)
                if 'verify-study-session.py' in command[1]:
                    (eye.parent/'capabilities.json').write_text('{}')
                return result
            with patch('audit_recording.subprocess.run', side_effect=changing):
                report = run_audit(run, [eye], [replay], root/'changed', no_plots=True)
            self.assertEqual(report['status'], 'issues_found')
            self.assertFalse(report['sources_unchanged'])
            with patch('audit_recording.subprocess.run', return_value=subprocess.CompletedProcess([], 0)):
                report = run_audit(run, [eye], [replay], root/'missing-reports', no_plots=True)
            self.assertEqual(report['status'], 'issues_found')
            self.assertEqual(report['checks'][1]['status'], 'check_failed')

    def test_partial_mode_stays_incomplete_even_when_all_checks_are_consistent(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            summary = json.loads((run/'trial_summary.json').read_text())
            summary['session_outcome'] = 'stopped_by_researcher'
            (run/'trial_summary.json').write_text(json.dumps(summary))
            report = run_audit(run, [eye], [replay], root/'partial', allow_incomplete=True, no_plots=True)
            self.assertTrue(all(c['status'] == 'consistent' for c in report['checks']), report)
            self.assertEqual(report['status'], 'evidence_incomplete')

    def test_rejects_duplicates_and_source_output_including_symlinks(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            alias = root/'source-alias'; alias.symlink_to(eye.parent, target_is_directory=True)
            for eyes, out in (([eye, eye], root/'duplicate'), ([eye], run/'report'),
                              ([eye], eye.parent/'report'), ([eye], alias/'report')):
                with self.assertRaises(ValueError):
                    run_audit(run, eyes, [replay], out, no_plots=True)
                self.assertFalse(out.exists())

    def test_cli_status_and_existing_output_are_preserved(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); run, eye, replay = self.inputs(root)
            (eye.parent/'end_status.json').unlink()
            command = [sys.executable, str(Path(__file__).with_name('audit_recording.py')), str(run),
                       '--eye', str(eye), '--replay', str(replay), '--out', str(root/'report'), '--no-plots']
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 2, result.stderr)
            before = (root/'report/audit_report.json').read_bytes()
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Output exists', result.stderr)
            self.assertEqual((root/'report/audit_report.json').read_bytes(), before)


if __name__ == '__main__':
    unittest.main()
