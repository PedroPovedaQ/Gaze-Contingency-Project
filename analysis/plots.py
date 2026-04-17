"""
Plotting functions. Each function takes a DataFrame and saves a figure.
Add new plots by following the pattern: take df, build a fig, save it.
"""

import matplotlib.pyplot as plt
import seaborn as sns
import pandas as pd
from pathlib import Path

# Academic theme — black/white/yellow consistent with presentation
sns.set_theme(style="whitegrid")
PALETTE = {
    "gaze_aware": "#FFC107",
    "gaze_unaware": "#666666",
    "overall": "#1F1F1F",
}
ORDER = ["gaze_aware", "gaze_unaware"]


def _save(fig, out_dir: Path, name: str):
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / f"{name}.png"
    fig.savefig(path, dpi=150, bbox_inches="tight", facecolor="white")
    plt.close(fig)
    print(f"  saved {path}")


def _conditions_in_plot_order(data: pd.DataFrame, preferred_order=None):
    preferred_order = preferred_order or ORDER
    present = [
        condition for condition in preferred_order
        if condition in set(data["condition"].dropna().astype(str))
    ]
    extras = sorted(
        condition for condition in data["condition"].dropna().astype(str).unique()
        if condition not in present
    )
    return present + extras


def _palette_for_conditions(conditions):
    return {
        condition: PALETTE.get(condition, "#4D4D4D")
        for condition in conditions
    }


def _safe_condition_boxplot(ax, data: pd.DataFrame, y_col: str, order=None):
    """
    Draw a condition plot that doesn't crash on tiny samples.
    Falls back to strip plot + mean marker when boxplot isn't meaningful.
    """
    plot_df = data[["condition", y_col]].dropna().copy()
    if plot_df.empty:
        ax.text(0.5, 0.5, "No data", ha="center", va="center", transform=ax.transAxes)
        ax.set_xticks([])
        return

    plot_order = order or _conditions_in_plot_order(plot_df)
    palette = _palette_for_conditions(plot_order)
    counts = plot_df.groupby("condition")[y_col].count()
    has_enough_for_box = (counts >= 2).sum() >= 1 and len(plot_df) >= 3

    if has_enough_for_box:
        series = []
        positions = []
        for index, condition in enumerate(plot_order):
            values = plot_df.loc[plot_df["condition"] == condition, y_col]
            if not values.empty:
                series.append(values.to_numpy())
                positions.append(index)

        if series:
            boxplot = ax.boxplot(
                series,
                positions=positions,
                widths=0.55,
                patch_artist=True,
                medianprops={"color": "black", "linewidth": 1.5},
                whiskerprops={"color": "black", "linewidth": 1.2},
                capprops={"color": "black", "linewidth": 1.2},
                boxprops={"edgecolor": "black", "linewidth": 1.2},
                flierprops={
                    "marker": "o",
                    "markersize": 4,
                    "markerfacecolor": "black",
                    "markeredgecolor": "black",
                    "alpha": 0.5,
                },
            )

            for patch, condition in zip(boxplot["boxes"], [plot_order[i] for i in positions]):
                patch.set_facecolor(palette[condition])
                patch.set_alpha(0.8)
    else:
        # Sparse-data fallback (e.g., a single NASA-TLX row)
        sns.stripplot(
            data=plot_df,
            x="condition",
            y=y_col,
            order=plot_order,
            hue="condition",
            hue_order=plot_order,
            palette=palette,
            legend=False,
            alpha=0.9,
            size=8,
            ax=ax,
        )
        means = plot_df.groupby("condition")[y_col].mean().reindex(plot_order)
        for i, cond in enumerate(plot_order):
            m = means.get(cond)
            if pd.notna(m):
                ax.plot(i, m, marker="_", markersize=20, color="black", linewidth=2)

    ax.set_xticks(range(len(plot_order)))
    ax.set_xticklabels(plot_order)
    ax.set_xlim(-0.5, len(plot_order) - 0.5)


