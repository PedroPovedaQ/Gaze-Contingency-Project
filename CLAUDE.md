# Gaze Contingency Project

A VR conjunction-search experiment that compares gaze-aware (hot/cold) tip
feedback against gaze-unaware (generic) encouragement. Built in Unity for
the HTC VIVE with passthrough AR.

## High-level Architecture

- **`Assets/FindObjectGameManager.cs`** — game state machine, round flow,
  object spawning, capture logic
- **`Assets/ChallengeSet.cs`** — deterministic 14-round challenge set,
  fixed seed (42), counterbalanced condition assignment per participant
- **`Assets/ShelfSpawner.cs`** — builds two bookcase units on the detected
  table, computes deterministic shelf spawn points
- **`Assets/ShapeObjectFactory.cs`** — instantiates shape/color combos,
  handles colliders/materials
- **`Assets/GazeHighlightManager.cs`** — eye gaze dwell selection (1.6s),
  progressive glow charge-up
- **`Assets/HintGenerator.cs`** — hot/cold/warm/cold/missed temperature tips
  (gaze-aware) and generic encouragement (gaze-unaware control). Uses 2D
  proximity (bookcase column + shelf row).
- **`Assets/VoiceAssistantController.cs`** — wires HintGenerator to
  VoiceSynthesizer for spoken tips
- **`Assets/VoiceSynthesizer.cs`** — ElevenLabs TTS with permanent disk cache
- **`Assets/SessionConfig.cs`** — participant ID assignment, output folder
  structure: `GazeData/P###/run_###_<condition>_<timestamp>/`
- **`Assets/GazeDataLogger.cs`** — per-frame eye tracking → `gaze_log.csv`
- **`Assets/TrialDataLogger.cs`** — fixation/capture events →
  `trial_events.csv` + per-round `trial_summary.json`

## Experimental Design

- **Within-subjects**, counterbalanced
- **14 rounds** = 2 blocks of 7
- **P001, P003, ...** (odd) → unaware first, then aware
- **P002, P004, ...** (even) → aware first, then unaware
- All participants see the **same 14 deterministic challenges** (seed 42)
- Conjunction search: 42 objects per round
  (1 target + 13 same-color + 13 same-shape + 15 neutral)

## Data Output

Saved to `Application.persistentDataPath/GazeData/`:

```
GazeData/
  P001/
    run_001_gaze_unaware_2026-04-07_14-30-22/
      gaze_log.csv          (per-frame: gaze origin, dir, eye openness, hovered, dwell %)
      trial_events.csv      (timestamped: fixation_start, fixation_end, capture_correct, capture_wrong)
      trial_summary.json    (per-round metrics: search time, accuracy, fixations, saccades)
    run_002_gaze_aware_.../
      ...
```

### Per-round metrics (in `trial_summary.json`)

- `time_to_find_seconds` — reaction time
- `wrong_captures` — error count
- `fixation_count_total`, `fixation_count_on_target`, `fixation_count_on_distractors`
- `fixation_time_on_target_seconds`, `fixation_time_on_distractors_seconds`
- `avg_fixation_duration_seconds`
- `saccade_count`, `saccade_frequency_hz`, `avg_saccade_amplitude_deg`

## Analysis Workflow

The Python analysis pipeline lives in **`analysis/`**. It loads all
`trial_summary.json` files, generates plots, and runs paired t-tests.

```bash
cd analysis
pip install -r requirements.txt
python run_analysis.py <path/to/GazeData> [output_dir]
```

Default output is `./results/` containing:
- `all_rounds.csv` — flattened per-round dataset (one row per round)
- `stats_report.txt` — paired t-tests with significance markers
- Per-metric box plots (search time, accuracy, fixations, saccades, etc.)
- `summary_grid.png` — paper-ready 4x2 grid
- `learning_curve.png` — round-over-round progression

### Adding a New Metric

1. Add the field to `TrialDataLogger.WriteSummary` in the JSON output
2. Add it to `analysis/load_data.py` inside the per-round dict
3. Optionally add a plot in `analysis/plots.py` (follow existing patterns)
4. Optionally add it to `analysis/stats.py` for paired t-tests

### Adding a New Plot

1. Add a function to `analysis/plots.py`:
   ```python
   def plot_my_metric(df, out_dir):
       fig, ax = plt.subplots(figsize=(6, 5))
       sns.boxplot(data=df, x="condition", y="my_metric",
                   order=ORDER, palette=PALETTE, ax=ax)
       _save(fig, out_dir, "my_metric")
   ```
2. Call it inside `generate_all(df, out_dir)`.

## Conventions

- The HintGenerator's `gazeAwareTips` flag is set per-round by the game
  manager based on `ChallengeSet.IsGazeAware(round, participantNumber)`.
  Don't hardcode it.
- All challenge generation uses fixed seed 42. Don't introduce
  `Random.Range` into round content — it would break determinism.
- The fixation cross between rounds pauses the timer (`m_UI.PauseTimer`)
  and the timer resumes only after the agent finishes announcing the new
  target. Data logging is also suspended during the transition.
- Eye gaze ray visualization is hidden by default
  (`EyeGazeRayVisual.cs` sets `m_FlipX = false`, line renderer disabled).
  The gaze raycast still works — just no visible line.
- ElevenLabs API key is loaded from
  `Assets/StreamingAssets/api_keys.json` (gitignored).

## Citations

- **Conjunction search**: Treisman & Gelade (1980)
- **NASA-TLX**: Hart & Staveland (1988)
- **Fixation thresholds**: Holmqvist et al. (2011), Rayner (1998)
