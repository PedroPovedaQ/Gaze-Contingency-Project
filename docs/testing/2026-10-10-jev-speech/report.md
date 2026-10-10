# Jev structured speech scenario evidence — 2026-10-10

**Implemented, v21:** semantic metadata for 300 navigation catalog entries and 215
stateful guidance texts; structured interruption state; local unknown/finish-only and
same-wall-arrival guards; final one-second speech protection; contextual “A bit more”
and “Just a little more” with progress/proximity/non-repetition gates.

## Live desktop checks

| Check | Result |
|---|---|
| Main interruption cases | 31 scenarios × 3 = 93 requests; 7 deadline fallbacks |
| Additional interruption cases | 12 scenarios × 3 = 36 requests; 5 deadline fallbacks |
| Completed interruption responses | 117; zero effective behavior mismatches after local gates |
| Expected cuts missed due to timeout | 8; phrase finished as designed |
| Completed-response timing | Median 203ms; observed p95 333ms; range 162–388ms |
| Navigation selection | 16 requests, 8 signed bearings; all exact eligible candidates, no immediate repeats or errors |
| Audio transport | 9 synthetic Eric utterances generated through production ElevenLabs PCM adapter |

Saved result rows are in `main-results.json`, `additional-results.json`, and
`navigation-results.json`. Inputs are in `scripts/tests/jev-interruption-scenarios.json`
and `scripts/tests/jev-interruption-holdouts.json`. The latter was used during tuning,
so these are regression scenarios, not an untouched statistical evaluation set.

The test deliberately sends even locally forbidden cases to Jev and then applies local
gates. Some raw approvals were wrong (protected endings/onsets, confirmations, unknown
wording, redundant same-wall arrival). Unity contains those responses. Zero effective
mismatches is **not** a claim that Jev's raw choices were all correct. Timeouts are counted
separately from correct completed responses and default to finish. Bounds do not identify
phonemes or guarantee that a longer eligible utterance will be cut at a word boundary.

## Regression scope

Production fake-audio checks cover a 150ms fade (half gain at 75ms, no Stop at 149ms,
zero gain at first tick after 150ms), obsolete/late approvals, once-per-utterance requests,
clone speech-tail exclusion, lifecycle cancellation, finish-only and unknown semantics,
ordinary-progress veto, same-wall arrival, and final-second protection. Gaze tests cover
near-target progress, stalling, wrong direction, large vertical error and continuation
repetition. Network tests cover malformed/out-of-set choices, cancellation, bounded
responses, fragmented WebSocket/PCM samples, underrun and overflow.

Unity integration checks cover the real serialized catalog/JSON semantics, all 2,888
integer-angle/distance combinations, relative geometry, prepared-audio filtering,
readiness/selection, guidance boundaries, voice-choice isolation and replay/audio.
Rotational layout and enrollment regressions also pass.

## Device build

Android Build And Run succeeded in 94.9 seconds. Package update confirmed on Vive
FA5AR3N00385 at 2026-10-10 18:52:31; process 5896 running. No matching fatal, null-reference,
missing-method, type-initialization or invalid-navigation-semantics exceptions appeared
in the inspected current-process startup log. This verifies installation/startup only.

## Review and remaining validation

Simplification/manual review checked repeated metadata construction, per-frame allocation,
serialization boundaries, stale async results, cache identity and generated assets.
The snapshot buffer is reused; serialization occurs only when advice is requested.
Incorrect horizontal magnitude metadata from repeated vertical-band registration was
corrected. Unknown semantics now fail closed locally instead of relying on the model.

Code review: skipped (ce-code-review unavailable) — the loaded review workflow requires
independent reviewer/finish-agent dispatch; this session's AGENTS instruction requires
all such work inline. No independent review receipt is claimed. A manual scoped diff
review was performed instead; Unity's transient unrelated preload changes were not intentionally edited.

**Open validation:** wearer listening, neutral/cloned prosody, real headset gaze transitions,
network variation and perceived naturalness. Audio previews are synthetic timing examples,
not recorded headset sessions or evidence of human listening validation.
