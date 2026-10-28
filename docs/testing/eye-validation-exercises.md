# Eye measurement validation exercises

## Implemented — 27 October 2026

Trial Viewer → **Live mode → Eye validation** controls a separate, unscored headset session. Use the voice/session setup screen before starting a study, or a completed study. Validation cannot start during a study, calibration, activation exercise or voice preparation. No voice-model call or clone is required.

Enter a participant code, measured IPD and its source, optional headset lens spacing, and brief fitting/lighting notes. Unknown measurements stay missing. Choose a battery and arithmetic order. The headset records the seed, exact generated schedule, software presentation times, participant responses, controller rays, raw per-eye gaze/pupil/head observations and source recording paths. Validation does not change the study participant or run number.

| Battery | Recorded exercise |
|---|---|
| Spatial dots | 27 world-fixed positions: horizontal −15/0/+15°, vertical −10/0/+10°, at 1.2/2.4/3.2 m forward distance; 2.5 s each |
| Saccades | 20 s stationary fixation, then 24 seeded jumps across horizontal, vertical and diagonal positions; 1.5–2 s each, no identical consecutive location |
| Controls | Four cued blink windows (8 s each, cue at 4 s), 30 s slow head turning while fixating, 20 s smooth pursuit, three 20 s background-gray blocks, four 15 s gaze-angle blocks |
| IPA arithmetic | Four easy/hard blocks, ABBA or BAAB, three problems per block. Problem shown 4 s; central dot/mental calculation 10 s; answer up to 20 s. Difficulty 1–7 after each block |
| Search practice | Three separate nine-object searches, red cube target, right-ray trigger, 25 s maximum; every trigger attempt and layout recorded |
| Drift | Centre, left, right, down, up; 2.5 s each |
| Full battery | All of the above, in table order |

The first screen of each dot/control exercise waits for **Begin** on the headset or Trial Viewer. Subsequent dots within an exercise advance automatically. Arithmetic problem screens advance automatically after four seconds. Answer using headset buttons, or have the researcher record the spoken answer in Trial Viewer. Answer timing identifies its source; desktop entry latency is not a pure participant reaction time.

**Stop:** Trial Viewer stop control or right-controller **B**. Sleep, focus loss, unexpected study start, or recording errors end the session explicitly as interrupted/error. Existing UI/camera/eye-dot state is restored. A stopped session cannot resume; start a new identified recording. Closing the viewer panel does not stop recording. Disconnecting Wi-Fi does not stop headset recording; reconnect to retrieve it.

## Implemented — evidence and analysis

Device: `Application.persistentDataPath/EyeMeasurements/validation_<UTC>_<id>/`.
The live receiver mirrors this beneath its saved-files `data/EyeMeasurements/` folder.

- `manifest.json`: protocol `eye-validation-v1`, exact schedule, seed/order, code/IPD/notes, build, world anchor/heading, source paths, final status and row count. Checkpointed every five seconds; `in_progress` after a crash is incomplete evidence.
- `events.jsonl`: instruction, stimulus start/end, blink cue, answer/rating prompt, answers, missing answers, search layout/selection, session end. Times use Unity monotonic seconds and include frame, revision, step and source.
- `samples.jsonl`: raw shared gaze snapshots tagged `phase=validation`, stage/condition/step; interleaved controller records have their own `kind`. Unavailable pupil/gaze fields retain validity flags.
- Referenced ordinary eye/native folders retain the original acquisition snapshots, SDK timing, capabilities and writer end-status. Keep these folders with the validation folder. Native clock data are not assumed synchronized beyond the recorded clock brackets.

After stopping the session and completing transfer, analyze a stable copy:

```sh
python3 analysis/validation_session.py /path/to/validation_session --out /path/to/new_report --measures
```

`--measures` uses the existing analysis environment/dependencies. Without it the report uses only standard Python. Outputs include step completion/data counts, missing/actual responses, exploratory event/IPA metrics, diagnostic plots and blank independent-review templates. Review traces do not contain inferred event labels. All results remain `measurements_validated=false`.

Only derived analysis copies map running validation frames into the existing candidate detector's input format. Raw files remain unchanged and are not mixed into study outcomes. Arithmetic excludes the first second after text removal; continuous saccade jumps share an analysis group so the cue boundary does not remove the movement. Partial steps remain explicitly incomplete. Spatial summary v2 recomputes full 3D angular error from the saved target and ray geometry. It uses one observation per HTC host query or EXT input delivery, removes cached copies before window selection, and excludes the first 0.5 s uniformly for every completed spatial/drift target. The 0.5 s window is a post-hoc descriptive pilot choice, not a validated fixation detector. Reports also disclose 0.25/0.5/1.0/1.5 s sensitivity, separate per-eye and unaligned results, and an equal-target mean with missing-target counts. Query identity does not establish unique sensor samples. Incomplete targets have no spatial accuracy estimate; their recorded rows remain available. No offset is fitted or subtracted from the validation run itself.

