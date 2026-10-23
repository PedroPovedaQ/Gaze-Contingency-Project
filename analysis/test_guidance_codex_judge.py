import copy
import fcntl
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from guidance_codex_judge import DIMENSIONS, MODEL, judge_benchmark, _sanitize, _validate, _codex_executable, output_schema


def source_case(number=0, trial="trial-hash"):
    return {"schema": "guidance-benchmark-v1", "case_id": f"source-{number}",
            "source": {"recording_id": "private-participant", "request_sequence": number},
            "group": {"trial_group": trial}, "evidence_ids": [f"seq:{number}"],
            "input": {"payload": {"state": {"relative_yaw_degrees": 20,
                "participant": "DO_NOT_SEND", "api_key": "DO_NOT_SEND",
                "runtime": {"evidence": {"valid": True, "guidanceState": {"phase": "Turning", "targetObjectId": "DO_NOT_SEND"}},
                            "playback": {"last_spoken_phrase": "The object is to the right."}}},
                "questions": {"navigation_phrase": {"instructions": "DO_NOT_SEND", "criteria": {
                    "Look slightly right.": {"tone": "Direct", "geometrically_valid": True},
                    "Look to the right.": {"tone": "Casual"}}}}}}}


def ratings(cases):
    return {"cases": [{"case_id": case["case_id"], "ratings": {
        dimension: {"verdict": "unknown", "rationale": "Insufficient evidence.", "evidence_ids": case["evidence_ids"][:1]}
        for dimension in DIMENSIONS}} for case in cases]}


class Runner:
    def __init__(self, outcome="success", transform=None):
        self.calls = []
        self.outcome = outcome
        self.transform = transform
        self.prompts = []

    def __call__(self, arguments, **kwargs):
        self.calls.append((arguments, kwargs))
        if arguments[1:3] == ["login", "status"]:
            return subprocess.CompletedProcess(arguments, 0, "", "Logged in using ChatGPT")
        if self.outcome == "timeout":
            raise subprocess.TimeoutExpired(arguments, 1)
        if self.outcome == "failure":
            return subprocess.CompletedProcess(arguments, 1, "secret diagnostics", "private failure details")
        if self.outcome == "interrupt":
            raise KeyboardInterrupt
        content = json.loads(kwargs["input"].split("EVIDENCE JSON:\n", 1)[1])
        self.prompts.append(content)
        output = ratings(content["cases"])
        if self.transform:
            self.transform(output)
        Path(arguments[arguments.index("-o") + 1]).write_text(json.dumps(output))
        return subprocess.CompletedProcess(arguments, 0, "", "")


