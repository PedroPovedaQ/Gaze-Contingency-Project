# Per-trial performance and subjective CSVs

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

**Implemented locally — September 28, 2026:** `analysis/trial_csv.py` writes one header and one summary row per logical trial, plus `trials.csv` containing those same rows and a provenance manifest. This implements the post-session export portion of [#20](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/20). It runs on the computer against copied, stopped recordings; it does **not** add a writer to the headset or replace raw replay/motion recordings.

```bash
python3 analysis/trial_csv.py /path/to/GazeData/P001/run_001_... \
  --replays /path/to/GazeReplays \
  --output /path/to/exports/P001-run001-trials
```

The run must contain `trial_summary.json`. Optional `trial_schedule.csv`, `object_manifest.csv`, `trial_events.csv` and `voice-library-manifest.json` supply planned geometry, target metadata, actual instruction counts and played voice identity. Replay packages match **participant, run number and source run-folder name**, never nearby timestamps. CSV-only exports still work, with unavailable replay metrics left blank. Existing files are never overwritten; a staged package is published only after validation. Filenames use sanitized trial identities and a stable hash suffix; the manifest maps them back to exact IDs.

Planned but unstarted, interrupted and practice trials remain separate, explicit records. Practice IDs include their recording ID, have no measured-run/block assignment and never inherit block surveys. A completed trial in an incomplete recording remains completed but carries `replay_incomplete` quality information. Do not treat planned rows as completed trials or count the combined table in addition to its individual files.

## October 25: exact pre-cue theta (implemented)

Full `trials.csv` and every per-trial performance CSV include `pre_cue_theta_*` fields: availability status, reference, coordinate frame, round, source gaze sequence, snapshot/receipt realtime, assigned/realized signed angle, absolute angle, circular assignment error, target-minus-cross height and all three eye/cross/target coordinates. These copy the recorded `pre_cue_theta` summary object without rounding or substituting later head poses. Malformed or contradictory snapshots stop export before publication. Older/missing snapshots have `not_recorded` status and blank measurements; zero remains a real recorded value.

Snapshot coordinates use **Unity world metres** and snapshot clocks use **application realtime seconds**. They are separate from the fixed-origin replay frame and replay elapsed clock. The snapshot is at readiness completion before opening speech, not necessarily at acoustic cue onset. It measures horizontal geometry, not accumulated movement.

Replay target geometry and cross-relative height now use finalized readiness transforms, including v7 lateral cross offsets. The v7 octagonal layout also supports the existing gaze-wall analysis. `direction_policy` documents independent block allocation; `angle_pair` is an allocation ID, not a promise of mirrored v7 trials.

Trial Review shows the snapshot under **At cursor**, keeps it through samples, removes future evidence on backward seeks, and clears it at the next trial. **Final data** shows every exported field. Selected-trial exports retain exact snapshot JSON in `pre-cue-theta.jsonl`; repeated readiness attempts remain separate records. The condensed CSV adds status, realized signed/absolute angle, assignment error and target-minus-cross height. Use full CSV/JSON for coordinates and clocks.

Validate stopped v7 runs with `python3 scripts/verify-study-session.py RUN_FOLDER`; add `--allow-incomplete` only for a deliberately stopped run. The validator audits the full 80-row plan even for shortened or 40-trial development modes, independent direction/height quotas, policy/identity metadata, snapshots and opening order, manifest target geometry, and completed/timeout timing. Valid timeouts are terminal outcomes, not successful selections. A passing log audit does not establish ergonomic comfort or headset calibration validity.

## What the columns mean

### October 8 measurement decisions (implemented locally, export schema v2)

- **Completion:** use `time_to_final_selection_seconds` for the final correct
  controller selection. The unused `time_to_final_correct_zone_seconds` placeholder
  is removed. There is no separate final gaze-zone arrival outcome.
- **Zones:** `unique_zones_visited_count` and its compatibility alias
  `zones_visited_count` count distinct horizontal walls; `zone_transition_count`
  counts confirmed changes and `zone_revisit_count` counts changes back to a
  previously seen wall. Only active-search, explicitly tracked gaze contributes.
  The current policy requires 0.15 seconds of continuous distinct observations
  within the same wall and breaks continuity across gaps over 0.1 seconds, invalid
  gaze, pauses or source changes. Exact corners and nearly vertical gaze are gaps.
  These are exported jitter-filter settings, **not validated fixation thresholds**.
  Counts are observed lower bounds: no transitions or revisits are inferred across
  gaps, though reacquired walls can contribute to the unique set. Missing geometry
  or no stable valid wall observation leaves counts blank with a status.
- **Height:** `scheduled_height_delta_m` is the target's trial-layout Y minus the
  recorded readiness-cross Y, which Unity sets to the object-height range midpoint.
  Positive is above the midpoint, negative below. The cross reference is exported
  separately in replay coordinates. This requires supported rotational layout and
  a valid recorded readiness event; it is never reconstructed from current defaults
  or from the min/max of randomly scattered objects. Actual head-relative height
  remains a separate field. This change does not impose half-above/half-below quotas.
- **Incomplete pauses:** no new pause-duration calculation is added, as requested.
  Existing completed-trial pause duration and raw pause events remain unchanged.

Zone geometry uses the same eight-wall horizontal ray/plane construction as
`RotationalSearchLayout.IsInHorizontalSector`, including the recorded gaze origin
instead of just its direction. In `rotational_scatter_v4` replay space, the cross
also supplies wall radius and orientation validation. Looking up/down within the
same horizontal wall does not add visits. Native sample identities are used to
reject duplicate observations when present; older recordings use recorded frames.

### October 8: separate motion files and demo sheets (implemented locally)

Add `--motion` to create `motion.csv` plus one `_motion.csv` companion per trial
with recorded sample/selection rows. The manifest links each companion to its
performance/subjective file and records row counts. Trials without replay data
have `motion_status=no_matched_replay`, not measured-zero movement. This option
does not change the existing summary metrics.

The motion export preserves invalid, inactive, paused and post-selection samples,
tracking flags, controller trigger values and nested source fields under
`replay__...`. It does not interpolate, resample or discard tracking loss. Each row
retains source line/sequence and the original record JSON. Positions are in the
replay's fixed-origin coordinates; rotations are quaternions. `replay_time_seconds`
is local to `recording_id`, whereas `active_search_seconds` excludes pauses. Never
join different recordings on time alone. Selection rows may share a timestamp with
a frame sample and are not independent sensor observations.

Keep original JSONL, scene, audio and video: object snapshots and non-pose events
are not standalone rows in the motion CSV. This file alone is not a scene replay
or a native 120 Hz eye-acquisition stream. Input fingerprints are rechecked before
publishing the package; export only copied, stopped recordings.

`session_mode`, `data_purpose`, `exclude_from_study_analysis` and
`analysis_exclusion_reason` now appear in trial and motion rows and the manifest.
Known neutral quick-test and synthetic runs are explicitly excluded; practice is
always excluded. Legacy/unknown session modes leave the exclusion flag blank and
require review. `study_declared` with flag `0` means the source declared study mode,
not that protocol compliance, data quality or participant eligibility was validated.
These are export labels: downstream analysis must apply them. Existing analysis
loaders are not changed by this export work.

Generate the repeatable, invented example package in a new directory:

```bash
python3 analysis/demo_trial_sheets.py --output analysis/results/synthetic-demo
```

It includes clean completion, a wrong selection followed by completion, and a
paused/interrupted incomplete trial, plus synthetic block survey responses. All
rows are labeled synthetic and excluded from study analysis. It is three example
trials, not the real 40-trial schedule, a power-analysis dataset, or headset
validation. Block responses repeat with the same response ID. Incomplete trials
have no final-selection time; currently completed-trial wall/pause totals remain
unavailable for incomplete trials, whose pause events remain in the raw replay.

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

**October 1 motion method:** `head_motion_method=tracked_frame_and_selection_intervals_v2` includes explicitly tracked head poses saved at correct and incorrect trigger selections, including the final observed movement before capture. `head_motion_observation_count` counts considered frame/selection poses; gaze-validity sample counts still use only rendered-frame samples. Invalid poses, pauses and excessive time gaps break the path; later post-capture poses are excluded. Older selection records without head-observation fields add nothing. A selection can supply the first observed geometry reference when no tracked frame exists; `geometry_reference` explicitly identifies that case. One pose supplies geometry, not a measured zero-length path. Preserve method versions when comparing exports; previous exports are not rewritten.

## Subjective values and later enrichment

The run's `nasa_tlx.csv` is imported automatically when present. Additional wide-format questionnaires use repeatable arguments:

```bash
python3 analysis/trial_csv.py /path/to/run_001_... \
  --replays /path/to/GazeReplays \
  --survey /path/to/reliance.csv --survey /path/to/voice_ratings.csv \
  --output /path/to/exports/P001-run001-with-surveys
```

Each survey row requires `participant_id` and `run_number`. Use `block` (zero-based) for block scope or `trial_id` for trial scope; neither means session scope. Provide `voice_condition`, `response_id` and `instrument_version` where recorded. A trial ID plus block must agree. Explicit mismatched voices, duplicate response scopes and unmatched trial/block references fail export rather than selecting an arbitrary answer. Rows for another participant/run are not broadcast. Survey filenames must have distinct alphanumeric/underscore/hyphen stems.

All raw response fields and original timestamps are retained under `survey__<instrument>__<scope>__...`, with source path/row, scope, response ID and instrument version. Missing response IDs get an explicitly source-row-derived identifier; absent versions say `not_recorded` and raise a quality flag. NASA-TLX dimensions are also exposed as named convenience columns; scoring follows the explicit contract below. Draft reliance items are not summed into a validated scale.

### NASA-TLX scoring and examples

**Implemented locally:** `nasa_tlx_score` is the unweighted Raw TLX mean of all six
dimensions on the declared native scale, under `raw_tlx_six_dimension_mean_0_10_v2`
or `raw_tlx_six_dimension_mean_0_100_v2`. The October 20 protocol uses raw 0–10.
`nasa_tlx_score_0_100` provides an explicitly labelled conversion; source ratings
and scale endpoints are preserved.
It is not the original weighted TLX. [Cao et al. (2009)](https://doi.org/10.3758/BRM.41.1.113)
describe software supporting both weighted and unweighted scores. A concrete
example is [Burian et al. (2013), NASA/FAA, Table 12](https://www.faa.gov/sites/faa.gov/files/data_research/research/med_humanfacs/oamtechreports/201317.pdf),
which reports average RTLX and aligns performance direction with the other
dimensions. [NASA's official overview](https://www.nasa.gov/human-systems-integration-division/nasa-task-load-index-tlx/)
describes the original weighted method.

The source `nasa_tlx.csv` must explicitly include `scale_min=0`, `scale_max=10` (current) or `100` (legacy) and
`performance_direction=high_is_poor` or `high_is_good`. With `high_is_good`, scoring
uses `scale_max-performance`; raw response values are unchanged. The other five
dimensions must run from low demand/effort/frustration to high. All six values must
be present and finite within the declared scale. No missing value is imputed, no partial mean is
reported, and no scale endpoints or weights are inferred. Scoring method/status
accompany every result. The legacy loader and plot now also use the validated
native mean rather than a sum; plots use the explicitly normalized 0–100 field. Unknown metadata results in an unscored row.

**Implemented study route:** no in-headset block survey is administered. The researcher collects both retrospective block sets in the final survey, recording `administration_phase=final`, `reference_scope=participant_by_block`, actual timestamp and instrument version. Qualtrics remains on hold. The development quick-test workload form is legacy and outside participant study data; never guess how participants understood historical performance sliders. External metadata must describe the administered instrument. A block's TLX score repeats into its trial rows but remains one block-level response.

Before collection, `survey_status` is `pending_or_not_collected`. A block response repeats into its block's trial rows but remains **one block-level observation**. Re-export to a new directory after surveys arrive: trial identities are stable, rows are rebuilt rather than appended and earlier/raw exports remain untouched. `available` means at least one response joined, not that every expected questionnaire was completed.

## Remaining #20 work

- Freeze the first-gaze persistence/noise rule if the primary outcome requires more than first recorded ray contact (#7/#14).
- Validate the implemented zone-visit filter on headset boundary jitter, pitch changes, tracking loss and replay gaps (#8/#21). Completion timing is final selection, per the October 8 decision.
- The vertical reference is now cross/plane midpoint. Enforcing a balanced above/below schedule remains separate work (#10); head-relative height is still descriptive geometry.
- Validate final external survey exports against the documented 0–10 anchors, reverse keys, block reference and administration metadata before collection. IMI/Guo automatic subscale scoring remains outstanding.
- Integrate device-side per-trial writing if required; this implementation is an offline export, not automatic on-headset persistence.
- Validate fresh full-session data and finalized survey exports on the headset/analysis workflow (#17/#28). P020 is an incomplete older task, not acceptance evidence for 40+40 hidden-target trials.

## Checks

```bash
python3 -m unittest discover -s analysis -p 'test_*csv.py' -v
```

Coverage includes one-row trial files, raw/combined equivalence, tracked contacts, selection snapshots, active clocks, quaternion wraparound, pause/gap handling, playback-versus-prefetch counting, CSV/replay isolation, wrong-run/voice/duplicate survey rejection, practice separation, truncation/corruption, deterministic re-export and overwrite refusal.

## September 30 provenance additions (implemented locally)

`repeat_request_count` records requests, including requests made without usable gaze; delivered repeat count records actual audio starts. `instruction_count` includes delivered repeats, whereas `hint_count` excludes them. `repeat_event_version` establishes whether zero is known; historical absence stays blank. CSV and replay are alternative evidence sources, not additive counts. `interaction_policy` records right-controller-only input.

`target_identity_policy` and `height_assignment_policy` accompany `schedule_version`. The 80-trial version balances absolute wall angles and identity counts between voices; it explicitly does not yet impose signed-height quotas. Raw files and old versions remain unchanged.

## Missed Targets — October 23 deadline policy

**Implemented locally.** `missed_target` is 1 when the deadline expires before a correct selection, including attempts with earlier wrong selections; 0 for a correct selection; and blank for technical failures, unstarted trials, or unresolved recordings. `missed_target_status` explains that classification. `active_search_limit_seconds` preserves the recorded limit; older runs without it remain unknown.

The generated `missed_targets.csv` and manifest report counts per participant/run/block/voice/deadline. Missed-target percentage is **100 × timeout count / (correct selections + timeouts)** among trials with a recorded positive deadline. Practice is excluded; unresolved and nonbehavioral endpoints are counted separately. A zero denominator produces a blank percentage. Separate deadlines are never pooled, and historical recordings are not retroactively truncated. Report the denominator and exclusions with any graph. This measure concerns failure to finish within the limit, not whether the participant ever gazed at the target. It adapts, rather than exactly duplicates, Chiossi et al.'s “no selection” definition because this task allows incorrect attempts before expiry.
