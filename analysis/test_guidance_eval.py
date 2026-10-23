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


def confirmation_sequence():
    rows = [fixture()[0]]
    for n, (time, choice) in enumerate(((10, "wait"), (11, "wait"), (12, "speak"))):
        state = {"reason": "gaze_target_vicinity_hold", "proposed_phrase": "You've got it!",
                 "runtime": {"evidence": {"valid": True, "targetGazeQualified": True},
                             "playback": {"is_speaking": False, "is_busy": False}}}
        for offset, stage in ((0, "request"), (1, "response")):
            data = dict(request_id=f"d{n}", stage=stage)
            if stage == "request":
                data["payload"] = {"state": state, "questions": {
                    "transmit": {"type": "choice", "criteria": {"speak": "", "wait": ""}}}}
            else:
                data.update(status="ok", answers={"transmit": {"choice": choice}})
            rows.append(dict(fixture()[1], sequence=1+n*2+offset, time=time+offset*.2,
                             detail="jev;" + json.dumps(data)))
    rows.append(dict(fixture()[3], sequence=7, time=12.3, guidanceDecisionId="d2",
                     text="You've got it!"))
    rows.append(dict(fixture()[-1], sequence=8, time=13))
    return rows


def recovered_samples(rows):
    rows.extend(dict(rows[0], time=tick/10, kind="sample",
                     gazeAvailable=True, gazeTracked=1) for tick in range(106, 124))
    rows.sort(key=lambda row: row["time"])
    for sequence, row in enumerate(rows):
        row["sequence"] = sequence
    return rows


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

    def test_action_choice_resolves_text_but_does_not_invent_playback(self):
        rows = fixture()
        request = self.payload(rows[1])
        request["payload"]["questions"] = {"navigation_action": {"type": "choice", "criteria": {
            "encourage": {"text": "Keep going left."}, "wait": {"text": None}}}}
        self.replace_payload(rows[1], request)
        response = self.payload(rows[2])
        response["answers"] = {"navigation_action": {"choice": "encourage"}}
        self.replace_payload(rows[2], response)
        del rows[3]  # A proposed action is not evidence of audible speech.
        result = self.evaluate(rows)["decisions"][0]
        self.assertEqual(result["phrase"], "Keep going left.")
        self.assertIsNone(result["played_phrase"])
        self.assertEqual(result["playback_status"], "not_observed")
        self.assertEqual(result["checks"][0]["status"], "pass")

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

    def test_v2_audio_gate_state_is_recognized(self):
        from guidance_eval import central_state_status, qualification
        state = {"runtime": {"evidence": {"guidanceState": {
            "version": "guidance-state-v2", "phase": "Acquired", "allowedAction": "ConfirmGaze",
            "newGuidanceAllowed": True, "gazeValid": True}}}}
        self.assertEqual(central_state_status({"state": state}), "recorded")
        self.assertEqual(qualification(state, "gaze_confirmation"), "qualified")

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
        self.assertEqual(report["summary"]["review_cases"], 1)
        cases=[json.loads(line) for line in (self.root/"out"/"guidance-review-cases.jsonl").read_text().splitlines()]
        self.assertEqual(cases[0]["source"]["decision_origin"],"local")
        self.assertEqual(cases[0]["input"]["payload"]["state"]["runtime"]["guidanceState"]["phase"],"Acquired")

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

    def test_sequence_delay_includes_both_waits_not_just_last_request(self):
        report = self.evaluate(confirmation_sequence())
        episode = report["confirmation_sequences"][0]
        self.assertAlmostEqual(episode["qualification_to_playback_seconds"], 2.3)
        self.assertEqual(episode["wait_count"], 2)
        self.assertEqual(episode["latency_review"], "over_budget")
        self.assertEqual(episode["outcome"], "played")
        self.assertEqual(len(episode["attempts"]), 3)
        for decision in report["decisions"]:
            self.assertEqual(decision["sequence_context"]["confirmation_episode"], episode)
        self.assertAlmostEqual(report["decisions"][-1]["software_playback_seconds"], .3)

    def test_controller_confirmation_has_own_qualification_and_history(self):
        rows = confirmation_sequence()
        state = self.payload(rows[5])
        state["payload"]["state"].update(reason="controller_target_gaze_vicinity",
                                        proposed_phrase="You're on it!")
        state["payload"]["state"]["runtime"]["evidence"] = dict(
            valid=True, targetGazeQualified=False, controllerEvidenceValid=True,
            controllerOnTarget=True, controllerTargetHoldQualified=True,
            gazeWithinTargetVicinity=True)
        self.replace_payload(rows[5], state)
        report = self.evaluate(rows)
        self.assertEqual(report["decisions"][-1]["event_type"], "controller_confirmation")
        self.assertEqual(report["decisions"][-1]["qualification_status"], "qualified")
        episode = report["decisions"][-1]["sequence_context"]["confirmation_episode"]
        self.assertAlmostEqual(episode["qualification_to_playback_seconds"], .3)
        self.assertEqual(episode["wait_count"], 0)
        self.assertEqual(report["confirmation_sequences"][0]["outcome"], "disqualified")

    def test_invalid_gaze_pause_and_new_trial_break_qualification_chain(self):
        for kind in ("sample", "search_paused", "trial"):
            with self.subTest(kind=kind):
                rows = confirmation_sequence()
                marker = dict(rows[0], sequence=3, time=10.5, kind=kind, gazeAvailable=False)
                for row in rows[3:]:
                    row["sequence"] += 1
                rows.insert(3, marker)
                report = self.evaluate(recovered_samples(rows))
                episode = report["decisions"][-1]["sequence_context"]["confirmation_episode"]
                self.assertAlmostEqual(episode["qualification_to_playback_seconds"], 1.3)
                self.assertNotEqual(report["confirmation_sequences"][0]["outcome"], "played")

    def test_missing_playback_does_not_become_slow_failure(self):
        rows = confirmation_sequence()
        del rows[-2]
        report = self.evaluate(rows)
        episode = report["confirmation_sequences"][0]
        self.assertIsNone(episode["qualification_to_playback_seconds"])
        self.assertEqual(episode["latency_review"], "unknown")

    def test_available_device_with_untracked_gaze_breaks_episode(self):
        rows = confirmation_sequence()
        for row in rows[3:]:
            row["sequence"] += 1
        rows.insert(3, dict(rows[0], sequence=3, time=10.5, kind="sample",
                            gazeAvailable=True, gazeTracked=0))
        report = self.evaluate(recovered_samples(rows))
        self.assertEqual(report["confirmation_sequences"][0]["outcome"], "invalid_gaze")
        self.assertAlmostEqual(report["decisions"][-1]["sequence_context"]
                               ["confirmation_episode"]["qualification_to_playback_seconds"], 1.3)

    def test_recorded_snapshot_departure_breaks_episode(self):
        for winner in ("distractor", ""):
            with self.subTest(winner=winner):
                rows = confirmation_sequence()
                for row in rows[3:]:
                    row["sequence"] += 1
                rows.insert(3, dict(rows[0], sequence=3, time=10.5, kind="sample",
                    gazeAvailable=True, gazeTracked=1, guidanceState={
                        "version": "guidance-state-v1", "phase": "Refining", "gazeValid": True,
                        "targetObjectId": "target", "vicinityWinnerObjectId": winner}))
                report = self.evaluate(rows)
                self.assertEqual(report["confirmation_sequences"][0]["outcome"], "disqualified")

    def test_sample_gap_before_playback_does_not_wait_for_next_sample(self):
        rows = confirmation_sequence()
        for row in rows[3:]:
            row["sequence"] += 1
        rows.insert(3, dict(rows[0], sequence=3, time=10.25, kind="sample",
                            gazeAvailable=True, gazeTracked=1))
        report = self.evaluate(rows)
        self.assertTrue(all(e["outcome"] != "played" for e in report["confirmation_sequences"]))

    def test_duplicate_identity_cannot_start_or_close_sequence(self):
        rows = confirmation_sequence()
        duplicate = dict(rows[1], time=10.5)
        rows.insert(3, duplicate)
        for sequence, row in enumerate(rows):
            row["sequence"] = sequence
        report = self.evaluate(rows)
        self.assertIsNone(report["decisions"][0]["sequence_context"]["confirmation_episode"])
        self.assertEqual(report["confirmation_sequences"][0]["first_qualified_request_time"], 11)
        rows[-2]["guidanceDecisionId"] = "d0"
        report = self.evaluate(rows)
        self.assertTrue(all(e["outcome"] != "played" for e in report["confirmation_sequences"]))

    def test_local_priority_event_uses_recorded_state_action(self):
        rows = confirmation_sequence()
        local = dict(rows[1], kind="guidance_decision", detail="", guidanceDecisionId="d2",
            guidanceDecision={"stage": "requested", "reason": "gaze_target_vicinity_hold",
                "phrase": "You've got it!", "guidanceState": {
                    "version": "guidance-state-v1", "phase": "Acquired",
                    "allowedAction": "ConfirmGaze", "gazeValid": True}})
        start = dict(rows[-2], time=10.04)
        report = self.evaluate([rows[0], local, start, rows[-1]])
        episode = report["confirmation_sequences"][0]
        self.assertAlmostEqual(episode["qualification_to_playback_seconds"], .04)
        self.assertEqual(episode["latency_review"], "within_budget")

    def test_first_observed_qualification_includes_pre_request_wait(self):
        rows=confirmation_sequence()
        sample=dict(rows[0],time=9.9,kind='sample',gazeAvailable=True,gazeTracked=1,
            guidanceState={'version':'guidance-state-v1','phase':'GazeConfirmed',
                'allowedAction':'ConfirmGaze','gazeValid':True,'targetObjectId':'t','vicinityWinnerObjectId':'t'})
        rows.extend(dict(sample,time=t/10) for t in range(99,124))
        rows.sort(key=lambda row:row['time'])
        for i,row in enumerate(rows): row['sequence']=i
        report=self.evaluate(rows);episode=report['confirmation_sequences'][0]
        self.assertAlmostEqual(episode['first_observed_qualification_time'],9.9)
        self.assertAlmostEqual(episode['observed_qualification_to_playback_seconds'],2.4)
        self.assertAlmostEqual(episode['qualification_to_playback_seconds'],2.3)
        self.assertEqual(report['summary']['first_observed_latency_reviews']['over_budget'],1)

    def test_playback_state_uses_only_prior_fresh_observation(self):
        rows=fixture()
        snapshot={'phase':'PointingTarget','targetObjectId':'t','controllerHitObjectId':'t',
            'controllerValid':True,'gazeValid':True,'vicinityWinnerObjectId':'other'}
        rows.insert(3,dict(rows[0],sequence=3,time=2.4,kind='sample',guidanceState=snapshot))
        for i,row in enumerate(rows): row['sequence']=i
        state=self.evaluate(rows)['decisions'][0]['sequence_context']['playback_state']
        self.assertTrue(state['controller_on_target']);self.assertAlmostEqual(state['age_seconds'],.1)
        rows[4]['time']=2.8
        self.assertIsNone(self.evaluate(rows)['decisions'][0]['sequence_context']['playback_state'])
        rows[4]['time']=2.5; rows[3]['time']=2.5; rows[3]['sequence']=5
        rows.sort(key=lambda x:(x['time'],x['sequence']))
        for i,row in enumerate(rows): row['sequence']=i
        self.assertIsNone(self.evaluate(rows)['decisions'][0]['sequence_context']['playback_state'])

    def test_recent_state_and_actions_do_not_conflate_pointing_with_completion(self):
        rows=confirmation_sequence()
        rows.append(dict(rows[0],sequence=90,time=9.5,kind="sample",guidanceState={
            "phase":"WithinTargetWall","allowedAction":"None","targetObjectId":"target",
            "controllerHitObjectId":"target","vicinityWinnerObjectId":"other",
            "gazeValid":True,"controllerValid":True}))
        rows.append(dict(rows[0],sequence=91,time=9.6,kind="audio_cancelled"))
        rows.sort(key=lambda row:row["time"])
        for i,row in enumerate(rows): row['sequence']=i
        context=self.evaluate(rows)['decisions'][0]['sequence_context']
        self.assertTrue(context['recent_states'][0]['controller_on_target'])
        self.assertFalse(context['recent_states'][0]['gaze_near_target'])
        self.assertEqual(context['recent_actions'][0]['kind'],'audio_cancelled')
        self.assertNotIn('targetObjectId',context['recent_states'][0])
        self.assertFalse(any(x['kind']=='selection' for x in context['recent_actions']))

    def test_recent_playback_is_trial_scoped_and_preserves_event_kind(self):
        rows = confirmation_sequence()
        for row in rows[1:]:
            row["sequence"] += 1
        rows.insert(1, dict(fixture()[3], sequence=1, time=9, text="Look right.",
                            guidanceDecisionId="previous"))
        report = self.evaluate(rows)
        history = report["decisions"][0]["sequence_context"]["recent_playback"]
        self.assertEqual(history[-1]["phrase"], "Look right.")
        rows[1]["sourceTrialId"] = "another-trial"
        report = self.evaluate(rows)
        self.assertEqual(report["decisions"][0]["sequence_context"]["recent_playback"], [])


if __name__ == "__main__":
    unittest.main()