class JudgeTests(unittest.TestCase):
    def test_memory_sanitization_keeps_outcomes_but_not_identity(self):
        from guidance_codex_judge import _state
        raw = {"runtime": {"playback": {"recent_history": {
            "scope": "since_guidance_reset", "speech": [
                {"text": "Look left.", "age_seconds": 2, "outcome": "interrupted", "participant": "PRIVATE"}] * 8,
            "transitions": [{"phase": "Refining", "age_seconds": 1, "targetObjectId": "PRIVATE"}]}},
            "api_key": "PRIVATE"}, "candidate_semantics_defaults": {"horizontal": "left"}}
        clean = _state(raw)
        history = clean["runtime"]["playback"]["recent_history"]
        self.assertEqual(len(history["speech"]), 4)
        self.assertEqual(history["speech"][0]["outcome"], "interrupted")
        self.assertEqual(clean["candidate_semantics_defaults"]["horizontal"], "left")
        self.assertNotIn("PRIVATE", json.dumps(clean))

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.benchmark = self.root / "benchmark.jsonl"
        self.output = self.root / "results"
        self.write([source_case()])
        resolver = patch("guidance_codex_judge._codex_executable", return_value="/test/bin/codex")
        resolver.start()
        self.addCleanup(resolver.stop)

    def write(self, cases):
        self.benchmark.write_text("".join(json.dumps(x) + "\n" for x in cases))

    def test_bounded_selection_preserves_full_artifact_identity_and_reports_scope(self):
        self.write([source_case(0),source_case(1)])
        runner=Runner()
        result=judge_benchmark(self.benchmark,self.output,selected_case_ids=['source-1'],run_process=runner)
        self.assertEqual(result['status'],'success')
        self.assertEqual(result['benchmark_sha256'],hashlib.sha256(self.benchmark.read_bytes()).hexdigest())
        self.assertEqual(result['assessment_scope'],'selected_cases')
        self.assertEqual(result['selected_cases'],1)
        self.assertEqual(result['total_available_cases'],2)
        self.assertEqual(result['batches'][0]['case_ids'],['source-1'])
        bad=judge_benchmark(self.benchmark,self.output,selected_case_ids=['missing'],run_process=runner)
        self.assertEqual(bad['status'],'failed')

    def test_gui_fallback_resolves_executable(self):
        bundled = "/Applications/ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex"
        with patch("guidance_codex_judge.os.path.isfile", side_effect=lambda p: p == bundled), patch("guidance_codex_judge.os.access", return_value=True), patch("guidance_codex_judge.shutil.which", return_value="/old/codex"):
            self.assertEqual(_codex_executable({"PATH": "/usr/bin"}), bundled)
        with patch("guidance_codex_judge.shutil.which", return_value=None), patch("guidance_codex_judge.os.path.isfile", side_effect=lambda p: p == "/opt/homebrew/bin/codex"), patch("guidance_codex_judge.os.access", return_value=True):
            self.assertEqual(_codex_executable({"PATH": "/usr/bin:/bin"}), "/opt/homebrew/bin/codex")
        with patch("guidance_codex_judge.shutil.which", return_value=None), patch("guidance_codex_judge.os.path.isfile", return_value=False):
            self.assertIsNone(_codex_executable({"PATH": "/usr/bin"}))

    def test_allowlist_removes_identifiers_credentials_and_request_instructions(self):
        result = _sanitize(source_case(), {}, 0)
        self.assertNotIn("DO_NOT_SEND", json.dumps(result))
        self.assertNotIn("private-participant", json.dumps(result))
        self.assertEqual(result["state"]["runtime"]["evidence"]["guidanceState"]["phase"], "Turning")
        self.assertEqual(len(result["questions"]["navigation_phrase"]["candidates"]), 2)

    def test_sequence_evidence_survives_sanitization_without_private_fields(self):
        decision = {"event_type": "controller_confirmation", "qualification_status": "qualified",
            "sequence_context": {"private": "DO_NOT_SEND", "recent_playback": [
                {"time": 8, "phrase": "Yep, that's it!", "event_type": "gaze_confirmation",
                 "evidence_ids": ["seq:8"], "participant": "DO_NOT_SEND"}],
                "recent_states": [{"time":8.5,"controller_on_target":True,"gaze_near_target":False,
                    "phase":"TargetFound","evidence_ids":["seq:85"],"targetObjectId":"DO_NOT_SEND"}],
                "recent_actions": [{"time":8.6,"kind":"selection","correct":True,"evidence_ids":["seq:86"]}],
                "confirmation_episode": {"event_type": "controller_confirmation", "wait_count": 2,
                    "qualification_to_playback_seconds": 2.2, "review_budget_seconds": 1,
                    "outcome": "played", "latency_review": "over_budget",
                    "evidence_ids": ["seq:9", "seq:10"], "attempts": [
                        {"time": 9, "transmit": "wait", "phrase": "You're on it!",
                         "evidence_ids": ["seq:9"], "api_key": "DO_NOT_SEND"}]}}}
        result = _sanitize(source_case(), decision, 0)
        self.assertNotIn("DO_NOT_SEND", json.dumps(result))
        self.assertEqual(result["event_type"], "controller_confirmation")
        self.assertEqual(result["sequence_context"]["confirmation_episode"]["wait_count"], 2)
        self.assertIn("seq:10", result["evidence_ids"])
        self.assertIn("seq:85", result["evidence_ids"])
        self.assertIn("seq:86", result["evidence_ids"])
        self.assertTrue(result["sequence_context"]["recent_states"][0]["controller_on_target"])
        self.assertEqual(result["sequence_context"]["recent_playback"][0]["event_type"],
                         "gaze_confirmation")

    def test_success_saved_auth_explicit_model_clean_environment_and_isolated_cwd(self):
        runner = Runner()
        with patch.dict(os.environ, {"OPENAI_API_KEY": "secret", "OPENAI_BASE_URL": "private", "JEV_KEY": "secret", "GIT_DIR": "private"}):
            result = judge_benchmark(self.benchmark, self.output, run_process=runner)
        self.assertEqual(result["status"], "success")
        args, options = runner.calls[-1]
        self.assertEqual(args[args.index("--model") + 1], MODEL)
        for flag in ("--ephemeral", "--ignore-user-config", "--skip-git-repo-check", "--output-schema"):
            self.assertIn(flag, args)
        self.assertEqual(args[args.index("--sandbox") + 1], "read-only")
        self.assertNotIn("shell", options)
        self.assertNotIn("OPENAI_API_KEY", options["env"])
        self.assertNotIn("OPENAI_BASE_URL", options["env"])
        self.assertNotIn("JEV_KEY", options["env"])
        self.assertNotIn("GIT_DIR", options["env"])
        self.assertTrue(options["env"]["PATH"].startswith("/test/bin:"))
        self.assertEqual(args[0], "/test/bin/codex")
        self.assertNotEqual(options["cwd"], str(Path.cwd()))
        self.assertFalse(Path(options["cwd"]).exists())
        self.assertEqual(result["benchmark_sha256"], hashlib.sha256(self.benchmark.read_bytes()).hexdigest())
        self.assertIsNone(result["evaluation_sha256"])
        self.assertFalse(result["authoritative"])

    def test_retained_text_fields_are_redacted_in_outgoing_prompt(self):
        private = ["sk_test0123456789abcdef", "Bearer synthetic-secret",
                   "researcher@example.invalid", "https://example.invalid/private",
                   "/Users/example/private.txt", "/home/example/private.txt"]
        phrase = "Look right. " + " ".join(private)
        case = source_case()
        state = case["input"]["payload"]["state"]
        state["runtime"]["playback"]["last_spoken_phrase"] = phrase
        case["input"]["payload"]["questions"]["navigation_phrase"]["criteria"] = {
            phrase: {"semantics": {"text": phrase, "tone": "Direct"}}}
        self.write([case])
        report = self.root / "evaluation.json"
        report.write_text(json.dumps({"schema": "guidance-evaluation-v1", "decisions": [{
            "case_id": case["case_id"], "phrase": phrase, "played_phrase": phrase,
            "answers": {"navigation_phrase": {"choice": phrase}},
            "evidence_ids": ["seq:0"]}]}))
        runner = Runner()
        result = judge_benchmark(self.benchmark, self.output, evaluation_path=report,
                                 run_process=runner)
        self.assertEqual(result["status"], "success")
        outgoing = runner.calls[-1][1]["input"]
        for secret in private:
            self.assertNotIn(secret, outgoing)
        self.assertIn("[redacted]", outgoing)
        self.assertIn("Look right.", outgoing)

    def test_cache_reused_without_cloud_and_corruption_rejudged(self):
        first = Runner()
        judge_benchmark(self.benchmark, self.output, run_process=first)
        second = Runner()
        result = judge_benchmark(self.benchmark, self.output, run_process=second)
        self.assertEqual(second.calls, [])
        self.assertTrue(result["batches"][0]["cached"])
        next((self.output / "guidance-judge-cache").glob("*.json")).write_text("broken")
        third = Runner()
        result = judge_benchmark(self.benchmark, self.output, run_process=third)
        self.assertEqual(len(third.calls), 2)
        self.assertFalse(result["batches"][0]["cached"])

    def test_input_change_invalidates_cache(self):
        judge_benchmark(self.benchmark, self.output, run_process=Runner())
        case = source_case()
        case["input"]["payload"]["state"]["relative_yaw_degrees"] = -20
        self.write([case])
        runner = Runner()
        result = judge_benchmark(self.benchmark, self.output, run_process=runner)
        self.assertEqual(len(runner.calls), 2)
        self.assertFalse(result["batches"][0]["cached"])

    def test_each_batch_is_trial_bounded(self):
        self.write([source_case(x, "a" if x < 3 else "b") for x in range(5)])
        runner = Runner()
        result = judge_benchmark(self.benchmark, self.output, max_cases_per_batch=2, run_process=runner)
        self.assertEqual([len(x["cases"]) for x in runner.prompts], [2, 1, 2])
        self.assertEqual(result["total_batches"], 3)
        self.assertEqual(result["completed_batches"], 3)

    def test_progress_is_written_before_and_between_batches(self):
        self.write([source_case(0, "a"), source_case(1, "b")])
        runner = Runner()
        progress = []
        def inspect(args, **kwargs):
            saved = json.loads((self.output / "guidance-codex-judge.json").read_text())
            progress.append((saved["status"], saved["completed_batches"]))
            return runner(args, **kwargs)
        judge_benchmark(self.benchmark, self.output, run_process=inspect)
        self.assertEqual(progress, [("pending", 0), ("pending", 0), ("pending", 1)])

    def test_api_key_login_does_not_fallback(self):
        calls = []
        def api_login(args, **kwargs):
            calls.append(args)
            return subprocess.CompletedProcess(args, 0, "Logged in using an API key", "")
        result = judge_benchmark(self.benchmark, self.output, run_process=api_login)
        self.assertEqual(result["status"], "unavailable")
        self.assertEqual(len(calls), 1)

    def test_invalid_judgment_does_not_get_cached(self):
        def bad(output):
            output["cases"][0]["ratings"]["direction"]["evidence_ids"] = ["seq:9999"]
        result = judge_benchmark(self.benchmark, self.output, run_process=Runner(transform=bad))
        self.assertEqual(result["status"], "failed")
        self.assertEqual(result["batches"][0]["reason"], "invalid_judge_result")
        self.assertEqual(result["batches"][0]["validation_reason"], "invented_evidence")
        self.assertFalse((self.output / "guidance-judge-cache").exists())

    def test_output_schema_requires_one_result_per_case(self):
        cases = output_schema(["case-000000", "case-000001"])["properties"]["cases"]
        self.assertEqual(cases["minItems"], 2)
        self.assertEqual(cases["maxItems"], 2)

    def test_request_only_cannot_claim_actual_timing(self):
        case = _sanitize(source_case(), {}, 0)
        output = ratings([case])
        output["cases"][0]["ratings"]["timing"]["verdict"] = "correct"
        with self.assertRaisesRegex(ValueError, "request_only_timing"):
            _validate(output, [case])

    def test_duplicate_cases_and_missing_dimensions_rejected(self):
        inputs = [_sanitize(source_case(), {}, 0), _sanitize(source_case(1), {}, 1)]
        output = ratings(inputs)
        output["cases"][1] = copy.deepcopy(output["cases"][0])
        with self.assertRaises(ValueError):
            _validate(output, inputs)
        output = ratings(inputs)
        del output["cases"][0]["ratings"]["direction"]
        with self.assertRaises(ValueError):
            _validate(output, inputs)

    def test_retrospective_join_is_allowlisted_and_hashed(self):
        report = self.root / "evaluation.json"
        report.write_text(json.dumps({"schema": "guidance-evaluation-v1", "decisions": [{
            "case_id": "source-0", "phrase": "Look slightly right.", "played_phrase": "Look to the right.", "playback_status": "recorded_start", "software_response_seconds": .3,
            "participant": "DO_NOT_SEND", "answers": {"navigation_phrase": {"type": "choice", "choice": "Look slightly right.", "private": "DO_NOT_SEND"}},
            "evidence_ids": ["seq:2"]}]}))
        runner = Runner()
        result = judge_benchmark(self.benchmark, self.output, evaluation_path=report, run_process=runner)
        case = runner.prompts[0]["cases"][0]
        self.assertEqual(case["assessment_scope"], "retrospective")
        self.assertEqual(case["outcome"]["software_response_seconds"], .3)
        self.assertEqual(case["outcome"]["selected_or_proposed_phrase"], "Look slightly right.")
        self.assertEqual(case["outcome"]["played_phrase"], "Look to the right.")
        self.assertEqual(case["outcome"]["playback_status"], "recorded_start")
        self.assertNotIn("DO_NOT_SEND", json.dumps(case))
        self.assertEqual(result["evaluation_sha256"], hashlib.sha256(report.read_bytes()).hexdigest())

    def test_terminal_provider_failure_stops_remaining_cloud_calls(self):
        self.write([source_case(0, "a"), source_case(1, "b")])
        messages = {"cli_upgrade_required": "This model requires a newer version of Codex.",
                    "authentication_required": "401 unauthorized", "quota_or_rate_limit": "You hit the usage limit",
                    "requested_model_unavailable": "Model requested is not available"}
        for reason, message in messages.items():
            with self.subTest(reason=reason):
                calls = []
                def fail(args, **kwargs):
                    calls.append(args)
                    if args[1] == "login":
                        return subprocess.CompletedProcess(args, 0, "Logged in using ChatGPT", "")
                    return subprocess.CompletedProcess(args, 1, "", message + " secret raw provider data")
                result = judge_benchmark(self.benchmark, self.root / reason, run_process=fail)
                self.assertEqual(len(calls), 2)
                self.assertEqual(result["batches"][0]["reason"], reason)
                self.assertEqual(result["batches"][1]["status"], "unavailable")
                self.assertEqual(result["batches"][1]["reason"], reason)
                self.assertNotIn("secret raw", json.dumps(result))

    def test_timeout_failure_and_interruption_are_explicit(self):
        for mode, reason in (("timeout", "codex_timeout"), ("failure", "codex_exit_nonzero"), ("interrupt", "judge_interrupted")):
            with self.subTest(mode=mode):
                result = judge_benchmark(self.benchmark, self.root / mode, run_process=Runner(mode))
                self.assertEqual(result["status"], "failed")
                self.assertEqual(result.get("reason") or result["batches"][0]["reason"], reason)
                self.assertNotIn("secret diagnostics", json.dumps(result))

    def test_directory_lock_does_not_overwrite_active_results(self):
        self.output.mkdir()
        current = self.output / "guidance-codex-judge.json"
        current.write_text('{"status":"pending","completed_batches":2}')
        with (self.output / ".guidance-codex-judge.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            result = judge_benchmark(self.benchmark, self.output, run_process=Runner())
        self.assertEqual(result["reason"], "already_running")
        self.assertEqual(json.loads(current.read_text())["completed_batches"], 2)

    def test_no_cases_and_bad_input_do_not_run_cloud(self):
        for text, status in (("", "unavailable"), ("bad-json", "failed")):
            self.benchmark.write_text(text)
            runner = Runner()
            result = judge_benchmark(self.benchmark, self.output, run_process=runner)
            self.assertEqual(result["status"], status)
            self.assertEqual(runner.calls, [])


if __name__ == "__main__":
    unittest.main()
