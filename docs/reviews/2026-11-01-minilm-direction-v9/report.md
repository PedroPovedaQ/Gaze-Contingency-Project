# Directional MiniLM v9 review

## Actionable Findings

None in the final reviewed diff. Earlier source checks corrected impossible rear-seam training geometry, the two-second/four-second motion-window mismatch and lexical inference of direction from “Right area”; these changes and discarded training runs are documented in the experiment report.

## Coverage

Sequential ce-code-review in the main thread, as required by this checkout's AGENTS.md tool mapping. No subagents, external peer, independent validation or cross-model corroboration are claimed. Correctness, maintainability, project standards, tests, API compatibility and adversarial failure scenarios were examined against Pedro's explicit request. No separate plan was supplied; settlement suppression was not evaluated.

Runtime review covered authoritative direction mapping, wrapped yaw and elevation signs, minimum span/staleness/discontinuity handling, pre-cue trial identity and missing snapshots, both request paths and both stage copies. Adversarial cases covered a rear-seam turn, recent reversal hidden by older improvement, tracking loss, pause, absent old-recording fields, unknown direction schema, offered-menu masking and every existing gate. Policy and feature compatibility preserve v8 text; new words are allowlisted and no target identity, answer or future observation is encoded. Phrase selection and playback authority remain unchanged.

Training review covered synthetic supervision, validation-only checkpoint selection, separate source snapshots, held-out feature deduplication, preserved parent weights and documented development iterations. Evaluation review covered identical requests to both versions, frozen old/new tests, fixed challenge pairs, real-recording fingerprints, raw/masked/gated counts, per-class denominators, repeated-phrase opportunities and complete-decision timing counts. Timing uses one offered speaking action plus observation and its existing candidate menu; it is a bounded benchmark, not full headset latency.

All active Python suites ran: 632 tests, 631 passed, one optional statsmodels skip. Offline C# checks passed; the adopted policy passed all 27 Unity integration checks; the desktop build passed 25 checks. Live provider/microphone smoke scripts and archived thesis test copies were excluded. Real development recordings have no second-stage requests, so real phrase agreement is unavailable. No wearer validation was performed.

## Residual risks

Both versions achieve 3/6 on the fixed directional challenge pairs: v9 still fails the three along-cue long-way observation cases. Some individual synthetic decisions regress despite aggregate gains. Authored agreement does not establish helpful guidance; the adopted candidate passes the explicit regression/gate/latency criteria but needs pilot evaluation. Direction bands are engineering choices, not validated eye-tracking events. The new test set follows disclosed generator corrections and is development evidence, not an untouched independent evaluation.

## Verdict

Ready for the authorized local commit and conditional deployment. Actionable findings: none. Adoption is supported by the specified regression, safety and latency checks, with the above model limitations retained in the report and coordination handoff. No publication is authorized.
