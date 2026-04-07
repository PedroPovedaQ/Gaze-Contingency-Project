"""
Statistical tests comparing gaze-aware vs gaze-unaware conditions.
Outputs a text report alongside the figures.
"""

import pandas as pd
from scipy import stats
from pathlib import Path


def _ttest_paired_within(df: pd.DataFrame, metric: str) -> dict:
    """
    Paired t-test on per-participant means: aware vs unaware.
    Same participant's mean across all rounds in each condition.
    """
    means = df.groupby(["participant_id", "condition"])[metric].mean().unstack()
    if "gaze_aware" not in means.columns or "gaze_unaware" not in means.columns:
        return {"metric": metric, "n": 0, "t": None, "p": None,
                "mean_aware": None, "mean_unaware": None}

    paired = means.dropna(subset=["gaze_aware", "gaze_unaware"])
    if len(paired) < 2:
        return {"metric": metric, "n": len(paired), "t": None, "p": None,
                "mean_aware": paired["gaze_aware"].mean() if len(paired) else None,
                "mean_unaware": paired["gaze_unaware"].mean() if len(paired) else None}

    t, p = stats.ttest_rel(paired["gaze_aware"], paired["gaze_unaware"])
    return {
        "metric": metric,
        "n": len(paired),
        "t": float(t),
        "p": float(p),
        "mean_aware": float(paired["gaze_aware"].mean()),
        "mean_unaware": float(paired["gaze_unaware"].mean()),
    }


def run_all_tests(df: pd.DataFrame) -> pd.DataFrame:
    metrics = [
        "time_to_find",
        "wrong_captures",
        "fixation_count_total",
        "avg_fixation_duration",
        "saccade_frequency_hz",
        "avg_saccade_amplitude_deg",
        "fixation_time_on_target",
        "fixation_time_on_distractors",
    ]
    results = [_ttest_paired_within(df, m) for m in metrics]
    return pd.DataFrame(results)


def save_report(df: pd.DataFrame, out_dir: Path):
    out_dir.mkdir(parents=True, exist_ok=True)
    results = run_all_tests(df)

    lines = ["=" * 70,
             "PAIRED T-TEST RESULTS (within-subjects, gaze_aware vs gaze_unaware)",
             "=" * 70, ""]

    for _, row in results.iterrows():
        sig = ""
        if row["p"] is not None:
            if row["p"] < 0.001: sig = " ***"
            elif row["p"] < 0.01: sig = " **"
            elif row["p"] < 0.05: sig = " *"

        lines.append(f"{row['metric']}:")
        if row["t"] is not None:
            lines.append(f"  n={row['n']}  t={row['t']:.3f}  p={row['p']:.4f}{sig}")
            lines.append(f"  aware: {row['mean_aware']:.3f}   "
                         f"unaware: {row['mean_unaware']:.3f}   "
                         f"diff: {row['mean_aware'] - row['mean_unaware']:+.3f}")
        else:
            lines.append(f"  insufficient data (n={row['n']})")
        lines.append("")

    lines.append("Significance: * p<.05  ** p<.01  *** p<.001")

    report_path = out_dir / "stats_report.txt"
    with open(report_path, "w") as f:
        f.write("\n".join(lines))

    csv_path = out_dir / "stats_results.csv"
    results.to_csv(csv_path, index=False)

    print(f"  saved {report_path}")
    print(f"  saved {csv_path}")
