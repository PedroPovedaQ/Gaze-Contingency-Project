# Post-calibration drift accuracy audit — 2026-10-27

## Problem and scope

The wearer confirmed HTC calibration was completed before drift run `85bb47a4d353414d9e10a4de6a54ca6c` (P032). Earlier per-target errors 4.2–7.0° were descriptive running-row averages after 250 ms, not certified fixation accuracy. User requested investigation and corrections. Work stayed in the existing checkout/branch at pre-fix HEAD `b89d0d804786df4440eca851265cff06d42f4400`, with substantial existing WIP; the new validation implementation itself was uncommitted. Pre-fix status saved at `/tmp/gaze-accuracy-before-status.txt`. No stash/reset or publication; these files are part of the active feature being corrected.

## Root cause and verified changes

`SharedGazeStream` records both fresh input/query observations and cached heartbeat copies. `analysis/validation_session.py:summarize` treated all valid running rows as independent observations, trusted the stored scalar angle, and used row receipt time after a 250 ms settling exclusion. This overweights cached observations and can promote an older query across a time-window boundary. The trace also shows the large left-to-right movement was still underway after 250 ms.

Added `analysis/validation_accuracy.py` and integrated it into the session report. It deduplicates by recording/device/source and HTC query bracket (host query time fallback), or EXT input-delivery identity, before phase/window filtering. Source-query time controls HTC eligibility. It recomputes the full 3D angle with atan2(cross magnitude, dot product), uses a uniform 500 ms descriptive settling exclusion, requires completed targets, filters other sessions, and reports per-eye/unaligned estimates plus 250/500/1000/1500 ms sensitivity. No same-run fitted correction. Unique queries are not asserted to be unique sensor observations.

Regression in the existing test file failed against the old report (99° stored scalar versus 4° geometric expectation), then passed after integration. Additional cases cover duplicates crossing the window, invalid/zero rays, mixed sessions, after-end observations and incomplete targets. Existing tests only covered session/response flow; they had no spatial-estimation coverage.

## Observed result

Equal-target mean after 500 ms: **5.03197°**. Centre 4.36532°, left 6.54838°, right 2.97687°, down 4.66521°, up 6.60405°. All five targets contribute; no selected gaze rows were marked invalid. Retained 1,667 host-query observations, not a sensor sample count.

Settling sensitivity: 250 ms 5.68879°; 500 ms 5.03197°; 1000 ms 5.02092°; 1500 ms 4.66987°. The window choice is post-hoc and disclosed, not a validated fixation detector. Cached-row deduplication alone changes the old numbers only slightly; transition exclusion accounts for most change.

All selected rays were `HTC_binocular_mean`, with no origin mirror or application quick-alignment correction. Independent double-precision geometry reconstruction agrees with Unity's saved scalar within 0.00018°. Native-to-world origin/direction reconstruction agrees within float tolerance; scheduled target centres match saved positions within 0.000001 m. Vendor conversion checked in installed OpenXRUtils.cs. These checks establish coordinate consistency, not independently measured physical ground truth.

Both eyes retain upward residual error. Head orientation changed within several windows (about 0.4–11.6° relative to each window's initial pose), while the nearly stationary down window still averages 4.67°. This recording cannot conclusively separate residual gaze behavior, headset calibration, and unsynchronized acquisition/display timing. Do not claim the analysis fix improved physical tracking or establishes intrinsic device accuracy.

## Quality and verification

Fix scope: new accuracy helper, targeted changes to validation_session.py and its existing tests, protocol documentation, this log and progress.md. Sequential manual review covered source identity, invalid/missing data, session isolation, window boundaries, aggregation, and interpretation. No independent or full formal CE review claimed; no file-wide simplification of overlapping WIP. Related existing tracking issues #14 and #35; no open PR found for this fix. Those broader issues remain open.

351 analysis tests passed; git diff --check passed. User-required Unity Build And Run completed successfully (21.1 s), installed and launched over Wi-Fi, Status: ok. No headset runtime behavior changed by this analysis correction. No commit/push: files overlap the ongoing uncommitted validation feature.

## Artifacts

`~/Library/Application Support/Follow My Voice/Validation Pilot 20261027/audits/drift-85bb47-accuracy-v2/` contains report.json, steps.csv, accuracy-audit.md and accuracy-traces.png. Original source files and the old report are retained separately. Reproduce numeric report with `analysis/validation_session.py` against the immutable completed-session copy using a new output directory.
