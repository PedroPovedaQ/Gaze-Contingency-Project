#!/usr/bin/env python3
"""Validate a collected two-voice run. This checks logs, not perceived voice quality."""
import argparse
import csv
import json
from collections import Counter
from pathlib import Path


def validate_schedule(folder, summary):
    """Audit the full v5 planned allocation, even when the observed run is partial."""
    version = "previous-wall-angle-v5-stratified-40-trials"
    if summary.get("challenge_set") != version:
        return []  # Historical schedules retain their original contract.
    errors = []
    try:
        with (folder / "trial_schedule.csv").open(encoding="utf-8-sig", newline="") as file:
            rows = list(csv.DictReader(file))
        if len(rows) != 80 or [int(r["trial_index"]) for r in rows] != list(range(80)):
            return ["Schedule: expected all 80 uniquely ordered planned trials"]
        count = summary["total_rounds"]
        override = summary["debug_round_override"]
        expected_count = max(1, min(80, override)) if override > 0 else 80
        if summary["rounds_per_block"] != 40 or summary["nominal_total_rounds"] != 80 or count != expected_count:
            errors.append("Schedule: block/full/debug counts disagree")
        for index, row in enumerate(rows):
            block = index // 40
            neutral = (block == 0) == (summary["voice_order"] == "neutral_then_selfsimilar")
            expected_voice = "neutral-" + summary["neutral_voice_profile"] if neutral else "selfsimilar"
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
                    or int(row["block_index"]) != block or int(row["trial_in_block"]) != index % 40
                    or row["voice_condition"] != expected_voice or int(row["enabled_in_run"]) != int(index < count)
                    or row["theta_reference"] != "previous_target_wall_center"
                    or row["target_identity_policy"] != summary["target_identity_policy"]
                    or row["height_assignment_policy"] != summary["height_assignment_policy"]):
                errors.append(f"Schedule trial {index}: identity, version, assignment or enabled flag mismatch")
        identity_counts = []
        for block in range(2):
            subset = rows[block * 40:(block + 1) * 40]
            if Counter(int(r["absolute_theta_deg"]) for r in subset) != Counter({angle: 8 for angle in (0,45,90,135,180)}):
                errors.append(f"Schedule block {block}: absolute-angle quota mismatch")
            if sorted(int(r["angle_pair"]) for r in subset) != list(range(40)):
                errors.append(f"Schedule block {block}: missing/duplicate angle pairs")
            for offset in range(0, 40, 5):
                if {int(r["absolute_theta_deg"]) for r in subset[offset:offset + 5]} != {0,45,90,135,180}:
                    errors.append(f"Schedule block {block}: five-trial angle group mismatch")
            identities = Counter((r["target_shape"], r["target_color"]) for r in subset)
            identity_counts.append(identities)
            if len(identities) != 24 or Counter(identities.values()) != Counter({1: 8, 2: 16}):
                errors.append(f"Schedule block {block}: target repetition quota mismatch")
        if identity_counts[0] != identity_counts[1]:
            errors.append("Schedule: voice blocks have different target identity counts")
        second = {int(r["angle_pair"]): r for r in rows[40:]}
        for row in rows[:40]:
            mirror = second.get(int(row["angle_pair"]))
            if mirror is None or (int(row["signed_theta_deg"]) + int(mirror["signed_theta_deg"])) % 360 != 0:
                errors.append("Schedule: angle pair is not mirrored")
        if len({r["layout_seed"] for r in rows}) != 80:
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
            for key in ("start_plane", "target_plane", "signed_theta_deg", "absolute_theta_deg", "angle_pair", "layout_seed"):
                if record[key] != int(planned[key]):
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
    if not allow_incomplete and len(records) != expected_trials:
        errors.append(f"Expected {expected_trials} experimental trials")
    with (folder / "trial_events.csv").open(encoding="utf-8-sig", newline="") as file:
        events = list(csv.DictReader(file))
    objects = []
    if (folder / "object_manifest.csv").exists():
        with (folder / "object_manifest.csv").open(encoding="utf-8-sig", newline="") as file:
            objects = list(csv.DictReader(file))
    clips = {clip["clip_id"]: clip for clip in json.loads((folder / "voice-library-manifest.json").read_text())["clips"]}
    sets = {}
    for clip in clips.values():
        sets.setdefault(clip_condition(clip), set()).add(clip["text"])
    explicit_conditions = all(clip.get("voice_condition") for clip in clips.values())
    # Historical perspectives deliberately differed. The current shared-command
    # policy must actually contain the same prepared text in both voices.
    shared_commands = summary.get("voice_perspective") == "shared_participant_directed_imperative"
    if not sets.get("neutral") or not sets.get("selfsimilar") or (
        (shared_commands or not explicit_conditions) and sets.get("neutral") != sets.get("selfsimilar")
    ):
        errors.append("Voice phrase sets differ or are empty")
    neutral_first = summary["voice_order"] == "neutral_then_selfsimilar"
    neutral_id = summary.get("neutral_voice_id") or {"male": "cjVigY5qzO86Huf0OWal", "female": "21m00Tcm4TlvDq8ikWAM"}[summary["neutral_voice_profile"]]
    rounds_per_block = summary.get("rounds_per_block", 7)
    objects_per_round = summary.get("objects_per_round", 56)
    for record in records:
        trial = record["trial_id"]
        neutral = (record["index"] < rounds_per_block) == neutral_first
        expected = "neutral-" + summary["neutral_voice_profile"] if neutral else "selfsimilar"
        if record["voice_condition"] != expected:
            errors.append(f"{trial}: wrong voice assignment")
        if not record["completed"]:
            continue
        trial_events = [event for event in events if event["trial_id"] == trial]
        starts = [float(event["timestamp"]) for event in trial_events if event["event_type"] == "search_start"]
        ends = [float(event["timestamp"]) for event in trial_events if event["event_type"] == "capture_correct"]
        announcements = [float(event["timestamp"]) for event in trial_events
                         if event["event_type"] == "audio_playback_end" and "context=round;" in event["detail"]]
        if len(starts) != 1 or len(ends) != 1 or not announcements:
            errors.append(f"{trial}: missing/duplicate onset, capture or completed announcement")
            continue
        if starts[0] < max(record["objects_ready_at"], announcements[-1]):
            errors.append(f"{trial}: search started before objects/announcement ready")
        active, elapsed = starts[0], 0.0
        for event in trial_events:
            if event["event_type"] == "search_paused" and active is not None:
                elapsed += float(event["timestamp"]) - active
                active = None
            elif event["event_type"] == "search_resumed":
                active = float(event["timestamp"])
        if active is not None:
            elapsed += ends[0] - active
        if abs(elapsed - record["time_to_find_seconds"]) > 0.03:
            errors.append(f"{trial}: search duration disagrees with events")
        trial_objects = [obj for obj in objects if obj["trial_id"] == trial]
        if len(trial_objects) != objects_per_round or len({obj["object_id"] for obj in trial_objects}) != objects_per_round or sum(obj["is_target"] == "1" for obj in trial_objects) != 1:
            errors.append(f"{trial}: expected {objects_per_round} unique objects and one target")
        for event in trial_events:
            if event["event_type"] != "audio_playback_start":
                continue
            details = dict(part.split("=", 1) for part in event["detail"].split(";") if "=" in part)
            clip = clips.get(details.get("clip_id"))
            if not clip or clip_condition(clip) != ("neutral" if neutral else "selfsimilar") or (neutral and clip["voice_id"] != neutral_id):
                errors.append(f"{trial}: playback identity disagrees with assignment")
    return errors


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_folder", type=Path)
    parser.add_argument("--allow-incomplete", action="store_true")
    args = parser.parse_args()
    try:
        failures = validate(args.run_folder, args.allow_incomplete)
    except (OSError, ValueError, KeyError) as error:
        failures = [str(error)]
    for failure in failures:
        print("FAIL:", failure)
    print("PASS: recorded voice, timing and object identities agree." if not failures else f"{len(failures)} validation failure(s)")
    raise SystemExit(bool(failures))
