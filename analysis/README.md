# Analysis Workflow

Generates plots and statistics from the experiment data collected by Unity.

## One-Command Runner (Recommended)

From the project root:

```bash
scripts/run-analysis-pipeline.sh
```

What it does:

- Auto-selects a data directory (`./GazeData` first, then macOS Editor path)
- Auto-pulls from headset only if local trial summaries are missing
- Creates/uses `.venv-analysis` and installs `analysis/requirements.txt`
- Runs `analysis/run_analysis.py`
- Writes results to `analysis/results` by default

Common variants:

```bash
# Force a fresh pull from headset first
scripts/run-analysis-pipeline.sh --pull

# Local-only analysis (never adb pull)
scripts/run-analysis-pipeline.sh --no-pull --data-dir "~/Library/Application Support/DefaultCompany/GazeContingencyProject/GazeData"

# Custom output folder
scripts/run-analysis-pipeline.sh --output-dir ./analysis/results_m2
```

## Setup

```bash
cd analysis
pip install -r requirements.txt
```

## Quick Start

```bash
# Point at your GazeData folder (Unity persistentDataPath/GazeData)
python run_analysis.py ~/Library/Application\ Support/DefaultCompany/GazeContingencyProject/GazeData

# Or relative path with custom output:
python run_analysis.py ./GazeData ./results
```

## What Gets Produced

In the `results/` folder:

- `all_rounds.csv` — flattened dataset, one row per (participant, run, round)
- `stats_report.txt` — paired t-test results with significance markers
- `stats_results.csv` — same as above in machine-readable form
- Per-metric plots:
  - `search_time.png`
  - `first_try_accuracy.png`
  - `wrong_captures.png`
  - `fixation_count.png`
  - `avg_fixation_duration.png`
  - `last_fixation_duration.png` — duration of final fixation on target
  - `saccade_frequency.png`
  - `saccade_amplitude.png`
  - `target_vs_distractor_fixation.png`
  - `learning_curve.png`
  - `nasa_tlx.png` — raw workload (only if `nasa_tlx.csv` is present)
  - `nasa_tlx_subscales.png` — 6 subscale breakdown
- `summary_grid.png` — 4x2 grid of all metrics, paper-ready
- `nasa_tlx_scores.csv` — the loaded NASA-TLX responses with `raw_tlx` added

## NASA-TLX Workflow

NASA-TLX is a paper questionnaire collected manually after each block.
After each session:

1. Have the participant rate the 6 subscales (0-100 each):
   mental demand, physical, temporal, performance, effort, frustration
2. Add a row to a file named **`nasa_tlx.csv`** in your `GazeData/` folder
3. Use this column order: `participant_id, condition, mental, physical,
   temporal, performance, effort, frustration`
4. See `nasa_tlx_template.csv` in the analysis folder for an example

The analysis pipeline auto-detects this file. If present, it generates
`nasa_tlx.png` (raw workload box plot) and `nasa_tlx_subscales.png`
(6-subscale grouped bar chart), and it copies the loaded questionnaire
data into `results/nasa_tlx_scores.csv`.

## File Structure

```
analysis/
├── README.md           # this file
├── requirements.txt    # Python dependencies
├── run_analysis.py     # main entry point
├── load_data.py        # JSON/CSV loaders → DataFrame
├── plots.py            # plotting functions (one per metric)
└── stats.py            # paired t-tests
```

## Adding a New Plot

1. Add a function to `plots.py` following the pattern:
   ```python
   def plot_my_new_metric(df: pd.DataFrame, out_dir: Path):
       fig, ax = plt.subplots(figsize=(6, 5))
       sns.boxplot(data=df, x="condition", y="my_metric",
                   order=ORDER, palette=PALETTE, ax=ax)
       _save(fig, out_dir, "my_new_metric")
   ```
2. Call it inside `generate_all(df, out_dir)`.

## Adding a New Metric

1. Add the field to `TrialDataLogger.WriteSummary` in Unity (it'll appear in the JSON next run).
2. Add the field name to `load_data.py` inside the per-round dict.
3. Optionally add it to the metrics list in `stats.py` for paired t-tests.
4. Optionally add a plot function in `plots.py`.

## Where Unity Saves Data

| Platform | Path |
|---|---|
| macOS Editor | `~/Library/Application Support/DefaultCompany/GazeContingencyProject/GazeData` |
| Windows Editor | `%APPDATA%\..\LocalLow\DefaultCompany\GazeContingencyProject\GazeData` |
| VIVE / Android | On-device app data, pull via `adb pull` |

Folder structure inside `GazeData/`:
```
GazeData/
  P001/
    run_001_gaze_aware_2026-04-07_14-30-22/
      gaze_log.csv          (per-frame eye tracking)
      trial_events.csv      (timestamped fixation/capture events)
      trial_summary.json    (per-round + per-session metrics)
    run_002_gaze_unaware_.../
      ...
  P002/
    ...
```

## Metrics Reference

Aggregate-level (in summary JSON top level):
- `correct_first_try`, `total_wrong_captures`, `first_try_accuracy`
- `total_fixation_on_targets_seconds`, `total_fixation_on_distractors_seconds`
- `total_blinks`, `blinks_per_minute`
- `gaze_behavior_classification`

Per-round (in summary JSON `objectives[]`):
- `time_to_find_seconds` — reaction time from objective onset to capture
- `wrong_captures` — incorrect selections this round
- `fixation_time_on_target_seconds` / `fixation_time_on_distractors_seconds`
- `fixation_count_on_target` / `fixation_count_on_distractors` / `fixation_count_total`
- `avg_fixation_duration_seconds` — total fixation time / fixation count
- `saccade_count` — number of saccades (fixation→fixation transitions)
- `saccade_frequency_hz` — saccades per second of round time
- `avg_saccade_amplitude_deg` — mean angular distance between consecutive fixations
