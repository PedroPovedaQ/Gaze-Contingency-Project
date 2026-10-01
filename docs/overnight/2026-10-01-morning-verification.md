# Overnight work and morning verification — October 1, 2026

Latest verified state: exact raw/replay observation links passed full Unity integration and Android packaging. The browser timeline now imports raw replay files and preserves source samples and gaps; verification details are recorded below. Work remains local with no device install or hardware acceptance. See the latest checkpoint for the current artifact fingerprint.

Status: work in progress. This report is updated during the overnight run ending at 06:00 America/New_York. It is not study approval or hardware acceptance.

## Implemented and checked locally

- Restored the three authorized dependencies: `GazeDataLogger`, `GazeHighlightManager`, `ShapeObjectFactory`.
- Replaced the restored logger's independent device/hover/blink pipeline with the same valid/fresh gaze stream used by guidance. Compatibility CSV retains invalid rows and blanks unmeasured poses/events; continuous raw eye JSONL remains separate. Re-enabling logging appends instead of destroying prior rows, and incompatible pre-existing headers stop logging visibly rather than overwrite data.
- Kept unavailable blink counts unavailable in downstream analysis even when eye openness exists. Updated voice-policy metadata to the shared imperative wording now used in both conditions.
- Invalid gaze cannot inject non-finite poses into reconstruction records. Native eye/pupil/openness validity now also checks numeric validity. Non-finite values use unavailable placeholders with false validity; they are not measured zeros.
- Corrected the Unity compile helper's stale-lock check. A lockfile with no live holder no longer substitutes an old editor log for a fresh compile.
- Fresh Unity batch compilation and `GazeQualityChecks.Run` passed. The full `StudyVerificationChecks.Run` suite passed its gaze/CSV, physics filtering, controller edges, readiness, guidance boundaries, voice selection and replay checks. Hardware accuracy/rate are explicitly excluded from this evidence.
- Python analysis suite: 66 tests passed. Focused C# gaze and voice-isolation checks passed.

- Added right-only study ray policy and a right-ray control panel for Settings, Pause and Repeat guidance. Settings reuse the original menu, dock in front of the participant and pause active search/readiness; closing settings restores the Continue checkpoint. Left-controller, hand-poke and gaze UI activation are disabled during the study. Hardware layout/comfort remain unverified.
- Repeat requests and actual delivered repeats are distinct, versioned events. Per-trial exports preserve unknown versus zero and avoid double-counting CSV and replay sources. `instruction_count` includes delivered repeats; `hint_count` excludes them. Final success feedback now remains in the trial event recording before it closes.
- Eye report recovery now preserves an interrupted final JSON prefix, rejects corrupt middle records/mixed identities/unordered sequences and checks clean-close row counts. Invalid numeric placeholders export as blank measurements.

- Implemented the selected 40+40 schedule (80 measured trials), eight per absolute-angle category per block, eight stratified five-trial groups and mirrored angle pairs. Sixteen target identities repeat twice and eight once per block, identically counted across voices; no adjacent target repeats. Versioned manifest/summary/replay/export explicitly identify signed-height balancing as pending, not implemented. Across 50 participant IDs, tests cover reproducibility, angle/identity quotas, target slots, both voice orders and debug truncation.
- Strengthened the study-package validator: it checks all 80 planned rows, geometry, pair quotas, reference chaining, target repetition, voice assignment, unique layout seeds, enabled-trial flags and summary/manifest agreement. Shared-command recordings must actually have equal prepared phrase sets across voices.
- Corrected the replay integration fixture to exercise the real 40-trial block boundary. Fresh full Unity suite passed in `/tmp/gaze-unity-40-trials-2.log`. The further right-ray startup/modality regression check and full suite passed in `/tmp/gaze-unity-40-trials-final.log`.
- Updated 14 existing GitHub tasks with restored-dependency and current implementation evidence; moved 12 applicable cards to In Progress. Removed obsolete deferred labels from active horizontal/vertical guidance #11 and applied app/P0. No task was closed or marked headset-verified.

- Expanded to six voices per gender by appending Liam and Laura, retaining all previous indices/IDs. All twelve cards have fictional portraits; eight were generated with the built-in image tool, with prompts/provenance in `docs/voice-avatar-prompts-2026-10-01.json`. Catalog inventory snapshots persist per participant/run and do not imply audition/rating. Unity integration and graphical six-card checks passed for both presentations; inspected `/tmp/gaze-ray-menu-male-six.png` and `/tmp/gaze-ray-menu-female-six.png`. Fixture audio is deliberately unavailable, so acoustic checks remain pending.
- Added an offline **Build Android APK** command so packaging can be tested without a connected headset. Build failures now throw instead of returning success to batch automation. First Android packaging succeeded in 676.4 seconds (`/tmp/gaze-android-overnight.log`), with a reported 1145.9 MB build. Its old Temp output was removed when Unity exited; the helper now targets ignored `Builds/CodexAndroid/` so the next package persists. No device was installed or launched.

## Review continuation — 00:57 EDT

