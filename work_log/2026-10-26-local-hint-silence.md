# Local MiniLM hint silence — 2026-10-26

## Diagnosis

The latest device replay contained two intervals of approximately 12 and 11 seconds between hint onsets. Requests to Local MiniLM succeeded, and accepted speaking strategies reached playback. During those gaps the model repeatedly chose observe_2s/observe_4s despite available spatial hints.

The adaptive input encoder omitted runtime.seconds_since_speech_ended. Its authored supervision always preferred observation for sustained progress, and observation/encouragement for mixed net improvement, regardless of elapsed silence. Consequently even eight seconds without a hint had the same model representation as one second. This was reproduced in a regression test before editing implementation. Confetti changes are independent success-only effects and cannot cause these active-search observation choices.

## Implementation

Add versioned elapsed-silence features (recent <4 seconds; extended 4–6; long >=6). Authored training examples use additional spatial help for unresolved mixed progress after extended silence; sustained progress retains observation before long silence and can then receive grounded reinforcement or a spatial cue. Unknown/recent silence preserves independent exploration. Insufficient history and the existing safety/target/playback gates still require observation. This is learned supervision, not a runtime forced-speech timer.

Continue adaptive v2 weights into separate v3 and v4 checkpoints. Preserve old weights and the exact legacy input representation. Keep the nine-choice interface, selected Jev policy, grounded phrase resolution, and timeout-only Almost behavior unchanged. The timing bands are development tuning choices, not validated research thresholds.

Training/validation/test use fresh authored synthetic data seeds, no recorded participant payloads. Recorded requests are diagnostic regression probes, not an untouched holdout. Selection is by validation accuracy before test or device probes. Results and deployment receipts follow below.

## Scope

Starting HEAD b89d0d8. Pre-existing changes are the completed spatial confetti sound and its feedback checks/progress log. Preserve those changes. No thesis synchronization, automation changes, tracker creation or push.

## Default provider steering

At the user's request, new study and quick-test sessions now default to MiniLM. Settings exposes the model button outside the development-only controls, still rejecting an unpaused active-search switch. A new participant clears a previous Jev opt-in. Selection and per-request telemetry remain explicit. MiniLM disables incidental Jev warmup, transmission/interruption advice and round-feedback requests; the prepared local catalog supplies round feedback. Voice preparation still uses its existing services.

The first continuation (v3) passed all six prior device probes and 192 closed-gate probes, and supplied hints in the longest recorded gaps, but missed three extended-silence cases. It was not deployed. Its authored corpus always offered both brief and explicit variants, whereas the runtime can offer only one; the follow-up corpus independently varies those capability subsets and balances elapsed-silence bands. No device payloads were inserted into training. A separate v4 continuation preserves v3 and v2. These replay probes are now diagnostic regression checks, not independent final evidence.

## Verification and selection

The v4 checkpoint was selected at epoch 6 by validation accuracy (530/540, 98.15%). Frozen synthetic test accuracy was 534/540 masked (98.89%) and 536/540 after existing gates (99.26%); one raw gate violation was blocked. This measures agreement with authored rules, not human helpfulness or probability calibration. All seven diagnosed silence requests now select the expected offered speech class. All six canonical device regression probes passed, including continued silence for independent progress and correction after departure. All 192 blocked-state probes returned deterministic observation. The previous adaptive-v2 weights remain unchanged (SHA-256 66895733262426fb8b5ae1e26e53e87dd599d443e389013c0281fd6890d5d5fc).

The preserved trainer's initialization prose refers to the original five-to-nine-class expansion; these runs actually copied all nine rows from the parent adaptive model (v2 → v3 → v4). Parent paths, hashes and saved source establish the actual lineage.

Python: 32 tests passed. C# voice isolation and spatial/adaptive suites passed. Unity Study Feedback, timeout and Model Routing checks passed; MiniLM attaches no Jev speech advisors, Jev opt-in attaches them, switching back removes them, and MiniLM round feedback completes locally. The running service was changed from v2 to v4 only after replay checks passed, and its health/tunnel check confirmed v4 on 192.168.1.208:5555.

Artifact directory: `/Users/pedro.poveda/.local/share/follow-my-voice-models/20261026-minilm-adaptive-v4`. Selected weights SHA-256: `e935692af06438aba7abe02025641834529895bb2b621c58f1ab6a39aba8d694`. Full manifests, frozen predictions, lineage and replay report are retained there.

## Deployment receipt

Android build succeeded in 77.0 seconds (1164.9 MB), installed successfully and launch returned Status: ok on Wi-Fi serial 192.168.1.208:5555. Device package lastUpdateTime is 2026-10-26 21:59:06; local APK modification time is 21:58:57 after the v4 version pin. The local v4 service/tunnel was rechecked after installation. Production C# client probes returned observe_4s for independent progress (28 ms) and orient_explicit for a stall (15 ms).

The headset reports Asleep. Its live-headset-state.json is still from 21:42:18, before this build, and says Jev; that is stale prior-run data, not evidence about the newly installed default. The new app process exists but has paused/stopped Android lifecycle events while asleep. Fresh runtime status, actual hint frequency and wearer preference await wake/testing. Existing VIVE/URP editor preprocess warnings appeared, but packaging completed successfully.

No commit or push was made for this change. Earlier confetti edits remain preserved in the checkout and APK.
