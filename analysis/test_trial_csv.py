import csv
import json
import math
import tempfile
import unittest
from pathlib import Path

from trial_csv import export


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


if __name__ == "__main__":
    unittest.main()