- Completed the ten selected review perspectives sequentially under the user instruction; this was a self-review, not independent/external review. Receipt: `/tmp/compound-engineering-501/ce-code-review/20261001-044134-0fa53ef9/review.json`. Two explicit-plan gaps were retained: per-trial/spatial quality summaries and guidance-decision/sample provenance. No broad completion/merge claim.
- Implemented the first gap: phase-aware `trial_eye_quality.csv`; per-target mean-direction offset, yaw/pitch bias, segment-specific angular SD and observation/time missingness. Invalid heartbeats now break spatial precision. The whole Python suite passed **72 tests**, including six new regression cases. Method definitions and limitations are in `analysis/README.md`.
- Added `guidance-decision-v1`: original observation/request snapshots, correction revalidation/abandonment, invalid suspension and audio lifecycle IDs. Voice fixtures passed and full Unity integration passed (`/tmp/gaze-unity-guidance-provenance-final.log`). Header/summary advertise the audit version. A fresh Android package is building (`/tmp/gaze-android-provenance.log`); the prior APK remains stale until this completes.

## Needs Pedro / Vive verification

1. Confirm the new full schedule displays 80 measured trials and switches voice after trial 40. Two practice trials remain separate. Start with a debug-short run for device checks; a short run does not establish full-session completion.
2. Connect the Focus Vision, fit it correctly and run the headset's eye calibration. Use **Eye check** during setup or explicit pause; hold each independent dot for at least two seconds before **Next target**. Check low/mid/high positions, wall seams, distances and head orientations. Repeat drift targets after re-donning/between blocks.
3. Record separate **Offset fit** and **Held-out** checks. Export with `analysis/eye_quality.py`; reject apparent corrections that worsen held-out regions. No correction is automatically applied.
4. Verify actual input delivery intervals, validity/gaps and sensor-timing capability. HTC's advertised 120 Hz and a 120 Hz timer do not prove distinct hardware observations. Native 120 Hz remains unestablished.
5. In trials, look up/down within the correct wall, blink, cross a boundary, overshoot, lose/recover tracking and pause/resume. Confirm only relevant cues play, invalid gaze suspends new directions, and vertical scanning does not retrigger area arrival.
6. Listen in both voice conditions: identical command wording, short varied cues, success chime and varied reinforcement, no hidden target identity leak. Judge pacing, voice identity, comfortable levels and whether success completes before transition.
7. With the left controller off, complete setup, open/close Settings, switch the guidance boundary, Pause/Continue and Repeat guidance. Opening settings during search must pause/hide the trial and closing must require Continue. Verify physical right-controller ray highlight, one entry vibration, trigger release/re-press, wrong-choice continuation and correct selection across all eight walls. Device haptics, comfort and full session flow cannot be established by editor fixtures.

## Open research decisions / evidence

- Independently annotated fixation, saccade and blink validation is still required. Candidate analysis is explicitly unvalidated; no pupil-based workload score is inferred.
- Signed target-height quotas need a confirmed reference, zero/deadband and magnitude range before implementing 20 above/20 below. Current seeded floor-relative scatter is explicitly unbalanced.
- Power/sensitivity and participant burden for 40+40 trials remain to justify; repeated trials are not independent participants.
- Final candidate-rating endpoints/order/ties, questionnaire wording/scoring and institutional consent/cloud lifecycle review require researcher decisions. No live survey publication or IRB submission is performed overnight.
- Pre-existing unrelated deletions remain outside the repair scope. Unity recreated existing script metadata with the original GUIDs during import; inspect those import-only changes before committing.

## Evidence and resume pointers

- Worktree: `/tmp/gaze-v2-project-update-20260922`.
- Branch: `codex/reconcile-meeting-20260922`.
- Fresh compile/gaze log: `/tmp/gaze-unity-overnight.log`.
- Integration logs: `/tmp/gaze-unity-integration-2.log` and `/tmp/gaze-unity-right-controls-2.log` (includes right-only policy fixture).
- First integration attempt exposed an edit-mode fixture lifecycle omission; the test now explicitly initializes the shared stream, and the second run passed.
- Run `StudyVerificationChecks.Run` in Unity batch mode or **Tools → Codex → Check Study Integration** for offline checks.
- Detailed measurement guide: `docs/gaze-measurement-validation.md`.

- Fixed annotation matching for empty Unity/CSV context IDs and prevented candidate events/precision pairs from crossing device changes. Two new regressions passed; detector v2 remains unvalidated.

## Current continuation — 00:38 EDT

