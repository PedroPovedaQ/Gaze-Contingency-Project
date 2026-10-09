"""Lossless CSV projection of matched replay pose records, not eye acquisition data.

Called after trial_csv validates identities. Keep invalid and paused observations
and native replay order; neither interpolate nor silently treat blanks as zero.
"""

from __future__ import annotations

import csv
import json
from collections import defaultdict
from contextlib import ExitStack
from pathlib import Path

from mega_csv import flat, replay_rows

SCHEMA = "trial-motion-v1"
CONTEXT = ["motion_schema", "participant_id", "run_number", "trial_id", "block_index",
           "voice_condition", "is_practice", "session_mode", "data_purpose",
           "exclude_from_study_analysis", "analysis_exclusion_reason", "coordinate_frame",
           "recording_id", "record_kind", "replay_time_seconds", "active_search_seconds",
           "source_file", "source_line", "source_sequence", "source_record_json"]


def pose_rows(path: Path, trials: dict):
    """Use the exact measured/practice keys already accepted by the summary export."""
    recording_id = None
    for line, record in replay_rows(path, []):
        if record["kind"] == "header":
            recording_id = record.get("recordingId")
        if record["kind"] not in ("sample", "selection"):
            continue
        practice = record.get("practice") is True
        tid = (f"{recording_id}:{record.get('trialId')}" if practice
               else record.get("sourceTrialId"))
        trial = trials.get(tid)
        if trial is None or record.get("participantCode") != trial["participant_id"]:
            continue
        if not practice and (
            str(record.get("runNumber")) != str(trial["run_number"])
            or record.get("sourceRunFolder") != Path(trial["source_summary"]).parent.name
        ):
            continue
        row = {key: trial.get(key) for key in CONTEXT if key in trial}
        row.update(motion_schema=SCHEMA, coordinate_frame="replay_fixed_origin",
                   recording_id=recording_id, record_kind=record["kind"],
                   replay_time_seconds=record["time"], active_search_seconds=record.get("searchSeconds"),
                   source_file=str(path), source_line=line, source_sequence=record["sequence"],
                   source_record_json=json.dumps(record, ensure_ascii=False, sort_keys=True))
        row.update(flat(record, "replay__"))
        yield tid, row


def write_motion_package(staged: Path, rows: list[dict], entries: list[dict]) -> dict:
    """Discover columns, then stream samples into combined and per-trial companions."""
    by_path = defaultdict(dict)
    for row in rows:
        if row.get("replay_file"):
            by_path[Path(row["replay_file"])][row["trial_id"]] = row
    extra_fields = set()
    for path, trials in by_path.items():
        for _, row in pose_rows(path, trials):
            extra_fields.update(row.keys() - set(CONTEXT))
    fields = CONTEXT + sorted(extra_fields)
    entry_by_id = {entry["trial_id"]: entry for entry in entries}
    for row in rows:
        entry_by_id[row["trial_id"]].update(
            motion_file=None, motion_row_count=0,
            motion_status="no_pose_records" if row.get("replay_file") else "no_matched_replay")

    def writer(stack, name):
        stream = stack.enter_context((staged / name).open("w", encoding="utf-8", newline=""))
        result = csv.DictWriter(stream, fieldnames=fields)
        result.writeheader()
        return result

    count = 0
    with ExitStack() as combined_stack:
        combined = writer(combined_stack, "motion.csv")
        for path, trials in by_path.items():
            # Close this recording's handles before starting the next recording.
            with ExitStack() as stack:
                writers = {}
                for tid, row in pose_rows(path, trials):
                    entry = entry_by_id[tid]
                    if tid not in writers:
                        name = entry["file"].replace("_performance_subjective.csv", "_motion.csv")
                        writers[tid] = writer(stack, name)
                        entry.update(motion_file=name, motion_status="exported_raw_records")
                    values = {k: int(v) if isinstance(v, bool) else v for k, v in row.items()}
                    combined.writerow(values)
                    writers[tid].writerow(values)
                    entry["motion_row_count"] += 1
                    count += 1
    return {"schema": SCHEMA, "file": "motion.csv", "row_count": count,
            "record_kinds": ["sample", "selection"],
            "sampling_policy": "recorded rendered-frame and trigger snapshots; not native eye sample frequency",
            "missing_policy": "retain invalid, inactive and paused records; blank remains unavailable",
            "clock_policy": "replay time is local to recording_id; active search clock excludes pauses",
            "reconstruction_policy": "retain original JSONL for object snapshots, changes and events, plus audio/scene/video"}
