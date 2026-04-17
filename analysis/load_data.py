"""
Loads all trial summary JSONs from the GazeData folder into a flat DataFrame.
One row per (participant, run, round). Easy to filter/aggregate by condition.
"""

import json
from pathlib import Path
import pandas as pd


SUMMARY_COLUMNS = [
    "participant_id", "run_number", "condition", "block", "round_index",
    "shape", "color", "completed", "time_to_find", "wrong_captures",
    "fixation_time_on_target", "fixation_time_on_distractors",
    "fixation_count_total", "fixation_count_on_target", "fixation_count_on_distractors",
    "avg_fixation_duration", "saccade_count", "saccade_frequency_hz",
    "avg_saccade_amplitude_deg", "first_try_correct",
    "run_total_blinks", "run_blinks_per_minute",
]


def _condition_for_round(round_idx: int, run_condition_label: str, rounds_per_block: int = 7) -> str:
    """
    Resolve per-round condition label from run-level metadata.

    Supports:
      - alternating_gaze_unaware_then_gaze_aware  -> even rounds unaware, odd rounds aware
      - mixed_gaze_unaware_then_gaze_aware        -> first block unaware, second block aware
      - gaze_aware / gaze_unaware                 -> fixed per run
    """
    label = (run_condition_label or "").lower()
    if not label:
        return "unknown"

    try:
        idx = int(round_idx)
    except (TypeError, ValueError):
        return "unknown"

    if "alternating" in label:
        return "gaze_aware" if idx % 2 == 1 else "gaze_unaware"

    if "gaze_unaware" in label and "gaze_aware" in label:
        return "gaze_unaware" if idx < int(rounds_per_block) else "gaze_aware"

    if label == "gaze_aware":
        return "gaze_aware"
    if label == "gaze_unaware":
        return "gaze_unaware"

    # Fallback: preserve unknown labels for troubleshooting.
    return label


def _normalize_tlx_condition(label: str) -> str:
    """Map TLX condition labels to plotting categories without inventing per-condition rows."""
    low = (label or "").lower()
    if low == "gaze_aware":
        return "gaze_aware"
    if low == "gaze_unaware":
        return "gaze_unaware"
    # Alternating/mixed runs represent overall workload unless manually split.
    if "alternating" in low or ("gaze_unaware" in low and "gaze_aware" in low):
        return "overall"
    return low or "unknown"


def load_all_summaries(data_dir: Path) -> pd.DataFrame:
    """
    Walk GazeData/ recursively, find every trial_summary.json,
    flatten per-round records into a single DataFrame.

    Columns:
        participant_id, run_number, condition, round_index, shape, color,
        completed, time_to_find, wrong_captures,
        fixation_time_on_target, fixation_time_on_distractors,
        fixation_count_total, avg_fixation_duration,
        saccade_count, saccade_frequency_hz, avg_saccade_amplitude_deg,
        block (A or B based on round index)
    """
    records = []
    summary_files = list(data_dir.rglob("trial_summary.json"))

    if not summary_files:
        print(f"No trial_summary.json files found under {data_dir}")
        return pd.DataFrame(columns=SUMMARY_COLUMNS)

    for path in summary_files:
        try:
            with open(path, encoding="utf-8-sig") as f:
                data = json.load(f)
        except (json.JSONDecodeError, OSError) as e:
            print(f"Skipping {path}: {e}")
            continue

        participant = data.get("participant_id", "unknown")
        run = data.get("run_number", 0)
        condition = data.get("condition", "unknown")
        rounds_per_block = data.get("rounds_per_block", 7)
        total_blinks = data.get("total_blinks", -1)
        blinks_per_minute = data.get("blinks_per_minute", -1)

        try:
            total_blinks = int(total_blinks)
        except (TypeError, ValueError):
            total_blinks = -1

        try:
            blinks_per_minute = float(blinks_per_minute)
        except (TypeError, ValueError):
            blinks_per_minute = -1.0

        for r in data.get("objectives", []):
            round_idx = r.get("index", 0)
            per_round_condition = _condition_for_round(round_idx, condition, rounds_per_block)
            block = "A" if round_idx < rounds_per_block else "B"
            records.append({
                "participant_id": participant,
                "run_number": run,
                "condition": per_round_condition,
                "block": block,
                "round_index": round_idx,
                "shape": r.get("shape", ""),
                "color": r.get("color", ""),
                "completed": r.get("completed", False),
                "time_to_find": r.get("time_to_find_seconds", -1),
                "wrong_captures": r.get("wrong_captures", 0),
                "fixation_time_on_target": r.get("fixation_time_on_target_seconds", 0),
                "fixation_time_on_distractors": r.get("fixation_time_on_distractors_seconds", 0),
                "fixation_count_total": r.get("fixation_count_total", 0),
                "fixation_count_on_target": r.get("fixation_count_on_target", 0),
                "fixation_count_on_distractors": r.get("fixation_count_on_distractors", 0),
                "avg_fixation_duration": r.get("avg_fixation_duration_seconds", 0),
                "saccade_count": r.get("saccade_count", 0),
                "saccade_frequency_hz": r.get("saccade_frequency_hz", 0),
                "avg_saccade_amplitude_deg": r.get("avg_saccade_amplitude_deg", 0),
                "first_try_correct": r.get("completed", False) and r.get("wrong_captures", 0) == 0,
                "run_total_blinks": total_blinks,
                "run_blinks_per_minute": blinks_per_minute,
            })

    df = pd.DataFrame(records, columns=SUMMARY_COLUMNS)
    print(f"Loaded {len(df)} round records from {len(summary_files)} sessions")
    if df.empty:
        print("No valid round records found after parsing summaries.")
        return df
    print(f"Participants: {df['participant_id'].nunique()}")
    print(f"Conditions: {df['condition'].value_counts().to_dict()}")
    return df