- Latest full Unity integration passed: `/tmp/gaze-unity-pause-durability.log`. Includes twelve-candidate catalog JSON, stable practice/39→40/79→80 trial linkage, frozen voice metadata, and compatibility-CSV snapshot metadata. No historical rows relabeled.
- Presented-trial linkage is now repaired: raw eye/replay/CSV preserve the completed trial through success feedback, and replay voice assignment no longer depends on event-listener order.
- Added a collision retry to guarantee distinct layout seeds; the existing schedule suite passed again across 50 participant IDs. Updated the authoritative gaze/telemetry guide to match shared-stream acquisition and current guidance.
- Android packaging completed successfully in 280.4 s (`/tmp/gaze-android-overnight-final.log`). Persistent APK exists at `Builds/CodexAndroid/codex_build.apk`: 71,123,184 bytes, SHA-256 `7c79c215d647ed295a2a6d0de72402f2c76f1b9ae1e6be8583caefa80dbd571a`. It predates the following settings/pause-flush repairs; rebuild after final review. No device install/launch.
- Before committing, review intended paths only; pre-existing unrelated deletions remain unapproved. Before final handoff, run the requested refocus/build-and-run helper against the correct Unity project and report actual device outcome. No headset currently attached.

### Settings and pause durability repairs — verified locally

Settings now enforces pause if readiness/search begins while the menu was already open. New lifecycle checks cover setup → active search and setup → readiness, hidden stimuli/Continue, closing without automatic resume and explicit Continue returning to the correct stage.

Eye/replay application-pause handlers now enqueue an ordered background flush after the final observation/pause marker. A real-file test fault-injects stalled storage, verifies the Unity caller stays responsive, releases the worker and verifies the last row is readable before disposal or the 90-write interval. The writer remains usable on resume and rejects flush requests after closure. Enqueue races with worker shutdown return a recording failure rather than leaking an exception. This is best effort, not fsync or proof against immediate OS termination; hardware interruption checks remain required.

Full Unity integration passed with these additions (`/tmp/gaze-unity-pause-durability.log`). The replay fixture's deliberately closed-writer error is expected failure-path coverage, not a failed real recording.

### Earlier review checkpoint — superseded by completed review above

Simplification completed with one dead-state removal (unused replay eye source/device fields and scene search); current full Unity suite passed again in `/tmp/gaze-unity-review-cleanup.log`. Corrected the setup completion copy to shared guidance wording. Formal `ce-code-review mode:agent base:HEAD` has completed scope/intent and selected full depth. Run directory: `/tmp/compound-engineering-501/ce-code-review/20261001-044134-0fa53ef9`. Read `context.json`, `intent.md`, `requirements.md`, `scope.json` and `criteria-map.json`; persona selection is saved in `roster.json`; next read the remainder of `dispatch-reviewers.md`, `action-class-rubric.md`, `subagent-template.md`, `diff-scope.md`, `findings-schema.json` and selected persona assets, then execute sequential inline passes. Cross-model/subagent delegation is not started under user AGENTS; no independent-review credit is claimed. Execute reviews sequentially under user AGENTS. Forty new source/test/avatar files are staged solely so the review includes them; no commit or push. Remaining unrelated deletions are not candidate changes. No live process remains.

## Packaging and provenance audit — 01:18 EDT

- Fresh Android packaging succeeded in **305.2 seconds** (`/tmp/gaze-android-provenance.log`, process exit 0). Current persistent APK: `Builds/CodexAndroid/codex_build.apk`, **71,124,672 bytes**, SHA-256 `3606afefa715bc4b5fa626c06dc947b6b1debfca8eaeea4978db9d4a7d9c3369`. This package includes the guidance audit version in replay headers and trial summaries. No headset installed/launched. It supersedes the older APK evidence above.
- Added the offline guidance audit command documented in `analysis/README.md`. It preserves exact original eye observation joins, flags mismatched trial/phrase/staleness claims, and distinguishes requested, abandoned, cancelled and software playback outcomes. Incomplete/legacy recordings do not establish hardware acceptance.
- Morning data check: after a short recording, pull the raw eye file and matching replay JSONL, run both `eye_quality.py` and `guidance_audit.py`, inspect completeness and all reported inconsistencies, then compare cue behavior with the observed headset session. This is required before claiming actual joins work on-device.

- Final analysis run for this slice: **82 tests passed**, `/tmp/gaze-python-guidance-audit-final.log`; `git diff --check` passed. Next unblocked measurement-plan work: capability CSV and delivery-interval plots with explicit source-clock limitations. No live build/test process remains. No commit/push yet; unrelated deletions remain excluded.

## Timing-report and panel-clearance continuation

The eye-quality CLI now includes phase delivery/capability/clock tables plus timing PNG/SVG. All 91 Python tests passed (`/tmp/gaze-python-timing.log`). The inspected fixture is at the directory recorded in `/tmp/gaze-timing-fixture-path`; it is explicitly synthetic. Do not use it as device-rate evidence.

The diagnostic panel previously covered the right-side local fit targets. It now moves lower while a dot is active. A new geometry check and full Unity integration are running (`/tmp/gaze-unity-diagnostic-clearance.log`); rebuild the APK afterward. The last package fingerprint remains historical until then.

### Verified checkpoint — 01:30 EDT

Analysis tools/tests/README are committed locally as `64d8325` (`feat(analysis): audit gaze quality and guidance provenance`); seven named paths only, no push. Other runtime/document changes are still uncommitted. Diagnostic-panel geometry and full Unity integration passed, process exit 0 (`/tmp/gaze-unity-diagnostic-clearance.log`). Android rebuild is running in `/tmp/gaze-android-diagnostic-clearance.log`; collect that process before starting another Unity operation.

