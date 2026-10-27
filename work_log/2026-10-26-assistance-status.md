# Assistance provenance in the application — October 26

**Implemented:** current assistance selection is visible in quick-test headset controls (also when Settings is closed), in the Trial Viewer live panel, and in the recorded Guidance State inspector.

The `assistance` diagnostic snapshot records mode, selected provider, model version, policy, connection state, last accepted decision ID/provider/strategy/outcome, and headset capture/decision timestamps. It distinguishes model selection from accepted advice and accepted advice from audible playback. Last decision labels show age relative to the recorded snapshot, so replay does not use the Mac clock. Jev's `configured` status only indicates credentials are present; it is not a network success assertion. Baseline uses local rules and reports no model. Missing legacy diagnostics show Not recorded, never inferred Jev/MiniLM.

`StudyNavigationVoiceBridge` records accepted advice only after freshness and application checks. The diagnostic context includes participant, run, objective, provider and active voice identity; another context cannot inherit the old decision. Explicit provider/policy switches clear the old diagnostic and emit a fresh guidance-state event even when the lifecycle revision is unchanged. Cue outcome `queued` is scheduling acceptance, not audio playback; observed onset remains linked through the decision ID in the existing trace inspector.

`HeadsetPreparationStatus` publishes the snapshot in its half-second sidecar even before a trial/replay exists. The receiver forwards a bounded, validated allowlist, rejecting mismatched providers, nonfinite/future times, unsupported strategies/outcomes and invalid IDs. Freshness/disconnection checks still hide stale live status. Replays retain the optional snapshot in guidance events and samples. Normal participant sessions do not gain a headset debug overlay; the headset indicator is development-only.

`VoiceSynthesizer.CaptureDecisionContext` excludes the diagnostic snapshot before creating any model request. This prevents model identity or previous decision labels from changing Jev/MiniLM inputs merely because the UI now records them. A regression check verifies both exclusion and preservation of the original telemetry object.

Checks: headset preparation heartbeat; voice/session isolation and diagnostic-input exclusion; spatial/adaptive transport; guidance lifecycle; 89 Trial Viewer Python checks; standalone viewer build including Unity serialization/legacy/replay-age checks. All passed. Source diff checks passed. Android deployment and actual sidecar receipt appended below.

Viewer: `/Users/pedro.poveda/Documents/Trial Review Builds/assistance-status-20261026/Trial Review.app`.
Viewer build log: `/private/var/folders/nx/rshb8w7x3fb5svqjk0xhchrm0000gn/T/trial-review-unity-rnmgro4b/build.log`.

**Open verification:** wearer legibility and a real accepted local/Jev decision in the new build. Synthetic fixtures, serialization and heartbeat checks do not establish wearer benefit.

Deployment receipt: Android build succeeded in 44.9 seconds (1164.8 MB), installed successfully over Wi-Fi ADB, and launched with `Status: ok`. Generated IL2CPP code contains the new assistance capture methods. The headset reports `mWakefulness=Asleep`; its existing sidecar still contains the previous paused heartbeat without assistance data. A fresh on-device diagnostic receipt therefore remains pending until wake, rather than being claimed from the successful install. The new standalone viewer is running in live mode against `192.168.1.208:5555`, with its bundled receiver running.
