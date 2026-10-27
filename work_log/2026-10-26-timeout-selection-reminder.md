# Timeout-only selection reminder — 2026-10-26

## Implemented

User correction: the “Almost” trigger reminder belongs only after time expires with the target held but not selected.

Cause: GuidanceStateMachine scheduled SelectionReminder after a three-second acknowledged gaze hold, and HintGenerator played it during active search. Removed that scheduling and playback path; retained the enum for recorded replay compatibility.

The game now captures fresh target gaze/controller evidence before timeout cleanup. The reminder requires target acknowledgement, a qualified controller target hold, current target gaze and pointing, and a released trigger. Missing or stale evidence uses ordinary timeout feedback. This is an observable missed-selection criterion, not an inference about participant intent.

Prepared timeout lines start “Time up. Almost!” and vary for the next trial, block break, and final trial. Both voice modes and both guidance modes use the same rule. The 25-active-second deadline and timeout outcome remain unchanged. No extra time or success credit is granted. Telemetry records the timeout criterion and evidence.

## Verification

- Clean starting checkpoint: 73e97c0; fix isolated from earlier branch work.
- Red: the new no-mid-search-reminder assertion failed against the previous implementation.
- Green: test-guidance-state.sh and test-trial-timeout.sh pass, including deadline-only eligibility and rejected missing gaze/pointing/trigger evidence.
- test-voice-isolation.sh and test-spatial-voice.sh pass.
- Unity Tools > Codex > Check Study Feedback passed, including terminal-event, prepared-voice and replay-boundary checks.
- Android build succeeded in 68.8 seconds; installed and launched with Status: ok on Wi-Fi headset 192.168.1.208:5555.
- Local MiniLM service/tunnel reports ready, model 20261026-minilm-adaptive-v2.
- Fix-scoped simplification and correctness review performed sequentially in the main context per project instructions; no independent subagent review claimed. No remaining actionable finding.

## Open verification

Wearer test: gaze at and point to the target until it is acknowledged, leave the trigger released, and let the trial expire. No early “Almost” should play; timeout should give the reminder. Looking away before expiry should give ordinary timeout feedback. Selecting before expiry should complete normally. Repeat with Jev and Local MiniLM if comparing providers.

No push, thesis synchronization, or changes to the stopped automation.