## Latest completed checkpoint — 01:38 EDT

- Local commits: `64d8325` (gaze quality/timing/guidance audit, seven files) and `932f516` (schedule-aware voice comparisons and unavailable-blink preservation, five files). No push. Runtime and protocol/doc changes remain pending a scoped commit; unrelated deletions remain excluded.
- Fixed the old descriptive analysis gate that demanded exactly 14 trials. Current 80-trial sessions pair by participant/run and declared 40-trial blocks; historical 14-trial sessions remain supported. Debug/shortened sessions, restarts, duplicate/missing IDs or indices, wrong/missing voice assignments and invalid/missing search durations do not become complete pairs. This does not resolve the power-analysis decision.
- All **95 analysis tests passed**, including actual voice-pair CSV readback, `/tmp/gaze-python-eighty-analysis-final.log`. Full Unity integration passed with the panel-clearance geometry check in `/tmp/gaze-unity-diagnostic-clearance.log`.
- Android packaging **succeeded in 216.3 s**, process exit 0, `/tmp/gaze-android-diagnostic-clearance.log`. Current APK: `Builds/CodexAndroid/codex_build.apk`, **71,122,616 bytes**, SHA-256 **`77f8d2b433e5db825ec8c8e55aebee2db693b10e075aac734a83faa57dfa4b22`**. This supersedes all older package fingerprints above; no installation/launch.
- No live build/test process remains. Remaining next steps include scoped runtime/document commits, any other unblocked task gaps, and the required correct-project Unity refocus/Build And Run before the final handoff. Headset tasks and research decisions above remain open.

- GitHub #34 moved to **Ready for headset QA** and read back in Project 2; only the completed software checklist items were checked. The issue remains open/unverified. Test the complete right-controller-only flow with the left controller off; exact steps are in the updated issue.

## Operator reconciliation and local checkpoint

The existing open run sheet now reflects 40+40 measured trials, two separate practice trials, hidden-target guidance, six choices per gender, shared imperative wording, right-controller controls and current previous-wall readiness. Built-in LaTeX compilation passed. Independent gaze validation is a pilot procedure; signed-height balancing and formal research decisions remain explicitly open. The procedure register now identifies old first-person, 14-trial, fixed-front and offline-provider designs as historical. GitHub #27 was updated and read back; live Overleaf/Qualtrics and submission artifacts are not synchronized by this local edit.

Runtime checkpoint `77f85f6` contains 81 explicitly selected files and passed a fresh Unity compile hook. Unrelated deletions and generated ProjectSettings changes were excluded. No remote branch was pushed.

## Graphical Unity / deployment attempt

Opened `/tmp/gaze-v2-project-update-20260922` in Unity (editor log `/tmp/gaze-unity-morning-editor.log`). `refocus-unity-and-build-device.sh` ran and exited 1: System Events could not obtain Unity's menu bar. Computer-use inspection reported the Mac locked; do not infer a missing Accessibility grant from the helper's generic message. ADB returned no connected devices. Unlock/connect in the morning and run again. The existing offline APK packaging success remains separate; no installation or launch is claimed. Graphical Unity remains running for morning use.

Updated `docs/voice-study-qa.md` to remove stale 14-trial, first-person, shortcut, countdown and fully-offline-from-starter-readiness claims. Next offline work: strengthen `verify-study-session.py` completion and event-order validation; inspection found that a `completed` session can currently skip records marked incomplete and NaN timing can evade a numeric-difference check. These are identified next actions, not yet repaired.

## Session integrity verifier — 103 tests passing

Reproduced and repaired false passes for completed sessions containing unfinished/duplicate trials, contradictory outcomes, NaN/missing/negative timing, malformed metadata and impossible pause/resume sequences. Readiness pauses no longer enter search duration. Interrupted records still undergo timing and voice checks. Full 80-trial/168-object fixtures pass in both voice orders; legacy behavior is retained. CLI success/failure paths are tested. Log: `/tmp/gaze-python-session-integrity-final.log`. This is offline recording consistency, not full device acceptance.

Follow-up discovered during review: `TrialDataLogger.OnSessionStopped` overwrites the last trial's outcome even when correct capture has already completed it. A stop during success speech should preserve the completed trial and mark the session stopped separately. Reproduce/fix this runtime case next; do not treat the new validator's rejection as evidence that the record should be silently repaired.

## Completed-trial preservation repair

The actual Unity stop handler was reproduced failing the new saved-summary regression (`/tmp/gaze-unity-stop-repro.log`). It now preserves a completed trial's outcome and captured search duration if success speech or transition fails; the session retains its separate technical-failure outcome and stop event. Tests cover the first trial, block boundary and final trial (rounds 0, 39, 79), with unfinished controls. Full Unity integration passed (`/tmp/gaze-unity-stop-integrity.log`). A fresh APK is building in `/tmp/gaze-android-stop-integrity.log`; do not call the older fingerprint current until this terminal result is collected.

