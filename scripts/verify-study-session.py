#!/usr/bin/env python3
"""Validate a collected two-voice run. This checks logs, not perceived voice quality."""
import argparse
import csv
import json
from pathlib import Path


def validate(folder, allow_incomplete=False):
    errors = []
    summary = json.loads((folder / "trial_summary.json").read_text(encoding="utf-8-sig"))
    records = summary["objectives"]
    if summary.get("schema_version") != 2:
        errors.append("Expected schema_version 2")
    if not allow_incomplete and summary.get("session_outcome") != "completed":
        errors.append("Session is incomplete")
    if not allow_incomplete and len(records) != 14:
        errors.append("Expected 14 experimental trials")
    with (folder / "trial_events.csv").open(encoding="utf-8-sig", newline="") as file:
        events = list(csv.DictReader(file))
    objects = []
    if (folder / "object_manifest.csv").exists():
        with (folder / "object_manifest.csv").open(encoding="utf-8-sig", newline="") as file:
            objects = list(csv.DictReader(file))
    clips = {clip["clip_id"]: clip for clip in json.loads((folder / "voice-library-manifest.json").read_text())["clips"]}
    sets = {}
    for clip in clips.values():
        sets.setdefault(clip["provider"], set()).add(clip["text"])
    if sets.get("elevenlabs") != sets.get("mistral") or not sets.get("elevenlabs"):
        errors.append("Voice phrase sets differ or are empty")
    neutral_first = summary["voice_order"] == "neutral_then_selfsimilar"
    neutral_id = {"male": "cjVigY5qzO86Huf0OWal", "female": "21m00Tcm4TlvDq8ikWAM"}[summary["neutral_voice_profile"]]
    for record in records:
        trial = record["trial_id"]
        neutral = (record["index"] < 7) == neutral_first
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
        if len(trial_objects) != 56 or len({obj["object_id"] for obj in trial_objects}) != 56 or sum(obj["is_target"] == "1" for obj in trial_objects) != 1:
            errors.append(f"{trial}: expected 56 unique objects and one target")
        for event in trial_events:
            if event["event_type"] != "audio_playback_start":
                continue
            details = dict(part.split("=", 1) for part in event["detail"].split(";") if "=" in part)
            clip = clips.get(details.get("clip_id"))
            if not clip or (neutral and clip["voice_id"] != neutral_id) or (not neutral and clip["provider"] != "mistral"):
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
