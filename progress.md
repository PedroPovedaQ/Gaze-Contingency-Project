# Project Progress Log

Daily progress on the Gaze Contingency Project ("Follow My Voice: Gaze-Contingent
XR Search with a Self-Similar Agent"). Newest entries on top.

## 2026-09-11

### Publish accumulated project work to v2
- User requested all current project changes be committed and pushed to `v2`. Remote `v2` matches the working branch base; no force push or merge is needed.
- Pre-push validation: both C# voice harnesses and all 9 analysis unit tests pass; diff whitespace checks pass. Local API credentials remain ignored and excluded.

### Cached setup pacing and audio diagnostics
- **Performance:** skip the 100 ms pacing delay for local cache hits, retaining it for newly generated clips. Removes 5.3 seconds of deliberate delay for 53 cached neutral clips, or 10.6 seconds when both libraries are cached; these are eliminated waits, not measured end-to-end speedups.
- **Implemented:** preserve synthesis error details, separate final matching and manifest-saving progress stages, and persist preparation diagnostics.
- Verification: cached preparation makes zero provider requests and adds no pacing waits; voice isolation and enrollment tests pass. Android build/install succeeded and launched the updated app.

### Explicit setup start gate
- **Implemented:** startup waits at a pointed-at Start setup button before any voice selection or recording. Button arms after a brief released-input interval while focused; focus loss/pause disarms it. A global trigger press does not activate this gate. A post-start cooldown and fresh input edge prevent that click selecting a voice underneath.
- Existing optional automatic defaults now apply only after Start setup. Public voice-selection methods cannot bypass the waiting phase; `StartSetup()` provides the programmatic equivalent.
- Enrollment and voice-isolation checks pass; full headset overlay/click-through behavior remains user QA. Updated consolidated checklist. Android build/install succeeded in 131.2 seconds and launched the updated app.

### Early completion of voice enrollment
- **Implemented:** clickable Finish recording button below the reading passage, with Trigger / 1 and public method equivalents. Recording ends on confirmation or its existing deadline, and encoding uses the captured frame count. One-second guard rejects accidental empty submissions; microphone is released on component disable.
- Verification: production enrollment coroutine tests cover early finish, automatic deadline, duplicate/idle input, captured frame count and interrupted microphone; voice isolation harness passes. Unity recompiled without C# errors; Android build/install succeeded in 57.7 seconds and launched the replacement process. Physical button/reading layout QA remains pending.
- **Observed:** user reached the reading passage after the clean rebuild, so startup progressed beyond the earlier scene-loading crash. Full study QA remains pending.
- **Open platform question:** no eye-tracking notice UI was found in app code/assets. The notice appears to be HTC runtime/system-owned; no supported app-side suppression setting was confirmed. System prompt behavior remains unchanged.

### Startup crash recovery — verification pending
- Added `Tools > Codex > Clean Build And Run Android` using Unity's `BuildOptions.CleanBuildCache`, preserving normal incremental builds. Clean rebuild/install completed in 443.6 seconds (1141.0 MB).
- **Observed:** clean rebuild produced identical level0 scene bytes. This does not establish a fix. Native symbolication shows script-component deserialization; the crash register's 251104 offset matches the packaged FindObjectGameManager record, making it the leading component to investigate if the crash persists.
- **Blocked device check:** the replacement process starts but the headset reports Asleep and pauses/stops before scene loading. Asked user to wake/wear it and retry; no response yet. No claim that the startup crash or full study QA has passed. Sanitized fresh-process evidence: `/tmp/gaze-clean-build-startup.log`.

### Headset startup crash diagnosis
- **Observed:** three native SIGTRAP crashes on `Loading.Preload` at 10:55:18, 10:55:35 and 10:55:46. Unity reports packaged `assets/bin/Data/level0` as corrupted with `Position out of bounds!`, before study startup.
- Installed APK SHA256 matches the local build exactly; the packaged level0 ZIP CRC passes. Evidence points to serialized scene/build data, not damage during USB installation. The eye-tracking prompt precedes the symptom but is not established as the cause.
- **Open investigation:** exact serialization/build-cache cause and fix remain unverified. Next diagnostic step is a clean scene/player-data rebuild followed by device startup verification. Sanitized process evidence saved locally at `/tmp/gaze-headset-crash-20260911.log`; no runtime changes made during this log check.

### Connected headset deployment
- **Implemented / deployment verified:** built the current local branch for Android ARM64, installed it on the connected VIVE Focus Vision, and verified the running Unity process and launch activity. The headset is asleep and immediately stops the activity; the user must wake/wear it to continue. Unity reported build success in 968.7 seconds (1164.9 MB).
- Reused the main checkout's existing voice configuration in the ignored worktree StreamingAssets file; verified inclusion in the APK without exposing credential values. No research audio was recorded by the agent.
- **Open validation:** participant-facing voice setup, full two-block flow and telemetry QA remain for the user on the headset. Device package is `com.DefaultCompany.MixedRealityTemplate`.

## 2026-09-10

### Four-step voice study implementation — ready for consolidated headset QA
- **Implemented:** mandatory two-voice readiness, full cached phrase libraries, common achieved RMS across both libraries, sample acceptance/replay/restart, bounded preparation retries and explicit enrollment cancellation. Self-similar failure never substitutes neutral speech; both voices are required for this study.
- **Implemented:** two separate practice trials, then two counterbalanced blocks of seven measured trials. Participant files persist voice order and neutral profile. Added per-block NASA-TLX, break checkpoints, pause/withdrawal/technical stops; timeout remains disabled pending a protocol decision. Fixed the scene's existing ten-round debug override.
- **Implemented:** hide stimuli until announcement completion, start a pause-aware search clock at reveal, and gate dwell to active search. Added schema-2 events, stable object IDs/manifests, voice clip identities, separate preparation gaze logs, per-run workload data and a session validator. Analysis reports descriptive block differences and explicit pairing exclusions.
- Verification: production C# voice/schedule/clock harness and all nine Python tests pass. Final production Assembly-CSharp compilation using Unity's generated response file and compiler passes (deprecation warnings only). Earlier Unity Editor reload passed; final interactive inspection was unavailable because the Mac was locked. Required Build And Run was dispatched; no connected-device QA was completed.
- Review: resolved the independent peer's actionable defects, including RMS mismatch, truncated rounds, lost workload rows, ambiguous cancellation, profile persistence and boundary IDs. Changes remain local and uncommitted on `codex/always-gaze-contingent`.
- **Open validation:** user will perform one full headset QA using `docs/voice-study-qa.md`; perceived similarity/neutrality, acoustic onset, memory use and real provider/device failure behavior require that run. Directional/theta mechanics, full spatial measures, power and IRB reconciliation remain separate backlog work. Historical entries below describe earlier states.

### Voice contamination fixes
- Unity Editor compilation passed. Required Build And Run was dispatched; Unity reported **No Android devices connected**, so headset audio verification remains pending.
- **Implemented:** self-similar synthesis never falls back to neutral audio, including when clone IDs, credentials or providers are missing. Cached self-similar audio remains usable offline only under its own clone ID; pending enrollment stays silent.
- **Implemented:** removed automatic reuse/persistence of device-wide clone IDs. Self-similar launch now enrolls the current participant; failure keeps selection incomplete with Retry or explicit neutral selection. Added a missing-provider enrollment guard and corrected immediate-failure speech busy-state cleanup.
- Verification: `scripts/test-voice-isolation.sh` executes the production synthesizer with deterministic providers and populated competing caches, covering missing/pending clones, missing credentials/provider, failed/short audio, successful and offline clone playback, male/female routing and busy-state cleanup.

### Neutral versus self-similar voice
- **User-directed decision / implemented:** voice identity is neutral versus self-similar. Added explicit male neutral (ElevenLabs Eric) and retained female neutral (Rachel); launch selection, synthesis and pre-cache use the selected profile. Voice IDs scope caches to prevent cross-voice playback, and new run labels preserve neutral-male / neutral-female.
- **Implemented:** intro and task start wait for voice selection; removed the fixed Ava introduction. Enrollment failures return to explicit neutral voice selection. Historical generic labels remain readable.
- Verification: Unity Editor compiled/reloaded the scripts without compiler errors; executable C# checks cover male/female neutral IDs, run labels and unchanged stimuli; all six Python tests pass, now including both neutral profiles. Build And Run was invoked and reported no Android device connected.
- **Open validation:** listening checks for perceived neutrality and matched audio, headset build/run, and full protocol reconciliation remain pending.

### Always gaze-contingent agent
- **Protocol proposal — user-directed decision:** gaze awareness is no longer an experimental factor. The agent always uses gaze-contingent guidance; generic versus self-similar voice remains the planned comparison.
- **Implemented:** removed the unaware hint toggle/control phrases; every round uses the existing gaze-responsive proximity policy, with its 2-second initial delay and 4-second cadence. New runs have one guidance block and `always_gaze_aware` summary metadata.
- **Implemented:** preserved all 14 seeded targets and 784 object configurations; normalized voice-suffixed labels in analysis while preserving legacy alternating data. Updated runtime guides, procedure and historical protocol notices; updated GitHub issues #6, #7, #13, #16 and #18.
- Verification: Unity 6000.3.10f1 batch compilation passed; executable C# checks covered all rounds, five participant IDs, both voice labels and unchanged stimuli; six analysis compatibility tests and worktree-environment checks passed; `git diff --check` passed.
- Review fixes: event logs resolve the new run folders correctly, raw voice labels remain available, and awareness comparisons reject datasets containing the new schedule. Updated remaining agent instructions and guide snippets that referenced the removed toggle.
- Used the shared Python environment and an isolated APFS Library clone from the closed primary v2 checkout. No credentials copied.
- Ran `scripts/refocus-unity-and-build-device.sh`; Unity accepted the menu command but reported **No Android devices connected**. Headset build/run verification remains blocked by device connection. Changes remain local on `codex/always-gaze-contingent`.
- **Open work:** directional coaching, spatial/theta mechanics, two-voice block orchestration, revised power/analysis and full IRB packet reconciliation remain separate issues. Historical audit from September 9 is preserved as the baseline before this decision.

## 2026-09-09

### Meeting gap audit and ordered backlog
- **Implemented evidence:** audited v2 at `3822269`; confirmed existing bookshelf runtime and voice enrollment/cache instead of treating all meeting tasks as new work.
- **Protocol proposal:** recorded the two-voice-block, balanced-theta, directional-coaching direction without replacing historical protocol decisions.
- Created GitHub roadmap [#6](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/6) and 12 linked issues (#7–#18), with evidence, dependencies and verification criteria.
- Found timing contamination from transitions, generic fallback in self-similar runs, provider mismatch, persistent clone identity concerns, missing spatial telemetry and outdated analysis assumptions.
- Checked primary literature and corrected the appearance-versus-voice interpretation of the doppelganger study. Full audit: `docs/meeting-gap-audit-2026-09-09.md`.
- **Open decisions:** advisor confirmation of factors/theta/posture, provider and institutional handling, pilot-derived counts/power, final protocol and actual Friday date. No runtime changes, hardware validation or approval claims.

## 2026-08-18

### CITI / IRB training — PASSED
Completed the following UCF CITI **Stage 1 – Basic Courses**, all passed **18-Aug-2026**:
- **IRB Protocol Review**
- **Research and HIPAA Privacy Protections**
- **Researchers – Information Privacy & Security (IPS)**

### IRB packet (v2 branch)
- Filled the administrative fields across the IRB source docs and rebuilt the 8-document
  submission packet:
  - PI: **Dr. Roshan Venkatakrishnan** — roshan.venkatakrishnan@ucf.edu — **407-823-3957** —
    Computer Science, College of Engineering and Computer Science
  - Student investigator: **Pedro Poveda** — pe011248@ucf.edu
  - Lab rooms: **HEC 208, HEC 308, BYC 119** (UCF Main Campus)
  - Submission date: **2026-08-18**; compensation from **approved laboratory funds**
- Reused institutional facts (department, rooms, funding, minimal-risk precedent) from the
  **Study 9491** lab reference packet (Mehrab Islam, same PI).
- Removed the "DRAFT — NOT FOR SUBMISSION" banner from all 8 generated documents
  (updated `build-irb-packet.py` + `verify-irb-packet.py`).
- Huron study created: **STUDY00009581**.

### Still open
- **UCF determinations** (their call): risk level, injury-language wording, clinical-trial
  classification, conflict-of-interest disclosure, Certificate of Confidentiality.
- **Post-approval fills**: IRB study number on recruitment materials, exact calendar dates.
- **Protocol vs. implementation gap**: the Unity build is still the 14-round bookshelf
  prototype and does not yet implement the planned 8-plane / 4-block / Williams design.