Validator/QA checkpoint: `d9a8aa1`, 103 Python tests. The correct graphical Unity instance was closed to run these batch checks; reopen it after packaging for morning testing. No headset deployment is claimed.

## Latest packaged artifact — 02:08 EDT

Android packaging succeeded in **269.7 s**, process exit 0. Current APK contains the completed-trial preservation repair: `Builds/CodexAndroid/codex_build.apk`, **71,124,916 bytes**, SHA-256 `676ef4157dc7884813f0cddd32dc761147cb35173c20be7712fd4c229dd7795e`. Log: `/tmp/gaze-android-stop-integrity.log`. This supersedes earlier fingerprints/building notes. No headset install or physical acceptance.

The system overview and study hub now point to the current hidden-target/right-controller flow, shared gaze stream and reconciled run sheet. Current overview/QA relative links passed.

## Exact recording linkage — October 1 checkpoint

New replay samples and selections retain `gazeRecordingId` with `gazeObservationSequence`, including invalid samples and raw-log reopenings. Production headers advertise `gaze-observation-link-v1`. `analysis/recording_links.py` audits explicitly supplied summary, object manifest, raw eye files and replays; checks participant/run/block/voice/trial joins, target/selection identity, source timestamps and valid world/local ray conversions; and saves input hashes. Legacy missing IDs and incomplete recordings are reported separately rather than guessed. This complements session and guidance checks; it does not verify media assets, surveys or hardware measurements.

Full Unity integration passed (`/tmp/gaze-unity-recording-links-final.log`), including a real recorder-header creation test and reopened-stream identity/serialization tests. All **115 Python tests** passed (`/tmp/gaze-python-recording-links-final.log`), including 12 recording-link tests with malformed input, incomplete evidence, coordinate changes, false tracking claims and CLI exit-code/readback coverage.

Fresh APK build succeeded in **84.3 s**, process exit 0: `Builds/CodexAndroid/codex_build.apk`, **71,125,376 bytes**, SHA-256 `6cd898b9885fe6c3fcc2e9654ee4d7ee39afdde5280d3d4c067a1e82c3a0de4b`. Log: `/tmp/gaze-android-recording-links.log`. This supersedes earlier APK fingerprints. Graphical Unity is currently closed for batch work; reopen the correct project near the morning handoff. The prior locked-Mac Build And Run failure and absent ADB device remain unresolved; no installation/launch is claimed.

Morning: after a consented headset run, pull the entire run, all linked raw recordings, and replay package. Run the session, recording-links, eye-quality and guidance checks documented in `docs/voice-study-qa.md`; inspect each report's incomplete evidence as well as errors. Native 120 Hz, target accuracy, physical haptics, audio/voice quality and device interruption/overhead remain empirical checks.

## Initial power/sensitivity work — #16

Saved `docs/voice-power-sensitivity.md`, `analysis/voice_sensitivity.py`, explicit configuration and `analysis/power/results-20261001/` artifacts. These simulate hypothetical repeated-trial log-time data and analyze participant contrasts with order adjustment, not a frozen mixed-effects/censored model. 120 cells × 2,000 simulations compare N=15/20/30/48, 10 versus 40 trials per voice, null/5%/10% effects, trial correlation, voice heterogeneity and random/informative loss. Inputs are synthetic; no P020 or participant data is fitted. Recruitment assurance is distinct from an expected retained count.

All **123 Python tests pass**, including eight new statistical/tooling checks (`/tmp/gaze-python-with-power.log`). Saved script/config hashes match; plot inspected. The conventional paired benchmark reproduces approximately d_z=.457 at N=48, alpha .025 and 80% power, but this does not validate the older four-condition design for the current study. Illustrative 40-trial/N20 no-loss scenarios range from roughly 16% to 41% detection for a 10% ratio change; informative missingness can inflate false positives rather than improve inference.

**Needs researcher review:** select primary estimand, meaningful effect, alpha/multiplicity, aggregate versus mixed/censored model, missingness/timeout rules, analyzable N and recruitment allowance. Run the current hidden-target task pilot to constrain noise, correlation, person-specific voice effects and burden. No final N, exclusion rule, recruitment change, IRB submission or study-power claim was made. The current complete-run descriptive exporter is unchanged. Android runtime/source is unchanged since the latest successful recording-link APK.

## Replay audio package completeness

`recording-links-v2` now produces `assets.csv` and checks the saved replay PCM16 audio itself: referenced-file presence, byte/frame counts, channel/rate/alignment fields, declared versus actual lengths, bounded file size and per-event volume range. Reused clips are inspected once with every referring event sequence retained. Playback without a saved clip and recorder-reported asset failures are incomplete evidence even if the replay footer says complete. Invalid WAVs, absolute/traversal paths and linked files/directories are issues; paths are rejected before reading outside the package. Existing assets are hashed before inspection and rechecked after the full audit.

All **130 Python tests pass** (`/tmp/gaze-python-audio-assets-final.log`), including seven new asset/integration cases, unsafe paths/symlinks, malformed/short headers, length/rate contradictions, duplicate clip references, missing clips and CLI output with mixed incomplete evidence. Runtime/APK sources are unchanged. No real participant audio was uploaded, listened to, regenerated or relabeled. File format consistency cannot establish the correct voice, naturalness, acoustic timing or audible delivery; these remain morning checks. Video and finalized survey completeness remain separate.

