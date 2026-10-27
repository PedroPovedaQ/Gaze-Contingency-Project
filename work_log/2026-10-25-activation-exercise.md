# Post-calibration activation exercise — 2026-10-25

User chose both gaze activation and right-controller selection.

## Implemented

- `FindObjectGameManager.Start` now runs calibration → activation exercise → centering → existing uncounted practice. `StartCenteredSession` cannot bypass exercise completion. Recalibrate restarts entry calibration and a fresh exercise; technical stop/disable aborts it.
- Three separated objects at eye height and 1.5 m forward use the study layer, object metadata, shape colliders, no-grab XRI configuration and material color setup.
- `ControllerRaySelector` routes its existing right-controller ray, haptic/highlight/rotation and release-then-trigger path to exercise objects while active. It does not emit scored capture callbacks during the exercise. The same `GazeTargetVicinity.ClosestObject` resolver drives shine and qualification.
- Prompted object requires 0.35 seconds of continuous, fresh, unambiguous gaze response followed by right-trigger selection while still looking at it. Wrong gaze/selection does not advance. Looking away, stale update gaps, focus loss and Settings clear qualification. Each next step requires a fresh trigger release.
- Three successes enable Continue. No skip or timeout silently passes the exercise. This is functional familiarization, not fixation measurement or held-out accuracy validation; it does not refit calibration.
- Separate `activation_<id>.json` under EyeMeasurements records attempts and outcomes with run/participant/alignment/recording IDs. Eye rows have phase `activation_exercise` and `activationStep`, with no scored trial ID. Failed outcome writes prevent continuation.

## Verification

- `bash scripts/test-activation-exercise.sh` passes: gaze/trigger order, wrong target, invalid gaze, looking away, stale/backward/nonfinite time, independent steps and completion.
- Unity exercise integration checks pass: correct geometry/metadata, actual gaze winner, selector routing ownership, invalid input, unscored state, telemetry, retry cleanup, completion receipt.
- Real-component rendered preview inspected. Initially caught incorrect shared shader color initialization; setting both study color properties fixed the red/blue/yellow presentation. Preview is `Temp/ActivationPreview/exercise.png`.
- Existing controller fixture required updating for the previous Eye dot UI fields; no runtime guard was weakened to make tests pass. Editor focus checks remain separate from completion-core tests because edit mode reports unfocused.
- Final review aligned the exercise object-only ray mask with the rotational study and restores the setup mask on exit.
- Final controller-selection, entry eye-alignment and gaze-quality regression checks passed. Rendered final preview confirms correct colors and readable gaze-response status. Final Android build passed in 226.6 s (1164.4 MB); streamed Wi-Fi install succeeded and launch returned `Status: ok` on `192.168.1.208:5555`. Generated native code includes the final ray-mask restoration field. Trigger: `/tmp/gaze-activation-final-build.log`; full build/test receipts: `~/Library/Logs/Unity/Editor.log`.
- Wearer verification on the headset remains open.
