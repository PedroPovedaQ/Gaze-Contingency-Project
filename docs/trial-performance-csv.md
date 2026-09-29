# Per-trial performance and subjective CSVs

**Implemented locally — September 28, 2026:** `analysis/trial_csv.py` writes one header and one summary row per logical trial, plus `trials.csv` containing those same rows and a provenance manifest. This implements the post-session export portion of [#20](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/20). It runs on the computer against copied, stopped recordings; it does **not** add a writer to the headset or replace raw replay/motion recordings.

```bash
python3 analysis/trial_csv.py /path/to/GazeData/P001/run_001_... \
  --replays /path/to/GazeReplays \
  --output /path/to/exports/P001-run001-trials
```

The run must contain `trial_summary.json`. Optional `trial_schedule.csv`, `object_manifest.csv`, `trial_events.csv` and `voice-library-manifest.json` supply planned geometry, target metadata, actual instruction counts and played voice identity. Replay packages match **participant, run number and source run-folder name**, never nearby timestamps. CSV-only exports still work, with unavailable replay metrics left blank. Existing files are never overwritten; a staged package is published only after validation. Filenames use sanitized trial identities and a stable hash suffix; the manifest maps them back to exact IDs.

Planned but unstarted, interrupted and practice trials remain separate, explicit records. Practice IDs include their recording ID, have no measured-run/block assignment and never inherit block surveys. A completed trial in an incomplete recording remains completed but carries `replay_incomplete` quality information. Do not treat planned rows as completed trials or count the combined table in addition to its individual files.

## What the columns mean

| Group | Implementation and limits |
|---|---|
| Identity | Participant/run/session/trial, zero-based block/trial index, practice flag, original voice label/order, actual voice ID when available, selection method, schedule/seed/layout/pair and build/replay provenance. Historical neutral voices are not relabeled as preferred. |
| Performance | Active-search final correct trigger-selection time, exposure, incorrect selections, Unity onset/capture timestamps and completed-trial wall/pause durations when supported. Correct replay selections supply precise active-clock times; summary times remain the fallback. Wrong choices do not end search. |
| First/final gaze | First and last recorded target intersections with explicitly available/tracked gaze, using sample or selection snapshots. Status columns distinguish observed, not observed and no valid samples. This is a versioned **interaction proxy**, with no added persistence threshold, not a validated fixation or final-gaze episode. Unknown tracking is not valid tracking. |
| First controller contact | First explicitly tracked **right** controller target-ray intersection, independent of gaze. Selection snapshots retain contacts occurring between regular samples. Source columns identify sample versus selection evidence. Historical left-hand selections retain their recorded hand; they do not become right-hand contacts. |
| Instructions | Actual playback starts in `tip`, `guidance` or `guidance_repeat` contexts during active search. CSV events take precedence; replay is fallback, never added on top. Setup, prefetch, requests that never play and success speech are excluded. Delivered repeats are included if labeled `guidance_repeat`; distinct request/delivery counts remain unavailable until separately instrumented. Interruptions are reported separately; partially played instructions still count as started. |
| Motion | Cumulative head translation length and absolute X/Y/Z movement, plus shortest quaternion rotational path and absolute rotation-vector components in fixed recording axes. Components are **not Euler pitch/yaw/roll differences** and need not sum to total rotation. Only adjacent explicitly tracked samples contribute. No interpolation, pause bridging or invalid-sample bridging. |
| Geometry | Target identity, position and planned wall angles; Euclidean head-to-target distance, signed target-minus-head height, target bearing and head-relative yaw at the **first tracked head sample after onset**, with its actual delay recorded. These are not guaranteed exact-onset measurements. All poses used together share the replay coordinate frame. World CSV positions remain labeled when replay is absent. |
| Quality | Sample counts, valid/unknown tracking, motion coverage/gaps, source disagreement, recording truncation and missing survey metadata. Zero requires evidence; blanks do not mean zero. A recording's rendered sample rate is not native eye-tracker frequency. |

`--max-motion-gap-seconds` defaults to 0.1 seconds and is written into the manifest. It is an explicit exporter quality policy, **not a validated physiological threshold**. Gaps above it are excluded from motion paths. The motion totals cover observed valid intervals only; they can underestimate the full path. Test sensitivity to this parameter before analysis.

The original objective record is preserved in `source_trial_json`. Its legacy fixation/saccade labels are source provenance, not newly validated measures. Planned schedule columns are retained as `source_schedule__...`. Raw scene, gaze, motion and audio sources remain necessary for reconstruction.

## Subjective values and later enrichment

The run's `nasa_tlx.csv` is imported automatically when present. Additional wide-format questionnaires use repeatable arguments:

```bash
python3 analysis/trial_csv.py /path/to/run_001_... \
  --replays /path/to/GazeReplays \
  --survey /path/to/reliance.csv --survey /path/to/voice_ratings.csv \
  --output /path/to/exports/P001-run001-with-surveys
```

Each survey row requires `participant_id` and `run_number`. Use `block` (zero-based) for block scope or `trial_id` for trial scope; neither means session scope. Provide `voice_condition`, `response_id` and `instrument_version` where recorded. A trial ID plus block must agree. Explicit mismatched voices, duplicate response scopes and unmatched trial/block references fail export rather than selecting an arbitrary answer. Rows for another participant/run are not broadcast. Survey filenames must have distinct alphanumeric/underscore/hyphen stems.

All raw response fields and original timestamps are retained under `survey__<instrument>__<scope>__...`, with source path/row, scope, response ID and instrument version. Missing response IDs get an explicitly source-row-derived identifier; absent versions say `not_recorded` and raise a quality flag. NASA-TLX dimensions are also exposed as named convenience columns; **no composite scoring or scale conversion is invented**. Import a versioned externally scored value if needed. Draft reliance items are not summed into a validated scale.

Before collection, `survey_status` is `pending_or_not_collected`. A block response repeats into its block's trial rows but remains **one block-level observation**. Re-export to a new directory after surveys arrive: trial identities are stable, rows are rebuilt rather than appended and earlier/raw exports remain untouched. `available` means at least one response joined, not that every expected questionnaire was completed.

## Remaining #20 work

- Freeze the first-gaze persistence/noise rule if the primary outcome requires more than first recorded ray contact (#7/#14).
- Define and record final correct-zone arrival, unique-zone/revisit metrics and distinct guidance-repeat requests/deliveries (#8/#21). Their columns currently remain blank with explicit status.
- Freeze the planned vertical quota reference; `target_minus_head_height_m` is descriptive geometry, not the selected above/below quota variable (#10).
- Integrate device-side per-trial writing if required; this implementation is an offline export, not automatic on-headset persistence.
- Validate fresh full-session data and finalized survey exports on the headset/analysis workflow (#17/#28). P020 is an incomplete older task, not acceptance evidence for 40+40 hidden-target trials.

## Checks

```bash
python3 -m unittest discover -s analysis -p 'test_*csv.py' -v
```

Coverage includes one-row trial files, raw/combined equivalence, tracked contacts, selection snapshots, active clocks, quaternion wraparound, pause/gap handling, playback-versus-prefetch counting, CSV/replay isolation, wrong-run/voice/duplicate survey rejection, practice separation, truncation/corruption, deterministic re-export and overwrite refusal.