## Browser timeline continuation — 03:15 EDT

All 139 Python tests passed (`/tmp/gaze-python-timeline-final.log`); both Chromium replay and video suites passed. This supersedes the earlier analysis-test counts, not the unchanged APK fingerprint.

Implemented direct raw replay import (`analysis/replay_timeline.py`) into the existing standalone page. Retains all saved frame samples/pose-bearing selections, exact source line/sequence and raw gaze identity, recorded transforms and visibility, pause-separated search segments, explicit age/unknown states and separate final performance/survey context. Optional trial CSV joins require the manifest's exact replay hash and recording/trial identity. Added previous/next sample, event selection, object inspection, original-record details and cursor snapshot download. Equal-timestamp sample stepping respects source sequence when applying object/state changes.

Verification: Python tests cover direct conversion, tracking loss, truncated tails, mixed context/nonfinite input rejection, explicit summary linkage, 80 synthetic trial contexts and the voice boundary, overlapping one-shot/speech events and CLI overwrite protection. Real Chromium checks cover raw import, fixed cursor/source samples, object movement/inspection, pause gaps, saved snapshots, event jumps, backward reconstruction and equal timestamps; existing video attachment/alignment/scrub/speed/coverage/error/mobile checks still pass. Screenshot inspected at `/tmp/gaze-timeline-generic-20261001/raw-replay-mobile.png`.

Morning verification: import a newly copied headset recording and its trial export, compare chosen exact samples and object positions against Unity, and attach the real continuous video with early/late sync checks. The 80-trial fixture is short synthetic context verification, not a full-duration performance benchmark. Full-rate pages may be large; measure representative memory/load/scrub performance before relying on a complete long run. Browser remains overhead/text; native Unity replay owns full 3D/WAV playback. No sensor-rate, spatial-accuracy or research-validation claim follows from a working timeline. Runtime code did not change; the latest APK remains current.

## Full-duration browser capacity — 03:35 EDT

All 142 Python tests passed (`/tmp/gaze-python-timeline-capacity-final.log`), plus both Chromium regression suites after the separate-trial loader and source labels changed.

Addressed the earlier synthetic capacity gap with 80-trial fixtures of 20, 40 and 60 seconds per trial at a synthetic 90 samples/second and 168 objects per trial. The old 570 MB single-payload page failed JSON parsing and never populated its inspector. New pages store independent trial payloads and parse only the selected trial; no observations are discarded. Page publication is atomic/no-overwrite, and the converter releases its full data graph before the packaging parse.

The final 432,000-sample / 758 MB page passed source-identity checks across every trial switch and scrub. On this M4/32 GiB Mac with Chromium 151, it loaded in 2.68 s (earlier run 3.19 s); p95 synchronous trial switch 34 ms and scrub 2.4 ms. These exclude later paint; the 3-second animation probe's p95 interval was about 18.5 ms. Sampled JS heap excludes DOM and total process memory. `docs/timeline-video.md` and `analysis/timeline/benchmarks/2026-10-01/capacity.json` preserve commands, fixture hashes, measurements and limits. Actual headset/Unity/video comparison still needs verification; synthetic capacity and frame cadence do not prove sensor rate or research validity. All work remains local.

## Native replay inspection — 03:55 EDT

The Unity viewer now supports exact sequence-aware sample stepping, original JSON/source-line inspection, raw gaze observation links and new-file cursor exports containing original context plus reconstructed transforms. Poses/highlights require fresh samples and known tracking; trial/search/pause/stop boundaries discard the previous displayed pose until a new sample. Unknown tracking never drives the recorded-head camera; the fallback overview is labeled. Historical samples remain available for inspection.

Full `StudyVerificationChecks.Run` passed with process exit 0 (`/tmp/gaze-unity-native-replay-final.log`). Tests cover equal timestamps, rewinds, pose gaps, unavailable selections, phase boundaries, unknown tracking, exact source lines and serialized snapshots. The graphical `TrialReplayChecks.RenderSmoke` passed with a real 168-object synthetic render (`/tmp/gaze-native-replay-render.log`, preview `/tmp/gaze-native-replay-render-20261001/preview.png`), including gap/unknown/recovery rendering assertions. Both browser replay and video regressions passed after matching phase-boundary handling. GitHub #36 and #37 were updated/read back as In Progress; neither was closed. Renderer evidence does not verify the full editor-window layout or actual headset reconstruction.

**Morning:** open a copied actual recording in Unity and the browser, step to the same source sequence, compare target/object transforms and gaze/controller/head flags, test pause/rewind/gaps, and save a cursor snapshot. Verify the editor controls fit comfortably at the chosen window size. Interactive rerunning with new input remains distinct unfinished work in #36. Runtime replay state types changed, so rebuild the APK before using the final artifact fingerprint.

