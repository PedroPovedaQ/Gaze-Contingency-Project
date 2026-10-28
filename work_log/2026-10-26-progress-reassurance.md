# Progress reassurance and one-second observation — 2026-10-26

## Implemented behavior

Both providers use `jev_adaptive_assistance_v4`: seven actions, with `observe_1s` as the only silence action. Retired two/four-second choices are neither offered nor accepted. Action selection and exact phrase selection remain consecutive decisions by the selected provider. Unity supplies truthful prepared alternatives and retains tracking, target hold, lifecycle, audio readiness and freshness overrides.

Current progress qualification now supports slower consistent movement: fresh two-second window, at least 0.5 seconds span, at least one degree net reduction in the combined yaw/pitch error, latest reduction greater than 0.1 degree, consistent direction and no gap over 1.25 seconds. Latest sample must be within 0.75 seconds. These are engineering criteria, not validated fixation or eye-tracker accuracy thresholds. Geometry remains the existing Euclidean norm of yaw/pitch errors; no metric change was made.

Jev instructions and synthetic MiniLM supervision ask for reassurance during qualifying progress, brief observation when the last encouragement ended less than four seconds ago, then further reassurance during continued progress. This is a model preference, not forced speech on a timer. Stalls and reversals should receive directions instead. Jev weights were not fine-tuned; its request policy changed. MiniLM weights were fine-tuned.

## Training and limitations

All checkpoint bundles are under `~/.local/share/follow-my-voice-models/`. The v6 candidate missed slow progress and repeated reassurance despite high synthetic agreement. Continued fitting v7 improved slow progress but the intermediate selected checkpoint still missed repeated reassurance. Those artifacts are preserved, not silently replaced.

The v8 dataset broadens cue history to pending, observed and interrupted outcomes, one to four earlier cues, and missing optional historical fields. Synthetic teacher rules still provide supervision. Different seeds/cadences and feature deduplication provide limited synthetic holdout; these iterations were informed by regression failures, so the regression suite is not an untouched final evaluation. No participant benefit, calibration or advantage over direct rules has been established.

The phrase stage is independently trained from v6's action encoder with the same current feature contract, using authored preferences for repetition avoidance and concise/explicit wording. Its exact parent remains recorded when packaged with the final action model.

## Verification so far

- Python model/contract tests: 38 passed after history coverage changes.
- C# spatial, adaptive and two-stage tests passed; voice isolation passed.
- Live Jev production C# action-to-phrase calls: both progress requests selected `encourage` / “Keep going right.”; both stalled requests selected `orient_explicit` / “Turn farther right.” These were synthetic desktop calls, not participant playback.
- Adversarial raw Jev advice replay: after criteria clarification, 14/16 passed; two already-on-target requests still recommended encouragement. Actual Unity target-hold overrides prevent these calls/playback; local service also guards target holds. Do not interpret raw provider decisions as guaranteed safe without those existing overrides.
- Final v8 action checkpoint: epoch 3 selected by validation after six epochs (449.5 seconds). Validation masked agreement 420/448 (93.75%), gated 434/448 (96.875%); test masked 424/448 (94.64%), gated 426/448 (95.09%). Raw/available-choice advice violated closed gates in 14 validation and two test cases; deterministic gates remain necessary. These are harder/different synthetic examples than earlier bundles, not an apples-to-apples improvement estimate.
- Frozen v8 checkpoint passed all 14 reassurance + prior device regression cases (12 neural decisions, two deterministic closed-gate responses). Action weights SHA-256: `659e0bd470e2a686e7b2b45f7b0a6aaac09466172eb08f6cc99f8a2d5f0ddfaf`. Cases, outputs, datasets, source snapshots, parent hashes and package versions are retained in the bundle.
- Phrase checkpoint test: 152/160 preferred-choice agreement (95%); unused wording selected in all 88 repeat opportunities. These are authored-preference tests, not naturalness or participant effectiveness judgments.
- Running v8 service, production C# pipeline: progress → `encourage` → “Keep going right.” (87 ms); stall → `orient_explicit` → “A bit farther right.” (84 ms). Desktop loopback timings, not acoustic latency or headset frame rate.
- Android build succeeded in 55.3 seconds; APK installed and launched on VIVE Focus Vision `192.168.1.208:5555` over Wi-Fi. App process verified (`9940`). The first menu dispatch failed during Unity script reload; retry after menus returned succeeded.
- Restored ADB loopback forwarding after installation. Actual headset-shell HTTP request to the v8 service selected `encourage` for continued progress after earlier encouragement, with the expected action-weight hash. The shell probe required keeping stdin open briefly so Toybox netcat could receive the response; temporary device probe file was removed. This verifies device transport, not app audio or participant interaction.
- Wearer verification remains open: use Adaptive / Local MiniLM, move steadily toward the target, then stall or reverse; compare with Jev while paused. Check reassurance usefulness, spacing, stale cue cancellation, repetition and audio timing. No commit, push or thesis changes were made.