## Protocol proposal — first headset pilot

1. Fit the headset, record IPD/lens spacing, complete HTC calibration, return to the app's setup screen.
2. In Trial Viewer, connect live. Confirm that gaze and pupil capture status are arriving. Missing timestamps must remain visible, not appear as zero measurements.
3. Run **Drift check** first (~13 s after Begin). Confirm dots are in front at eye height, ordered centre/left/right/down/up. Stop and adjust fitting if placement is wrong.
4. Run **Saccades** (~62 s plus instructions). Keep your head still; follow the dots with your eyes. Check stationary/jump segments in the saved raw traces, not just the candidate count.
5. Run **Controls**, then **IPA arithmetic**. Keep room lighting and headset brightness fixed. Use both arithmetic orders across pilot wearers.
6. Deliberately stop one short run and sleep the headset during another. Confirm distinct aborted/interrupted receipts and preserved recordings. Restart for a complete run.
7. Complete a full battery and repeat drift after a normal task. Compare independently reviewed traces on held-out data before setting acceptance thresholds.

## Open decisions and limits

All counts, durations, detector thresholds and arithmetic difficulty levels are pilot settings, not validated Focus Vision criteria. Two independent reviewers and cross-wearer repeatability remain necessary. The target jump is a software cue, not ground-truth saccade onset. Head-movement/pursuit controls test false positives. Deliberate blinks are cues; actual eyelid events must be inspected.

The gray background is an unlit rendered surface isolated from scene objects; it is not a photometrically calibrated display. Visual arithmetic prompts, response screens and ratings are outside the solve window, but removing text can still evoke a pupil response. The one-second exclusion is provisional. IPA construct validity still requires performance/difficulty checks and independent numerical comparison; completing this battery does not establish workload sensitivity. The nine-object search is a sandbox, not the actual study layout or an ecological validation of the full experiment.

This implementation collects the evidence needed for validation; it does not issue a physiological pass/fail certificate or establish a 120 Hz sensor rate.

## Implemented: alignment followed by independent measurement (2026-10-27)

In Trial Viewer → Live → Eye measurement validation, select **Drift check** and **Align gaze first, then independently check accuracy**. Start validation; in the headset press **Start calibration**, watch each red center, complete the three separate shape checks, and confirm **OK** only if the highlighting matches. Then press **Begin** for the separate five-dot measurement. Keep the headset on and keep its fit unchanged throughout.

The existing bounded three-dot fit is reused: at most 8° total correction (raised from 5° at the researcher’s request on October 27), stable collection, three independent checks with median error ≤2° and P90 ≤3°, and wearer confirmation. These are pilot gates, not validated device specifications. Skipping, failure, focus loss, or a cleared correction cannot silently proceed as an aligned test. Calibration is labeled `validationStage=alignment`, separate from scored `running` dots; the validation manifest embeds the alignment report. Raw per-eye and pre-alignment rays remain unchanged. The validation participant is bound before fitting/applying the correction, so a different participant cannot inherit the previous offset. The validation-only correction is cleared at the end; study setup must run its own alignment. Requests without `alignFirst` retain measurement-only behavior. Ordinary checks may measure an already-active correction, which is explicitly logged.

**Observed:** the earlier completed P032 drift run had no application correction. Native eye rays contained an upward bias, while the native head pose and Unity camera agreed (median orientation difference 0.0015°, P95 0.0302°). Target placement and native-to-world reconstruction agreed with saved geometry. This supports residual tracker calibration/headset fit as the working explanation, not a proven distinction between those causes. No fixed offset was taken from those scored validation dots.

**Open decision / validation:** physical improvement requires a fresh completed recording. Compare raw and corrected error by target, including the left/up regions; do not use calibration fit error as final accuracy. Saccade and IPA validation remains separate.

## Standard study setup (implemented 2026-10-27)

Participant setup now runs three-dot calibration followed automatically by the independent five-dot check, before activation and centering. It has no separate shape screen or second Begin button. The correction remains provisional until verification passes. For corrections ≥0.25°, per-dot before/after comparisons also require average median improvement ≥0.25° and prohibit worsening beyond 0.1° median/0.25° P90. The five-dot result reuses the pilot median ≤2° / P90 ≤3° gates per target (minimum 30 deduplicated observations after 500 ms settling), displays equal-target mean error, and records a participant-specific preparation receipt. Retry repeats the entire fit/check; Skip clears the offset and records skipped. Successful study preparation preserves its accepted correction into activation and trials. Standalone validation commands retain their existing cleanup behavior.

Device smoke test: fresh neutral/clone setup → calibration → five dots → result → activation → Center and begin. Confirm a new participant has no inherited offset; test Skip at both calibration and accuracy instructions; test Retry and focus loss before centering; inspect `study_preparation_*.json` and the linked immutable validation directory.
