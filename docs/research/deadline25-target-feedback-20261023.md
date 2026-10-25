# 25-second searches and qualified target feedback

Implemented October 23, 2026. Search limit is 25 seconds for both voices, practice and quick tests. `StudyTrialClock.SearchLimitSeconds` supplies the gameplay limit and timeout/summary/replay metadata. Existing recordings retain their recorded limits.

Qualified target confirmations have a local interruption fast path: current speech must be misleading tip/repeat guidance, replacement must be an acknowledgment, elapsed speech at least 0.35 s, remaining speech at least 0.5 s, and at least 2.5 s since the last interruption. No extra Jev interruption request is required. The 150 ms fade and freshness/qualification checks before fading, after fading and at playback remain. Gaze hold remains 0.1 s, look-away rearm 0.15 s, controller hold 1 s. Setup, success, acknowledgments, reminders, and ordinary direction policies remain protected.

Validation: clock, gaze-quality/history, centralized guidance, voice-isolation and 42 trial-export tests passed. Added fast-path stale-target, protected ending/context, no-network and eligibility tests. Android build succeeded (228.2 s); ADB streamed install and launch returned Success/Status ok on 192.168.1.208:5555.

This removes interruption-advice waiting for eligible target confirmations; no headset acoustic latency or perceived pacing improvement has yet been measured. Wearer checks: let an ordinary search cue play, acquire target, confirm correct acknowledgment; leave during fade, verify no stale confirmation; let 25 active seconds expire and hear Time up; verify timeout export limit is 25. Repeat in both voice modes.
