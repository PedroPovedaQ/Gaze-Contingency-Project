import csv
import json
import math
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

from trial_csv import export, read_summary, missed_target_summary


def vector(x=0, y=0, z=0):
    return {"x": x, "y": y, "z": z}


def yaw(degrees):
    half = math.radians(degrees) / 2
    return {"x": 0, "y": math.sin(half), "z": 0, "w": math.cos(half)}


class TrialCSVTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.run = self.root / "run_001_test"
        self.run.mkdir()
        self.tid = "P001_run001_r00"
        self.summary = {
            "participant_id": "P001",
            "run_number": 1,
            "session_id": "P001_run001",
            "rounds_per_block": 40,
            "voice_order": "selfsimilar_then_neutral",
            "objectives": [
                {
                    "index": 0,
                    "trial_id": self.tid,
                    "block": 0,
                    "voice_condition": "selfsimilar",
                    "outcome": "completed",
                    "completed": True,
                    "search_started_at": 100,
                    "capture_at": 105,
                    "search_exposure_seconds": 3,
                    "wrong_captures": 1,
                },
                {
                    "index": 1,
                    "trial_id": "P001_run001_r01",
                    "block": 0,
                    "voice_condition": "selfsimilar",
                    "outcome": "not_started",
                    "completed": False,
                    "search_started_at": -1,
                },
            ],
        }
        self.package = self.root / "replays" / "recording"
        self.package.mkdir(parents=True)
        self.records = [
            {"kind": "header", "recordingId": "rec", "time": 0},
            {
                "kind": "trial",
                "time": 1,
                "targetId": "target",
                "startPlane": 0,
                "objects": [
                    {
                        "id": "target",
                        "target": True,
                        "position": vector(0, 2, 3),
                        "shape": "Cube",
                        "color": "Blue",
                    }
                ],
            },
            {"kind": "search_start", "time": 2, "searchSeconds": 0},
            self.sample(2, 0, 0, 179),
            self.sample(2.1, 0.1, 1, -179, hit=True),
            {
                "kind": "audio_request",
                "time": 2.15,
                "detail": "tip;",
                "searchSeconds": 0.15,
            },
            {
                "kind": "audio_playback_start",
                "time": 2.2,
                "detail": "tip;text=Look up",
                "searchSeconds": 0.2,
            },
            {
                "kind": "audio_cancelled",
                "time": 2.25,
                "detail": "tip;",
                "searchSeconds": 0.25,
            },
            {"kind": "search_paused", "time": 3, "searchSeconds": 1},
            self.sample(4, 1, 99, 0, active=False),
            {"kind": "search_resumed", "time": 5, "searchSeconds": 1},
            self.sample(5, 1, 10, -179),
            {
                "kind": "selection",
                "time": 6,
                "searchSeconds": 2,
                "correct": False,
                "objectId": "other",
            },
            {
                "kind": "selection",
                "time": 7,
                "searchSeconds": 3,
                "correct": True,
                "objectId": "target",
                "selectingHand": "right",
            },
            {
                "kind": "audio_playback_start",
                "time": 7.1,
                "detail": "congratulations;",
                "searchSeconds": 3,
            },
            {"kind": "end", "time": 8, "complete": True},
        ]
        self.save()

    def tearDown(self):
        self.temp.cleanup()

    def sample(self, time, elapsed, x, angle, hit=False, active=True):
        return {
            "kind": "sample",
            "time": time,
            "searchSeconds": elapsed,
            "searchActive": active,
            "headAvailable": True,
            "headTracked": 1,
            "headPosition": vector(x, 1, 0),
            "headRotation": yaw(angle),
            "gazeAvailable": True,
            "gazeTracked": 1,
            "gazeOrigin": vector(0, 1, 0),
            "gazeDirection": vector(0, 0, 1),
            "hoveredId": "target" if hit else "",
            "rightController": {
                "available": True,
                "rayAvailable": True,
                "tracked": 1,
                "hoveredId": "target" if hit else "",
            },
        }

    def save(self, tail=""):
        (self.run / "trial_summary.json").write_text(json.dumps(self.summary))
        with (self.package / "recording.jsonl").open("w") as f:
            for seq, source in enumerate(self.records):
                record = {
                    "schema": 2,
                    "sequence": seq,
                    "frame": seq,
                    "participantCode": "P001",
                    "runNumber": 1,
                    "sourceRunFolder": self.run.name,
                    "sourceTrialId": self.tid,
                    "practice": False,
                }
                record.update(source)
                f.write(json.dumps(record) + "\n")
            f.write(tail)

    def survey(self, name, rows):
        p = self.root / name
        with p.open("w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0]))
            w.writeheader()
            w.writerows(rows)
        return p

    def read(self, output="export", **kwargs):
        target = self.root / output
        report = export(self.run, target, self.root / "replays", **kwargs)
        with (target / "trials.csv").open(newline="") as f:
            rows = list(csv.DictReader(f))
        return rows, report

    def test_participant_ipd_export_and_legacy_missing(self):
        self.save()
        rows, _ = self.read('legacy-ipd')
        self.assertEqual(rows[0]['participant_ipd_mm'], '')
        self.assertEqual(rows[0]['participant_ipd_source'], 'not_recorded')
        self.summary['participant_fit'] = dict(ipdRecorded=True, participantIpdMm=63.5, ipdSource='ruler',
                                              headsetIpdRecorded=True, headsetIpdMm=64)
        self.save()
        rows, _ = self.read('with-ipd')
        self.assertEqual(rows[0]['participant_ipd_mm'], '63.5')
        self.assertEqual(rows[0]['participant_ipd_source'], 'ruler')
        self.assertEqual(rows[0]['headset_lens_spacing_mm'], '64')
        self.summary['participant_fit']['participantIpdMm'] = float('nan')
        self.save()
        with self.assertRaises(ValueError): self.read('invalid-ipd')

    def test_exact_precue_snapshot_export_and_legacy_blanks(self):
        from test_theta_snapshot import snapshot
        self.summary["objectives"][0]["pre_cue_theta"] = snapshot()
        self.save()
        rows, _ = self.read()
        row = next(r for r in rows if r["trial_id"] == self.tid)
        self.assertEqual(row["pre_cue_theta_realized_signed_deg"], "90")
        self.assertEqual(row["pre_cue_theta_assignment_error_deg"], "0")
        self.assertEqual(row["pre_cue_theta_eye_origin_x_m"], "0.2")
        self.assertEqual(row["pre_cue_theta_realtime_seconds"], "15.25")
        self.assertEqual(row["pre_cue_theta_coordinate_frame"], "unity_world")
        legacy = next(r for r in rows if r["trial_id"] != self.tid and r["is_practice"] == "0")
        self.assertEqual(legacy["pre_cue_theta_status"], "not_recorded")
        self.assertEqual(legacy["pre_cue_theta_realized_signed_deg"], "")
        for path in (self.root / "export").glob("trial_*_performance_subjective.csv"):
            with path.open() as stream:
                per_trial = list(csv.DictReader(stream))[0]
            if per_trial["trial_id"] == self.tid:
                self.assertEqual(per_trial["pre_cue_theta_eye_origin_x_m"], "0.2")

    def test_corrupt_snapshot_cannot_publish_export(self):
        from test_theta_snapshot import snapshot
        self.summary["objectives"][0]["pre_cue_theta"] = snapshot(index=7)
        self.save()
        with self.assertRaisesRegex(ValueError, "round mismatch"):
            self.read()
        self.assertFalse((self.root / "export").exists())

    def test_25_second_timeout_keeps_recorded_deadline(self):
        self.summary["active_search_limit_seconds"] = 25
        self.summary["objectives"][0].update(outcome="timeout", completed=False,
            capture_at=-1, search_exposure_seconds=25)
        self.records = [r for r in self.records if r.get("time", 0) < 6]
        self.records += [{"kind": "trial_timeout", "time": 30, "searchSeconds": 25,
                          "searchActive": False, "detail": "active_search_limit_seconds=25"},
                         {"kind": "end", "time": 31, "complete": True}]
        self.save()
        rows, report = self.read()
        self.assertEqual(float(rows[0]["active_search_limit_seconds"]), 25)
        self.assertEqual(float(rows[0]["search_exposure_seconds"]), 25)
        self.assertEqual(rows[0]["missed_target"], "1")
        self.assertEqual(report["missed_targets"][0]["active_search_limit_seconds"], 25)

    def test_timeout_is_terminal_without_selection_and_survives_late_audio(self):
        self.summary["active_search_limit_seconds"] = 15
        self.summary["objectives"][0].update(outcome="timeout", completed=False,
            capture_at=-1, search_exposure_seconds=15)
        self.records = [r for r in self.records if r.get("time", 0) < 6]
        self.records += [
            {"kind": "trial_timeout", "time": 20, "searchSeconds": 15, "searchActive": False},
            {"kind": "audio_playback_start", "time": 20.1, "searchSeconds": 15,
             "detail": "trial_timeout;text=Time up. Let's start the next round."},
            {"kind": "selection", "time": 20.2, "searchSeconds": 15, "correct": True},
            {"kind": "end", "time": 21, "complete": True},
        ]
        self.save()
        rows, report = self.read()
        self.assertEqual(rows[0]["missed_target"], "1")
        self.assertEqual(rows[1]["missed_target"], "")
        group = report["missed_targets"][0]
        self.assertEqual(group["missed_target_count"], 1)
        self.assertEqual(group["missed_target_denominator"], 1)
        self.assertEqual(group["other_or_unresolved_count"], 1)
        self.assertEqual(group["missed_target_percentage"], 100)
        self.assertTrue((self.root / "export" / "missed_targets.csv").exists())
        self.assertEqual(rows[0]["outcome"], "timeout")
        self.assertEqual(rows[0]["completed"], "0")
        self.assertEqual(float(rows[0]["search_exposure_seconds"]), 15)
        self.assertEqual(rows[0]["time_to_final_selection_seconds"], "")

    def test_missed_target_rate_keeps_practice_failure_legacy_and_deadlines_separate(self):
        base = dict(participant_id="P1", run_number=1, block_index=0,
                    voice_condition="selfsimilar", active_search_limit_seconds=15,
                    is_practice=False)
        rows = [{**base, "missed_target": value} for value in (0, 1, None, None)]
        rows += [{**base, "missed_target": 1, "is_practice": True},
                 {**base, "missed_target": 0, "active_search_limit_seconds": None},
                 {**base, "missed_target": 1, "active_search_limit_seconds": 45},
                 {**base, "missed_target": 0, "block_index": 1, "voice_condition": "neutral"}]
        groups = missed_target_summary(rows)
        self.assertEqual(len(groups), 4)
        self.assertEqual(groups[0]["missed_target_percentage"], 50)
        self.assertEqual(groups[0]["missed_target_denominator"], 2)
        self.assertEqual(groups[0]["other_or_unresolved_count"], 2)
        self.assertIsNone(groups[1]["missed_target_percentage"])
        self.assertEqual(groups[1]["missed_target_denominator"], 0)
        self.assertEqual(groups[2]["active_search_limit_seconds"], 45)
        self.assertEqual(groups[3]["missed_target_percentage"], 0)

    def test_technical_failure_is_not_a_missed_target(self):
        self.summary["active_search_limit_seconds"] = 15
        self.summary["objectives"][0].update(outcome="technical_failure", completed=False, capture_at=-1)
        self.records = [r for r in self.records if r.get("time", 0) < 6]
        self.save()
        rows, report = self.read()
        self.assertEqual(rows[0]["missed_target"], "")
        self.assertIsNone(report["missed_targets"][0]["missed_target_percentage"])
        self.assertEqual(report["missed_targets"][0]["missed_target_denominator"], 0)

    def test_outputs_one_row_files_counts_clocks_and_geometry(self):
        rows, report = self.read()
        a, b = rows
        self.assertEqual(len(rows), 2)
        self.assertEqual(a["time_to_final_selection_seconds"], "3")
        self.assertEqual(a["time_to_first_target_gaze_seconds"], "0.1")
        self.assertEqual(a["time_to_first_right_controller_target_seconds"], "0.1")
        self.assertEqual(a["instruction_count"], "1")
        self.assertEqual(a["interrupted_instruction_count"], "1")
        self.assertEqual(a["incorrect_selection_count"], "1")
        self.assertAlmostEqual(float(a["target_distance_m"]), math.sqrt(10))
        self.assertEqual(a["target_minus_head_height_m"], "1")
        self.assertEqual(b["instruction_count"], "")
        self.assertEqual(b["time_to_final_selection_seconds"], "")
        self.assertEqual(a["survey_status"], "pending_or_not_collected")
        for trial in report["trials"]:
            with (self.root / "export" / trial["file"]).open(newline="") as f:
                self.assertEqual(len(list(csv.DictReader(f))), 1)
        self.assertEqual(report["trial_count"], 2)
        self.assertFalse((self.run / "trials.csv").exists())

    def test_motion_wrap_and_no_bridging_pause(self):
        a = self.read()[0][0]
        self.assertAlmostEqual(float(a["gross_head_translation_m"]), 1)
        self.assertAlmostEqual(float(a["gross_head_rotation_deg"]), 2)
        self.assertAlmostEqual(float(a["gross_head_rotation_y_deg"]), 2)
        self.assertEqual(a["head_motion_interval_count"], "1")

    def test_selection_pose_retains_final_motion_without_inflating_frame_counts(self):
        selection = dict(self.sample(2.15, .15, 1.4, -177), kind='selection', correct=True,
                         objectId='target', selectingHand='right')
        self.records = self.records[:5] + [selection, self.sample(2.2, .2, 99, 0),
                                          dict(kind='end', time=3, complete=True)]
        self.save()
        row = self.read()[0][0]
        self.assertAlmostEqual(float(row['gross_head_translation_m']), 1.4)
        self.assertAlmostEqual(float(row['gross_head_rotation_deg']), 4)
        self.assertEqual(row['head_motion_interval_count'], '2')
        self.assertAlmostEqual(float(row['head_motion_covered_seconds']), .15)
        self.assertEqual(row['head_motion_observation_count'], '3')
        self.assertEqual(row['head_motion_method'], 'tracked_frame_and_selection_intervals_v2')
        self.assertEqual(row['gaze_total_samples'], '2')

    def test_selection_motion_keeps_invalid_gaps_and_legacy_absence_separate(self):
        baseline = self.records[:5]
        for case in ('invalid', 'missing', 'late'):
            with self.subTest(case=case):
                selection = dict(self.sample(2.15, .15, 99, 0), kind='selection', correct=True,
                                 objectId='target', selectingHand='right')
                if case == 'invalid': selection['headTracked'] = 0
                if case == 'missing': selection.pop('headAvailable')
                if case == 'late': selection.update(time=2.5, searchSeconds=.5)
                self.records = baseline + [selection, dict(kind='end', time=3, complete=True)]
                self.save()
                row = self.read('export-'+case)[0][0]
                self.assertAlmostEqual(float(row['gross_head_translation_m']), 1)
                self.assertEqual(row['head_motion_interval_count'], '1')
                self.assertEqual(row['head_motion_observation_count'], '2' if case == 'missing' else '3')

    def test_first_tracked_selection_geometry_is_explicit_and_not_zero_motion(self):
        selection = dict(self.sample(2.05, .05, 0, 0), kind='selection', correct=True,
                         objectId='target', selectingHand='right')
        self.records = self.records[:3] + [selection, dict(kind='end', time=3, complete=True)]
        self.save()
        row = self.read()[0][0]
        self.assertEqual(row['geometry_reference'], 'first_tracked_head_selection_after_search_start')
        self.assertAlmostEqual(float(row['target_distance_m']), math.sqrt(10))
        self.assertEqual(row['gross_head_translation_m'], '')
        self.assertEqual(row['gaze_total_samples'], '0')

    def test_wrong_selection_retains_excursion_and_invalid_selection_breaks_motion(self):
        original = list(self.records)
        wrong = dict(self.sample(2.05, .05, 2, 180), kind='selection', correct=False,
                     objectId='other', selectingHand='right')
        self.records = original[:4] + [wrong] + original[4:]
        self.save()
        row = self.read('wrong-pose')[0][0]
        self.assertAlmostEqual(float(row['gross_head_translation_m']), 3)
        self.assertEqual(row['head_motion_interval_count'], '2')
        wrong['headTracked'] = 0
        self.save()
        row = self.read('invalid-wrong-pose')[0][0]
        self.assertEqual(row['gross_head_translation_m'], '')
        self.assertEqual(row['head_motion_interval_count'], '0')

    def test_unknown_tracking_does_not_become_valid_gaze(self):
        self.records[4]["gazeTracked"] = -1
        self.records[4]["rightController"]["tracked"] = -1
        self.save()
        a = self.read()[0][0]
        self.assertEqual(a["time_to_first_target_gaze_seconds"], "")
        self.assertEqual(a["time_to_first_right_controller_target_seconds"], "")
        self.assertEqual(a["gaze_unknown_tracking_samples"], "1")

    def test_block_survey_repeated_with_scope_and_raw_response(self):
        p = self.survey(
            "reliance.csv",
            [
                {
                    "participant_id": "P001",
                    "run_number": 1,
                    "block": 0,
                    "voice_condition": "selfsimilar",
                    "response_id": "R01",
                    "instrument_version": "draft1",
                    "reliance": 0,
                    "comment": 'A comma, and "quotes"\nnew line',
                }
            ],
        )
        rows, _ = self.read(survey_paths=[p])
        for row in rows:
            self.assertEqual(row["survey__reliance__block__reliance"], "0")
            self.assertEqual(row["survey__reliance__block__response_id"], "R01")
            self.assertEqual(row["survey__reliance__block__scope"], "block")
            self.assertEqual(
                row["survey__reliance__block__comment"],
                'A comma, and "quotes"\nnew line',
            )
        self.assertEqual(rows[0]["survey_status"], "available")

    def test_duplicate_or_wrong_voice_survey_fails_without_output(self):
        row = {
            "participant_id": "P001",
            "run_number": 1,
            "block": 0,
            "voice_condition": "neutral",
            "score": 1,
        }
        p = self.survey("bad.csv", [row])
        with self.assertRaisesRegex(ValueError, "voice"):
            self.read(survey_paths=[p])
        self.assertFalse((self.root / "export").exists())
        row["voice_condition"] = "selfsimilar"
        self.survey("bad.csv", [row, row])
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.read(survey_paths=[p])

    def test_missing_replay_leaves_unmeasured_fields_blank(self):
        for r in self.records:
            r["sourceRunFolder"] = "wrong_run"
        self.save()
        a = self.read()[0][0]
        self.assertEqual(a["time_to_final_selection_seconds"], "3")
        self.assertEqual(a["time_to_first_target_gaze_seconds"], "")
        self.assertEqual(a["instruction_count"], "")
        self.assertEqual(a["gross_head_translation_m"], "")
        self.assertIn("no_matching_replay", a["quality_flags"])

    def test_truncated_recording_preserves_incomplete_outcomes(self):
        self.records = self.records[:12]
        self.summary["objectives"][0].update(
            completed=False, outcome="interrupted", search_exposure_seconds=2
        )
        self.save(tail='{"schema":2')
        a, report = self.read()
        self.assertEqual(a[0]["time_to_final_selection_seconds"], "")
        self.assertIn("replay_incomplete", a[0]["quality_flags"])
        self.assertTrue(report["warnings"])

    def test_practice_has_separate_identity_and_no_block_survey(self):
        measured = self.records[1:3]
        practice = [
            dict(
                r,
                practice=True,
                sourceRunFolder="",
                sourceTrialId="",
                runNumber=0,
                trialId="practice_001",
            )
            for r in measured
        ]
        # Place practice before the measured trial with monotonically ordered times.
        practice[0]["time"] = 0.3
        practice[1]["time"] = 0.4
        self.records[1:1] = practice
        self.save()
        rows, _ = self.read()
        p = next(r for r in rows if r["is_practice"] == "1")
        self.assertEqual(p["trial_id"], "rec:practice_001")
        self.assertEqual(p["block_index"], "")
        self.assertEqual(p["survey_status"], "not_applicable_practice")
        self.assertTrue(
            all(".." not in f.name for f in (self.root / "export").glob("trial_*.csv"))
        )

    def test_overwrite_refused_sources_unchanged(self):
        original = (self.run / "trial_summary.json").read_bytes()
        self.read()
        with self.assertRaises(FileExistsError):
            self.read()
        self.assertEqual(original, (self.run / "trial_summary.json").read_bytes())

    def test_large_sample_gap_is_not_counted_as_motion(self):
        self.records[4]["time"] = 2.11
        self.save()
        a = self.read()[0][0]
        self.assertEqual(a["gross_head_translation_m"], "")
        self.assertEqual(a["head_motion_gap_count"], "1")
        self.assertIn("sample_gaps_not_bridged", a["quality_flags"])

    def test_prefetch_cancellation_does_not_interrupt_playing_instruction(self):
        self.records.insert(
            7,
            {
                "kind": "audio_cancelled",
                "time": 2.21,
                "searchSeconds": 0.21,
                "detail": "prefetch;",
            },
        )
        self.save()
        self.assertEqual(self.read()[0][0]["interrupted_instruction_count"], "1")

    def test_survey_wrong_run_not_broadcast_and_invalid_trial_block_rejected(self):
        p = self.survey(
            "other.csv",
            [{"participant_id": "P001", "run_number": 2, "block": 0, "score": 7}],
        )
        self.assertEqual(
            self.read(survey_paths=[p])[0][0]["survey_status"],
            "pending_or_not_collected",
        )
        p = self.survey(
            "mismatch.csv",
            [
                {
                    "participant_id": "P001",
                    "run_number": 1,
                    "block": 1,
                    "trial_id": self.tid,
                    "score": 7,
                }
            ],
        )
        with self.assertRaisesRegex(ValueError, "block"):
            self.read(output="second", survey_paths=[p])

    def test_interior_replay_corruption_rejected(self):
        p = self.package / "recording.jsonl"
        lines = p.read_text().splitlines()
        lines[4] = "{bad"
        p.write_text("\n".join(lines) + "\n")
        with self.assertRaisesRegex(ValueError, "Corrupt"):
            self.read()
        self.assertFalse((self.root / "export").exists())

    def test_repeat_requests_are_separate_from_delivered_repeats(self):
        self.records[0]["repeatEventVersion"] = "guidance-repeat-v1"
        self.records[5].update(kind="guidance_repeat_requested", detail="guidance_repeat;")
        self.records[6]["detail"] = "guidance_repeat;text=Look up"
        self.records[7]["detail"] = "guidance_repeat;"
        self.records.insert(8, dict(kind="guidance_repeat_requested", time=2.3,
                                    searchSeconds=.3, detail="guidance_repeat;"))
        self.save()
        row = self.read()[0][0]
        self.assertEqual(row["repeat_request_count"], "2")
        self.assertEqual(row["delivered_repeat_count"], "1")
        self.assertEqual(row["instruction_count"], "1")
        self.assertEqual(row["repeat_count_source"], "replay")
        self.assertEqual(row["hint_count"], "0")

    def test_catalog_version_preserved_and_conflicting_sources_rejected(self):
        version = "elevenlabs-six-per-gender-v1"
        self.records[0]["neutralCatalogVersion"] = version
        self.summary["neutral_catalog_version"] = version
        self.save()
        self.assertEqual(self.read()[0][0]["neutral_catalog_version"], version)
        self.records[0]["neutralCatalogVersion"] = "different-catalog"
        self.save()
        with self.assertRaisesRegex(ValueError, "Conflicting voice catalog"):
            self.read(output="conflicting")

    def test_repeat_zero_requires_versioned_logging(self):
        legacy = self.read()[0][0]
        self.assertEqual(legacy["repeat_request_count"], "")
        self.records[0]["repeatEventVersion"] = "guidance-repeat-v1"
        self.save()
        modern = self.read(output="modern")[0][0]
        self.assertEqual(modern["repeat_request_count"], "0")
        self.assertEqual(modern["delivered_repeat_count"], "0")

    def test_csv_repeat_counts_without_replay(self):
        self.summary["repeat_event_version"] = "guidance-repeat-v1"
        self.save()
        records = [("search_start", ""), ("guidance_repeat_requested", "context=guidance_repeat;"),
                   ("guidance_repeat_requested", "context=guidance_repeat;"),
                   ("audio_request", "context=guidance_repeat;"),
                   ("audio_playback_start", "context=guidance_repeat;"), ("capture_correct", "")]
        rows = [dict(participant_id="P001", run_number=1, trial_id=self.tid, voice_condition="selfsimilar",
                     event_type=kind, detail=detail) for kind, detail in records]
        source = self.survey("trial_events.csv", rows)
        (self.run / source.name).write_bytes(source.read_bytes())
        row = self.read()[0][0]
        self.assertEqual(row["repeat_request_count"], "2")
        self.assertEqual(row["delivered_repeat_count"], "1")
        self.assertEqual(row["repeat_count_source"], "trial_events_csv")

    def test_two_recordings_for_one_trial_rejected(self):
        p = self.root / "replays" / "another"
        p.mkdir()
        (p / "recording.jsonl").write_bytes(
            (self.package / "recording.jsonl").read_bytes()
        )
        with self.assertRaisesRegex(ValueError, "Multiple replay"):
            self.read()

    def test_csv_events_supply_counts_without_replay_and_are_not_added_twice(self):
        records = [
            ("search_start", ""),
            ("audio_request", "context=tip;"),
            ("audio_playback_start", "context=tip;text=Look up"),
            ("audio_cancelled", "context=tip;"),
            ("capture_correct", ""),
            ("audio_playback_start", "context=congratulations;"),
        ]
        rows = [
            {
                "participant_id": "P001",
                "run_number": 1,
                "trial_id": self.tid,
                "voice_condition": "selfsimilar",
                "event_type": k,
                "detail": d,
            }
            for k, d in records
        ]
        p = self.survey("trial_events.csv", rows)
        (self.run / p.name).write_bytes(p.read_bytes())
        row = self.read()[0][0]
        self.assertEqual(row["instruction_count"], "1")
        self.assertEqual(row["instruction_count_source"], "trial_events_csv")
        for r in self.records:
            r["sourceRunFolder"] = "wrong_run"
        self.save()
        row = self.read(output="without-replay")[0][0]
        self.assertEqual(row["instruction_count"], "1")
        self.assertEqual(row["time_to_first_target_gaze_seconds"], "")

    def test_event_identity_mismatch_rejected(self):
        p = self.survey(
            "trial_events.csv",
            [
                {
                    "participant_id": "P002",
                    "run_number": 1,
                    "trial_id": self.tid,
                    "event_type": "search_start",
                }
            ],
        )
        (self.run / p.name).write_bytes(p.read_bytes())
        with self.assertRaisesRegex(ValueError, "identity"):
            self.read()

    def test_reexport_with_surveys_produces_same_trial_identities(self):
        rows, _ = self.read()
        p = self.survey(
            "reliance.csv",
            [{"participant_id": "P001", "run_number": 1, "block": 0, "score": 7}],
        )
        enriched, _ = self.read(output="with-surveys", survey_paths=[p])
        self.assertEqual(
            [r["trial_id"] for r in rows], [r["trial_id"] for r in enriched]
        )
        self.assertEqual(rows[0]["survey_status"], "pending_or_not_collected")
        self.assertEqual(enriched[0]["survey_status"], "available")

    def test_selection_snapshot_preserves_contact_between_regular_samples(self):
        self.records[4]["hoveredId"] = ""
        self.records[4]["rightController"]["hoveredId"] = ""
        selection = self.records[13]
        selection.update(
            searchActive=True,
            gazeAvailable=True,
            gazeTracked=1,
            hoveredId="target",
            rightController={
                "available": True,
                "rayAvailable": True,
                "tracked": 1,
                "hoveredId": "target",
            },
        )
        self.save()
        row = self.read()[0][0]
        self.assertEqual(row["time_to_first_right_controller_target_seconds"], "3")
        self.assertEqual(row["first_controller_contact_source"], "selection")
        self.assertEqual(row["first_gaze_contact_source"], "selection")
        self.assertEqual(row["gaze_total_samples"], "3")

    def test_vertical_head_has_no_horizontal_heading(self):
        self.records[3]["headRotation"] = {
            "x": math.sqrt(0.5),
            "y": 0,
            "z": 0,
            "w": math.sqrt(0.5),
        }
        self.save()
        rows, _ = self.read()
        self.assertEqual(rows[0]["initial_head_yaw_deg"], "")
        self.assertEqual(rows[0]["actual_signed_head_to_target_yaw_deg"], "")
        self.assertAlmostEqual(float(rows[0]["target_distance_m"]), math.sqrt(10))

    def test_conflicting_target_identity_rejected(self):
        self.records[1]["targetId"] = "different-target"
        self.save()
        with self.assertRaisesRegex(ValueError, "Conflicting target"):
            self.read()

    def test_geometry_uses_target_position_at_reference_sample(self):
        self.records[3]["changes"] = [{"id": "target", "position": vector(0, 4, 4)}]
        self.save()
        rows, _ = self.read()
        self.assertEqual(rows[0]["target_y"], "4")
        self.assertEqual(rows[0]["target_z"], "4")
        self.assertEqual(rows[0]["target_distance_m"], "5")

    def test_active_search_clock_regression_rejected(self):
        self.records[11]["searchSeconds"] = 0.5
        self.save()
        with self.assertRaisesRegex(ValueError, "clock regressed"):
            self.read()


    def test_data_purpose_labels_do_not_certify_legacy_data(self):
        rows, report = self.read()
        self.assertEqual(rows[0]["data_purpose"], "unknown")
        self.assertEqual(rows[0]["exclude_from_study_analysis"], "")
        self.summary["session_mode"] = "neutral_quick_test"
        self.save()
        rows, report = self.read("dev")
        self.assertEqual(rows[0]["exclude_from_study_analysis"], "1")
        self.assertEqual(report["data_purpose"], "development")
        self.summary["session_mode"] = "synthetic_demo"
        self.save()
        rows, report = self.read("synthetic")
        self.assertTrue(report["exclude_from_study_analysis"])
        self.assertEqual(rows[0]["data_purpose"], "synthetic")

    def test_motion_preserves_all_pose_records_and_raw_values(self):
        self.records[9]["gazeTracked"] = 0
        self.save()
        rows, report = self.read(include_motion=True)
        with (self.root / "export/motion.csv").open(newline="") as stream:
            motion = list(csv.DictReader(stream))
        expected = [r for r in self.records if r["kind"] in ("sample", "selection")]
        self.assertEqual(len(motion), len(expected))
        self.assertEqual(report["motion"]["row_count"], len(expected))
        for actual, source in zip(motion, expected):
            raw = json.loads(actual["source_record_json"])
            for key, value in source.items():
                self.assertEqual(raw[key], value)
            self.assertEqual(actual["trial_id"], self.tid)
        paused = next(r for r in motion if r["replay_time_seconds"] == "4")
        self.assertEqual(paused["replay__searchActive"], "0")
        self.assertEqual(paused["replay__headPosition__x"], "99")
        self.assertEqual(paused["replay__gazeTracked"], "0")
        self.assertEqual(motion[-1]["replay__headPosition__x"], "")
        with (self.root / "export" / report["trials"][0]["motion_file"]).open(newline="") as stream:
            self.assertEqual(list(csv.DictReader(stream)), motion)
        self.assertEqual(report["trials"][1]["motion_status"], "no_matched_replay")

    def test_motion_is_optional_and_missing_replays_are_explicit(self):
        self.read()
        self.assertFalse((self.root / "export/motion.csv").exists())
        report = export(self.run, self.root / "no-replay", include_motion=True)
        self.assertEqual(report["motion"]["row_count"], 0)
        self.assertTrue(all(e["motion_status"] == "no_matched_replay" for e in report["trials"]))

    def test_change_during_initial_read_rejects_package(self):
        def mutate_after_read(run):
            result = read_summary(run)
            self.summary["session_mode"] = "synthetic_demo"
            self.save()
            return result
        with patch("trial_csv.read_summary", side_effect=mutate_after_read):
            with self.assertRaisesRegex(ValueError, "Source files changed"):
                self.read(include_motion=True)
        self.assertFalse((self.root / "export").exists())

    def test_final_retrospective_tlx_keeps_block_scope_and_raw_ten_point_scale(self):
        source = self.survey("final_tlx.csv", [dict(participant_id="P001", run_number=1,
            block=0, voice_condition="selfsimilar", response_id="final-block1", instrument_version="tlx-0-10-20261020",
            administration_phase="final", reference_scope="participant_by_block", administered_at="2026-10-20T20:00:00Z",
            scale_min=0, scale_max=10, performance_direction="high_is_poor",
            mental=1, physical=2, temporal=3, performance=4, effort=5, frustration=6)])
        (self.run / "nasa_tlx.csv").symlink_to(source)
        rows, _ = self.read()
        self.assertEqual(rows[0]["nasa_tlx_mental"], "1")
        self.assertEqual(float(rows[0]["nasa_tlx_score"]), 3.5)
        self.assertEqual(float(rows[0]["nasa_tlx_score_0_100"]), 35)
        self.assertEqual(rows[0]["survey__final_tlx__block__administration_phase"], "final")
        self.assertEqual(rows[0]["survey__final_tlx__block__reference_scope"], "participant_by_block")

    def test_unchanged_symlinked_native_survey_is_accepted(self):
        source = self.survey("linked_tlx.csv", [dict(participant_id="P001", run_number=1,
            block=0, voice_condition="selfsimilar", response_id="R1", instrument_version="raw-v1", mental=40)])
        (self.run / "nasa_tlx.csv").symlink_to(source)
        rows, report = self.read(include_motion=True)
        self.assertEqual(rows[0]["nasa_tlx_mental"], "40")
        self.assertIn(str(source.resolve()), [p["path"] for p in report["inputs"]])

    def test_height_is_trial_target_minus_recorded_cross_not_head(self):
        self.records[1]["layout"] = "rotational_scatter_v4"
        self.records[1]["selectionMethod"] = "controller_ray_trigger_press_v1"
        self.records.insert(2, dict(kind="readiness_completed", time=1.5,
            readinessPosition=vector(0, .4, 1.5), readinessWall=0))
        self.save()
        rows, _ = self.read()
        self.assertEqual(rows[0]["scheduled_height_delta_m"], "1.6")
        self.assertEqual(rows[0]["scheduled_height_reference_y_m"], "0.4")
        self.assertEqual(rows[0]["target_minus_head_height_m"], "1")
        self.assertEqual(rows[0]["completion_timing_definition"], "final_correct_controller_selection")
        self.assertNotIn("time_to_final_correct_zone_seconds", rows[0])

    def test_v7_export_uses_final_target_after_offset_cross_readiness(self):
        self.records[1]["layout"] = "rotational_scatter_v6_precue_theta"
        target = next(o for o in self.records[1]["objects"] if o["target"])
        self.records.insert(2, dict(kind="readiness_completed", time=1.5, trialId="a",
            readinessPosition=vector(.1,.4,1.5), readinessWall=0,
            changes=[dict(id=target["id"], position=vector(0,1.7,2), rotation=dict(x=0,y=0,z=0,w=1), scale=vector(.1,.1,.1))]))
        self.save()
        rows, _ = self.read()
        self.assertAlmostEqual(float(rows[0]["scheduled_height_delta_m"]),1.3)
        self.assertEqual(rows[0]["scheduled_height_reference_y_m"],"0.4")
        self.assertAlmostEqual(float(rows[0]["target_minus_head_height_m"]),.7)
        self.assertEqual(rows[0]["target_y"],"1.7")

    def test_legacy_completion_is_not_relabelled_controller_selection(self):
        self.records[1]["selectionMethod"] = "gaze_dwell"
        self.save()
        rows, _ = self.read()
        self.assertEqual(rows[0]["completion_timing_definition"], "legacy_gaze_dwell_completion")


if __name__ == "__main__":
    unittest.main()
