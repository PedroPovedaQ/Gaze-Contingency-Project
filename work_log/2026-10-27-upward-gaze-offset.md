# Upward gaze offset investigation — 2026-10-27

## Evidence

Completed drift run: `85bb47a4d353414d9e10a4de6a54ca6c`, P032 development check. Corrected descriptive five-target mean error 5.03197°. Both native eyes show upward residuals; no app alignment was active. Previous audit independently reconstructed scalar error (max discrepancy 0.000172°), target geometry (≤3.33e-7 m), and eye coordinate conversion (~5.3e-7 direction-vector discrepancy). This investigation additionally compared native requested-time head orientation to Unity camera orientation over 2,068 distinct input deliveries during running: median 0.0014935°, P95 0.0301861°, max 0.2173551°. Native eye origins relative to head were about 2.6 cm forward and within millimetres vertically. The persistent stationary-target bias is not explained by those pose differences.

Working explanation: residual eye-tracker calibration or headset fit. The recording does not prove which; the user reports HTC calibration had already been run. No global pitch subtraction or refit on the scored drift data was introduced.

## Implemented

- Trial Viewer `alignFirst` option stages the existing guided entry alignment without requiring voice synthesis or starting study trials. Three fit dots → three independent shape checks → wearer OK → selected validation exercise.
- Uses existing 5° correction cap, stability gates, ≤2° median/≤3° P90 separate checks, improvement requirement and manual confirmation. No relaxation of research gates.
- Calibration observations have separate `alignment` stage; later `running` dots are independently scored. Manifest retains fit/check report; raw eye/pre-correction rays remain available.
- Validation participant context is selected before SharedGazeStream fitting/application, fixing the prior late-annotation identity boundary. Validation cannot reuse another participant's offset.
- Skip/failed completion stops this aligned attempt; focus/pause or cleared alignment interrupts. Ending validation clears its scoped correction. Study setup still requires its own alignment.

## Verification

- Validation schedule/transition C# checks, five receiver command tests and seven analysis tests passed.
- Unity QuickGazeAlignmentChecks, EntryGazeAlignmentChecks, and new ValidationAlignmentChecks passed (participant scope, fit-row separation, preserved target geometry, no remote acceptance bypass, backward-compatible request defaults).
- Desktop build succeeded: `~/Documents/Trial Review Builds/Trial Review - Gaze Alignment 20261027.app`.
- Android build succeeded in 56.0 s (1166.7 MB); installed and launched over Wi-Fi. Package lastUpdateTime: 2026-10-27 09:53:09; version 2.2.1. Startup error-filtered log returned no Unity errors, but the app remained paused, so this is not exercise validation.
- Updated desktop app launched and receiver connected. CUA window interactions were unreliable; no claim of completing a UI-driven alignment test.
- User is not ready to wear the headset yet. No new alignment/validation exercise started; physical improvement remains unmeasured.

## Test procedure

Keep fit unchanged and remain in Follow My Voice. Stage P032 drift with `alignFirst=true`; participant starts calibration and confirms shape highlighting. Begin five-dot check only after calibration passes. Pull immutable completed artifacts, recompute raw/corrected angular errors with the fixed 500 ms windows and report each target. Do not call the study well calibrated until wearer evidence supports it; saccades/IPA need their separate validation.

## Wearer retries, 14:27 UTC / 10:27 Eastern

Validation session `c2b42f16b8424f588be479e4ff9de51c`, participant code P033. Retrieved immutable rejection reports `f6e7856570bc4f14afea14c65ba4fd50` and `69cfb2b04abd4f6bb07a34b0859805cf` into the local validation audit archive (`alignment-rejections-20261027T1427`). First fit: yaw correction +0.1074°, pitch −6.0346°. Second: yaw +0.0526°, pitch −5.6926°. Per-dot required downward corrections: first 6.0574/5.7171/6.3294°, second 5.7160/5.7227/5.6392°. All three dots collected in both attempts; spread P90 values 0.185–1.0555°, below existing 1.5° stability gate. Both rejected on unchanged 5° correction cap, before independent shape checks. Thus this is a repeatable predominantly vertical bias, not proof of poor fixation or a successful correction. No new independent corrected accuracy is available. Next diagnostic step: refit comfortably, verify lens spacing, repeat HTC system eye calibration in current fit, then repeat app alignment without removing/adjusting headset. The 5° cap is an application pilot guard, not a published HTC validity boundary. No code/threshold change.

## Researcher-requested correction cap change

User explicitly requested raising the limit and using the measured angular offset to continue. Changed `MaxCorrectionDegrees` from 5 to 8, allowing the observed 5.69–6.03° fits to proceed to separate checks. This is a deliberate pilot parameter change, not evidence that native gaze improved. No hardcoded wearer offset; fresh fit, immutable raw rays, head-relative application, participant/source binding and all held-out gates remain. Added regression coverage for a six-degree fit needing separate checks and confirmation; a six-degree vertical fit that fails independent accuracy still rejects; nine-degree fits still reject. Active failed validation was stopped and acknowledged before deployment to seal its files.

Cap-change verification: QuickGazeAlignmentChecks passed in an isolated Unity project (`/tmp/calibration-cap-checks-20261027.log`); validation schedule checks, five receiver tests and seven analysis tests passed. Android build succeeded in 52.4 s; Wi-Fi installation returned Success and launch Status: ok. Device package lastUpdateTime 2026-10-27 10:32:33 Eastern. Fresh wearer calibration/held-out result remains pending.
