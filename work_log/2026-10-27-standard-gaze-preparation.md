# Simplified standard preparation follow-up — 2026-10-27

This supersedes the standard shape-confirmation sequence described in the earlier entry below. Standard setup now fits three dots, transitions automatically (three seconds) into five 2.5-second verification dots, then presents results and activation practice. Skip remains available and clears the offset. The fit is provisional until verification passes; no manual shape confirmation is invented in telemetry. Standalone optional validation retains its prior behavior.

Paired unaligned/corrected per-dot median/P90 and improvement gates now replace the removed shape-stage benefit checks. Mean error still uses five equally weighted target means after 500 ms settling and query/delivery deduplication. See `docs/research/standard-gaze-accuracy-method.md` for the full algorithm and thesis wording limits.

Unity preparation suite (including provisional-to-accepted, skip cancellation, hidden shape OK, before/after benefit checks) passed; validation and activation shell checks passed. Android build succeeded (177.7 s), installed over 192.168.1.208:5555 with Success and launch Status: ok; package lastUpdateTime 2026-10-27 20:58:48. MiniLM reverse restored and headset-origin health returned HTTP 200 ready for action/phrase stages. App process present. No new wearer test or numerical result is claimed. Claude acknowledged the explanation and six geometry/ray/gate questions were answered on the board; the method note now includes those details. No manuscript edits or commits.

Build diagnostics: the VIVE package manifest callback logged a nonfatal NullReferenceException at AndroidManifestProcess.cs:294; Unity nevertheless reported Build Result Success and installation/launch succeeded. Do not describe this as a warning-free build. Log: /Users/pedro.poveda/Library/Logs/Unity/Editor.log. Wearer verification of the new combined flow remains outstanding.

---

# Standard participant gaze preparation — 2026-10-27

## Implemented

- Every rotational launch waits for participant and voice setup, then runs fresh three-dot calibration and its three held-out shape checks, followed by the independent five-dot accuracy protocol, activation practice, and Center and begin.
- Both assigned participant IDs and existing DEV quick-test IDs use this preparation flow; remote validation retains its P### participant requirement.
- Study setup uses a private-in-process validation entry path; remote validation commands cannot request retained study calibration. Standalone Trial Viewer checks still clean up their validation-only correction.
- Five-dot results use 500 ms settling, source-query/delivery deduplication, full 3D angular error, equal-target mean and the existing pilot median 2° / P90 3° gates at every dot with at least 30 observations. No offset is fitted from scored check samples.
- Continue requires success and the still-active alignment. Retry repeats preparation. Explicit Skip remains, clears the offset and records skipped. Participant ID and alignment ID bind readiness; an invalidated correction cannot launch a trial.
- `study_preparation_<id>.json` records each attempt and references immutable validation artifacts plus IPD metadata. Raw/scored task boundaries remain distinct.

## Verification

- Initial new accuracy tests failed because StudyGazeAccuracy did not exist; implemented then passed.
- Full project Unity batch `StudyPreparationChecks.Run` passed, including calibration UI/guards, protocol isolation, synthetic angle/dedup/settling and readiness tests.
- Replayed the previously completed P033 five-dot recording through the runtime calculator: 1.256514° (independent Python result 1.256516°), with matching target counts. This is reuse of a recorded pilot, not a new participant test.
- `scripts/test-eye-validation.sh`: schedule checks, 5 bridge tests, 7 offline-analysis tests passed.
- `scripts/test-activation-exercise.sh` passed. `git diff --check` passed.
- First Android attempt failed due stale editor/player serialized layout. Restarted Unity, reloaded and reran preparation checks successfully. Final Android build succeeded in 52.5 s; installed and launched over Wi-Fi with install Success and launch Status: ok. Package lastUpdateTime: 2026-10-27 10:50:36.
- Review: `/tmp/study-preparation-review.md`; sequential in-thread checks per project instruction, no independent review claimed. Unrelated dirty changes preserved; no commit/push.

## Remaining device check

Run a fresh neutral or clone setup through calibration → five dots → results → activation → centering. Exercise Skip and Retry, inspect participant-scoped receipts and verify focus-loss behavior. This new combined flow has not yet been completed by a wearer.