def load_event_logs(data_dir: Path) -> pd.DataFrame:
    """
    Load all trial_events.csv files (timestamped fixation/capture events).
    Useful for time-series analysis or per-event filtering.
    """
    csv_files = list(data_dir.rglob("trial_events.csv"))
    if not csv_files:
        return pd.DataFrame()

    dfs = []
    for path in csv_files:
        try:
            df = pd.read_csv(path, encoding="utf-8-sig")
            # Extract participant info from path:
            # GazeData/P001/run_001_gaze_aware_<timestamp>/trial_events.csv
            df["participant_id"] = path.parent.parent.name
            run_folder = path.parent.name
            df["run_folder"] = run_folder
            rounds_per_block = 7
            if "objective_index" in df.columns:
                df["condition"] = df["objective_index"].apply(
                    lambda idx: _condition_for_round(idx, run_folder, rounds_per_block))
            else:
                df["condition"] = _condition_for_round(0, run_folder, rounds_per_block)
            dfs.append(df)
        except Exception as e:
            print(f"Skipping {path}: {e}")

    return pd.concat(dfs, ignore_index=True) if dfs else pd.DataFrame()


def compute_last_fixation_durations(events_df: pd.DataFrame) -> pd.DataFrame:
    """
    For each (participant, run, round), find the last fixation_end event
    where is_target=1 and return its duration. This is the "last fixation
    duration on target before trial ends" metric (Chiossi et al. 2024).

    Returns columns: participant_id, condition, round_index, last_fix_on_target_s
    """
    if events_df.empty:
        return pd.DataFrame()

    target_fix_ends = events_df[
        (events_df["event_type"] == "fixation_end")
        & (events_df["is_target"] == 1)
    ].copy()

    if target_fix_ends.empty:
        return pd.DataFrame()

    # Last fixation per (participant, run, round)
    last = (target_fix_ends
            .sort_values("timestamp")
            .groupby(["participant_id", "run_folder", "objective_index"])
            .last()
            .reset_index())

    result = last[[
        "participant_id", "run_folder", "condition",
        "objective_index", "dwell_duration"
    ]].rename(columns={
        "objective_index": "round_index",
        "dwell_duration": "last_fix_on_target_s",
    })
    return result


def load_nasa_tlx(data_dir: Path) -> pd.DataFrame:
    """
    Load manually-entered NASA-TLX scores. Expected file:
        <data_dir>/nasa_tlx.csv

    Columns: participant_id, condition, mental, physical, temporal,
             performance, effort, frustration

    Raw TLX = sum of all 6 subscale scores (each 0-100).
    """
    tlx_path = data_dir / "nasa_tlx.csv"
    if not tlx_path.exists():
        return pd.DataFrame()

    try:
        df = pd.read_csv(tlx_path, encoding="utf-8-sig")
        subscales = ["mental", "physical", "temporal",
                     "performance", "effort", "frustration"]
        if "condition" in df.columns:
            # Keep compatible labels for grouping/plots where possible.
            df["condition"] = df["condition"].apply(_normalize_tlx_condition)
        if all(c in df.columns for c in subscales):
            df["raw_tlx"] = df[subscales].sum(axis=1)
        return df
    except Exception as e:
        print(f"Skipping {tlx_path}: {e}")
        return pd.DataFrame()


def load_gaze_logs(data_dir: Path) -> pd.DataFrame:
    """
    Load all gaze_log.csv files (per-frame gaze samples).
    WARNING: large. Don't load everything unless you need it.
    """
    csv_files = list(data_dir.rglob("gaze_log.csv"))
    if not csv_files:
        return pd.DataFrame()

    dfs = []
    for path in csv_files:
        try:
            df = pd.read_csv(path, encoding="utf-8-sig")
            df["participant_id"] = path.parent.parent.name
            df["run_folder"] = path.parent.name
            dfs.append(df)
        except Exception as e:
            print(f"Skipping {path}: {e}")

    return pd.concat(dfs, ignore_index=True) if dfs else pd.DataFrame()


if __name__ == "__main__":
    import sys
    data_dir = Path(sys.argv[1] if len(sys.argv) > 1 else "GazeData")
    df = load_all_summaries(data_dir)
    print(df.head())
    print(f"\nShape: {df.shape}")
