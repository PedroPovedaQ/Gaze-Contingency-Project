# Automatic fit and participant numbering review

Reviewed November 1, 2026, sequentially in the main thread under the repository's agent mapping. This is not an independent peer review.

## Actionable Findings

No unresolved actionable findings after fixes and verification. Resolved during review:

- Reset the change baseline when the view API source changes; retain the source with an observed event so different APIs cannot manufacture an adjustment event.
- Accept Unity JsonUtility's inactive zero-valued nested measurement objects, while rejecting inactive objects containing nonzero measurements. Verified against actual Unity-produced JSON, not just Python fixtures.
- Preserve an existing manual field when recording only the other fallback; bind fallback and synchronization to the active participant session; reject replacement of an available automatic source.
- Keep an explicit study use of a code from being hidden by older development metadata; serialize reservation checks and ledger writes under the existing lock. Cover concurrent reservations and numeric aliases.
- Keep capture controls inert, avoid microphone/provider/live activity, and retain separate labels for view geometry, eye-origin estimate, and manual entry.

## Coverage

Reviewed runtime lifecycle and assembly boundaries, fresh-eye gating, source fallback, stable-window/change logic, missing/implausible behavior, persistence/copy isolation, live message ownership, Python/C# serialization, legacy fields and export invalidation, reservation inventory/ledger/concurrency, editable duplicate handling, Finish Here refresh, development namespace, packaging, documentation and synthetic capture isolation. Sequential correctness, contract, data-integrity, reliability, security, maintainability and test lenses were applied; no subagents dispatched.

Validation includes the Python suites, actual Unity checks, nine offline C#/native scripts, actual desktop build and two-size synthetic captures, and an Android build. Detailed results are in the adjacent receipts and work log.

## Verdict

Accept for a local commit and retained build artifacts. Installation waits because the existing desktop app is open and rehearsal inactivity has not been confirmed. No wearer measurement, physical lens-readout comparison, auto-adjustment completion detection or measurement accuracy has been validated. Hardware availability of these runtime measurements remains a deployment check; missing values warn without blocking the study.
