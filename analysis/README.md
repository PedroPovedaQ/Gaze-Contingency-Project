# Analysis Workflow

**Implemented measurement safeguards (October 25):** [Research-measure report and validation requirements](../docs/research-eye-measures.md). `research_measures.py` produces the four requested panels and coverage audit from explicit raw-eye/run inputs. Eye outcomes remain exploratory pending physical/detector validation. New raw files support explicitly labeled uncensored target-fixation candidates; validated last fixation and missing TLX stay unavailable. The legacy loader now preserves ray-visit counters as `proxy_*` and masks physiological columns, so older plot names below do not imply validated measurements.

**Reusable browser timeline (#37):** `python3 analysis/replay_timeline.py /path/to/recording.jsonl --output /path/to/new-data.json --html /path/to/new-page.html` imports a saved replay directly. Optional `--trial-export` links the exact performance/subjective export. The page supports all saved samples, source IDs, pause/visibility/gap states, recorded object changes, sample/event stepping, cursor JSON export and locally attached run video. See [workflow and remaining 3D/hardware/performance limits](../docs/timeline-video.md).

**Current data quality (September 30):** `analysis/eye_quality.py` exports raw eye rows plus spatial/delivery-rate quality reports. Invalid measurements remain blank; interrupted final JSON rows can be recovered with an incomplete warning, while middle corruption is rejected. Candidate eye events remain unvalidated. Per-trial `instruction_count` counts actual deliveries including requested repeats; `hint_count` excludes delivered repeats; repeat requests and deliveries are separate. A versioned repeat logger can establish zero; historical absence stays unknown. Schedule/identity/height-policy versions travel with trial rows so 40+40 horizontal balance is not mistaken for implemented vertical balance.

**Per-trial performance/subjective files (#20):** use `python3 analysis/trial_csv.py <run-directory> --replays <GazeReplays-directory> --output <new-directory>`. It writes one summary CSV per trial, a combined trial table and provenance manifest, keeping raw replay data separate. Add external questionnaires with repeated `--survey` arguments. See [field definitions, survey joins and remaining measurement gaps](../docs/trial-performance-csv.md). This is a post-session computer export; the older analysis/scoring guidance below may describe legacy protocols.

Add `--motion` for combined and per-trial motion CSV companions. For a reproducible
invented example with a full trial-column dictionary, run
`python3 analysis/demo_trial_sheets.py --output analysis/results/synthetic-demo`.
All demo rows are labeled synthetic and excluded from study analysis. Export
labels do not automatically filter the legacy analysis loaders.

**Combined CSV:** use `python3 analysis/mega_csv.py <run-directory> --replays <GazeReplays-directory> --output <mega.csv>` to export gaze, motion, events, objects, existing trial metrics and survey context into one wide file. See [row types, joins and missing-data rules](../docs/mega-csv.md). Original recordings remain separate.

**Measurement planning (2026-09-16):** see the [living measures register](../docs/measures-register.md) for all surveys, behavioral metrics, scoring, implementation gaps and open decisions. IMI is now included; final subscales/form are proposed.

**Implemented update (2026-09-10):** New runs are always gaze-aware; the loader recognizes the generic/self-similar voice suffix without inferring an unaware condition. Summary and NASA-TLX outputs retain the original label in `run_condition`, including voice identity. Awareness t-tests refuse datasets containing `always_gaze_aware` runs, rather than pooling them with legacy runs. Existing aware-versus-unaware plots are for legacy data only. Do not pool the new one-condition sessions with the old alternation design to infer an awareness effect. Voice/theta analysis is pending issue #15.


Generates plots and statistics from the experiment data collected by Unity.

## Exact replay/raw-eye link audit

**Implemented locally (October 1, 2026):** audit one measured run against explicitly supplied replay and raw-eye recordings. This supplements the session validator, per-trial export and guidance audit; it does not replace them.

```sh
python3 analysis/recording_links.py /path/to/run \
  --eye /path/to/EyeMeasurements/recording-a/gaze_observations.jsonl \
  --eye /path/to/EyeMeasurements/recording-b/gaze_observations.jsonl \
  --replay /path/to/GazeReplays/recording/recording.jsonl \
  --out /path/to/new-link-report
```

Use one `--eye` for each raw recording belonging to the run; add more only when logging reopened. `--replay` can also repeat. The tool never follows a stored device path automatically or guesses a nearby observation. New replays advertise `gaze-observation-link-v1`; each sample/selection carries both the raw recording ID and sequence. A header path alone cannot establish this link.

- `recording_links.json`: input paths, SHA-256 hashes, byte counts, raw clean-close receipts, trial counts, findings and limits. Inputs are rehashed after parsing to detect changes during the audit.
- `trials.csv`: per-trial raw search rows, replay records, exact linked/unavailable observations, correct selections and source files.
- `assets.csv`: referenced replay audio files, playback-reference counts, byte/frame counts, channels, sample rate and duration. Reused clips are inspected once and every referring event sequence is retained.
- `issues.csv`: contradictory participant/run/block/voice/trial links, object/target mismatch, missing exact observations, source/timestamp mismatch or invalid coordinate transforms.
- `incomplete.csv`: missing/unfinished recordings, absent started-trial streams or legacy/unknown linkage contracts.

Exit **0** means the supplied package is consistent with these checks; **1** means an integrity/structural error; **2** means evidence is incomplete. Existing output directories are refused. Invalid gaze rows remain linkable without interpreting their zero pose placeholders as measurements. A non-search transition may explicitly refer to the preceding trial's observation; active search and selection may not. Practice is excluded from measured-trial counts.

The `recording-links-v2` report also checks saved PCM16 WAV headers, declared/actual lengths, rates and channel alignment. Missing files or playback without saved audio are incomplete evidence; malformed files and unsafe references are integrity issues. Absolute/traversal paths and symlinked assets are rejected before reading them. Referenced regular files within the size limit are hashed before parsing and rechecked after the audit. This does not repair or regenerate clips. No audio references means no audio evidence, not proof of a successful silent run.

Coordinate checks use the saved origin quaternion/translation to compare replay-local valid rays with raw world rays. They do not establish that the raw hardware conversion is correct. PCM format consistency does not establish the right voice, naturalness, audibility or acoustic timing. This report does not validate video, surveys, physiological eye events, native 120 Hz or task-wide spatial accuracy. Historical replays remain readable, but cannot gain missing raw-observation provenance retrospectively.

## Voice-study power/sensitivity planning

**Protocol proposal; synthetic assumptions only:** `python3 analysis/voice_sensitivity.py --config analysis/power/voice_sensitivity.json --out /path/to/new-report` generates a paired-test sensitivity benchmark and a trial-level simulation analyzed as order-adjusted participant log-time differences. It compares 10 versus the selected 40 trials per voice across participant counts, voice-response variability, serial correlation and missingness. Null rejection, bias, retained N and Monte Carlo uncertainty are explicit; the informative-loss scenario is a failure-mode stress test. Separate binomial recruitment scenarios distinguish an expected retained count from a stated completion probability.

See [methods, assumptions, outputs and decisions still needed](../docs/voice-power-sensitivity.md). Saved synthetic results are in `analysis/power/results-20261001/`. This does not select an outcome, sample size or exclusion rule, and does not turn repeated block surveys into independent trial outcomes. The current complete-run descriptive exporter remains unchanged. Final mixed-model/censoring choices and power justification require the actual-task pilot and researcher decisions in #16.

## Combined recording audit before analysis

**Implemented; recording consistency only:** after copying a closed run and its explicitly matching eye/replay files, run:

Use the [three-directory Vive transfer procedure](../docs/recording-transfer.md); the older descriptive runner's `--pull` retrieves only `GazeData`.

```sh
python3 analysis/audit_recording.py /copied/GazeData/P001/run_001 \
  --eye /copied/EyeMeasurements/recording/gaze_observations.jsonl \
  --replay /copied/GazeReplays/recording/recording.jsonl \
  --out /reports/P001-run001-audit
```

Repeat `--eye` and `--replay` for all matching files, including eye-recording restarts. Choose a new output directory outside the supplied recording directories. Nothing is pulled, uploaded or rewritten. Use the analysis Python environment described below; `--no-plots` generates tables without matplotlib figures. Use `--allow-incomplete` to inspect an interrupted run; its aggregate result remains incomplete even if its retained records agree.

The command runs the existing session, exact recording-link/audio-asset, guidance and per-eye-file quality checks. It keeps each command, stdout/stderr log and report even when another check fails, hashes the known source files/sidecars before and after, and writes `audit_report.json` after each step. A report left at `running` is interrupted, never a pass. Inspect the individual findings; no guessed filename joins are performed.

Exit 0 / `consistent` means the selected recording checks agree. Exit 1 means a check failed, found an inconsistency or observed source changes. Exit 2 / `evidence_incomplete` means missing completion/provenance evidence or an intentionally partial session. The declared nominal/short-run counts are retained separately; a successful short debug session is not a full-study check. The report never certifies native 120 Hz, spatial accuracy, validated eye events, survey completeness, voice naturalness or video/acoustic synchronization. P020's historical recording lacks current raw links and cannot retrospectively pass this current-contract audit.

Raw eye JSON flags must be actual booleans, not text such as `"false"`; numeric timestamps/sequences cannot be booleans. Malformed declared values are rejected rather than coerced into valid gaze. A clean-close claim also requires a nonnegative integer `acceptedRows` matching the retained file. Missing counts remain incomplete, and invalidly typed counts/flags are errors. These rules do not invent fields absent from older recordings.

The shared replay reader likewise rejects non-finite JSON measurements, malformed sequence/time/schema values, non-boolean declared completion/selection/visibility flags and noninteger gaze/head tracking states. A missing or false footer-completion flag stays incomplete. Malformed declared types fail before link/CSV/timeline derivation; absent historical fields retain each consumer's existing missing-evidence handling. The historical P020 prefix still loads unchanged with both truncated-tail and missing-footer warnings.

## Descriptive analysis runner

From the project root:

```bash
scripts/run-analysis-pipeline.sh
```

What it does:

- Auto-selects a data directory (`./GazeData` first, then macOS Editor path)
- Auto-pulls from headset only if local trial summaries are missing
- Syncs a content-addressed environment in Git's common directory from `uv.lock`
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
brew install uv
scripts/sync-python-env.sh
```

The environment is shared by worktrees with the same lockfile and Python
version. See [`docs/worktree-environments.md`](../docs/worktree-environments.md)
for storage details and Unity cache guidance.

## Quick Start

```bash
# From the project root, point at Unity's persistentDataPath/GazeData
scripts/run-analysis-pipeline.sh --no-pull \
  --data-dir "$HOME/Library/Application Support/DefaultCompany/GazeContingencyProject/GazeData"

# Or use relative input and a custom output directory
scripts/run-analysis-pipeline.sh --no-pull \
  --data-dir ./GazeData --output-dir ./analysis/results
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

**Current protocol proposal (October 20):** collect two separately labelled retrospective TLX sets in the final survey after both blocks, with raw ratings 0–10. Preserve the referenced block, actual voice/condition, participant/run, `administration_phase=final`, `reference_scope=participant_by_block`, administration timestamp and instrument version. Qualtrics is on hold.

**Implemented scoring:** `tlx_scoring.py` accepts explicit `scale_min=0`, `scale_max=10` or legacy `100`, plus `performance_direction` (higher is better/worse). All six dimensions must be valid and complete. Raw values remain intact; `nasa_tlx_score` is the native-scale unweighted mean and `nasa_tlx_score_0_100` is a separately labelled conversion for cross-scale plots. Missing/invalid metadata produces an unscored result, not a guessed scale. Performance is aligned to higher workload before averaging. The example `nasa_tlx_template.csv` is legacy; do not infer its scale for current responses.

The loader auto-detects `nasa_tlx.csv` and saves the loaded/scored records to `results/nasa_tlx_scores.csv`. Workload plots explicitly label rescaling to 0–100. Trial export repeats the referenced block’s responses for convenience, preserving their block scope; repeated rows are not independent survey observations. See `test_tlx_scoring.py`, `test_tlx_plots.py` and `test_trial_csv.py` for native-scale, missingness and retrospective-join cases.

## File Structure

```
analysis/
├── README.md           # this file
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

New neutral runs use `gaze_aware_voice-neutral-male` / `gaze_aware_voice-neutral-female`; the self-similar label is unchanged. `run_condition` retains this assignment. Historical `voice-generic` remains readable. Male/female are neutral voice profiles, not additional guidance conditions.

For schema-2 two-block sessions, `voice_condition`, `voice_order`, `trial_id`, `outcome`, search-onset and capture timestamps are retained per trial. `voice_block_metrics.csv` reports completed trials by block; `voice_pair_differences.csv` pairs only complete two-block runs within the same participant/run, using saved nominal and per-block trial counts (80 trials in the current 40+40 design; historical 14-trial runs remain supported). Debug/shortened runs, duplicate or missing trials, inconsistent voice order/assignment and invalid search durations are explicitly excluded. Restarts never supply a replacement block. Historical awareness plots/tests remain legacy outputs. Run `python3 scripts/verify-study-session.py /absolute/path/to/run` from the repository root to check exported timing/voice/object consistency. NASA-TLX block rows preserve the actual voice label; a historical run-wide two-voice score is `overall`.

## October 1 quality report additions — implemented, validation pending

`eye-quality-v2-phase-aware` adds `trial_eye_quality.csv`, keyed by recording, participant, run and trial. It summarizes **search-only** observed rows, distinct input updates, valid-update fraction, bounded valid duration, gap duration, delivery rate, device changes and invalid reasons. Pauses, practice, participant/trial changes and absent trial-end samples are not joined or extrapolated. These windows are not authoritative trial duration. Unknown vendor calibration identity and researcher exclusion decisions remain blank with explicit status fields. Input delivery rate is still not native sensor rate.

`spatial_validation.csv` now distinguishes mean-direction offset, signed mean yaw/pitch bias, median/p90 sample error, invalid-update fraction, valid-time coverage, RMS sample-to-sample fluctuation and angular spread. Bias is measured minus expected; positive yaw is right and positive pitch is up. Yaw uses a circular mean. Angular SD is explicitly the pooled radial angular spread about each contiguous segment's mean, `sqrt(sum(angle(sample, segment_mean)^2)/(N-S))`; `S` counts usable segments and each has at least two observations. Invalid rows/heartbeats, device changes and gaps break precision segments. Head movement can still contribute to world-ray spread; these are not validated fixation measures.

Tests cover a high-loss trial beside a good trial, pause/practice separation, remaining freshness, changed devices/participants, fixed spatial bias versus jitter, and invalid heartbeats. No automatic study exclusion or correction threshold was introduced.

## Guidance provenance audit

**Implemented locally, October 1.** Run this after pulling both the continuous raw eye file and matching replay packages:

```sh
python3 analysis/guidance_audit.py \
  --eye /path/to/EyeMeasurements/recording/gaze_observations.jsonl \
  --replay /path/to/GazeReplays/trial/recording.jsonl \
  --out /path/to/new-guidance-audit
```

Repeat `--eye` and `--replay` for additional files. The destination must be new; original recordings and existing reports are never overwritten. Outputs are `guidance_audit.json`, `guidance_decisions.csv`, and `guidance_issues.csv`. A consistency issue exits with code 1 while preserving the report. Structural input errors also fail; inspect the error rather than treating a missing report as a pass.

The audit requires exact `(eyeRecordingId, observationSequence)` joins. It checks participant/trial context, saved timestamps/age, invalid or stale gaze marked usable, correction revalidation, audio decision IDs, spoken phrase consistency, and audio lifecycle contradictions. Requested, abandoned, cancelled-before-playback, played, and ended states remain distinct. Unavailable/suspended decisions may legitimately have no observation. Legacy files without `guidance-decision-v1` are explicitly unsupported; an empty new recording reports `no_decisions` rather than a successful cue test.

`software_request_to_playback_seconds` subtracts the **replay-relative decision-record time** from the first software playback-start time. It does not subtract absolute eye receipt time from relative replay time. Raw delivery age is audited separately against the decision's absolute Unity realtime. The area-correction commitment callback may be recorded immediately after its audio request; that order alone is not an error. The report retains original requested and committed snapshots in the raw replay.

`consistent` means no observed consistency violation, not complete data or correct gaze guidance. The JSON separately reports raw-eye clean-close evidence, replay completion and truncated-prefix warnings. This tool cannot establish acoustic onset, native 120 Hz acquisition, physiological fixation, the correct wall/vertical direction, or that the participant heard the sound. Ten regression tests cover clock origins, exact joins, staleness/invalidity, trial mismatch, revalidation, cancellation, missing IDs, old data, phrase mismatch and the actual CLI/readback/overwrite/failure paths.

## Delivery timing and signal-availability exports

**Implemented locally, October 1.** `eye_quality.py` now also writes:

- `phase_delivery_quality.csv`: counts and interval statistics grouped by participant/run, phase, practice status and device. Re-entering a phase starts a new interval segment. Invalid delivered updates still count as deliveries; repeated heartbeats do not. Equal gaze coordinates are never used to deduplicate observations.
- `delivery_intervals.csv`: every accepted positive input-delivery interval used by the timing report, with its phase/device/context and closing application sequence.
- `signal_capabilities.csv`: combined gaze and independently flagged left/right gaze, pupil and openness availability. HTC counts use distinct host-query timestamps because copied fields are not new queries. `no_timed_query_observed` and `not_observed_or_unavailable` are not proof of unsupported hardware.
- `clock_frame_dictionary.csv` and `timing_report.json`: clock/frame meanings, captured device/build metadata where present, clean-close status and explicit unknown native capture identity, sample count and loss.
- `delivery_timing.png` and `.svg`: input-delivery interval histogram and saved delivery age over host time. The 8.33 ms line is only a 120 Hz reference, never evidence of native acquisition. Rows without usable delivery timestamps are omitted from the age plot; their unknown counts remain in the tables.

`delivery_interval_hz` is the reciprocal of the mean positive interval between successive flagged updates in a continuous context. `recorded_row_interval_hz` uses positive adjacent saved-row intervals; it is not a sensor rate or a count of every API poll. Duplicate/nonincreasing update flags, unknown delivery times, heartbeats and distinct render-frame IDs are separate counters. Native drop counts stay blank; missing application row sequences are reported separately and cannot reveal unexposed sensor loss.

Plots use the existing analysis environment's matplotlib dependency. Pass `--no-plots` for tables/JSON only. Nine timing/capability regressions include 120 saved rows/s with 60 input deliveries/s, phase/device/identity breaks, duplicated update flags, absent timing and copied HTC signals. The CLI test also verifies its generated SVG and tables. The inspected plot in `/tmp/gaze-timing-fixture-path` is a labeled synthetic fixture, not a headset measurement.

**October 1 descriptive voice-report repair:** the summary loader retains `rounds_per_block`, `total_rounds`, `nominal_total_rounds` and `debug_round_override`. Complete pairing requires every nominal trial index and unique trial ID, two correctly assigned voice blocks, complete session/trial outcomes and finite nonnegative search durations. Block summaries still describe available completed trials; incomplete blocks are not paired. This is a consistency/descriptive report, not confirmation of statistical power or a replacement for a repeated-measures analysis plan.

## Session integrity validation — October 1

`verify-study-session.py` now rejects a completed session with unfinished or contradictory trial outcomes, duplicate/missing trial identities or indices, invalid voice order/block counts, duplicate clip identities, non-finite/negative timing and impossible pause sequences. Readiness pauses are outside active search; capture during a search pause fails. Saved onset/capture times, when present, must agree with events. Explicit incomplete mode permits interruption but still checks recorded timing and playback identity; it cannot excuse a false completion claim.

Regression fixtures include complete current 80-trial/168-object packages in both voice orders, historical 14-trial data, legitimate readiness/search pauses and malformed-input CLI exit behavior. These checks do not establish native eye rate, acoustic onset, completeness of separate raw-eye/replay files or hardware durability.

### Fifteen-second timeout (October 23 build onward)

A `trial_timeout` closes a search without a correct selection. Export it as `outcome=timeout`, `completed=0`, with recorded active exposure (15 seconds in this build) and an empty successful-selection time. The terminal event takes precedence over late audio or stale selection records. Timeout feedback and the next cross are outside search exposure. This is an unsuccessful/censored search, not a technical failure; preserve earlier recordings and their original limits. Do not replace missing historical timing with 15 seconds.

`missed_targets.csv` summarizes deadline-expired attempts by participant/run/block/voice/deadline, alongside correct selections and other/unresolved endpoints. The percentage denominator is correct selections plus timeouts with a recorded deadline; practice is excluded. Keep counts, denominators, unknown deadlines and development/study labels when plotting. Per-trial `missed_target` and `missed_target_status` are also in the ordinary performance CSVs.

## Exploratory eye outcomes

`eye_outcomes.py` adds HTC per-eye query-clock fixation/saccade candidates and IPA diagnostics, with real-data figures, hash/configuration reports and optional independent annotation comparisons. See [measurement definitions and validation plan](../docs/research/eye-measures-validation-20261023.md). These outputs are separate from the condensed performance CSV until detector validation; zero and unavailable remain distinct. No synthetic signals enter the empirical figures.

### Pupil exclusion audit and independent review

The candidate eye-outcome command now always exports `pupil_segments.csv` (all
failed checks plus first failure) and `pupil_quality.json`. `--plot` adds a pupil
exclusion-duration figure; `--review-traces` exports unlabeled raw traces and empty
annotation templates. See `docs/research/pupil-readiness-20261023.md` for the actual
Run 30 diagnosis and the remaining held-out validation requirements. More retained
data is not automatically better measurement; production thresholds remain unchanged.

**Implemented, October 26:** [Capture/timing and study-readiness contract](../docs/research/eye-measurement-readiness-20261026.md). Per-stream native timestamps, matched head-pose evidence, target AOI joins, completion receipts and additional precision definitions are retained without certifying physiological outcomes.

### Separate headset validation sessions

Implemented: `validation_session.py SESSION --out NEW_FOLDER [--measures]` audits the
`EyeMeasurements/validation_*` bundle. It preserves validation/study separation,
explicit missing responses and incomplete steps. See
[the exercise protocol](../docs/testing/eye-validation-exercises.md) for controls,
recorded evidence and remaining validation decisions. Completing exercises is not
measurement approval.
