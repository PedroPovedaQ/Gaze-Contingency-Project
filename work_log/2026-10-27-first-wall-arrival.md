# First target-wall arrival override

**Implemented:** Adaptive mode prioritizes its first qualified target-wall arrival and sends a phrase-only decision to the currently selected model. Four existing prepared area phrases are eligible. The event bypasses action selection, not phrase selection or live geometry checks. Round playback history is preserved in the request. Once-per-round consumption occurs at audio onset and survives pause/resume. Target confirmations outrank and suppress redundant arrival announcements.

The arrival request may run during audio. Local MiniLM's phrase endpoint relaxes only the playback-busy gate for this exact event/action/menu and qualified wall evidence; invalid gaze, target acquisition, controller acquisition and closed guidance gates still reject it. The model weights are unchanged. The loopback service was restarted on the existing v8 action/phrase checkpoints to load the updated endpoint code.

The existing 150 ms fade is used for eligible outside-wall speech. No syllable detector was added or claimed. Onset, ending, cooldown, setup/success and confirmation protections remain. Full details: `docs/guide/09-adaptive-assistance.md`.

## Verification

- Spatial voice host checks passed, including phrase-only arrival requests for both providers, cancellation/staleness, unoffered replies and once-per-round history reset/preservation.
- Voice isolation/interruption checks passed, including local arrival fade admission without a cloud interruption advisor, stale arrival rejection and protected onset/ending/cooldown/within-wall/confirmation cases.
- Centralized guidance state checks passed.
- All 39 local model tests passed, including learned phrase scoring during the override and rejection when any required evidence is missing.
- Synthetic live request: MiniLM selected “Stay in this area and keep looking.” in 171 ms; Jev selected “This is the correct area. Keep looking.” in 820 ms. Both returned only offered phrases and probabilities. These single desktop requests do not measure participant benefit or headset audio latency. Evidence: `/tmp/wall-arrival-live-check.json`.
- Final Android build passed in 41.3 seconds, including the guard suppressing arrival after target acknowledgment. Output: `Builds/CodexAndroid/codex_build.apk`. Unity log: `/Users/pedro.poveda/Library/Logs/Unity/Editor.log`. Guidance state tests also confirm target acknowledgment survives pause/resume.
- Default device build/deploy was attempted, but no authorized headset is online. Local APK retained; not installed or wearer-tested.

Manual review covered provider isolation, cancellation, fresh controller/gaze rechecks, prepared-phrase membership, actual-onset consumption, playback history, pause/new-participant resets and interruption bounds. No independent review was performed (repository maps agent work to sequential execution in the main thread). Unrelated work was preserved; no commit or push.
