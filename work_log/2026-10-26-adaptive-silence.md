# Adaptive assistance silence — 2026-10-26

## Problem and observed chain

The user reported too little assistance during neutral Adaptive tests, and no correction after target confirmation followed by looking at another object. Read-only device listing confirmed neutral run 34 was latest; inspected the Trial Review Live mirrors of runs 33/34. These are development data, not independent study participants.

Run 34: at elapsed 19.07 s, audio “Yep, that's it!”; at 19.77 s, state returned to Refining; 20.14 and 21.38 s requests offered `correct` with `ReturnTarget`, targetReturnPending=true, targetAcknowledged=true but current targetGazeQualified=false. Jev chose observe_1s twice. A wrong selection followed at 21.58 s. Run 33 and the second trial of run 34 also show repeated observation choices despite increasing error and idle audio. These were successful requests, not lost audio downloads.

The new adaptive path owns the search loop, so these observation choices intentionally do not fall through to baseline. v1 prompt weakly suggested intervention and emphasized silence. Whole-window progress could remain positive despite recent retreat. The persistent acknowledgment flag also needed an explicit distinction from current target acquisition. Do not claim insight into the model's hidden reasoning; the evidence is the inputs, recorded choices, and paired replays.

## Fix and verification

Updated only the adaptive prompt/criteria behavior: weigh the latest 2–4 s, prefer useful orient/refine assistance for unresolved stalls or retreat, and prioritize an offered correction after verified target departure. Current target gaze and controller acquisition remain separate from historical acknowledgment. No forced-speech timer, forced strategy override, new threshold or baseline fallback. Policy version is v2 in client and SessionConfig. Added assistance_policy to summaries, preserving the old directional guidance_policy identifier. Per-decision traces remain authoritative for sessions that switch modes.

Existing `scripts/tests/AdaptiveGuidanceChecks.cs` and its CLI runner now support `--replay-adaptive`. Frozen cases: `scripts/tests/adaptive-device-replay.json`; outcomes: `docs/testing/2026-10-26-adaptive-silence-replay.json`. Before: 6/12 pilot expectations; after: 12/12. Target-departure failure appears in recorded device evidence but did not recur in either v1 or v2 desktop replay. Healthy-progress controls remained silent. These probes are selected from failures, not held-out validation, and cannot establish participant benefit or acoustic timing.

Additional model-only stress probes flipped individual guard flags in otherwise unchanged historical snapshots; 7/8 still chose speech. These inconsistent snapshots are blocked by existing runtime pre-request/relevance/playback guards. They establish that natural-language model instructions must not replace those guards, not that speech was played on invalid evidence. Results retained in the report.

Spatial/adaptive transport checks, guidance state/lifecycle checks, voice isolation checks, and 88 Trial Viewer Python tests passed. git diff --check passed. Android build succeeded in 97.9 s (1164.4 MB); wireless install returned Success and launch Status: ok on 192.168.1.208:5555. Build trigger log: /tmp/gaze-adaptive-v2-build.log; full receipt: /Users/pedro.poveda/Library/Logs/Unity/Editor.log.

## Scope and review

Pre-fix HEAD b75b1641fe2f58c6c93723a2f8cec841ae86ef2e; dirty shared branch experiment/local-jev-20261025. Pre-fix inventory at machine-local /tmp/adaptive-silence-pre-fix-status.txt. Existing dirty files are prior authorized work; preserved in place. No stash/reset/commit/push. All changes remain local for headset testing.

Fix-owned edits: JevDecisionClient.Adaptive.cs, SessionConfig.cs, TrialDataLogger.cs (one summary field), AdaptiveGuidanceChecks.cs, SpatialVoiceChecks.cs (one CLI route), VoiceIsolationChecks.cs (policy-version expectation), replay fixture/report, guide 09, progress and this log. Simplification skipped for overlapping pre-existing edits. Code review: targeted manual due to unrelated branch work; lightweight available review tool is PR CI-only, not an explicit-file review. Checked that model replies cannot bypass existing current-evidence, capability, cancellation, voice/participant identity and audio readiness checks. No new safety enforcement depends on prompt text.

Separate observation: both runs later stopped with theta_assignment_mismatch during readiness after pause; not the cause of in-search silent decisions. That lifecycle issue was not changed by this fix. Wearer retest remains required.
