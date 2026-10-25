import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import subprocess
import sys
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

    def v7_fixture(self, root, reverse=False, quick=False, debug=0, clone=False):
        summary, rows = self.schedule_fixture(root, reverse, debug)
        summary.update(challenge_set="readiness-target-angle-v7-quick-origin0" if quick else "readiness-target-angle-v7-one-practice",
                       session_mode=("clone_quick_test" if clone else "neutral_quick_test") if quick else "study",
                       voice_condition=("selfsimilar" if clone else "neutral-male") if quick else "counterbalanced",
                       theta_reference=validator.REFERENCE, direction_policy="independent-blocks-four-left-four-right-v1",
                       height_assignment_policy="four-above-four-below-per-angle-v1", angle_balance_applied=True)
        if quick: summary["total_rounds"] = min(40, summary["total_rounds"])
        start = 0 if quick else 3
        for i, row in enumerate(rows):
            local = i % 40
            angle = local % 5 * 45
            # Identical directions across blocks deliberately violate the old mirror rule.
            signed = -angle if local // 5 >= 4 and angle not in (0,180) else angle
            target = (start + signed // 45) % 8
            row.update(schedule_version=summary["challenge_set"],theta_reference=validator.REFERENCE,
                       direction_policy=summary["direction_policy"],height_assignment_policy=summary["height_assignment_policy"],
                       target_height_fraction=.7 if local // 5 % 2 else .3,cross_offset_fraction=0 if i==0 else .12,
                       start_plane=start,target_plane=target,start_wall_azimuth_deg=start*45,wall_azimuth_deg=target*45,
                       signed_theta_deg=signed,absolute_theta_deg=angle,enabled_in_run=int(i<summary["total_rounds"]))
            if quick: row["voice_condition"]=summary["voice_condition"]
            start=target
        summary["objectives"]=[dict(index=r["trial_index"],shape=r["target_shape"],color=r["target_color"],
            voice_condition=r["voice_condition"],outcome="not_started",search_started_at=-1,
            **{key:r[key] for key in ("start_plane","target_plane","signed_theta_deg","absolute_theta_deg","angle_pair","layout_seed")})
            for r in rows[:summary["total_rounds"]]]
        self.write_schedule(root,rows)
        return summary,rows

    def test_v7_independent_blocks_quick_modes_and_debug_caps(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for reverse in (False,True):
                for quick in (False,True):
                    for clone in (False,True):
                        for debug in (0,7,45,100):
                            summary,_=self.v7_fixture(root,reverse,quick,debug,clone)
                            if debug==100:
                                summary["total_rounds"]=40 if quick else 80
                            self.assertEqual(validator.validate_schedule(root,summary),[])

    def test_v7_rejects_direction_height_and_bad_geometry_evidence(self):
        from test_theta_snapshot import snapshot
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for key,value in (("target_height_fraction",.5),("cross_offset_fraction",.9),("direction_policy","mirror")):
                summary,rows=self.v7_fixture(root)
                rows[0][key]=value;self.write_schedule(root,rows)
                self.assertTrue(validator.validate_schedule(root,summary),key)
            summary,rows=self.v7_fixture(root)
            # Keep range valid, break only the per-angle signed-height quota.
            rows[0]["target_height_fraction"]=.7;self.write_schedule(root,rows)
            self.assertTrue(any("height quota" in e for e in validator.validate_schedule(root,summary)))
            summary,rows=self.v7_fixture(root)
            rec=summary["objectives"][1];rec["search_started_at"]=20
            self.assertTrue(any("missing pre-cue" in e for e in validator.validate_schedule(root,summary)))
            rec["pre_cue_theta"]=snapshot(45,1,-.4)
            self.assertEqual(validator.validate_schedule(root,summary),[])
            rec["pre_cue_theta"]["targetPosition"]["x"]+=.2
            self.assertTrue(any("geometry" in e for e in validator.validate_schedule(root,summary)))

    def completed_fixture(self, root, count=14, block_size=7, object_count=56, reverse=False):
        """Complete event/object evidence for legacy or current schedule tests."""
        records, events, objects = [], [], []
        for i in range(count):
            trial = f"P001_run001_r{i:02}"
            records.append(dict(index=i, trial_id=trial, completed=True, outcome="completed", objects_ready_at=9,
                                voice_condition="neutral-male" if (i < block_size) != reverse else "selfsimilar", time_to_find_seconds=8))
            for kind, time, detail in (("audio_playback_start", 1, f"context=round;clip_id={'neutral' if (i < block_size) != reverse else 'self'};"),
                                      ("audio_playback_end", 8, "context=round;"),
                                      ("search_start", 10, ""), ("capture_correct", 18, "")):
                events.append(dict(trial_id=trial, timestamp=time, event_type=kind, detail=detail))
            for j in range(object_count):
                objects.append(dict(trial_id=trial, object_id=f"r{i}_o{j}", is_target=int(j == 0)))
        summary = dict(schema_version=2, session_outcome="completed", voice_order="selfsimilar_then_neutral" if reverse else "neutral_then_selfsimilar",
                       total_rounds=count, rounds_per_block=block_size, objects_per_round=object_count,
                       neutral_voice_profile="male", objectives=records)
        self.write_recording(root, summary, events)
        with (root / "object_manifest.csv").open("w", newline="") as file:
            writer = csv.DictWriter(file, fieldnames=objects[0].keys())
            writer.writeheader(); writer.writerows(objects)
        (root / "voice-library-manifest.json").write_text(json.dumps(dict(clips=[
            dict(clip_id="neutral", voice_id="cjVigY5qzO86Huf0OWal", provider="elevenlabs", text="Find."),
            dict(clip_id="self", voice_id="test-clone", provider="mistral", text="Find."),
        ])))
        return summary, events

    def write_recording(self, root, summary, events):
        (root / "trial_summary.json").write_text(json.dumps(summary))
        with (root / "trial_events.csv").open("w", newline="") as file:
            writer = csv.DictWriter(file, fieldnames=("trial_id", "timestamp", "event_type", "detail"))
            writer.writeheader(); writer.writerows(events)

    def test_complete_session_requires_completed_unique_trials_even_in_partial_mode(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for mutation in ("unfinished", "duplicate_id", "duplicate_index", "contradictory_outcome", "nonboolean"):
                with self.subTest(mutation=mutation):
                    summary, events = self.completed_fixture(root)
                    if mutation == "unfinished": summary["objectives"][0]["completed"] = False
                    if mutation == "duplicate_id": summary["objectives"][1]["trial_id"] = summary["objectives"][0]["trial_id"]
                    if mutation == "duplicate_index": summary["objectives"][1]["index"] = 0
                    if mutation == "contradictory_outcome": summary["objectives"][0]["outcome"] = "not_started"
                    if mutation == "nonboolean": summary["objectives"][0]["completed"] = "false"
                    self.write_recording(root, summary, events)
                    self.assertTrue(validator.validate(root))
                    self.assertTrue(validator.validate(root, allow_incomplete=True))

    def test_nonfinite_negative_or_missing_completed_timing_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for field in ("time_to_find_seconds", "objects_ready_at"):
                for value in (float("nan"), float("inf"), -1, None, True):
                    with self.subTest(field=field, value=value):
                        summary, events = self.completed_fixture(root)
                        summary["objectives"][0][field] = value
                        self.write_recording(root, summary, events)
                        self.assertTrue(validator.validate(root))
            for value in ("nan", "inf", "-1", ""):
                summary, events = self.completed_fixture(root)
                events[2]["timestamp"] = value
                self.write_recording(root, summary, events)
                self.assertTrue(validator.validate(root))

    def test_pause_state_and_event_order_are_validated(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for kinds, times, duration in ((["search_resumed"], [12], 6),
                                          (["search_paused", "search_paused", "search_resumed"], [12,13,14], 6),
                                          (["search_paused"], [12], 2),
                                          (["search_paused", "search_resumed"], [14,12], 10)):
                summary, events = self.completed_fixture(root)
                trial = summary["objectives"][0]["trial_id"]
                events[3:3] = [dict(trial_id=trial, timestamp=t, event_type=k, detail="") for k,t in zip(kinds,times)]
                summary["objectives"][0]["time_to_find_seconds"] = duration
                self.write_recording(root, summary, events)
                self.assertTrue(validator.validate(root))

    def test_legitimate_readiness_pause_and_search_pause_exclude_break_time(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            summary, events = self.completed_fixture(root)
            trial = summary["objectives"][0]["trial_id"]
            def event(kind, time):
                return dict(trial_id=trial, timestamp=time, event_type=kind, detail="")
            events[2:2] = [event("search_paused", 8.2), event("search_resumed", 8.5)]
            events[5:5] = [event("search_paused", 12), event("search_resumed", 14)]
            summary["objectives"][0]["time_to_find_seconds"] = 6
            self.write_recording(root, summary, events)
            self.assertEqual(validator.validate(root), [])

    def test_partial_mode_preserves_incomplete_status_without_ignoring_bad_events(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            summary, events = self.completed_fixture(root)
            summary["session_outcome"] = "withdrawn"
            summary["objectives"][0].update(completed=False, outcome="withdrawn", time_to_find_seconds=-1)
            events.pop(3)  # No capture: the recorded search was interrupted.
            self.write_recording(root, summary, events)
            self.assertEqual(validator.validate(root, allow_incomplete=True), [])
            self.assertTrue(validator.validate(root))
            events[2]["timestamp"] = "nan"
            self.write_recording(root, summary, events)
            self.assertTrue(validator.validate(root, allow_incomplete=True))
            events[2]["timestamp"] = 10
            events[0]["detail"] = "context=round;clip_id=self;"
            self.write_recording(root, summary, events)
            self.assertTrue(any("identity" in error for error in validator.validate(root, allow_incomplete=True)))

    def test_invalid_voice_order_duplicate_clips_and_numeric_block_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for field, value in (("voice_order", "unknown"), ("rounds_per_block", None), ("objects_per_round", True)):
                summary, events = self.completed_fixture(root)
                summary[field] = value
                self.write_recording(root, summary, events)
                self.assertTrue(validator.validate(root))
            self.completed_fixture(root)
            path = root / "voice-library-manifest.json"
            manifest = json.loads(path.read_text())
            manifest["clips"].append(manifest["clips"][0])
            path.write_text(json.dumps(manifest))
            self.assertTrue(any("duplicate" in error for error in validator.validate(root)))

    def test_cli_returns_failure_without_traceback_for_invalid_timing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            summary, events = self.completed_fixture(root)
            command = [sys.executable, str(Path(validator.__file__)), str(root)]
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            summary["objectives"][0]["time_to_find_seconds"] = None
            self.write_recording(root, summary, events)
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 1)
            self.assertIn("invalid time_to_find_seconds", result.stdout)
            self.assertNotIn("Traceback", result.stderr)

    def test_current_complete_eighty_trial_package_both_orders(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for reverse in (False, True):
                schedule, _ = self.schedule_fixture(root, reverse=reverse)
                summary, events = self.completed_fixture(root, count=80, block_size=40, object_count=168, reverse=reverse)
                for observed, planned in zip(summary["objectives"], schedule.pop("objectives")):
                    observed.update(planned)
                summary.update(schedule)
                self.write_recording(root, summary, events)
                self.assertEqual(validator.validate(root), [])
                summary["objectives"][40]["completed"] = False
                self.write_recording(root, summary, events)
                self.assertTrue(validator.validate(root))

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

    def v7_recording(self, root, reverse=False, quick=False, clone=False, debug=0):
        from test_theta_snapshot import snapshot
        plan, rows = self.v7_fixture(root, reverse, quick, debug, clone)
        count = plan['total_rounds']
        summary, events = self.completed_fixture(root, count=count, block_size=40, object_count=168, reverse=reverse)
        planned_records = plan.pop('objectives')
        summary.update(plan)
        summary['active_search_limit_seconds'] = 25
        for record, planned in zip(summary['objectives'], planned_records):
            record.update(planned)
            record.update(completed=True, outcome='completed', search_started_at=10, capture_at=18,
                          search_exposure_seconds=8)
            record['pre_cue_theta'] = snapshot(record['signed_theta_deg'], record['index'],
                .4 if rows[record['index']]['target_height_fraction'] > .5 else -.4)
        rebuilt = []
        for event in events:
            record = next(r for r in summary['objectives'] if r['trial_id'] == event['trial_id'])
            if event['event_type'] == 'audio_playback_start':
                voice = 'neutral' if record['voice_condition'].startswith('neutral-') else 'self'
                event['detail'] = f'context=round;clip_id={voice};'
            if event['event_type'] == 'search_start':
                rebuilt.append(dict(trial_id=event['trial_id'], timestamp=9.5, event_type='theta_snapshot',
                                    detail=json.dumps(record['pre_cue_theta'])))
            rebuilt.append(event)
        with (root / 'object_manifest.csv').open(newline='') as file:
            objects = list(csv.DictReader(file))
        for obj in objects:
            record = next(r for r in summary['objectives'] if r['trial_id'] == obj['trial_id'])
            obj.update(record['pre_cue_theta']['targetPosition'] if obj['is_target'] == '1' else dict(x=0,y=0,z=0))
        self.write_objects(root, objects)
        self.write_recording(root, summary, rebuilt)
        return summary, rebuilt, objects

    def write_objects(self, root, objects):
        with (root / 'object_manifest.csv').open('w', newline='') as file:
            writer = csv.DictWriter(file, fieldnames=objects[0].keys())
            writer.writeheader(); writer.writerows(objects)

    def test_v7_full_recordings_both_orders_quick_voices_and_debug(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for reverse, quick, clone, debug in ((False,False,False,0),(True,False,False,0),
                    (False,True,False,0),(True,True,True,0),(False,False,False,7)):
                self.v7_recording(root,reverse,quick,clone,debug)
                self.assertEqual(validator.validate(root), [])

    def test_v7_rejects_event_summary_geometry_corruption(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for mutation in ('missing', 'mismatch', 'null', 'malformed', 'after_search', 'duplicate', 'manifest', 'event_only_start'):
                with self.subTest(mutation=mutation):
                    summary, events, objects = self.v7_recording(root, debug=1)
                    event = next(e for e in events if e['event_type']=='theta_snapshot')
                    if mutation=='missing': events.remove(event)
                    if mutation=='mismatch': event['detail']=json.dumps(dict(summary['objectives'][0]['pre_cue_theta'],time=99))
                    if mutation=='null': event['detail']='null'
                    if mutation=='malformed': event['detail']='{}'
                    if mutation=='after_search': events.remove(event); events.insert(-1,event)
                    if mutation=='duplicate': events.insert(events.index(event),dict(event))
                    if mutation=='manifest': objects[0]['x'] += .1; self.write_objects(root, objects)
                    if mutation=='event_only_start':
                        record=summary['objectives'][0]
                        record.update(completed=False,outcome='withdrawal',search_started_at=-1,pre_cue_theta=None)
                        summary['session_outcome']='withdrawal'
                        events=[e for e in events if e['event_type'] not in ('capture_correct','theta_snapshot')]
                    self.write_recording(root,summary,events)
                    self.assertTrue(validator.validate(root,allow_incomplete=True),mutation)

    def test_v7_readiness_restart_uses_last_valid_snapshot(self):
        import copy
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            summary,events,_=self.v7_recording(root,debug=1)
            event=next(e for e in events if e['event_type']=='theta_snapshot')
            earlier=copy.deepcopy(summary['objectives'][0]['pre_cue_theta'])
            earlier.update(time=14,gazeReceiptTime=13.99)
            i=events.index(event)
            events[i:i]=[dict(event,timestamp=9.1,detail=json.dumps(earlier)),
                         dict(event,timestamp=9.2,event_type='readiness_reset',detail='')]
            self.write_recording(root,summary,events)
            self.assertEqual(validator.validate(root),[])

    def timeout_recording(self,root):
        summary,events,objects=self.v7_recording(root,debug=1)
        record=summary['objectives'][0]
        record.update(completed=False,outcome='timeout',search_exposure_seconds=25,time_to_find_seconds=-1,capture_at=-1)
        events[-1].update(event_type='trial_timeout',timestamp=37.2,detail='active_search_limit_seconds=25')
        events[-1:-1]=[dict(events[-1],event_type='search_paused',timestamp=12,detail=''),
                        dict(events[-1],event_type='search_resumed',timestamp=14,detail='')]
        self.write_recording(root,summary,events)
        return summary,events,objects

    def test_timeout_is_terminal_with_deadline_capped_exposure(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            self.timeout_recording(root)
            self.assertEqual(validator.validate(root),[])
            self.assertEqual(validator.validate(root,allow_incomplete=True),[])

    def test_timeout_corrupt_timing_outcome_and_manifest_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for mutation in ('early','exposure','limit','detail','capture','duplicate','missing','paused','objects','success_time'):
                with self.subTest(mutation=mutation):
                    summary,events,objects=self.timeout_recording(root)
                    record=summary['objectives'][0]
                    if mutation=='early': events[-1]['timestamp']=34
                    if mutation=='exposure': record['search_exposure_seconds']=27.2
                    if mutation=='limit': summary['active_search_limit_seconds']=None
                    if mutation=='detail': events[-1]['detail']='active_search_limit_seconds=20'
                    if mutation=='capture': events.insert(-1,dict(events[-1],event_type='capture_correct'))
                    if mutation=='duplicate': events.append(dict(events[-1]))
                    if mutation=='missing': events.pop()
                    if mutation=='paused': events.pop(-2)
                    if mutation=='objects': self.write_objects(root,objects[:-1])
                    if mutation=='success_time': record['time_to_find_seconds']=25
                    self.write_recording(root,summary,events)
                    self.assertTrue(validator.validate(root),mutation)

    def test_partial_v7_started_trial_still_requires_full_object_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            summary,events,objects=self.v7_recording(root,debug=1)
            summary['session_outcome']='withdrawal'
            summary['objectives'][0].update(completed=False,outcome='withdrawal')
            events=[e for e in events if e['event_type']!='capture_correct']
            self.write_recording(root,summary,events)
            self.assertEqual(validator.validate(root,allow_incomplete=True),[])
            self.write_objects(root,objects[:-1])
            self.assertTrue(validator.validate(root,allow_incomplete=True))

    def test_quick_mode_cannot_mislabel_voice_condition(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            summary,_=self.v7_fixture(root,quick=True,clone=True)
            summary['session_mode']='neutral_quick_test'
            self.assertTrue(any('mode and voice' in e for e in validator.validate_schedule(root,summary)))

    def test_v7_numeric_metadata_and_angle_policy_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for key in ('total_rounds','debug_round_override','nominal_total_rounds','rounds_per_block','schedule_seed'):
                for value in (True, 1.0, '1', None):
                    summary,_=self.v7_fixture(root)
                    summary[key]=value
                    self.assertTrue(validator.validate_schedule(root,summary),(key,value))
            for value in (False, 1, 'true', None):
                summary,_=self.v7_fixture(root)
                summary['angle_balance_applied']=value
                self.assertTrue(validator.validate_schedule(root,summary),value)
            summary,_=self.v7_fixture(root)
            summary['objectives'][0]['target_plane']=float(summary['objectives'][0]['target_plane'])
            self.assertTrue(validator.validate_schedule(root,summary))

    def test_successful_search_respects_recorded_deadline(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for duration, fails in ((24.9,False),(25.02,False),(25.1,True),(30,True)):
                summary,events,_=self.v7_recording(root,debug=1)
                summary['objectives'][0].update(time_to_find_seconds=duration,
                    search_exposure_seconds=duration,capture_at=10+duration)
                events[-1]['timestamp']=10+duration
                self.write_recording(root,summary,events)
                errors=validator.validate(root)
                self.assertEqual(bool(errors),fails,(duration,errors))
            # Do not retroactively impose today's deadline on a historical run.
            summary,events=self.completed_fixture(root)
            summary['active_search_limit_seconds']=40
            summary['objectives'][0]['time_to_find_seconds']=30
            events[3]['timestamp']=40
            self.write_recording(root,summary,events)
            self.assertEqual(validator.validate(root),[])

    def test_opening_cue_requires_snapshot_for_current_readiness_attempt(self):
        import copy
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for kind,detail in (('opening_direction_started',''),
                    ('audio_playback_start','context=guidance_opening;clip_id=neutral;')):
                summary,events,_=self.v7_recording(root,debug=1)
                snapshot_event=next(e for e in events if e['event_type']=='theta_snapshot')
                i=events.index(snapshot_event)
                opening=dict(snapshot_event,event_type=kind,detail=detail,timestamp=9.4)
                events.insert(i,opening)
                self.write_recording(root,summary,events)
                self.assertTrue(any('opening cue precedes' in e for e in validator.validate(root)))
                events.remove(opening)
                opening['timestamp']=9.6
                events.insert(events.index(snapshot_event)+1,opening)
                self.write_recording(root,summary,events)
                self.assertEqual(validator.validate(root),[])
                # A cancelled earlier opening must not invalidate the second snapshot.
                earlier=copy.deepcopy(summary['objectives'][0]['pre_cue_theta'])
                earlier.update(time=14,gazeReceiptTime=13.99)
                i=events.index(snapshot_event)
                events[i:i]=[dict(snapshot_event,timestamp=9.1,detail=json.dumps(earlier)),
                    dict(opening,timestamp=9.2),
                    dict(opening,timestamp=9.3,event_type='readiness_reset',detail='')]
                self.write_recording(root,summary,events)
                self.assertEqual(validator.validate(root),[])
                # After a reset, the earlier snapshot no longer authorizes a new cue.
                events.remove(snapshot_event)
                self.write_recording(root,summary,events)
                self.assertTrue(any('opening cue precedes' in e for e in validator.validate(root)))
