# Participant intake implementation and checks

Current scope: `docs/plans/2026-10-25-desktop-participant-intake.md`; implementation in `tools/trial-review/participant_intake.py`, `Runtime/TrialReviewIntake.cs`, the minimal assignment DTO, headset setup, calibration and voice admission.

Review was performed sequentially in the main thread under this checkout's AGENTS task mapping. This is not an independent subagent or external review receipt. The Compound Engineering multi-agent receipt path was not completed; no PR was published.

The inline correctness/reliability/contract/privacy and simplification passes found and addressed:
- Retrying an answer after prompt synthesis failed could advance the interview twice. Persisted audio identity now makes this retry idempotent; a regression test exercises it.
- A completed asynchronous prompt could play in a replacement session. Playback checks both session UUID and prompt revision and is blocked during microphone startup/capture.
- Unchecking agreement during recording could disable Stop. Stop remains available for an active recording.
- Mono's Documents mapping can resolve to HOME. Intake now uses the existing viewer's explicit UserProfile/Documents convention.
- Existing participant data could be adopted under a new desktop identity. Assignment rejects preexisting non-desktop participant directories and mismatched UUID/order.
- A remaining library loudness threshold could still block provider-trusted audio. Its rejection is guarded by the same disabled validation flag; quiet audio has a regression test.
- Repeated headset directory scans were unnecessary. Suggested participant code is cached until the current code is consumed.
- Reused the existing tested PCM16 WAV encoder instead of keeping a second serializer.

Verification evidence:
- `/tmp/intake-python-tests.log`: 88 passing tests, including local session persistence, prompt/answer retry, bounded follow-ups, answer-only clone snapshot, uncertain clone no-auto-retry, minimal handoff and active-trial rejection.
- `/tmp/intake-voice-tests.log`: voice isolation/preparation tests plus provider-trust default, quiet library, explicit desktop A/B overriding parity, UUID run linkage, order conflict protection and participant reset.
- `/tmp/intake-headset-tests.log`: heartbeat and remote command regressions passed.
- Unity Editor log: QuickGazeAlignmentChecks and EntryGazeAlignmentChecks passed, including downward-pitch panel-height assertion.
- `/tmp/trial-review-intake-20261025/build.log`: final desktop verification/build marker `TRIAL_REVIEW_BUILD_PASS`.
- `/Users/pedro.poveda/Library/Logs/Unity/Editor.log`: Android build succeeded in 59.0 seconds, 1163.7 MB. Existing VIVE preprocessing warnings/exceptions did not prevent the build.
- Live generic River TTS request produced 87,814 bytes; a synthetic VR-experience answer received a valid adaptive response. This verifies provider connectivity, not participant clone quality.

Device deployment: headset reappeared wirelessly at 192.168.1.208:5555. APK install returned Success, package update time is 2026-10-25 14:06:20 EDT, and launch returned Status: ok (PID 8051). Logcat shows immediate APP_CMD_PAUSE/STOP before Unity runtime initialization; the saved heartbeat remains from the previous run even after a wake request. Installation is verified; new runtime startup/control availability is not.

Remaining acceptance work: resume the app in the worn headset; rehearse desktop mic capture, real participant-authorized cloning, headset confirmation, both assigned orders and eye-height calibration. Do not interpret offline/fake-provider tests as human-subject or hardware validation.

Native UI inspection: the viewer launched and its replay screen rendered. Further automated UI interaction was interrupted by window/screenshot failures; the interview panel and microphone still need hands-on rehearsal.
