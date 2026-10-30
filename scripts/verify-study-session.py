#!/usr/bin/env python3
"""Validate a collected two-voice run. This checks logs, not perceived voice quality."""
import argparse
import csv
import json
import math
from collections import Counter
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "analysis"))
from theta_snapshot import V8, PRE_CUE_SCHEDULES, QUICK_SCHEDULES, REFERENCE, flatten as flatten_theta


def validate_schedule(folder, summary):
    """Audit versioned full plans, including disabled/unstarted trials."""
    version = summary.get("challenge_set")
    pre_cue = version in PRE_CUE_SCHEDULES
    v8 = version in V8
    block_size = 32 if v8 else 40
    total = block_size * 2
    quick = version in QUICK_SCHEDULES
    if not pre_cue and version != "previous-wall-angle-v5-stratified-40-trials":
        return []  # Historical schedules retain their original contract.
    errors = []
    try:
        with (folder / "trial_schedule.csv").open(encoding="utf-8-sig", newline="") as file:
            rows = list(csv.DictReader(file))
        if len(rows) != total or [int(r["trial_index"]) for r in rows] != list(range(total)):
            return [f"Schedule: expected all {total} uniquely ordered planned trials"]
        if pre_cue:
            for key in ("total_rounds", "debug_round_override", "nominal_total_rounds", "rounds_per_block", "schedule_seed"):
                if type(summary.get(key)) is not int:
                    errors.append(f"Schedule: pre-cue schedule {key} must be an integer")
            if errors:
                return errors
            if summary["debug_round_override"] < 0 or summary["schedule_seed"] < 0:
                errors.append("Schedule: negative debug override/seed")
            if summary.get("angle_balance_applied") is not True:
                errors.append("Schedule: pre-cue schedule requires angle balancing to be applied")
            if not quick and (summary.get("session_mode") != "study" or summary.get("voice_condition") != "counterbalanced"):
                errors.append("Schedule: normal pre-cue schedule requires study mode and counterbalanced voices")
        count = summary["total_rounds"]
        override = summary["debug_round_override"]
        expected_count = max(1, min(total, override)) if override > 0 else total
        if quick:
            expected_count = min(block_size, expected_count)
            if summary.get("session_mode") not in ("neutral_quick_test", "clone_quick_test", "new_clone_quick_test"):
                errors.append("Schedule: quick version requires a development quick-test mode")
            expected_quick_voice = ("neutral-" + summary["neutral_voice_profile"]
                                    if summary.get("session_mode") == "neutral_quick_test" else "selfsimilar")
            if summary.get("voice_condition") != expected_quick_voice:
                errors.append("Schedule: quick-test mode and voice disagree")
        if pre_cue and (summary.get("theta_reference") != REFERENCE
                or summary.get("direction_policy") != ("independent-blocks-four-per-signed-step-v1" if v8 else "independent-blocks-four-left-four-right-v1")
                or summary.get("height_assignment_policy") != ("two-above-two-below-per-signed-step-v1" if v8 else "four-above-four-below-per-angle-v1")
                or summary.get("target_identity_policy") != ("matched-counts-shuffled-24-plus-8-v1" if v8 else "matched-counts-shuffled-24-plus-16-v1")):
            errors.append("Schedule: pre-cue schedule policy/reference mismatch")
        if summary["rounds_per_block"] != block_size or summary["nominal_total_rounds"] != total or count != expected_count:
            errors.append("Schedule: block/full/debug counts disagree")
        for index, row in enumerate(rows):
            block = index // block_size
            neutral = (block == 0) == (summary["voice_order"] == "neutral_then_selfsimilar")
            expected_voice = "neutral-" + summary["neutral_voice_profile"] if neutral else "selfsimilar"
            if quick:
                expected_voice = summary["voice_condition"]
            start, target = int(row["start_plane"]), int(row["target_plane"])
            steps = (target - start + 8) % 8
            signed = (steps if steps <= 4 else steps - 8) * 45
            if not (0 <= start < 8 and 0 <= target < 8 and 0 <= int(row["target_slot"]) < 21):
                errors.append(f"Schedule trial {index}: invalid wall/slot")
            if int(row["signed_theta_deg"]) != signed or int(row["absolute_theta_deg"]) != abs(signed):
                errors.append(f"Schedule trial {index}: theta disagrees with wall geometry")
            if index and start != int(rows[index - 1]["target_plane"]):
                errors.append(f"Schedule trial {index}: previous-target reference broken")
            if index and (row["target_shape"], row["target_color"]) == (rows[index - 1]["target_shape"], rows[index - 1]["target_color"]):
                errors.append(f"Schedule trial {index}: immediate target repetition")
            if (row["participant_id"] != summary["participant_id"] or row["schedule_version"] != version
                    or int(row["schedule_seed"]) != summary["schedule_seed"]
                    or int(row["block_index"]) != block or int(row["trial_in_block"]) != index % block_size
                    or row["voice_condition"] != expected_voice or int(row["enabled_in_run"]) != int(index < count)
                    or row["theta_reference"] != (REFERENCE if pre_cue else "previous_target_wall_center")
                    or row["target_identity_policy"] != summary["target_identity_policy"]
                    or row["height_assignment_policy"] != summary["height_assignment_policy"]):
                errors.append(f"Schedule trial {index}: identity, version, assignment or enabled flag mismatch")
            if pre_cue:
                height, offset = float(row["target_height_fraction"]), float(row["cross_offset_fraction"])
                if (row["direction_policy"] != summary["direction_policy"] or not math.isfinite(height)
                        or not (.05 - 1e-6 <= height <= .4 + 1e-6 or .6 - 1e-6 <= height <= .95 + 1e-6)
                        or not math.isfinite(offset) or abs(offset) > .200001
                        or index == 0 and offset != 0
                        or int(row["wall_azimuth_deg"]) != target * 45
                        or int(row["start_wall_azimuth_deg"]) != start * 45):
                    errors.append(f"Schedule trial {index}: invalid pre-cue direction/height/cross geometry")
                if index == 0 and quick and start != 0:
                    errors.append("Schedule: wrong initial readiness wall")
        identity_counts = []
        for block in range(2):
            subset = rows[block * block_size:(block + 1) * block_size]
            if Counter(int(r["absolute_theta_deg"]) for r in subset) != Counter({0:4,45:8,90:8,135:8,180:4} if v8 else {angle:8 for angle in (0,45,90,135,180)}):
                errors.append(f"Schedule block {block}: absolute-angle quota mismatch")
            if sorted(int(r["angle_pair"]) for r in subset) != list(range(block_size)):
                errors.append(f"Schedule block {block}: missing/duplicate angle pairs")
            if v8:
                signed_steps = {0,45,-45,90,-90,135,-135,180}
                if Counter(int(r["signed_theta_deg"]) for r in subset) != Counter({a:4 for a in signed_steps}):
                    errors.append(f"Schedule block {block}: signed-step quota mismatch")
                for offset in range(0, block_size, 8):
                    if {int(r["signed_theta_deg"]) for r in subset[offset:offset+8]} != signed_steps:
                        errors.append(f"Schedule block {block}: eight-trial signed-step group mismatch")
                if sum(float(r["target_height_fraction"]) > .5 for r in subset) != 16:
                    errors.append(f"Schedule block {block}: total height quota mismatch")
                for angle in signed_steps:
                    above = sum(float(r["target_height_fraction"]) > .5 for r in subset if int(r["signed_theta_deg"]) == angle)
                    if above != 2:
                        errors.append(f"Schedule block {block}: signed height quota mismatch at {angle}")
                for angle in (45,90,135):
                    if sum(float(r["target_height_fraction"]) > .5 for r in subset if int(r["absolute_theta_deg"]) == angle) != 4:
                        errors.append(f"Schedule block {block}: absolute height quota mismatch at {angle}")
            else:
                for offset in range(0, block_size, 5):
                    if {int(r["absolute_theta_deg"]) for r in subset[offset:offset + 5]} != {0,45,90,135,180}:
                        errors.append(f"Schedule block {block}: five-trial angle group mismatch")
                if pre_cue:
                    for angle in (0,45,90,135,180):
                        category = [r for r in subset if int(r["absolute_theta_deg"]) == angle]
                        if sum(float(r["target_height_fraction"]) > .5 for r in category) != 4:
                            errors.append(f"Schedule block {block}: height quota mismatch at {angle}")
                        if angle in (45,90,135) and sum(int(r["signed_theta_deg"]) > 0 for r in category) != 4:
                            errors.append(f"Schedule block {block}: direction quota mismatch at {angle}")
            identities = Counter((r["target_shape"], r["target_color"]) for r in subset)
            identity_counts.append(identities)
            if len(identities) != 24 or Counter(identities.values()) != Counter({1:16,2:8} if v8 else {1:8,2:16}):
                errors.append(f"Schedule block {block}: target repetition quota mismatch")
        if identity_counts[0] != identity_counts[1]:
            errors.append("Schedule: voice blocks have different target identity counts")
        second = {int(r["angle_pair"]): r for r in rows[block_size:]}
        for row in ([] if pre_cue else rows[:block_size]):
            mirror = second.get(int(row["angle_pair"]))
            if mirror is None or (int(row["signed_theta_deg"]) + int(mirror["signed_theta_deg"])) % 360 != 0:
                errors.append("Schedule: angle pair is not mirrored")
        if len({r["layout_seed"] for r in rows}) != total:
            errors.append("Schedule: repeated layout seed")
        records = summary["objectives"]
        if [r["index"] for r in records] != list(range(count)):
            errors.append("Schedule: summary must retain every enabled trial once, including not-started outcomes")
        for record in records:
            index = record["index"]
            if not isinstance(index, int) or not 0 <= index < count:
                errors.append("Schedule: summary trial index outside enabled plan")
                continue
            planned = rows[index]
            if pre_cue:
                snapshot = record.get("pre_cue_theta")
                started = finite_time(record.get("search_started_at")) or record.get("outcome") in ("completed", "timeout")
                if snapshot is None and started and summary.get("angle_balance_applied") is not False:
                    errors.append(f"Schedule trial {index}: missing pre-cue theta snapshot")
                if snapshot is not None:
                    try:
                        flatten_theta(snapshot, trial_index=index, assigned=int(planned["signed_theta_deg"]))
                        if (snapshot["targetHeightRelativeToCross"] > 0) != (float(planned["target_height_fraction"]) > .5):
                            errors.append(f"Schedule trial {index}: realized target height side disagrees with plan")
                    except ValueError as error:
                        errors.append(f"Schedule trial {index}: {error}")
            for key in ("start_plane", "target_plane", "signed_theta_deg", "absolute_theta_deg", "angle_pair", "layout_seed"):
                if (pre_cue and type(record[key]) is not int) or record[key] != int(planned[key]):
                    errors.append(f"Schedule trial {index}: summary {key} disagrees with manifest")
            if (record["shape"] != planned["target_shape"] or record["color"] != planned["target_color"]
                    or record["voice_condition"] != planned["voice_condition"]):
                errors.append(f"Schedule trial {index}: summary target/voice disagrees with manifest")
    except (OSError, ValueError, KeyError, TypeError) as error:
        errors.append(f"Schedule: missing or malformed evidence ({error})")
    return errors