def plot_search_time(df: pd.DataFrame, out_dir: Path):
    """Box plot: search time by condition. Like Fig. 6a in Chiossi et al."""
    fig, ax = plt.subplots(figsize=(6, 5))
    completed = df[df["completed"] & (df["time_to_find"] > 0)]
    _safe_condition_boxplot(ax, completed, "time_to_find")
    sns.stripplot(data=completed, x="condition", y="time_to_find",
                  order=ORDER, color="black", alpha=0.4, size=4, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Search Time (seconds)")
    ax.set_title("Search Time per Round")
    _save(fig, out_dir, "search_time")


def plot_first_try_accuracy(df: pd.DataFrame, out_dir: Path):
    """Bar plot: % of rounds correct on first try."""
    by_condition = (df.groupby("condition")["first_try_correct"]
                      .mean().reindex(ORDER) * 100).reset_index()
    by_condition.columns = ["condition", "accuracy_pct"]

    fig, ax = plt.subplots(figsize=(6, 5))
    sns.barplot(data=by_condition, x="condition", y="accuracy_pct",
                order=ORDER, hue="condition", hue_order=ORDER,
                palette=PALETTE, legend=False, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("First-Try Accuracy (%)")
    ax.set_title("Search Accuracy")
    ax.set_ylim(0, 100)
    for i, row in by_condition.iterrows():
        ax.text(i, row["accuracy_pct"] + 1, f"{row['accuracy_pct']:.1f}%",
                ha="center", fontsize=11)
    _save(fig, out_dir, "first_try_accuracy")


def plot_wrong_captures(df: pd.DataFrame, out_dir: Path):
    """Box plot: number of wrong selections per round."""
    fig, ax = plt.subplots(figsize=(6, 5))
    _safe_condition_boxplot(ax, df, "wrong_captures")
    sns.stripplot(data=df, x="condition", y="wrong_captures",
                  order=ORDER, color="black", alpha=0.4, size=4, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Wrong Captures per Round")
    ax.set_title("Errors")
    _save(fig, out_dir, "wrong_captures")


def plot_fixation_count(df: pd.DataFrame, out_dir: Path):
    """Box plot: total fixations per round."""
    fig, ax = plt.subplots(figsize=(6, 5))
    _safe_condition_boxplot(ax, df, "fixation_count_total")
    sns.stripplot(data=df, x="condition", y="fixation_count_total",
                  order=ORDER, color="black", alpha=0.4, size=4, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Fixations per Round")
    ax.set_title("Fixation Count")
    _save(fig, out_dir, "fixation_count")


def plot_avg_fixation_duration(df: pd.DataFrame, out_dir: Path):
    """Box plot: average fixation duration per round (ms)."""
    fig, ax = plt.subplots(figsize=(6, 5))
    df = df.copy()
    df["avg_fix_ms"] = df["avg_fixation_duration"] * 1000
    _safe_condition_boxplot(ax, df, "avg_fix_ms")
    ax.set_xlabel("Condition")
    ax.set_ylabel("Average Fixation Duration (ms)")
    ax.set_title("Average Fixation Duration")
    _save(fig, out_dir, "avg_fixation_duration")


def plot_saccade_frequency(df: pd.DataFrame, out_dir: Path):
    """Box plot: saccades per second per round."""
    fig, ax = plt.subplots(figsize=(6, 5))
    _safe_condition_boxplot(ax, df, "saccade_frequency_hz")
    sns.stripplot(data=df, x="condition", y="saccade_frequency_hz",
                  order=ORDER, color="black", alpha=0.4, size=4, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Saccade Frequency (Hz)")
    ax.set_title("Saccade Frequency")
    _save(fig, out_dir, "saccade_frequency")


def plot_saccade_amplitude(df: pd.DataFrame, out_dir: Path):
    """Box plot: average saccade amplitude per round (degrees)."""
    fig, ax = plt.subplots(figsize=(6, 5))
    _safe_condition_boxplot(ax, df, "avg_saccade_amplitude_deg")
    ax.set_xlabel("Condition")
    ax.set_ylabel("Average Saccade Amplitude (degrees)")
    ax.set_title("Saccade Amplitude")
    _save(fig, out_dir, "saccade_amplitude")


def plot_target_vs_distractor_fixation(df: pd.DataFrame, out_dir: Path):
    """Stacked bar: time on target vs distractors per condition."""
    agg = df.groupby("condition")[["fixation_time_on_target",
                                     "fixation_time_on_distractors"]].mean()
    agg = agg.reindex(ORDER)

    fig, ax = plt.subplots(figsize=(6, 5))
    agg.plot(kind="bar", stacked=True, ax=ax,
             color=["#FFC107", "#444444"], width=0.6)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Mean Fixation Time (seconds)")
    ax.set_title("Fixation Time: Target vs Distractors")
    ax.legend(["On Target", "On Distractors"], loc="upper right")
    plt.xticks(rotation=0)
    _save(fig, out_dir, "target_vs_distractor_fixation")


def plot_learning_curve(df: pd.DataFrame, out_dir: Path):
    """Line plot: search time over rounds, by condition."""
    fig, ax = plt.subplots(figsize=(8, 5))
    completed = df[df["completed"] & (df["time_to_find"] > 0)]
    means = completed.groupby(["round_index", "condition"])["time_to_find"].mean().reset_index()

    for cond in ORDER:
        sub = means[means["condition"] == cond]
        ax.plot(sub["round_index"] + 1, sub["time_to_find"],
                marker="o", label=cond, color=PALETTE[cond], linewidth=2)

    ax.set_xlabel("Round Number")
    ax.set_ylabel("Mean Search Time (seconds)")
    ax.set_title("Learning Curve Over Rounds")
    ax.legend()
    _save(fig, out_dir, "learning_curve")


def plot_last_fixation_duration(last_fix_df: pd.DataFrame, out_dir: Path):
    """Box plot: duration of the last fixation on target before capture."""
    if last_fix_df.empty:
        print("  skipping last_fixation_duration: no data")
        return
    fig, ax = plt.subplots(figsize=(6, 5))
    _safe_condition_boxplot(ax, last_fix_df, "last_fix_on_target_s")
    sns.stripplot(data=last_fix_df, x="condition", y="last_fix_on_target_s",
                  order=ORDER, color="black", alpha=0.4, size=4, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Last Fixation on Target (seconds)")
    ax.set_title("Last Fixation Duration")
    _save(fig, out_dir, "last_fixation_duration")


def plot_nasa_tlx(tlx_df: pd.DataFrame, out_dir: Path):
    """Box plot: NASA-TLX raw workload score by condition."""
    if tlx_df.empty or "raw_tlx" not in tlx_df.columns:
        print("  skipping nasa_tlx: no data (need analysis/nasa_tlx.csv)")
        return
    tlx_order = _conditions_in_plot_order(tlx_df)
    fig, ax = plt.subplots(figsize=(6, 5))
    _safe_condition_boxplot(ax, tlx_df, "raw_tlx", order=tlx_order)
    sns.stripplot(data=tlx_df, x="condition", y="raw_tlx",
                  order=tlx_order, color="black", alpha=0.5, size=6, ax=ax)
    ax.set_xlabel("Condition")
    ax.set_ylabel("Raw NASA-TLX (0-600)")
    ax.set_title("Perceived Workload")
    _save(fig, out_dir, "nasa_tlx")


def plot_nasa_tlx_subscales(tlx_df: pd.DataFrame, out_dir: Path):
    """Grouped bar: NASA-TLX subscale means by condition."""
    if tlx_df.empty:
        return
    subscales = ["mental", "physical", "temporal",
                 "performance", "effort", "frustration"]
    if not all(c in tlx_df.columns for c in subscales):
        return

    tlx_order = _conditions_in_plot_order(tlx_df)
    means = tlx_df.groupby("condition")[subscales].mean().reindex(tlx_order).reset_index()
    melted = means.melt(id_vars="condition", var_name="subscale", value_name="score")

    fig, ax = plt.subplots(figsize=(10, 5))
    sns.barplot(data=melted, x="subscale", y="score", hue="condition",
                hue_order=tlx_order, palette=_palette_for_conditions(tlx_order), ax=ax)
    ax.set_xlabel("NASA-TLX Subscale")
    ax.set_ylabel("Mean Score (0-100)")
    ax.set_title("NASA-TLX Subscale Breakdown")
    _save(fig, out_dir, "nasa_tlx_subscales")


def plot_summary_grid(df: pd.DataFrame, out_dir: Path):
    """4x2 grid of all key metrics for the paper figure."""
    fig, axes = plt.subplots(2, 4, figsize=(20, 10))

    completed = df[df["completed"] & (df["time_to_find"] > 0)]
    df_ms = df.copy()
    df_ms["avg_fix_ms"] = df_ms["avg_fixation_duration"] * 1000

    plots = [
        (axes[0, 0], completed, "time_to_find", "Search Time (s)"),
        (axes[0, 1], df, "wrong_captures", "Wrong Captures"),
        (axes[0, 2], df, "fixation_count_total", "Fixations / Round"),
        (axes[0, 3], df_ms, "avg_fix_ms", "Avg Fixation (ms)"),
        (axes[1, 0], df, "saccade_frequency_hz", "Saccade Freq (Hz)"),
        (axes[1, 1], df, "avg_saccade_amplitude_deg", "Saccade Amp (°)"),
        (axes[1, 2], df, "fixation_time_on_target", "Time on Target (s)"),
        (axes[1, 3], df, "fixation_time_on_distractors", "Time on Distractors (s)"),
    ]

    for ax, data, ycol, title in plots:
        _safe_condition_boxplot(ax, data, ycol)
        ax.set_title(title)
        ax.set_xlabel("")

    plt.suptitle("Conjunction Search: Gaze-Aware vs Gaze-Unaware",
                 fontsize=16, fontweight="bold")
    plt.tight_layout()
    _save(fig, out_dir, "summary_grid")


def generate_all(df: pd.DataFrame, out_dir: Path,
                 last_fix_df: pd.DataFrame = None,
                 tlx_df: pd.DataFrame = None):
    """Run every plot. Add new plots here when you write them."""
    if df.empty:
        print("No data to plot.")
        return
    print(f"Generating plots in {out_dir}/")
    plot_search_time(df, out_dir)
    plot_first_try_accuracy(df, out_dir)
    plot_wrong_captures(df, out_dir)
    plot_fixation_count(df, out_dir)
    plot_avg_fixation_duration(df, out_dir)
    plot_saccade_frequency(df, out_dir)
    plot_saccade_amplitude(df, out_dir)
    plot_target_vs_distractor_fixation(df, out_dir)
    plot_learning_curve(df, out_dir)
    if last_fix_df is not None:
        plot_last_fixation_duration(last_fix_df, out_dir)
    if tlx_df is not None:
        plot_nasa_tlx(tlx_df, out_dir)
        plot_nasa_tlx_subscales(tlx_df, out_dir)
    plot_summary_grid(df, out_dir)
    print("Done.")