Android rebuild after native pose/inspection changes succeeded in **259.8 s**, process exit 0 (`/tmp/gaze-android-native-replay.log`). APK: **71,121,788 bytes**, SHA-256 `63afb0878c3c894984144ad692d928bb6206de9991c0949db6cde69b2a833cfe`. This supersedes the prior APK fingerprint; no device installation.

## Native replay audio — 04:12 EDT

Fixed single-channel playback that lost overlapping success effects, and cache reuse that applied the first event's gain to later uses. The viewer now reconstructs separate speech/one-shot intervals, uses each event's volume, seeks in sample frames, trims at recorded stop/natural duration, and releases its own preview-scene sources on pause/close. Native WAV validation now checks the RIFF length, byte rate/block alignment, nonempty data size and finite 0..1 saved volume. Missing/corrupt audio is explicit; original files stay unchanged.

Full Unity integration passed (`/tmp/gaze-unity-replay-audio-final.log`, exit 0). `TrialReplayAudioChecks.RunEngine` also passed with real silent AudioSources advancing, seeking and stopping independently, and preview disposal leaving the study scene's dirty state unchanged (`/tmp/gaze-unity-audio-engine-final.log`, exit 0). The initial reflected per-clip Editor preview API proved unavailable; final code uses owned AudioSources instead. An exploratory asynchronous probe lost its callback to an Editor domain reload and was terminated; the final engine test prevents reload during its short lifetime and exits explicitly.

**Morning:** listen to a known saved run with speech/chime overlap, pause/seek/restart and corrections. Interruption fades and acoustic onset are not measured/reconstructed exactly. The engine checks use silent synthetic data and do not claim listening quality or headset synchronization. New Android packaging is underway (`/tmp/gaze-android-replay-audio.log`); the prior fingerprint remains historical until collected.

Audio-unit Android packaging succeeded in **229.9 s**, exit 0. APK: **71,126,828 bytes**, SHA-256 `4c77cb03aa01f1dc1ae91d030b542888ff69ca9c4804f586843670c1d6a562e0`. This supersedes the native-inspector package; no device installation.

## Survey reference reconciliation — October 1

Updated `docs/protocol/overleaf/survey-instruments.tex` (v0.3) and `survey-questions-by-block.tex` (v0.2) in place: 40+40, separate practice, six choices per gender presentation, hidden-target matched imperative guidance, and catalog availability distinct from candidate rating collection. Added the three draft willingness/reliance/independence items and use-percentage estimate, preserving unknown/skipped values and unresolved semantic endpoints. Debrief open answers precede optional eeriness/own-thought probes, with explicit no-use/no-preference cases and response provenance. GitHub #26 update was read back.

Both final sources compiled successfully with the built-in LaTeX compiler. No new editor tab, standalone PDF, live Qualtrics form or Overleaf publication was created; the existing run-sheet editor remains the intended active document. The absent TXT imports were not restored. **Morning researcher review:** final visual page layout; final item subset/burden, anchors and timing; interview core/time budget; notes versus recording under consent; and qualitative coding. Compiling is not visual or institutional acceptance. Existing published-instrument source text/citations were retained, not independently re-audited in this edit.

## Native full-session reconstruction — October 1

Added indexed saved-trial seeks with exact original JSONL source lines. Regression checks compare all current state fields and transforms against full-prefix reconstruction across 80 trials, same-time sequence boundaries and random backwards queries. Also reproduced a selection-time geometry omission: moving an object after the prior frame was not captured at trigger confirmation. The recorder now saves those changes before round advance, and native reconstruction applies them. The real selection-hook fixture failed before the fix (`/tmp/gaze-unity-selection-geometry-before.log`) and the full integration suite passed afterward (`/tmp/gaze-unity-native-state-final.log`).

Saved `analysis/timeline/benchmarks/2026-10-01/native-capacity.json` and reproducible generator/Unity benchmark entry point. Same 652 MB synthetic source, 80 trials, 432,000 samples, 168 objects/trial; 240 descending late/middle/early state seeks: full-prefix p95 111.6 ms, max 476.5 ms; indexed p95 1.35 ms, max 7.36 ms. Every trial/sample/static-transform check passed. Indexed load 9.54 s, sampled post-load process working set about 2.15 GB. Host not isolated, memory not peak, timings exclude GUI/render/audio. Source regenerated with the same SHA-256; all observations retained. Hardware/participant evidence is not inferred.

Android rebuild is underway in `/tmp/gaze-android-native-state.log`; collect before treating the final APK as current. **Morning:** load a newly copied actual run, compare chosen native/browser source samples including a selection, scrub across trial and voice boundaries, and inspect real-window responsiveness. Interactive rerunning remains open.

Native-state Android rebuild succeeded in **82.9 s**, exit 0. APK: **71,127,664 bytes**, SHA-256 `85972d62dd7e4a61b521675e967ed0f062ab7a2a14100e8bddb4425f81c11eda`. This supersedes the audio-unit fingerprint; no device install/launch.

## Historical replay comparison — 04:46 EDT

