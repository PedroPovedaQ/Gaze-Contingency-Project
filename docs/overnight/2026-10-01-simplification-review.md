# Simplification pass — October 1, 2026

Scope: current shared-gaze, guidance, replay/CSV, right-ray controls and neutral-catalog changes. Sequential inline passes under the project's no-subagent instruction, using the ce-simplify-code reuse, quality and efficiency rubrics. This is not the separate correctness/code-review receipt.

- **Reuse:** no behavior-equivalent helper replacement justified. Keep raw versus used observations, per-eye validity and separate fit/held-out contexts distinct; collapsing them would lose evidence.
- **Quality:** removed unused replay `m_Gaze`, `m_Eyes` and `m_NextDeviceCheck` state and the corresponding now-unused interactor import. Project-wide symbol search confirms these private fields have no consumer after migration to the shared stream. One applied finding.
- **Efficiency:** the same removal eliminates a redundant scene search during replay resolution. Counted under the quality finding above, not twice. Deferred caching the target lookup and reusing observation objects: ownership/lifecycle equivalence is not established, and mutable reused snapshots could corrupt queued measurement evidence.
- **Preserved checks:** source freshness, independent native validity, nonfinite filtering, overflow errors, invalid-row retention, append/header protection, event context boundaries and controller trigger gating remain intact.

Applied counts: reuse 0, quality 1, efficiency 0 additional. Two speculative optimization opportunities skipped. No safety checks removed and no serialized/public contract changed by this simplification.

Validation: the full Unity integration suite passed after cleanup in `/tmp/gaze-unity-review-cleanup.log`; the preceding pause-durability suite passed. The latest Python suite has 66 passing tests. No separate repository-wide C# linter is configured; Unity compilation is the type check, and `git diff --check` covers whitespace.

Separate implementation correction: setup's final status text now describes shared guidance wording rather than the retired self-first-person policy. This copy fix is not counted as behavior-preserving simplification.
