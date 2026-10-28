# Eye measurement implementation and verification — 2026-10-26

**Implemented:** native HTC stream timing provenance, requested-time head pose, raw per-eye target collider evidence, independent stream deduplication, precision definitions, censored last-target candidates, validation receipts, explicit recording closure and Trial Viewer timing availability. The full measurement contract and remaining evidence are in `docs/research/eye-measurement-readiness-20261026.md`.

## Verification

- Analysis: 343 tests passed; `/tmp/eye-study-analysis-tests-20261026.log`.
- Native interception: eight suites passed, including missing/repeated timestamps, thread association, exact nanoseconds, lifecycle and the exact VIEW/base-space/requested-time head query. Host fixtures do not establish headset runtime behavior.
- Managed gaze quality/history/freshness and headset preparation heartbeat checks passed. Stub compilation emits unused-field/event warnings.
- Unity native diagnostics: PASS for ABI, integer nanosecond JSON, asynchronous writer and Android feature enablement.
- ARM64 native plugin built successfully.
- Android local build succeeded in 81.2 seconds after refreshing the editor. The earlier attempt before refresh failed on an editor/player serialized-layout mismatch; it was superseded by the successful build. Output: `Builds/CodexAndroid/codex_build.apk`, SHA-256 `9ce0ff6113de890ef46daf942bb4e988863a478ac4bc9dbb9a8ee7fbefcb2690`.
- Trial Viewer built successfully at `/Users/pedro.poveda/Documents/Trial Review Builds/Trial Review - Eye Measurements 20261026.app`; log `/private/tmp/trial-review-eye-readiness-20261026/build.log`. Its source snapshot precedes the final descriptive `taskAoiDefinition` field addition; that field is not used by the viewer's timing display or raw-file export.
- `git diff --check` passed.
- Default Build And Run was attempted. ADB reports no connected devices; the APK is retained locally and was not installed. No wearer trial was started.

## Review coverage

Simplification and manual review were performed sequentially in the main thread, following the repository's delegation mapping. Removed a duplicate timing-analysis pass over large recordings and clarified that capture-presence checks describe one segment; validation and task recordings may be separate. Checked native/managed layouts, native lock boundaries, session cleanup, missing/failed timing, integer CSV export with mixed row shapes, matching head pose, independent pupil timing, conservative target-event censoring, recording closure and legacy contract compatibility. Added an explicit test of the requested head-query time/base space.

Code review: skipped (ce-code-review unavailable). The loaded workflow's independent review stage could not be completed under the repository instruction to execute agent work sequentially in the main thread; no independent review receipt is claimed. The available review connector only reads PR CI diagnostics. The manual diff review above is recorded separately, not represented as an independent review.

## Open evidence and decisions

The app is ready for headset validation, not yet cleared for participant eye-outcome collection. Deployment must establish actual native hook output, coordinate orientation, matching head-pose availability and added query cost. Held-out spatial accuracy/drift, event-detector agreement, pupil-method validation and prospective coverage/exclusion criteria remain required. Actual gaze sensor cadence is unverified. All six intended Raw TLX responses still need to be collected per block. Candidate graphs and interaction proxies cannot be promoted to validated outcomes by passing these software tests.

Unrelated model/audio work was preserved. No commit, push, manuscript edits or Overleaf automation changes were made.