Regenerated P020 directly from the original JSONL into the ignored local directory `analysis/results/p20-20261001-replay-audit/`; the latest page is `timeline-v2.html`. Both viewers retained the same 10,119 valid records / 9,703 frame samples, nine trial snapshots, two completed practice and six completed measured trials. The last measured trial never started search. The final JSON fragment is truncated and the footer is absent, so completeness remains false. Source SHA-256 is unchanged; no participant data was uploaded or added to Git.

Across 44 early/middle/late and search/selection cursors, original sample fields, source lines, freshness and all 168 object identities/positions agree. The local `comparison-report.json` describes the comparison and limits; browser/native snapshots, local audit probes and desktop/mobile screenshots accompany it. Floating pose serialization uses 1e-6 tolerance. At exact event boundaries, use the event sequence to resolve a sub-1e-9-second difference between JavaScript and Unity decimal parsing; do not infer a timing measurement from parser rounding. Both pages were visually inspected; no matching video or acoustic comparison was available.

Browser **Recorded event** and **Next event** now preserve the selected source sequence, so later records sharing its timestamp cannot leak into the cursor. Synthetic regressions cover consecutive same-time events and sample stepping; replay and video suites pass. This changes the browser only, so the previously built APK remains current. Historical P020 predates the current exact gaze-observation link and hidden-target 40+40 protocol and does not validate either.

## Current figure and runtime documentation — 04:58 EDT

Task #30 now has original editable SVG, vector PDF, PNG, caption/alt text and a ReportLab builder: `resources/figures/follow-my-voice-study-flow-20261001.md`. The PDF is one 526 × 546 pt vector page, designed for approximately 180 mm manuscript width. Rendered and visually inspected after margin/label corrections; text extraction and new relative links pass. Single-column reduction is not claimed readable; reflow panels if the venue needs that. The figure distinguishes implemented task controls/counts from proposed operator/survey flow and open measurement/research decisions. No paper artwork was reused.

Replaced contradictory table/dwell/14-/20-trial guidance in `docs/diagram.md`, `.mmd`, `diagram_export.md`, `guide/02-gameplay-round-flow.md` and `eight-plane-beta.md`. Old generated diagram PDFs/PNGs are preserved and explicitly historical. The Study Hub links the current figure. Removed an unsupported all-American-accent assertion from the open run sheet; the native compiler passes. Requested, offered and perceived accents remain distinct. No live Overleaf publication or change in participant voice assignment.

Morning review: check figure semantics/setup ordering and final survey route, then choose final manuscript sizing. This documentation unit does not change runtime or require another APK. Historical deletions and Unity ProjectSettings remain excluded.

## Combined recording audit and clock semantics — 05:23 EDT

`analysis/audit_recording.py` now runs the existing session, recording-link/audio-asset, guidance and eye-quality commands into one new report directory. Explicit input paths avoid guessed joins; each child report/stdout/stderr is retained even after another fails. Known files and missing sidecars are inventoried before and after. Duplicate sources, outputs inside source directories, missing child reports and source mutations cannot pass. Exit 0 means selected recording checks agree, exit 1 means issues/failure, and exit 2 means incomplete evidence. It never certifies hardware, native 120 Hz, physiological events, surveys or acoustic/video alignment. An interrupted audit remains `running`.

All **149 Python tests passed**, `/tmp/gaze-python-audit-bundle-final.log`, including seven real-command audit regressions. The final report-field clarification also passed the focused suite. Usage is in `analysis/README.md`. Run after the app's normal thank-you/exit path has closed the eye writer; merely returning Home may leave the clean-close receipt absent. For a deliberately interrupted run, use `--allow-incomplete` and preserve the incomplete result. Keep the copied originals.

`docs/vive-eye-clock-audit.md` records the primary-source and installed-package finding: HTC's gaze result time is documented as the requested information time; EXT gaze sample time may be clamped, predicted or interpolated, or unavailable. Unity's current managed eye pose does not expose that EXT structure. A proposed diagnostic native route still requires implementation, supported semantics and headset validation; neither timestamp alone proves native sample identity. The existing measurement plan now labels its September 28 baseline historical and links the implemented October 1 tools.

The refocus helper's failure message now asks to check lock/menu availability and only suggests Accessibility when macOS reports authorization failure. `bash -n` passes; a new hardware/UI dispatch remains for the final morning handoff. No Unity runtime change or APK rebuild is required for this analysis/documentation unit.

## Raw import type/completion repair — 05:31 EDT

Reproduced two standalone eye-report false passes: `valid: "false"` produced 100% valid time, and `complete: true` without `acceptedRows` was reported complete. The raw reader now rejects non-boolean declared flags, boolean schema/sequence/timestamps, malformed recording IDs and nonnumeric declared timing. Clean-close counts require a nonnegative integer and a matching retained-row count; missing counts stay incomplete. Missing historical fields are not invented or retroactively added.

All **151 analysis tests passed**, `/tmp/gaze-python-strict-eye-final.log`, including the existing link/guidance reports and combined runner. New regressions cover malformed JSON values and completion receipts. This changes offline import validation only; no APK rebuild is needed.