def clip_condition(clip):
    # Current recordings share a provider. Only legacy manifests infer condition
    # from the old two-provider arrangement.
    return clip.get("voice_condition") or {"elevenlabs": "neutral", "mistral": "selfsimilar"}.get(clip.get("provider"))


def finite_time(value):
    """CSV numbers may be strings; JSON booleans are never measured seconds."""
    if value is None or isinstance(value, bool):
        return False
    try:
        return math.isfinite(float(value)) and float(value) >= 0
    except (TypeError, ValueError, OverflowError):
        return False


def completed_trial_timing(record, events, timeout_limit=None):
    """Use the event sequence to delimit active search, excluding readiness pauses."""
    trial = record["trial_id"]
    errors = []
    timeout = timeout_limit is not None
    duration_key = "search_exposure_seconds" if timeout else "time_to_find_seconds"
    for key in ("objects_ready_at", duration_key):
        if not finite_time(record.get(key)):
            errors.append(f"{trial}: invalid {key}")
    if any(not finite_time(e.get("timestamp")) for e in events):
        errors.append(f"{trial}: invalid event timestamp")
    if errors:
        return errors
    times = [float(e["timestamp"]) for e in events]
    if any(later < earlier for earlier, later in zip(times, times[1:])):
        return [f"{trial}: event timestamps are out of order"]
    starts = [i for i, e in enumerate(events) if e["event_type"] == "search_start"]
    terminal_event = "trial_timeout" if timeout else "capture_correct"
    ends = [i for i, e in enumerate(events) if e["event_type"] == terminal_event]
    if any(e["event_type"] == ("capture_correct" if timeout else "trial_timeout") for e in events):
        errors.append(f"{trial}: contradictory terminal event")
    if len(starts) != 1 or len(ends) != 1 or starts[0] >= ends[0]:
        return errors + [f"{trial}: missing/duplicate or reversed onset/{terminal_event}"]
    start, end = starts[0], ends[0]
    announcements = [i for i, e in enumerate(events) if e["event_type"] == "audio_playback_end"
                     and "context=round;" in e["detail"]]
    if not announcements or max(announcements) >= start:
        errors.append(f"{trial}: missing completed announcement before search")
    if times[start] < float(record["objects_ready_at"]):
        errors.append(f"{trial}: search started before objects ready")
    # Readiness may pause using the same named events. Only the search interval
    # contributes to active duration; success/transition events are outside it.
    active, elapsed = times[start], 0.0
    for index in range(start + 1, end):
        kind, timestamp = events[index]["event_type"], times[index]
        if kind == "search_paused":
            if active is None:
                errors.append(f"{trial}: duplicate search pause")
            else:
                elapsed += timestamp - active
                active = None
        elif kind == "search_resumed":
            if active is not None:
                errors.append(f"{trial}: search resumed without a pause")
            else:
                active = timestamp
    if active is None:
        errors.append(f"{trial}: {terminal_event} occurred while search was paused")
    else:
        elapsed += times[end] - active
    if timeout:
        # Expire() caps exposure at the deadline; its event is emitted on the
        # first frame that observes expiry and can be later than that deadline.
        if (not finite_time(timeout_limit) or float(timeout_limit) <= 0
                or elapsed + .03 < float(timeout_limit)
                or abs(float(record[duration_key]) - float(timeout_limit)) > .03):
            errors.append(f"{trial}: timeout duration/deadline mismatch")
        detail = dict(part.split("=", 1) for part in events[end]["detail"].split(";") if "=" in part)
        declared = detail.get("active_search_limit_seconds")
        if not finite_time(declared) or not finite_time(timeout_limit) or abs(float(declared) - float(timeout_limit)) > .001:
            errors.append(f"{trial}: timeout event deadline mismatch")
        if finite_time(record.get("capture_at")) or finite_time(record.get("time_to_find_seconds")):
            errors.append(f"{trial}: timeout contains a success time")
    elif abs(elapsed - float(record[duration_key])) > 0.03:
        errors.append(f"{trial}: search duration disagrees with events")
    if not timeout and "search_exposure_seconds" in record and (
            not finite_time(record["search_exposure_seconds"])
            or abs(float(record["search_exposure_seconds"]) - elapsed) > .03):
        errors.append(f"{trial}: search exposure disagrees with events")
    for key, actual in (("search_started_at", times[start]),) + (() if timeout else (("capture_at", times[end]),)):
        if key in record and (not finite_time(record[key]) or abs(float(record[key]) - actual) > 0.03):
            errors.append(f"{trial}: {key} disagrees with events")
    return errors


