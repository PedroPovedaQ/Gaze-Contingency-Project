"""
Loads all trial summary JSONs from the GazeData folder into a flat DataFrame.
One row per (participant, run, round). Easy to filter/aggregate by condition.
"""

import json
from pathlib import Path
import pandas as pd


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
        return pd.DataFrame()

    for path in summary_files:
        try:
            with open(path) as f:
                data = json.load(f)
        except (json.JSONDecodeError, OSError) as e:
            print(f"Skipping {path}: {e}")
            continue

        participant = data.get("participant_id", "unknown")
        run = data.get("run_number", 0)
        condition = data.get("condition", "unknown")
        rounds_per_block = data.get("rounds_per_block", 7)

        for r in data.get("objectives", []):
            round_idx = r.get("index", 0)
            block = "A" if round_idx < rounds_per_block else "B"
            records.append({
                "participant_id": participant,
                "run_number": run,
                "condition": condition,
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
            })

    df = pd.DataFrame(records)
    print(f"Loaded {len(df)} round records from {len(summary_files)} sessions")
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
            df = pd.read_csv(path)
            # Extract participant info from path:
            # GazeData/P001/run_001_gaze_aware_<timestamp>/trial_events.csv
            df["participant_id"] = path.parent.parent.name
            run_folder = path.parent.name
            df["run_folder"] = run_folder
            df["condition"] = "gaze_aware" if "gaze_aware" in run_folder else "gaze_unaware"
            dfs.append(df)
        except Exception as e:
            print(f"Skipping {path}: {e}")

    return pd.concat(dfs, ignore_index=True) if dfs else pd.DataFrame()


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
            df = pd.read_csv(path)
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
