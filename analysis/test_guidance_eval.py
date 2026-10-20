import csv
import json
import tempfile
import unittest
from pathlib import Path

from guidance_eval import RATING_FIELDS, evaluate_recording


def fixture():
    payload = {
        "state": {"schema": "speech-navigation-v2", "relative_yaw_degrees": -30,
                  "runtime": {"captured_at_seconds": 10002,
                              "evidence": {"valid": True, "geometryAvailable": True},
                              "playback": {"is_speaking": False, "is_busy": False}}},
        "questions": {
            "navigation_phrase": {"type": "choice", "criteria": {
                "Look left.": {"semantics": {"horizontal": "left"}},
                "Look right.": {"semantics": {"horizontal": "right"}}}},
            "transmit": {"type": "choice", "criteria": {"speak": "", "wait": ""}}}}
    def row(sequence, time, kind, **kwargs):
        return dict(schema=2, sequence=sequence, time=time, kind=kind,
                    participantCode="p1", sourceTrialId="p1_run1_r0", trialId="trial_001",
                    runNumber=1, round=0, block=0, **kwargs)
    def trace(sequence, time, stage, **kwargs):
        return row(sequence, time, "jev_trace", detail="jev;" + json.dumps(dict(
            request_id="decision1", stage=stage, **kwargs)))
    return [
        row(0, 0, "header", recordingId="session1"),
        trace(1, 2, "request", payload=payload),
        trace(2, 2.3, "response", status="ok", answers={
            "navigation_phrase": {"choice": "Look left."}, "transmit": {"choice": "speak"}}),
        row(3, 2.5, "audio_playback_start", guidanceDecisionId="decision1", text="Look left."),
        row(4, 3, "end", complete=True)]


class GuidanceEvaluationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)

    def evaluate(self, rows):
        path = self.root / "recording.jsonl"
        path.write_text("".join(json.dumps(row) + "\n" for row in rows))
        return evaluate_recording(path, self.root / "out")

    def payload(self, row):
        return json.loads(row["detail"].split(";", 1)[1])

    def replace_payload(self, row, value):
        row["detail"] = "jev;" + json.dumps(value)

    def test_timebases_and_no_future_context_in_benchmark(self):
        report = self.evaluate(fixture())
        row = report["decisions"][0]
        self.assertAlmostEqual(row["software_response_seconds"], .3)
        self.assertAlmostEqual(row["software_playback_seconds"], .5)
        benchmark = json.loads((self.root / "out/guidance-benchmark.jsonl").read_text())
        self.assertEqual(benchmark["input"]["payload"], self.payload(fixture()[1])["payload"])
        self.assertEqual(benchmark["evidence_ids"], ["seq:1"])
        self.assertNotIn("answers", benchmark)
        self.assertNotIn("response_status", benchmark["input"])
        self.assertEqual(report["summary"]["geometry_checks"], {"pass": 1, "fail": 0, "unknown": 0})

    def test_missing_state_is_not_a_correct_geometry_score(self):
        rows = fixture()
        trace = self.payload(rows[1])
        del trace["payload"]["state"]
        self.replace_payload(rows[1], trace)
        report = self.evaluate(rows)
        self.assertEqual(report["summary"]["geometry_checks"], {"pass": 0, "fail": 0, "unknown": 1})
        self.assertEqual(report["decisions"][0]["guidance_state_status"], "unavailable_legacy_or_missing")

    def test_missing_response_is_not_failure(self):
        rows = fixture()
        del rows[2]
        report = self.evaluate(rows)
        self.assertEqual(report["summary"]["choice_checks"], {"pass": 0, "fail": 0, "unknown": 2})
        self.assertIsNone(report["decisions"][0]["software_response_seconds"])

    def test_wrong_direction_and_unoffered_choice_are_separate_checks(self):
        rows = fixture()
        trace = self.payload(rows[2])
        trace["answers"]["navigation_phrase"]["choice"] = "Look right."
        self.replace_payload(rows[2], trace)
        report = self.evaluate(rows)
        self.assertEqual(report["summary"]["geometry_checks"]["fail"], 1)
        self.assertEqual(report["summary"]["choice_checks"]["fail"], 0)
        trace["answers"]["navigation_phrase"]["choice"] = "Made up."
        self.replace_payload(rows[2], trace)
        report = self.evaluate(rows)
        self.assertEqual(report["summary"]["choice_checks"]["fail"], 1)
        self.assertEqual(report["summary"]["geometry_checks"]["unknown"], 1)

    def test_legacy_no_requests_has_no_invented_accuracy(self):
        rows = fixture()
        report = self.evaluate([rows[0], rows[-1]])
        self.assertEqual(report["summary"]["benchmark_cases"], 0)
        self.assertEqual(report["summary"]["geometry_checks"], {"pass": 0, "fail": 0, "unknown": 0})

    def test_ratings_blank_then_preserved(self):
        rows = fixture()
        self.evaluate(rows)
        path = self.root / "out/guidance-human-ratings.csv"
        with path.open(newline="") as stream:
            reader = csv.DictReader(stream)
            columns, saved = reader.fieldnames, list(reader)
        self.assertTrue(all(saved[0][field] == "" for field in RATING_FIELDS))
        saved[0]["appropriateness_1_5"] = "4"
        with path.open("w", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=columns)
            writer.writeheader()
            writer.writerows(saved)
        self.evaluate(rows)
        with path.open(newline="") as stream:
            self.assertEqual(next(csv.DictReader(stream))["appropriateness_1_5"], "4")
        rows[0]["recordingId"] = "another_session"
        with self.assertRaisesRegex(ValueError, "Existing ratings"):
            self.evaluate(rows)

    def test_split_groups_participant_across_sessions_and_dev_unknown(self):
        rows = fixture()
        self.evaluate(rows)
        path = self.root / "out/guidance-benchmark.jsonl"
        first = json.loads(path.read_text())
        rows[0]["recordingId"] = "another_session"
        self.evaluate(rows)
        second = json.loads(path.read_text())
        self.assertEqual(first["group"]["participant_group"], second["group"]["participant_group"])
        self.assertNotEqual(first["group"]["session_group"], second["group"]["session_group"])
        rows[1]["participantCode"] = "DEV_NEUTRAL"
        self.evaluate(rows)
        dev = json.loads(path.read_text())
        self.assertIsNone(dev["group"]["participant_group"])
        self.assertTrue(dev["group"]["requires_participant_mapping"])

    def test_duplicate_requests_excluded_from_benchmark(self):
        rows = fixture()
        duplicate = dict(rows[1], sequence=2, time=2.1)
        for row in rows[2:]:
            row["sequence"] += 1
        rows.insert(2, duplicate)
        report = self.evaluate(rows)
        self.assertEqual(report["summary"]["benchmark_cases"], 0)
        self.assertIsNone(report["decisions"][0]["software_response_seconds"])

    def test_invalid_gaze_speak_fails_guard_without_geometric_score(self):
        rows = fixture()
        trace = self.payload(rows[1])
        trace["payload"]["state"]["runtime"]["evidence"]["valid"] = False
        self.replace_payload(rows[1], trace)
        report = self.evaluate(rows)
        checks = {c["name"]: c["status"] for c in report["decisions"][0]["checks"]}
        self.assertEqual(checks["speak_prerequisites"], "fail")
        self.assertEqual(checks["direction_sign_consistency"], "unknown")

    def test_unrelated_playback_is_not_nearest_joined(self):
        rows = fixture()
        rows[3]["guidanceDecisionId"] = "another-decision"
        report = self.evaluate(rows)
        self.assertIsNone(report["decisions"][0]["software_playback_seconds"])
        self.assertNotIn("seq:3", report["decisions"][0]["evidence_ids"])

    def test_versioned_state_is_distinct_from_legacy_phase(self):
        rows = fixture()
        trace = self.payload(rows[1])
        trace["payload"]["state"]["runtime"]["guidanceState"] = {
            "version": "guidance-state-v1", "phase": "Turning"}
        self.replace_payload(rows[1], trace)
        report = self.evaluate(rows)
        self.assertEqual(report["decisions"][0]["guidance_state_status"], "recorded")

    def test_local_decision_reads_nested_recorded_state(self):
        rows = fixture()
        local = dict(rows[1], kind="guidance_decision", detail="",
                     guidanceDecisionId="local1", guidanceDecision={
                         "stage": "requested", "phrase": "You've got it.",
                         "guidanceState": {"version": "guidance-state-v1", "phase": "Acquired"}})
        report = self.evaluate([rows[0], local, rows[-1]])
        self.assertEqual(report["decisions"][0]["kind"], "local")
        self.assertEqual(report["decisions"][0]["guidance_state_status"], "recorded")
        self.assertEqual(report["summary"]["benchmark_cases"], 0)

    def test_default_state_placeholder_is_not_recorded_phase(self):
        for phase in (None, "", "  "):
            with self.subTest(phase=phase):
                rows = fixture()
                trace = self.payload(rows[1])
                trace["payload"]["state"]["runtime"]["guidanceState"] = {
                    "version": "guidance-state-v1", "phase": phase}
                self.replace_payload(rows[1], trace)
                report = self.evaluate(rows)
                self.assertEqual(report["decisions"][0]["guidance_state_status"],
                                 "unavailable_legacy_or_missing")

    def test_proposed_phrase_is_not_spoken_phrase(self):
        rows = fixture()
        rows[3]["text"] = "Actually recorded wording."
        report = self.evaluate(rows)
        row = report["decisions"][0]
        self.assertEqual(row["phrase"], "Look left.")
        self.assertEqual(row["played_phrase"], "Actually recorded wording.")
        self.assertEqual(row["playback_status"], "recorded_start")
        rows[3]["guidanceDecisionId"] = "another-decision"
        report = self.evaluate(rows)
        row = report["decisions"][0]
        self.assertEqual(row["phrase"], "Look left.")
        self.assertIsNone(row["played_phrase"])
        self.assertEqual(row["playback_status"], "not_observed")


if __name__ == "__main__":
    unittest.main()
