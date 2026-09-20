#!/usr/bin/env python3
"""Descriptive replay metrics. Hover is an interaction proxy, never a validated fixation."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path

VERSION = "trial-replay-analysis/1"


def read_records(path):
    records = []
    warning = None
    with Path(path).open(encoding="utf-8") as stream:
        lines = stream.readlines()
    for index, line in enumerate(lines):
        try:
            record = json.loads(line)
        except json.JSONDecodeError:
            if index == len(lines) - 1 and records and not line.rstrip().endswith("}"):
                warning = "Incomplete trailing record omitted"
                break
            raise ValueError(f"Corrupt replay at line {index + 1}") from None
        if not isinstance(record, dict) or record.get("schema") != 1:
            raise ValueError("Unsupported replay schema")
        timestamp = record.get("time")
        sequence = record.get("sequence")
        if not isinstance(timestamp, (int, float)) or not math.isfinite(timestamp) or timestamp < 0:
            raise ValueError("Invalid replay time")
        if not isinstance(sequence, int) or sequence < 0:
            raise ValueError("Invalid replay sequence")
        if records and (timestamp < records[-1]["time"] or sequence <= records[-1]["sequence"] or records[-1]["kind"] == "end"):
            raise ValueError("Invalid replay ordering")
        if not records and (record.get("kind") != "header" or not record.get("recordingId")):
            raise ValueError("Missing replay header")
        if records and record.get("kind") == "header":
            raise ValueError("Unexpected replay header")
        records.append(record)
    if not records:
        raise ValueError("Empty replay")
    if records[-1].get("kind") != "end" or not records[-1].get("complete"):
        warning = warning or "Incomplete recording; metrics use saved prefix only"
    return records, warning


def metrics(records, gap_limit=0.1):
    if not math.isfinite(gap_limit) or gap_limit <= 0:
        raise ValueError("gap_limit must be positive")
    trials = {}
    current = None
    prior_sample = None
    active_start = None

    def observe_search_clock(record):
        if "searchSeconds" not in record:
            return
        value = record["searchSeconds"]
        if not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
            raise ValueError("Invalid recorded search clock")
        previous = current.get("_search_clock")
        if previous is not None and value < previous - 0.0001:
            raise ValueError("Recorded search clock went backward within a trial")
        current["_search_clock"] = value

    def stop(timestamp):
        nonlocal active_start
        if current is not None and active_start is not None:
            current["search_seconds"] += max(0, timestamp - active_start)
        active_start = None

    for r in records:
        kind = r["kind"]
        if kind == "trial":
            stop(r["time"])
            trial_id = r.get("trialId")
            objects = r.get("objects", [])
            ids = [o["id"] for o in objects]
            if not trial_id or trial_id in trials or len(set(ids)) != len(ids) or r.get("targetId") not in ids:
                raise ValueError("Invalid trial/object identity")
            current = dict(trial_id=trial_id, practice=bool(r.get("practice")), target_id=r["targetId"],
                           selections=0, wrong_selections=0, correct_selections=0, search_seconds=0.0,
                           samples=0, gaze_valid_samples=0, gaze_invalid_samples=0, gaze_unknown_samples=0,
                           sample_gap_seconds=0.0, sampled_hover_target_seconds=0.0,
                           sampled_hover_other_seconds=0.0, outcome="incomplete")
            trials[trial_id] = current
            current["_ids"] = set(ids)
            prior_sample = None
        elif kind in ("search_start", "search_resumed") and current is not None:
            if active_start is None:
                active_start = r["time"]
        elif kind in ("search_paused", "transition", "stop", "end"):
            if current is not None and active_start is not None:
                observe_search_clock(r)
            stop(r["time"])
            prior_sample = None
        elif kind == "selection":
            if current is None or r.get("trialId") != current["trial_id"] or r.get("objectId") not in current["_ids"]:
                raise ValueError("Selection has no matching object/trial")
            expected = r["objectId"] == current["target_id"]
            if bool(r.get("correct")) != expected:
                raise ValueError("Selection correctness disagrees with recorded target")
            current["selections"] += 1
            observe_search_clock(r)
            current["correct_selections" if expected else "wrong_selections"] += 1
            if expected:
                stop(r["time"])
                current["outcome"] = "completed"
        elif kind == "sample":
            if current is None or r.get("trialId") != current["trial_id"]:
                raise ValueError("Sample has no matching trial")
            current["samples"] += 1
            if r.get("searchActive") and active_start is not None:
                observe_search_clock(r)
            validity = r.get("gazeTracked", -1)
            if validity not in (-1, 0, 1):
                raise ValueError("Invalid gaze tracking state")
            current[{1: "gaze_valid_samples", 0: "gaze_invalid_samples", -1: "gaze_unknown_samples"}[validity]] += 1
            if prior_sample is not None and prior_sample.get("searchActive") and r.get("searchActive"):
                interval = r["time"] - prior_sample["time"]
                if interval > gap_limit:
                    current["sample_gap_seconds"] += interval
                elif prior_sample.get("gazeAvailable") and prior_sample.get("gazeTracked") != 0:
                    hover = prior_sample.get("hoveredId")
                    if hover:
                        if hover not in current["_ids"]:
                            raise ValueError("Unknown hovered object")
                        key = "sampled_hover_target_seconds" if hover == current["target_id"] else "sampled_hover_other_seconds"
                        current[key] += interval
            prior_sample = r
    # An interrupted prefix bounds exposure at its last observation; no extrapolation.
    stop(records[-1]["time"])
    for row in trials.values():
        del row["_ids"]
        recorded_clock = row.pop("_search_clock", None)
        row["search_clock"] = "event_timeline" if recorded_clock is None else "recorded_search_clock"
        if recorded_clock is not None:
            row["search_seconds"] = recorded_clock
        for key, value in row.items():
            if isinstance(value, float):
                row[key] = round(value, 6)
    return list(trials.values())


def analyze(source, output, gap_limit=0.1):
    source = Path(source).resolve(strict=True)
    output = Path(output).resolve()
    if output == source.parent or source.parent in output.parents:
        raise ValueError("Derived output must be outside the original recording folder")
    records, warning = read_records(source)
    rows = metrics(records, gap_limit)
    output.mkdir(parents=True, exist_ok=False)
    provenance = dict(analysis_version=VERSION, source_recording_id=records[0]["recordingId"],
                      source_type=records[0].get("sourceType"), source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                      warning=warning, gap_limit_seconds=gap_limit,
                      interpretation="Derived measurements from original signals; hover is an interaction proxy, not a validated fixation.",
                      unknown_validity="Unknown tracking remains unknown; sampled hover includes available interaction poses with unknown validity.")
    (output / "provenance.json").write_text(json.dumps(provenance, indent=2) + "\n")
    if rows:
        with (output / "trial_metrics.csv").open("w", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
    return rows


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("output", type=Path, help="New directory outside the recording folder")
    parser.add_argument("--gap-limit", type=float, default=0.1)
    args = parser.parse_args()
    try:
        rows = analyze(args.recording, args.output, args.gap_limit)
    except (OSError, ValueError) as error:
        parser.exit(1, f"Replay analysis failed: {error}\n")
    print(f"Wrote {len(rows)} trial rows to {args.output}")