def is_opening_event(event):
    return event["event_type"] == "opening_direction_started" or (
        event["event_type"] == "audio_playback_start"
        and "context=guidance_opening;" in event["detail"])


def validate(folder, allow_incomplete=False):
    errors = []
    summary = json.loads((folder / "trial_summary.json").read_text(encoding="utf-8-sig"))
    errors.extend(validate_schedule(folder, summary))
    records = summary["objectives"]
    if summary.get("schema_version") != 2:
        errors.append("Expected schema_version 2")
    if not allow_incomplete and summary.get("session_outcome") != "completed":
        errors.append("Session is incomplete")
    expected_trials = summary.get("total_rounds", 14)
    if type(expected_trials) is not int or expected_trials <= 0:
        return errors + ["Invalid total_rounds"]
    claims_complete = summary.get("session_outcome") == "completed"
    if (claims_complete or not allow_incomplete) and len(records) != expected_trials:
        errors.append(f"Expected {expected_trials} experimental trials")
    indices, trial_ids = set(), set()
    for record in records:
        index, trial = record.get("index"), record.get("trial_id")
        if type(index) is not int or not 0 <= index < expected_trials or index in indices:
            errors.append("Invalid or duplicate trial index")
        else:
            indices.add(index)
        if not isinstance(trial, str) or not trial.strip() or trial in trial_ids:
            errors.append("Missing or duplicate trial identity")
        else:
            trial_ids.add(trial)
        completed = record.get("completed")
        if type(completed) is not bool:
            errors.append(f"{trial}: completed must be boolean")
        if claims_complete and completed is not True and record.get("outcome") != "timeout":
            errors.append(f"{trial}: completed session contains an unfinished trial")
        if "outcome" in record and ((record["outcome"] == "completed") != (completed is True)):
            errors.append(f"{trial}: completion flag and outcome disagree")
    if any(type(record.get("index")) is not int for record in records):
        return errors  # Cannot safely derive voice blocks from malformed indices.
    with (folder / "trial_events.csv").open(encoding="utf-8-sig", newline="") as file:
        events = list(csv.DictReader(file))
    objects = []
    if (folder / "object_manifest.csv").exists():
        with (folder / "object_manifest.csv").open(encoding="utf-8-sig", newline="") as file:
            objects = list(csv.DictReader(file))
    clip_rows = json.loads((folder / "voice-library-manifest.json").read_text())["clips"]
    clips = {clip["clip_id"]: clip for clip in clip_rows}
    if len(clips) != len(clip_rows) or any(not isinstance(key, str) or not key for key in clips):
        errors.append("Missing or duplicate voice clip identity")
    sets = {}
    for clip in clips.values():
        sets.setdefault(clip_condition(clip), set()).add(clip["text"])
    explicit_conditions = all(clip.get("voice_condition") for clip in clips.values())
    # Historical perspectives deliberately differed. The current shared-command
    # policy must actually contain the same prepared text in both voices.
    shared_commands = summary.get("voice_perspective") == "shared_participant_directed_imperative"
    quick = summary.get("challenge_set") in QUICK_SCHEDULES
    quick_neutral = str(summary.get("voice_condition", "")).startswith("neutral-")
    if quick:
        if not sets.get("neutral" if quick_neutral else "selfsimilar"):
            errors.append("Quick-test voice phrase set is empty")
    elif not sets.get("neutral") or not sets.get("selfsimilar") or (
        (shared_commands or not explicit_conditions) and sets.get("neutral") != sets.get("selfsimilar")
    ):
        errors.append("Voice phrase sets differ or are empty")
    if summary.get("voice_order") not in ("neutral_then_selfsimilar", "selfsimilar_then_neutral"):
        return errors + ["Invalid voice order"]
    neutral_first = summary["voice_order"] == "neutral_then_selfsimilar"
    neutral_id = summary.get("neutral_voice_id") or {"male": "cjVigY5qzO86Huf0OWal", "female": "21m00Tcm4TlvDq8ikWAM"}[summary["neutral_voice_profile"]]
    rounds_per_block = summary.get("rounds_per_block", 7)
    objects_per_round = summary.get("objects_per_round", 56)
    if type(rounds_per_block) is not int or rounds_per_block <= 0 or type(objects_per_round) is not int or objects_per_round <= 0:
        return errors + ["Invalid block or object count"]
    if summary.get("challenge_set") in PRE_CUE_SCHEDULES and objects_per_round != 168:
        errors.append("Schedule: pre-cue schedule requires 168 objects per trial")
    for record in records:
        trial = record["trial_id"]
        neutral = quick_neutral if quick else (record["index"] < rounds_per_block) == neutral_first
        expected = "neutral-" + summary["neutral_voice_profile"] if neutral else "selfsimilar"
        if record["voice_condition"] != expected:
            errors.append(f"{trial}: wrong voice assignment")
        trial_events = [event for event in events if event["trial_id"] == trial]
        if summary.get("challenge_set") in PRE_CUE_SCHEDULES:
            snapshots = [event for event in trial_events if event["event_type"] == "theta_snapshot"]
            snapshot = record.get("pre_cue_theta")
            started = any(e["event_type"] == "search_start" for e in trial_events)
            if started and snapshot is None and summary.get("angle_balance_applied") is not False:
                errors.append(f"{trial}: search event has no pre-cue theta snapshot")
            if snapshots or snapshot is not None or any(is_opening_event(e) for e in trial_events):
                try:
                    if not snapshots or json.loads(snapshots[-1]["detail"]) != snapshot:
                        errors.append(f"{trial}: final theta event/summary mismatch")
                    seen_search = False
                    previous_snapshot = None
                    reset_since_snapshot = False
                    for event in trial_events:
                        kind = event["event_type"]
                        if kind == "search_start": seen_search = True
                        if kind == "readiness_reset": reset_since_snapshot = True
                        if is_opening_event(event) and (previous_snapshot is None or reset_since_snapshot):
                            errors.append(f"{trial}: opening cue precedes its pre-cue theta snapshot")
                        if kind != "theta_snapshot": continue
                        decoded = json.loads(event["detail"])
                        if decoded is None:
                            raise ValueError("null snapshot event")
                        flatten_theta(decoded, trial_index=record["index"], assigned=record["signed_theta_deg"])
                        if seen_search:
                            errors.append(f"{trial}: theta snapshot occurs after search onset")
                        if previous_snapshot is not None and (not reset_since_snapshot or decoded["time"] <= previous_snapshot["time"]):
                            errors.append(f"{trial}: repeated theta snapshot without readiness reset/new timestamp")
                        previous_snapshot = decoded
                        reset_since_snapshot = False
                except (ValueError, KeyError, TypeError):
                    errors.append(f"{trial}: malformed theta snapshot event")
            if started:
                presented = [o for o in objects if o["trial_id"] == trial]
                if (len(presented) != objects_per_round
                        or len({o["object_id"] for o in presented}) != objects_per_round
                        or sum(o["is_target"] == "1" for o in presented) != 1):
                    errors.append(f"{trial}: started trial lacks a complete unique object manifest")
            if snapshot is not None and started:
                targets = [o for o in objects if o["trial_id"] == trial and o["is_target"] == "1"]
                try:
                    if len(targets) != 1 or any(not math.isfinite(float(targets[0][a])) or abs(float(targets[0][a]) - snapshot["targetPosition"][a]) > .001 for a in "xyz"):
                        errors.append(f"{trial}: manifest target disagrees with pre-cue snapshot")
                except (KeyError, TypeError, ValueError):
                    errors.append(f"{trial}: invalid theta manifest geometry")
        for event in trial_events:
            if event["event_type"] != "audio_playback_start":
                continue
            details = dict(part.split("=", 1) for part in event["detail"].split(";") if "=" in part)
            clip = clips.get(details.get("clip_id"))
            if not clip or clip_condition(clip) != ("neutral" if neutral else "selfsimilar") or (neutral and clip["voice_id"] != neutral_id):
                errors.append(f"{trial}: playback identity disagrees with assignment")
        is_timeout = record.get("outcome") == "timeout"
        if record.get("completed") is not True and not is_timeout:
            if any(e["event_type"] in ("capture_correct", "trial_timeout") for e in trial_events):
                errors.append(f"{trial}: unfinished trial contains a terminal event")
            if any(not finite_time(e.get("timestamp")) for e in trial_events):
                errors.append(f"{trial}: invalid event timestamp")
            else:
                times = [float(e["timestamp"]) for e in trial_events]
                if any(b < a for a, b in zip(times, times[1:])):
                    errors.append(f"{trial}: event timestamps are out of order")
            continue
        if is_timeout:
            limit = summary.get("active_search_limit_seconds")
            if not finite_time(limit) or float(limit) <= 0:
                errors.append(f"{trial}: missing/invalid timeout deadline")
            else:
                errors.extend(completed_trial_timing(record, trial_events, limit))
        else:
            errors.extend(completed_trial_timing(record, trial_events))
            limit = summary.get("active_search_limit_seconds")
            if limit is not None or summary.get("challenge_set") in PRE_CUE_SCHEDULES:
                if not finite_time(limit) or float(limit) <= 0:
                    errors.append(f"{trial}: missing/invalid search deadline")
                elif finite_time(record.get("time_to_find_seconds")) and float(record["time_to_find_seconds"]) > float(limit) + .03:
                    errors.append(f"{trial}: successful search exceeds the recorded deadline")
        trial_objects = [obj for obj in objects if obj["trial_id"] == trial]
        if len(trial_objects) != objects_per_round or len({obj["object_id"] for obj in trial_objects}) != objects_per_round or sum(obj["is_target"] == "1" for obj in trial_objects) != 1:
            errors.append(f"{trial}: expected {objects_per_round} unique objects and one target")
    return errors


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_folder", type=Path)
    parser.add_argument("--allow-incomplete", action="store_true")
    args = parser.parse_args()
    try:
        failures = validate(args.run_folder, args.allow_incomplete)
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        failures = [str(error)]
    for failure in failures:
        print("FAIL:", failure)
    print("PASS: recorded voice, timing and object identities agree." if not failures else f"{len(failures)} validation failure(s)")
    raise SystemExit(bool(failures))
