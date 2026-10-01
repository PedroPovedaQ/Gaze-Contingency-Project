import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("study_validator", Path(__file__).parents[1] / "scripts/verify-study-session.py")
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


class StudyValidationTests(unittest.TestCase):
    def schedule_fixture(self, root, reverse=False, debug=0):
        summary = dict(challenge_set="previous-wall-angle-v5-stratified-40-trials", participant_id="TEST",
                       schedule_seed=42, voice_order="selfsimilar_then_neutral" if reverse else "neutral_then_selfsimilar",
                       neutral_voice_profile="male", total_rounds=debug or 80, nominal_total_rounds=80,
                       rounds_per_block=40, debug_round_override=debug,
                       target_identity_policy="matched-counts-shuffled-24-plus-16-v1",
                       height_assignment_policy="unbalanced-floor-scatter-pending-reference-v1")
        rows, start = [], 3
        for index in range(80):
            block, local = divmod(index, 40)
            signed = (local % 5) * 45 * (-1 if block else 1)
            if signed == -180: signed = 180
            target = (start + signed // 45) % 8
            identity = local % 24
            rows.append(dict(participant_id="TEST", schedule_version=summary["challenge_set"], schedule_seed=42,
                             trial_index=index, block_index=block, trial_in_block=local,
                             voice_condition="neutral-male" if (block == 0) != reverse else "selfsimilar",
                             angle_pair=local, start_plane=start, target_plane=target, target_slot=5,
                             signed_theta_deg=signed, absolute_theta_deg=abs(signed),
                             target_shape=f"shape{identity // 4}", target_color=f"color{identity % 4}",
                             layout_seed=index+1, enabled_in_run=int(index < (debug or 80)),
                             theta_reference="previous_target_wall_center",
                             target_identity_policy=summary["target_identity_policy"],
                             height_assignment_policy=summary["height_assignment_policy"]))
            start = target
        summary["objectives"] = [dict(index=r["trial_index"], shape=r["target_shape"], color=r["target_color"],
                                      voice_condition=r["voice_condition"], **{key: r[key] for key in
                                      ("start_plane", "target_plane", "signed_theta_deg", "absolute_theta_deg", "angle_pair", "layout_seed")})
                                 for r in rows[:debug or 80]]
        self.write_schedule(root, rows)
        return summary, rows

    def write_schedule(self, root, rows):
        with (root / "trial_schedule.csv").open("w", newline="") as file:
            writer = csv.DictWriter(file, fieldnames=rows[0].keys())
            writer.writeheader(); writer.writerows(rows)

    def test_eighty_trial_contract_both_orders_and_short_run(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for reverse in (False, True):
                for debug in (0, 7, 45):
                    summary, _ = self.schedule_fixture(root, reverse, debug)
                    self.assertEqual(validator.validate_schedule(root, summary), [])

    def test_schedule_rejects_missing_duplicated_or_misassigned_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for field, value, expected in (("voice_condition", "selfsimilar", "assignment"),
                                            ("signed_theta_deg", 90, "geometry"),
                                            ("start_plane", 0, "reference"),
                                            ("angle_pair", 2, "angle pairs"),
                                            ("layout_seed", 1, "layout seed"),
                                            ("height_assignment_policy", "balanced", "version"),
                                            ("enabled_in_run", 0, "enabled flag")):
                summary, rows = self.schedule_fixture(root)
                rows[1][field] = value
                self.write_schedule(root, rows)
                self.assertTrue(any(expected in error for error in validator.validate_schedule(root, summary)), field)
            self.write_schedule(root, rows[:20])
            self.assertTrue(validator.validate_schedule(root, summary))

    def test_summary_cannot_silently_change_or_replace_a_planned_trial(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            summary, _ = self.schedule_fixture(root)
            summary["objectives"][5]["index"] = 4
            self.assertTrue(any("every enabled" in error for error in validator.validate_schedule(root, summary)))
            summary, _ = self.schedule_fixture(root)
            summary["objectives"][5]["target_plane"] = 7
            self.assertTrue(any("summary target_plane" in error for error in validator.validate_schedule(root, summary)))
            (root / "trial_schedule.csv").unlink()
            self.assertTrue(validator.validate_schedule(root, summary))

    def test_valid_session_and_wrong_voice_or_timing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            records, events, objects = [], [], []
            for i in range(14):
                trial = f"P001_run001_r{i:02}"
                records.append(dict(index=i, trial_id=trial, completed=True, objects_ready_at=9,
                                    voice_condition="neutral-male" if i < 7 else "selfsimilar", time_to_find_seconds=8))
                for kind, time, detail in (("audio_playback_start", 1, f"context=round;clip_id={'neutral' if i < 7 else 'self'};"),
                                           ("audio_playback_end", 8, "context=round;"),
                                           ("search_start", 10, ""), ("capture_correct", 18, "")):
                    events.append(dict(trial_id=trial, timestamp=time, event_type=kind, detail=detail))
                for j in range(56):
                    objects.append(dict(trial_id=trial, object_id=f"r{i}_o{j}", is_target=int(j == 0)))
            for name, rows in (("trial_events.csv", events), ("object_manifest.csv", objects)):
                with (root / name).open("w", newline="") as file:
                    writer = csv.DictWriter(file, fieldnames=rows[0].keys())
                    writer.writeheader(); writer.writerows(rows)
            summary = dict(schema_version=2, session_outcome="completed", voice_order="neutral_then_selfsimilar",
                           neutral_voice_profile="male", objectives=records)
            (root / "trial_summary.json").write_text(json.dumps(summary))
            (root / "voice-library-manifest.json").write_text(json.dumps(dict(clips=[
                dict(clip_id="neutral", voice_id="cjVigY5qzO86Huf0OWal", provider="elevenlabs", text="Find."),
                dict(clip_id="self", voice_id="test-clone", provider="mistral", text="Find."),
            ])))
            self.assertEqual(validator.validate(root), [])
            # Both new conditions use ElevenLabs; identity must use the explicit
            # condition and the chosen voice ID rather than provider or gender defaults.
            manifest_path = root / "voice-library-manifest.json"
            manifest = json.loads(manifest_path.read_text())
            manifest["clips"][0].update(voice_condition="neutral", voice_id="new-catalog-voice")
            manifest["clips"][1].update(voice_condition="selfsimilar", provider="elevenlabs", text="I need to find it.")
            summary["neutral_voice_id"] = "new-catalog-voice"
            manifest_path.write_text(json.dumps(manifest))
            (root / "trial_summary.json").write_text(json.dumps(summary))
            self.assertEqual(validator.validate(root), [])
            summary["voice_perspective"] = "shared_participant_directed_imperative"
            (root / "trial_summary.json").write_text(json.dumps(summary))
            self.assertTrue(any("phrase sets" in error for error in validator.validate(root)))
            summary.pop("voice_perspective")
            (root / "trial_summary.json").write_text(json.dumps(summary))
            manifest["clips"][1]["voice_condition"] = "neutral"
            manifest_path.write_text(json.dumps(manifest))
            self.assertTrue(any("identity" in error for error in validator.validate(root)))
            manifest["clips"][1]["voice_condition"] = "selfsimilar"
            manifest_path.write_text(json.dumps(manifest))
            records[0]["time_to_find_seconds"] = 18
            records[1]["voice_condition"] = "selfsimilar"
            (root / "trial_summary.json").write_text(json.dumps(summary))
            errors = validator.validate(root)
            self.assertTrue(any("duration" in error for error in errors))
            self.assertTrue(any("assignment" in error for error in errors))
