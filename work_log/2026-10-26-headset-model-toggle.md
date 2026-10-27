# Active headset Jev / MiniLM selection — October 26

Implemented in the shared `gaze-recovered` checkout without committing or overwriting other work. User request: switch between Jev and the updated local model and test both in the headset.

- `SessionConfig` keeps a development-only local-provider selection, separate from Adaptive/Baseline. New participant reset restores Jev. Study sessions reject selecting local.
- `StudyRayControls` adds the Model button beside DEV guidance in Settings, including local connection status. `HintGenerator` switches only outside running/unpaused search, increments the pending-decision epoch, resets scheduling/continuous observation, cancels provider/queued audio, and records the change. Prior observed cue/outcome history is retained honestly.
- A separate `JevDecisionClient.CreateLocalAdaptive` instance shares the exact assistance payload/parser, bound response handling and deadline. It accepts only assistance, sends no cloud credential, uses fixed headset loopback port 8766, checks checkpoint version and response decision ID, and retains provider/version/hash/probability semantics. The primary Jev client keeps its existing unrelated duties.
- `StudyNavigationVoiceBridge` routes active adaptive decisions to the selected provider. Local health verifies assistance scope/version, reconnects periodically and exposes status. Provider selection is captured in freshness checks/application traces. No local-to-cloud/baseline fallback for unavailable adaptive decisions.
- `VoiceSynthesizer.Transmission` recognizes successful local adaptive preapproval as speech rather than fallback; tests verify no second cloud transmission call and actual onset consumption.
- `tools/local-jev/headset.py` starts/reuses the pinned CPU model service and sets ADB reverse for one authorized headset. It binds only Mac loopback, leaves ADB intact, refuses wrong-model services, and writes private service logs/PID under `~/.local/share/follow-my-voice-models/headset-service`. Rerun after reconnect/restart. Mac must stay awake.
- Trial Viewer retains the existing trace compatibility, labels per-request provider/model and probability semantics. Summary provider field is explicitly named `assistance_provider_at_summary`, not a whole-run assignment. Request IDs link responses, applications and audio onset.
- Stopped the previous v2 shadow observer via its stop-request marker; confirmed stopped. Updated shadow filtering/tests to ignore active local traces, preventing local outputs from being counted as cloud Jev comparisons. Training artifacts/weights were not changed.

Verification:

- Spatial/adaptive network C# suite passed: loopback route, no auth header, correct contract, malformed/unoffered strategies, checkpoint/request mismatch, cancellation, old-contract rejection and trace attribution.
- Voice/session isolation suite passed including local selection, baseline identity, normal-study exclusion/reset, no extra cloud call and truthful local speech outcome.
- Python local-model suite: 31 passed.
- Actual production C# client against running MiniLM: progress → observe_4s; stall → orient_explicit.
- Actual headset-shell POST through Wi-Fi ADB reverse: correction scenario → correct; model checkpoint `66895733262426fb8b5ae1e26e53e87dd599d443e389013c0281fd6890d5d5fc`. Evidence in `docs/testing/2026-10-26-headset-local-transport.json`. This is not an app audio test or latency benchmark (builds were running concurrently).
- Standalone Trial Viewer build and all built-in checks passed, including provider identity/probability regression. App: `/Users/pedro.poveda/Documents/Trial Review Builds/model-toggle-20261026/Trial Review.app`. Build log: `/private/var/folders/nx/rshb8w7x3fb5svqjk0xhchrm0000gn/T/trial-review-unity-zpgqvnqv/build.log`.
- First Android APK built in 168.8 seconds and installed/launched over Wi-Fi. A final incremental build includes the corrected local transmission-outcome label; receipt appended below.

Wearer verification remains open: in a neutral or cloned quick test, Settings → DEV guidance: adaptive → Model: Local MiniLM; wait for Ready, close settings/resume. Compare with Jev using stalled search, healthy progress, confirmed-target departure and trigger reminder. Local mode is Mac-assisted, not on-headset inference or fully offline. Intro/confirmation/reminder/voice pipeline remain common; unrelated Jev encouragement/interruption services keep existing routing. Use separate runs for cleaner comparisons; mixed trials are only development evidence.

Final receipt: Android build succeeded in **78.9 seconds**, APK 1164.7 MB; Wi-Fi ADB install returned `Success`, launch returned `Status: ok`. Final local service/tunnel readiness rechecked after installation. No recent Unity/Android fatal exceptions were observed in the bounded error-buffer check. `git diff --check` passed. No wearer interaction/audio test was claimed.
