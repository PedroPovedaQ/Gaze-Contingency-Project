#!/usr/bin/env python3
"""Audit passive OpenXR gaze-pose queries; never certify a sensor acquisition rate.

Usage: python3 analysis/native_gaze_timing.py EYE_MEASUREMENT_DIRECTORY --output report.json
Only compare timestamps within their own clock domain. Native receipt time describes
software query completion; XrEyeGazeSampleTimeEXT describes runtime pose-expression
time, which may be clamped, interpolated or predicted. Neither is raw capture time.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
import json
import math
from pathlib import Path
from typing import Any

GROUP_FIELDS = ("epoch", "session", "space", "baseSpace")
RECORD_FIELDS = GROUP_FIELDS + ("sequence", "locationFlags", "requestedTime", "runtimePoseTime", "receiptMonotonicNs", "result", "timestampChained")
DISCLAIMER = "120 Hz sensor acquisition is not established by this report. Runtime pose times may be clamped, predicted or interpolated; receipt times measure software query completion."


def _integer(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool)


def _distribution(values: list[float]) -> dict[str, Any]:
    if not values:
        return {"count": 0, "median": None, "p95": None, "maximum": None}
    ordered = sorted(values)
    def percentile(q: float) -> float:
        index = (len(ordered) - 1) * q
        low = math.floor(index)
        return ordered[low] + (ordered[math.ceil(index)] - ordered[low]) * (index - low)
    return {"count": len(values), "median": percentile(.5), "p95": percentile(.95), "maximum": ordered[-1]}


def _sidecar(path: Path, warnings: list[str]) -> dict[str, Any] | None:
    if not path.exists():
        warnings.append(f"Missing {path.name}; corresponding evidence is unknown.")
        return None
    try:
        value = json.loads(path.read_text())
        if not isinstance(value, dict):
            raise ValueError("expected object")
        return value
    except (OSError, ValueError) as error:
        warnings.append(f"Cannot read {path.name}: {error}")
        return None


def _group_report(key: tuple[int, ...], records: list[dict], gap_ms: float, sustained_seconds: float) -> dict:
    # Preserve recorded enqueue order: sorting by timestamp would hide regressions.
    receipts = [r["receiptMonotonicNs"] for r in records if r["receiptMonotonicNs"] > 0]
    receipt_deltas = [(b-a)/1e6 for a,b in zip(receipts, receipts[1:])]
    successful = [r for r in records if r["result"] >= 0]
    valid = [r for r in successful if r["locationFlags"] & 3 == 3]
    timed = [r for r in successful if r["timestampChained"] and r["runtimePoseTime"] > 0]
    times = [r["runtimePoseTime"] for r in timed]
    pose_deltas = [(b-a)/1e6 for a,b in zip(times, times[1:])]
    span = (max(receipts)-min(receipts))/1e9 if len(receipts)>1 else 0
    time_span = (max(times)-min(times))/1e9 if len(times)>1 else 0
    seen: set[int] = set()
    repeats = 0
    for timestamp in times:
        repeats += timestamp in seen
        seen.add(timestamp)
    # Qualifying diagnostic runs require successful, valid poses and focused drain
    # evidence. Focus is observed at drain, not necessarily at native capture time.
    runs: list[list[dict]] = []
    current: list[dict] = []
    for record in records:
        qualifies = record["result"] >= 0 and record["locationFlags"] & 3 == 3 and record["_focused"] is True and record["receiptMonotonicNs"] > 0
        if current:
            delta = (record["receiptMonotonicNs"]-current[-1]["receiptMonotonicNs"])/1e6
            if not qualifies or not 0 < delta <= gap_ms or record["_continuity_segment"] != current[-1]["_continuity_segment"]:
                runs.append(current)
                current=[]
        if qualifies:
            current.append(record)
    if current:
        runs.append(current)
    def run_stats(run: list[dict]) -> dict:
        duration=(run[-1]["receiptMonotonicNs"]-run[0]["receiptMonotonicNs"])/1e9
        return {"start_sequence":run[0]["sequence"], "end_sequence":run[-1]["sequence"], "queries":len(run), "duration_seconds":duration, "query_completion_hz":(len(run)-1)/duration if duration else None}
    run_results=[run_stats(run) for run in runs]
    return {
        **dict(zip(GROUP_FIELDS,key)), "queries":len(records), "successful_queries":len(successful),
        "failed_queries":len(records)-len(successful), "valid_position_and_orientation_queries":len(valid),
        "tracked_position_and_orientation_queries":sum(r["locationFlags"] & 15 == 15 for r in successful),
        "focused_at_drain_queries":sum(r["_focused"] is True for r in records),
        "unfocused_at_drain_queries":sum(r["_focused"] is False for r in records),
        "unknown_focus_queries":sum(r["_focused"] is None for r in records),
        "zero_pose_times_on_success":sum(r["runtimePoseTime"] == 0 for r in successful),
        "negative_pose_times_on_success":sum(r["runtimePoseTime"] < 0 for r in successful),
        "timestamp_not_chained_on_success":sum(not r["timestampChained"] for r in successful),
        "distinct_reported_pose_times":len(seen), "repeated_reported_pose_times":repeats,
        "regressing_reported_pose_times":sum(delta<0 for delta in pose_deltas),
        "pose_time_equals_requested_time":sum(r["runtimePoseTime"] == r["requestedTime"] for r in timed),
        "pose_time_after_requested_time":sum(r["runtimePoseTime"] > r["requestedTime"] for r in timed),
        "pose_time_before_requested_time":sum(r["runtimePoseTime"] < r["requestedTime"] for r in timed),
        "query_receipt_span_seconds":span or None,
        "query_completion_hz":(len(receipts)-1)/span if span else None,
        "distinct_pose_time_span_seconds":time_span or None,
        "distinct_reported_pose_time_hz":(len(seen)-1)/time_span if time_span else None,
        "positive_receipt_intervals_ms":_distribution([x for x in receipt_deltas if x>0]),
        "receipt_gaps_over_threshold":sum(x>gap_ms for x in receipt_deltas),
        "nonincreasing_receipt_intervals":sum(x<=0 for x in receipt_deltas),
        "unavailable_receipt_times":len(records)-len(receipts),
        "positive_pose_time_intervals_ms":_distribution([x for x in pose_deltas if x>0]),
        "longest_valid_focused_query_run":max(run_results,key=lambda r:r["duration_seconds"],default=None),
        "sustained_valid_focused_query_runs":[run for run in run_results if run["duration_seconds"]>=sustained_seconds],
    }


def analyze(path: str | Path, *, gap_ms: float = 50, sustained_seconds: float = 60) -> dict[str, Any]:
    """Return JSON-compatible metrics with null for unavailable evidence."""
    if not math.isfinite(gap_ms) or not math.isfinite(sustained_seconds) or gap_ms <= 0 or sustained_seconds <= 0:
        raise ValueError("gap_ms and sustained_seconds must be positive")
    path=Path(path)
    if path.is_dir():
        path=path/"native_gaze_observations.jsonl"
    warnings: list[str]=[]
    capabilities=_sidecar(path.parent/"native_gaze_capabilities.json",warnings)
    end=_sidecar(path.parent/"native_gaze_end_status.json",warnings)
    groups: dict[tuple,list]=defaultdict(list)
    epochs: dict[int,dict]=defaultdict(lambda:{"sequences":[],"dropped":None,"reported_records":None,"final_reported_records":None,"continuity_segment":0})
    batches=malformed_lines=invalid_records=unknown_lines=0
    availability: set[bool]=set()
    file_available=path.exists()
    if not file_available:
        warnings.append("Native observation file unavailable; an empty stream does not establish zero sensor output.")
    else:
        with path.open() as stream:
            for number,line in enumerate(stream,1):
                if not line.strip():
                    continue
                try:
                    batch=json.loads(line)
                    if not isinstance(batch,dict):
                        raise ValueError("expected object")
                except ValueError:
                    malformed_lines+=1
                    warnings.append(f"Malformed JSON at line {number}; possibly a partial write.")
                    continue
                if batch.get("kind") != "native_gaze_batch" or batch.get("schema") != 1:
                    unknown_lines+=1
                    continue
                batches+=1
                if isinstance(batch.get("featureAvailable"),bool):
                    availability.add(batch["featureAvailable"])
                status=batch.get("status")
                if isinstance(status,dict) and _integer(status.get("epoch")):
                    epoch=epochs[status["epoch"]]
                    for source,destination in (("dropped","dropped"),("records","reported_records")):
                        value=status.get(source)
                        if _integer(value) and value>=0:
                            epoch[destination]=max(epoch[destination] or 0,value)
                entries=batch.get("records")
                if not isinstance(entries,list):
                    invalid_records+=1
                    continue
                for record in entries:
                    if not isinstance(record,dict) or not all(_integer(record.get(field)) for field in RECORD_FIELDS) or any(record[field]<0 for field in GROUP_FIELDS+("locationFlags",)) or record["sequence"]<=0:
                        invalid_records+=1
                        continue
                    record=dict(record)
                    record["_focused"]=batch.get("focusedAtDrain") if isinstance(batch.get("focusedAtDrain"),bool) else None
                    key=tuple(record[field] for field in GROUP_FIELDS)
                    epoch = epochs[record["epoch"]]
                    # Detect gaps globally before splitting by action space. A query
                    # for another space is legitimate interleaving, not data loss.
                    if epoch["sequences"] and record["sequence"] != epoch["sequences"][-1] + 1:
                        epoch["continuity_segment"] += 1
                    record["_continuity_segment"] = epoch["continuity_segment"]
                    groups[key].append(record)
                    epoch["sequences"].append(record["sequence"])
    end_native=end.get("nativeStatus") if end else None
    if isinstance(end_native,dict) and _integer(end_native.get("epoch")):
        epoch=epochs[end_native["epoch"]]
        if _integer(end_native.get("records")) and end_native["records"] >= 0:
            epoch["final_reported_records"] = end_native["records"]
        for source,destination in (("dropped","dropped"),("records","reported_records")):
            value=end_native.get(source)
            if _integer(value) and value>=0:
                epoch[destination]=max(epoch[destination] or 0,value)
    epoch_reports=[]
    for number,epoch in sorted(epochs.items()):
        sequences=epoch["sequences"]
        unique=sorted(set(sequences))
        # Native sequence is global per epoch, so never count gaps per action space.
        missing=unique[0]-1+sum(b-a-1 for a,b in zip(unique,unique[1:])) if unique else 0
        tail=max(0,epoch["reported_records"]-(unique[-1] if unique else 0)) if epoch["reported_records"] is not None else None
        last_observed = unique[-1] if unique else 0
        counters = [value for value in (epoch["reported_records"], epoch["final_reported_records"]) if value is not None]
        counter_behind = any(value < last_observed for value in counters)
        epoch_reports.append({"reported_counter_behind_observed_sequence":counter_behind,"epoch":number,"observed_records":len(sequences),"duplicate_sequences":len(sequences)-len(unique),"nonincreasing_enqueue_sequences":sum(b<=a for a,b in zip(sequences,sequences[1:])),"missing_sequences_before_last_observed":missing,"reported_records_not_observed_after_last":tail,"native_buffer_drops_reported":epoch["dropped"]})
    total=sum(len(records) for records in groups.values())
    reasons=[]
    if not file_available: reasons.append("observation_file_missing")
    if not capabilities: reasons.append("capabilities_missing_or_invalid")
    if not end: reasons.append("end_status_missing_or_invalid")
    elif end.get("complete") is not True or end.get("error"): reasons.append("recorder_did_not_confirm_clean_close")
    if end and (not _integer(end.get("acceptedRecords")) or end["acceptedRecords"]!=total): reasons.append("accepted_record_count_unverified_or_mismatched")
    if malformed_lines or invalid_records or unknown_lines: reasons.append("unreadable_or_unsupported_records")
    if any(e["nonincreasing_enqueue_sequences"] for e in epoch_reports): reasons.append("nonincreasing_enqueue_sequences")
    if any(e["reported_counter_behind_observed_sequence"] for e in epoch_reports): reasons.append("reported_counter_behind_observed_sequence")
    if any(e["missing_sequences_before_last_observed"] or e["duplicate_sequences"] or e["reported_records_not_observed_after_last"] or e["native_buffer_drops_reported"] for e in epoch_reports): reasons.append("record_loss_or_sequence_inconsistency")
    if any(e["native_buffer_drops_reported"] is None or e["reported_records_not_observed_after_last"] is None for e in epoch_reports): reasons.append("native_status_coverage_unknown")
    if not epoch_reports: reasons.append("native_status_coverage_unknown")
    if not total: warnings.append("No native queries observed. Hook coverage, binding, runtime availability and session activity must be checked; sensor rate is unknown.")
    return {"schema":1,"kind":"native_gaze_timing_report","source":str(path),"sensor_120hz_established":False,"sensor_acquisition_hz":None,"interpretation":DISCLAIMER,
        "clock_policy":"No subtraction between XrTime, native steady-clock time, and Unity realtime. Pose time versus requested time compares XrTime only; it is not capture latency.",
        "parameters":{"gap_threshold_ms":gap_ms,"sustained_run_seconds":sustained_seconds,"sustained_definition":"Consecutive successful position-and-orientation-valid queries, focused at drain, without known global sequence gaps or ordering breaks, with positive receipt intervals no larger than the engineering gap threshold; not a sensor validation criterion."},
        "recording_integrity":{"status":"unknown_or_incomplete" if reasons else "complete_as_recorded","reasons":reasons,"batches":batches,"records":total,"malformed_lines":malformed_lines,"invalid_records":invalid_records,"unsupported_lines":unknown_lines},
        "feature_available_observed":sorted(availability),"capabilities":capabilities,"end_status":end,"epochs":epoch_reports,
        "groups":[_group_report(key,records,gap_ms,sustained_seconds) for key,records in sorted(groups.items())],"warnings":warnings}


def main() -> None:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path",type=Path)
    parser.add_argument("--output",type=Path)
    parser.add_argument("--gap-ms",type=float,default=50)
    parser.add_argument("--sustained-seconds",type=float,default=60)
    args=parser.parse_args()
    report=analyze(args.path,gap_ms=args.gap_ms,sustained_seconds=args.sustained_seconds)
    text=json.dumps(report,indent=2,allow_nan=False)+"\n"
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(text)
    else:
        print(text,end="")


if __name__=="__main__":
    main()
