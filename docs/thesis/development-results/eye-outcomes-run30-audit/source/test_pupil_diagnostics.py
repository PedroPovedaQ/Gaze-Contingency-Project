"""Synthetic fixtures verify diagnostics; they are never study outcomes."""
import unittest
import tempfile
from pathlib import Path
import numpy as np
from eye_outcomes import Config, analyze, pupil_screen
from pupil_diagnostics import audit, plot_quality
from test_eye_outcomes import sample


class PupilDiagnosticsTests(unittest.TestCase):
    def test_screen_reports_all_failures_without_interpolation(self):
        info,_=pupil_screen([1,1.1,1.2],Config())
        self.assertEqual(info['reasons'],['short_segment','low_query_rate','insufficient_wavelet_points'])
        self.assertEqual(pupil_screen([1,1],Config())[0]['reasons'],['invalid_timing'])

    def test_audit_does_not_change_outcomes(self):
        rows=[sample(1+i*.01) for i in range(401)]
        baseline=analyze(rows);diagnostics=[]
        self.assertEqual(analyze(rows,pupil_diagnostics=diagnostics),baseline)
        self.assertEqual(len(diagnostics),2)
        self.assertTrue(all(s['first_failure']=='accepted' for s in diagnostics))
        report=audit(rows,Config(),diagnostics,baseline[0])
        self.assertAlmostEqual(report['eyes']['left']['segment_seconds_by_first_failure']['accepted'],4)
        self.assertEqual(report['gaps_over_limit'],0)
        with tempfile.TemporaryDirectory() as folder:
            plot_quality(report,Path(folder))
            self.assertTrue((Path(folder)/'pupil-quality.png').exists())

    def test_pauses_and_trial_changes_are_not_query_gaps(self):
        rows=[sample(1),sample(1),sample(1.01),sample(2,phase='paused'),sample(3),sample(3.01),sample(5,sourceTrialId='r1'),sample(5.01,sourceTrialId='r1')]
        report=audit(rows,Config(),[],[])
        self.assertEqual(report['gaps_over_limit'],0)
        self.assertEqual(report['repeated_query_rows'],1)
        self.assertEqual(report['within_search_query_intervals'],3)

    def test_invalid_data_remains_missing_and_missing_ipa_is_not_zero(self):
        rows=[sample(1+i*.01,leftPupilValid=False,rightPupilValid=False) for i in range(401)]
        diagnostics=[];metrics,_=analyze(rows,pupil_diagnostics=diagnostics)
        report=audit(rows,Config(),diagnostics,metrics)
        self.assertTrue(all(m['ipa_candidate_hz'] is None for m in metrics))
        self.assertEqual(diagnostics,[])
        self.assertEqual(report['eyes']['left']['reported_valid_pupil_rows'],0)

    def test_irregular_timing_explained_even_with_valid_pupils(self):
        times=np.r_[1.,1+np.cumsum([.005,.015]*150)]
        info,_=pupil_screen(times,Config(pupil_max_p95_relative_jitter=.4))
        self.assertIn('irregular_query_timing',info['reasons'])
        self.assertNotIn('short_segment',info['reasons'])

    def test_review_export_is_unlabeled_and_preserves_missing_pupil(self):
        import csv
        from pupil_diagnostics import export_review_traces
        rows=[sample(1+i*.01,leftPupilValid=False) for i in range(10)]
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder);export_review_traces(rows,Config(),path)
            with (path/'trial-01.csv').open() as file:
                records=list(csv.DictReader(file))
            self.assertTrue(all(r['pupil_mm']=='' for r in records if r['eye']=='left'))
            self.assertNotIn('kind',records[0])
            self.assertEqual(len((path/'annotations.csv').read_text().splitlines()),1)
            self.assertTrue((path/'trial-01.pdf').exists())

    def test_empty_report_keeps_unknown_interval_quantiles(self):
        report=audit([],Config(),[],[])
        self.assertIsNone(report['query_interval_ms_percentiles']['50'])
        self.assertEqual(report['eyes']['left']['raw_search_rows'],0)

if __name__=='__main__': unittest.main()
