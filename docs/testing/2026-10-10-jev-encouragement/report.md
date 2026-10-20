# End-of-round encouragement: expanded checks

Date: 2026-10-10. All remote inputs in this run were synthetic. No participant recordings or identifiers were sent.

**Implemented:** expanded tests for the existing round-encouragement policy. This change adds verification rather than new speech lines or different eligibility thresholds.

## Local verification

- 1,408 combinations of measurement availability, practice status, duration, incorrect selections and previous wording. Includes negative/nonfinite durations, invalid error counts, exactly 30 seconds and immediately below it. Specific claims must have supporting facts; the previous line cannot repeat.
- All 1,024 subsets of the ten prepared clips. An unavailable clip can never become a candidate.
- Invalid answer types, absent fields, malformed JSON, additional words, changed capitalization and plausible but unoffered wording are rejected.
- HTTP 401/429/500 failures, oversized responses, missing credentials, provider deadline and caller cancellation. Provider error bodies do not leak into exception messages.
- Unity: frozen snapshots survive next-round resets; presented trial and practice identity prevent stale measurements. Guidance-repeat playback counts, congratulations and playback-end events do not. Telemetry records the facts, chosen phrase and absent-audio status. Missing client bypasses remote advice; repeated stop, restart and disable release/cancel pending feedback correctly. All lines remain in quick/full preparation libraries.

Commands: `bash scripts/test-spatial-voice.sh`, then Unity **Tools > Codex > Check Study Feedback**. Both passed.

## Live Jev scenarios

Ran 15 scenarios with three consecutive choices each:

| Scenario | Expected eligible wording |
|---|---|
| First selection; immediate selection; 29.999-second search | First-selection praise |
| Exactly 30 seconds; 180-second search without errors | First-selection or persistence wording |
| One incorrect selection; twelve incorrect selections | Recovery wording |
| Many guidance/repeat requests; no guidance | First-selection praise, without inferring reliance or effort |
| Practice with errors | Practice acknowledgment, then generic wording when repetition exclusion removes it |
| Missing measurements; null snapshot; negative duration; negative errors | Generic praise only |
| Only generic audio prepared | Generic praise only |

**Result:** 45/45 responses were eligible and nonrepeating; no deadlines. No generic choice displaced an available expected specific choice. Latency minimum 177 ms, median 210 ms, maximum 490 ms. The full case-by-case results are in [live-results.txt](live-results.txt).

Repeat with `bash scripts/test-spatial-voice.sh --live-encouragement Assets/StreamingAssets/api_keys.json`. This command makes 45 billed remote decisions and prints no credentials. Timeouts are reported separately from successful model responses.

## Interpretation limits

Passing these checks verifies factual eligibility, failure handling and observed choice behavior. It does not establish perceived naturalness or a research benefit. The 30-second threshold remains a pilot wording rule. Longer successful searches can still receive first-selection praise when that claim is true; persistence wording is not guaranteed. Test headset timing and both voice conditions with a wearer.
