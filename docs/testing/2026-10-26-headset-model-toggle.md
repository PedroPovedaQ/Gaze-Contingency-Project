# Headset adaptive-provider comparison

**Implemented — development quick tests only.** Open a neutral or cloned-voice quick test. Open headset **Settings**, leave **DEV guidance: adaptive** selected, and use the neighboring **Model** button to switch between **Jev** and **Local MiniLM**. Settings pauses an active search; switching is rejected while a search is running unpaused. Close settings and resume using the existing pause flow. Local mode shows Connecting, Ready, or Unavailable. Both use the same adaptive-v2 state, nine strategy choices, capability masks, prepared audio, voice condition, freshness checks and final geometry/target gates. This selects the active assistance provider, not a shadow prediction.

The Mac hosts MiniLM; the weights are not embedded in the headset. Run from this checkout:

```sh
python3 tools/local-jev/headset.py
```

The helper starts or reuses the pinned `20261026-minilm-adaptive-v2` CPU service on Mac loopback port 8766 and adds an ADB reverse tunnel to the authorized headset (USB or Wi-Fi ADB). Keep the Mac awake and connected. Rerun after reconnecting ADB or restarting the Mac. With multiple devices, provide `--serial`. It refuses to replace a service with an unexpected contract/checkpoint and never resets ADB. Service logs are under `~/.local/share/follow-my-voice-models/headset-service/`; the helper records its launched process ID there. It starts no trials. No cloud API key is sent to the local endpoint.

A local connection check verifies the assistance scope and checkpoint version. Each accepted decision also verifies the response decision ID and version, and retains the checkpoint SHA-256. Unavailable, wrong-model, malformed or timed-out responses cannot fall back to cloud Jev or baseline search hints. Pending requests and queued guidance are cancelled when switching; provider identity and the policy epoch invalidate stale work. The local connection is rechecked while selected. Baseline ignores the provider selection. New participant reset restores Jev and normal study sessions cannot enable the development model.

**Comparison scope:** the toggle changes the adaptive when/how-to-help decision. Unity still resolves directions/phrases and handles shared confirmation, trigger reminders and safety. Separate existing Jev services, such as round encouragement or interruption advice where used, retain their existing routing in both modes; MiniLM only implements the `assistance` contract. This is not a fully offline mode. Switching mid-trial preserves the participant's real cue/outcome history; it is an informal paired development test, not randomized study evidence. For a cleaner comparison, use separate quick-test runs, the same voice and the same scenarios, and alternate which model you try first.

**Recorded evidence:** request/response traces include `provider` and `model_version`; local responses retain probabilities, probability semantics, source and checkpoint hash. Application traces retain provider and request ID; playback links use that ID. Provider changes are explicit non-study events. `assistance_provider_at_summary` is only the final selection, never a claim that every decision in a mixed run came from it. The Trial Viewer model inspector labels each request's provider, checkpoint version, probabilities and playback. Older traces lacking provider tags retain their historical Jev interpretation. The shadow comparator excludes active local traces so they cannot become mislabeled cloud comparisons; the earlier observer was stopped when active switching was introduced.

## Verification

- C# spatial/adaptive transport suite: local loopback endpoint, no Authorization header, shared contract, model/request identity rejection, cancellation and no cross-contract routing; passed.
- Voice isolation/session suite: quick-test selection, baseline identity, reset and study exclusion; passed.
- Production C# client to actual model: independent progress returned `observe_4s`; stall returned `orient_explicit`.
- Actual headset-shell HTTP request through the Wi-Fi ADB reverse tunnel returned `correct` with valid probabilities and the expected checkpoint hash. This verifies transport from the device, not wearer interaction or app audio.
- Python local-model suite: 31 passed. Standalone Trial Viewer build/checks passed. Final Android build passed in 78.9 seconds, installed and launched over Wi-Fi ADB. Detailed receipts are in the session work log.
- **Open verification:** wearer toggles both modes in the installed APK, sees Local Ready, and listens to the same stall/progress/departure scenarios. Assess usefulness and request-to-audio timing from those recordings; synthetic accuracy and loopback speed are not participant-benefit evidence.


**Implemented — application status display:** the quick-test headset control panel now shows the selected mode/provider, connection state and latest accepted decision with its age, even with Settings closed. Trial Viewer Live shows the same information from fresh headset heartbeats; its Guidance State inspector shows provider, model version, connection, accepted strategy/outcome and decision ID at the replay cursor. `queued` is not proof of playback, and `configured` is not proof of cloud reachability. Older recordings show Not recorded. The `assistance` snapshot is included in replay samples/events and pretrial status, but removed before model requests so it cannot bias the comparison. See [implementation and verification](../../work_log/2026-10-26-assistance-status.md).
