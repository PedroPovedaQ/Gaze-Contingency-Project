# Project Progress Log

Daily progress on the Gaze Contingency Project ("Follow My Voice: Gaze-Contingent
XR Search with a Self-Similar Agent"). Newest entries on top.

## 2026-09-20

### Add trial recording, replay, analysis and synthetic demo export
- **Implemented:** detailed plan and versioned JSONL/WAV recording of exact object meshes/transforms, head/gaze poses with validity, pause-aware timing, trial linkage and authoritative wrong/correct choices. Existing study CSVs are unchanged.
- **Implemented:** isolated Unity Editor viewer with seeking/cameras/audio; Python derived metrics; separate desktop mouse rehearsal using saved layouts; fixed-rate PNG/WAV export and labeled MP4 encoding. Guide: `docs/trial-replay.md`; plan: `docs/plans/2026-09-20-1603-feat-trial-demo-replay-plan.md`.
- **Verification:** Unity compile and Editor integration checks passed, including the real capture callback; ten Python tests and existing layout/voice/enrollment/worktree checks passed. Rendered and visually inspected a labeled eight-second 1280×720 synthetic demo (240 frames). Analysis recovered one wrong choice, one correct choice and five seconds of active search. Installed Apple's missing Metal toolchain and corrected Retina export cropping during validation.
- **Open verification:** Android Build And Run was attempted; the first pass cancelled during cached build preparation, and retry reached “No Android devices connected.” No deployment or physical headset fidelity/performance validation is claimed. The viewer loaded visibly through the Unity menu; coordinate-based UI control failed in the automation service, so full manual interaction QA remains open.
- **Protocol proposal / Open decision:** repeat-layout collection currently records explicitly labeled desktop mouse responses. Headset repetition, participant learning effects and revised research procedures require a separate protocol decision. Vikunja task 24 remains open for hardware verification; no reusable external logger was supplied.

## 2026-09-16

### Archive submission-video reference and checkpoint all work for v2
- Preserved the original TimeFlow MP4 under `resources/videos/` with provenance, technical metadata and checksum; configured Git LFS for the 177 MB file.
- Created #32 for reference review, original storyboard, headset/setup capture, narration/captions, evidence-backed claims and final submission export/review; linked epic #19 and the Study Hub.
- Checkpointed the voice-script implementation and accumulated protocol, measures, survey/paper/figure/video resources for the user-requested v2 push. Prior voice/enrollment tests and Unity compilation passed; headset QA remains pending.

### Track master's thesis and committee document readiness
- Created #31 for official template verification, canonical thesis source/build, chapter-level readiness, citation/format checks and a versioned committee-review package; linked epic #19.
- Recorded dependencies on design/power, instruments/protocol, setup figure and actual analysis/results. Committee milestone, recipients, review lead time and dates require confirmation; task creation does not claim approval or authorize sending/submission.

### Archive motor-control paper and track the study setup figure
- Saved the supplied motor-control submission and flow-image reference under `resources/papers/` and `resources/figures/`, with provenance and SHA-256 checksums. Linked the reference shelf; publication status remains unverified.
- Created #30 for an original two-panel seated eight-plane setup/session-flow figure, including editable vector and PDF/PNG exports, caption and alt text. Linked epic #19 and dependencies for final counts, wording and surveys; did not import the reference paper's training design into this study.

### Make power analysis an explicit GitHub work package
- Expanded and renamed #16 to own primary-outcome definition and reproducible power/sensitivity analysis for participant and trial counts. Added assumptions, repeated-measures structure, practical effect, pilot uncertainty, attrition, reporting artifacts and final sample-size decision tasks.
- Linked #10 candidate schedules, #17 pilot inputs and #19 epic readiness to that work. No calculation was performed or final sample/trial count selected; initial sensitivity work starts now and finalization follows pilot evidence.

### Implement issue #24 script pilot modes
- Added Collaborative and External wording selection after Start setup, with a shared versioned phrase set across both voice conditions. Perspective locks before enrollment and persists through preparation, retries and both blocks; a new session resets the choice.
- Scoped audio caches and manifests by perspective/version while preserving eight starter clips and background loading. Playback telemetry retains exact spoken text, and run metadata records the selected script.
- Added the script comparison guide and pilot rubric. Final listening assessment and study-script selection remain open; issue #24 is not yet fully complete.
- Used Luna workers for phrase modes, setup/session changes, regression coverage and review. Unity compilation, voice-isolation tests and enrollment tests passed. Build And Run was triggered, but Unity stopped with “No Android devices connected”; hardware playback/UI QA remains pending.

### Create the experimenter protocol from the supplied run-sheet example
- Added a six-page Word run sheet and Markdown companion under `docs/protocol/`, preserving the supplied example's typography, headings, Say/Ask/Action structure and page footer. Linked both from the Study Hub and procedure document.
- Checked setup, voice recording/readiness, practice, blocks, pause controls and saved records against the Unity scripts. Included participant scripts, survey handoffs, debrief and final data checks; distinguished implemented beta behavior from the fixed-front proposal and other open decisions.
- Rendered and visually checked all six pages. Documentation only; no device build or submission-packet regeneration.

### Add a single study-materials hub
- Added `docs/STUDY-HUB.md` with the current protocol summary, survey shelf, reference shelf, decisions/gaps and delivery-board link. Marked older PDFs and IRB sources as drafts needing reconciliation.
- Added the hub to the repository README and survey/measures/IRB indexes; corrected the README’s superseded gaze-aware versus unaware question to the current voice comparison. Documentation only; no device build.

### Create GitHub participant-readiness project
- Created private GitHub Project #2, “Gaze Study — Participant Readiness,” with the 24 existing repository issues and an automatic repository import workflow: https://github.com/users/PedroPovedaQ/projects/2.
- Configured Backlog, Ready, In Progress, Ready for headset QA and Verified statuses. Added stage and priority issue labels, separated deferred/historical scope, and documented the participant journey, work order and dependency gates in the project README. Linked epic #19 and updated #26 with IMI/Guo survey decisions.
- Used the signed-in browser because the CLI token lacks Projects scopes; no permission expansion was required. No runtime changes or device build.

### Add Guo et al. agent-perception survey
- Saved the 23-page author-hosted paper (DOI 10.1145/3651288); visually checked Table A1 against the user's screenshot. Added an unchanged-wording 43-item Markdown/CSV source bank with original item IDs, anchors and all five source references.
- Extended the measures register with S16–S24; linked the questionnaire source and added missing bibliography entries. Flagged appearance-dependent items, pending scoring/adaptations, overlap with legacy eeriness and the proposed combined survey burden. No runtime or generated submission form changes.

### Add IMI and establish the measures register
- Saved Kao et al. (2021), DOI 10.1145/3474665, from the author's university site and the official IMI complete packet under `resources/surveys/`, with provenance and SHA-256 checksums.
- Added `docs/measures-register.md` to track subjective, behavioral and quality measures, operational definitions, timing, scoring, implementation support, analysis joins and unresolved decisions. Recorded confirmed IMI inclusion and proposed the paper's four subscales; distinguished this from the unapproved final item selection.
- Linked protocol, alignment and analysis docs; removed the questionnaire source's stale frozen status, added IMI planning, corrected two-block administration and retired directional guidance item. IPQ remains on hold; legacy forms still require reconciliation. No runtime changes or generated submission-packet rebuild.

### Clarify the seated rotating-search thesis and documentation conflicts
- Recorded the user-directed seated rotation task and audited the protocol, beta documentation and older IRB decision register. Added `docs/thesis-study-alignment.md` with document-by-document reconciliation work and explicitly pending decisions.
- Corrected stale beta branch/implementation descriptions and marked mixed historical protocol content as needing reconciliation. The user confirmed return to a fixed front direction before each trial, warmer/colder-only guidance and cloud voice processing, and reported that the IRB application has not been submitted. Recorded these as target-protocol decisions; the beta still needs its fixed-front gate. Pilot trial count remains unanswered. Runtime behavior and generated submission artifacts are unchanged.

### Checkpoint the beta and recording workflow for v2
- User requested promotion of the current beta work to `v2`, including the surrounding layout, through-wall gaze selection, progressive/background voice preparation, recording package commands and meeting backlog notes.
- Voice isolation, enrollment, rotational geometry and synthetic recording/merge checks pass. The runtime changes were built and launched successfully on September 12; this checkpoint adds no new runtime behavior.
- Different collaborative/external voice scripts are tracked by [issue #24](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/24), not implemented by this checkpoint. The future voice-by-perspective factor is deferred in #29.

## 2026-09-15

### Translate the latest meeting into a thesis epic and executable backlog
- Created [epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19), nine native child issues #20–#28, and a separately deferred ISMAR issue #29. Each includes scope, dependencies and acceptance criteria. Added an ordered delivery sequence and updated existing issues #6–#18 instead of duplicating their calibration, schedule, analysis, pilot and material/lifecycle work.
- **Meeting decision:** the thesis compares generic versus self-similar voice within participants, always gaze-contingent. **Open decision:** choose one shared wording/perspective after piloting alternatives; current condition-specific phrasing is a confound to resolve. Preserve eight-starter/background preparation rather than the older full-library startup requirement.
- **Protocol proposal / open decisions:** enrollment around 15–20, repeated geometric difficulty, instrument timing, headset breaks and reported thesis dates require finalization/verification. Future perspective factors and unvalidated physiological interpretations are not thesis launch requirements.
- Verified all 11 newly created issues and nine native child links, resolved dependency references, and confirmed meeting updates on 13 older issues. This was planning only: no participant data collection, external messages, implementation changes or Unity/device build.

## 2026-09-12

### Reusable headset and Yeti recording commands
- Added `scripts/record-vr.py` and dependency-free package commands for start/status/stop/devices/merge. Silent scrcpy video and a separately captured Yeti WAV finalize on disconnect or stop; raw files remain available. The microphone is resolved from current device names, with no silent fallback.
- Created the personal `record-vr-session` skill with the same helper and package workflow. Audio timing uses approximate launch offsets and supports manual refinement; a static final video frame is extended to the recording endpoint when necessary.
- Live validation: stopped the previous session, preserved its earlier Mac microphone and Yeti files, and started a new verified video/Yeti session through `npm run record:start`. Synthetic FFmpeg checks pass for audio delay/advance, final duration, stream presence and preservation of originals; skill frontmatter validation passes.

### Install the latest beta on the headset
- Built and launched the current workspace on the connected VIVE Focus Vision, including the eight starter clips followed by silent background loading and the existing through-wall gaze fix. Android Build And Run succeeded in 99.4 seconds; the replacement app process is running.
- Sampled startup logs contain no fatal exception, null-reference, missing-method or missing-library markers. Full headset listening and interaction QA remains pending.

## 2026-09-11

### Continue loading audio after the eight starter clips
- **Implemented:** background loading begins during the voice checks, queues upcoming trial instructions before other phrases, and silently prepares both voice variants. It does not change the active voice condition, block controls or play audio. Foreground playback interrupts the loader and the interrupted item stays queued for resumption.
- **Implemented:** background errors do not change foreground audio-check errors or stop an active trial. A needed missing clip still uses foreground generation and normal validation/failure handling. The manifest identifies `starter_then_background`.
- Verification: production voice harness passes background silence, voice isolation, foreground preemption, resumption, instant cached playback and background-versus-foreground failure handling. Unity compiled the changes; Build And Run stopped at “No Android devices connected.” Installation and on-headset QA remain pending.

### Prepare voice prompts as the session progresses
- **Implemented:** setup prepares four starter phrases per voice instead of the full phrase library. Remaining authorized trial and hint audio is generated on first use and cached independently per voice. Both audio checks are still required.
- **Implemented:** new clips retain first-person self-similar wording, must match the accepted starter RMS/peak constraints, and update the participant and active-run manifests with preparation mode and actual spoken text. Round preparation displays a waiting message; search timing and capture remain gated until the instruction finishes. First-use hint synthesis may add latency during search.
- Verification: production voice isolation harness covers lazy generation, cache reuse, condition separation, matching levels, active-run manifest writes, unknown phrase rejection and failure handling. Voice enrollment tests also pass. Unity compiled the changes without C# errors; the requested Build And Run stopped at “No Android devices connected.” Deployment and headset timing/listening QA remain pending.

### Through-wall gaze selection in the rotational beta
- User reported the eight-plane beta works well and requested a checkpoint; pre-fix beta is saved at `751097a`.
- **Implemented:** beta start configures the eye-gaze ray to hit searchable layer 8 only, ignore UI graphics, and select the nearest searchable object. Room colliders no longer block gaze targets; controller menus retain their existing interaction.
- Verified the mechanism in XRI's ray target selection and added an Editor regression check with actual wall/target colliders. The all-layer ray hits the wall; the configured ray hits the target behind it. Check passes. Android Build And Run succeeded in 52.7 seconds and launched the replacement process; user headset QA remains pending.

### Eight-plane surrounding-search beta
- **Implemented:** separate `codex/eight-plane-beta` branch from the stable `v2` checkpoint. Explicit seated centering captures headset position/forward direction once; eight vertical planes surround that fixed origin at 45-degree intervals and a default 1.5 m radius, seven objects per plane. Surface taps no longer start the beta.
- **Implemented:** noninteractive plane outlines/degree labels, a goal display below the turning view, practice and existing voice setup/announcements, 14 measured rounds, and gaze dwell. Coverage identifies eight planes by stable object ID; hints use within-plane distance. The object manifest labels beta runs and records plane/slot/azimuth and origin/heading.
- Verified geometry determinism, counts, handedness, spacing and invalid-radius behavior (test initially failed before the implementation existed). Both voice harnesses and all 9 analysis tests pass. Review/simplification ran inline per the project instruction; no independent peer review was used. Resolved duplicate centering and retry behavior. Final Android Build And Run succeeded in 37.8 seconds; the headset process launched with no fatal/managed startup exception markers in the sampled log.
- **Open validation:** full seated headset QA, radius/readability/comfort and side/rear gaze acquisition. The beta does not implement the larger proposed schedule, plane-balanced targets, forced forward alignment, theta calibration or coarse directional audio. `docs/eight-plane-beta.md` records the boundary and QA procedure.

### Checkpoint voice setup and practice updates on v2
- **Implemented:** neutral recording introduction uses the requested wording: "We will now connect a short sample of your voice, after the tone please read the script." Permission checks precede the countdown and tone; capture opens only after the cue finishes. Failed instruction playback offers an enrollment retry.
- Checkpoint includes explicit practice labels and announcements, command-based enrollment, processing declarations, first-person plural self-similar prompts, and the updated yellow star instruction.
- Verification: both voice harnesses pass, including neutral introduction before enrollment and cue-before-capture ordering. Android Build And Run succeeded in 50.4 seconds and launched the updated app. User requested this checkpoint be pushed to `v2`; headset listening QA remains pending.

### Spoken processing status and first-person self-similar wording
- **Implemented:** announce "Processing voice" before preparing the voice libraries and declare both voices ready after successful preparation, before the audio checks. Setup status uses the selected neutral voice and restores the assigned condition afterward.
- **Implemented:** self-similar targets, practice, hints, audio checks, introduction, congratulations and closing use first-person plural wording. The same conversion runs before synthesis/cache lookup and is recorded as actual spoken text in the manifest. Neutral wording is retained.
- Updated procedure and QA notes: wording and voice identity now vary together. Verification covers all live hint strings, target/practice synthesis, prepared playback without network requests, and setup-status condition restoration. Both voice harnesses pass; Android Build And Run succeeded in 49.1 seconds and launched the app. End-to-end listening QA remains pending.

### Clarify practice and use command-based enrollment
- **Implemented:** practice announcements explicitly say the round is practice and does not count toward the study. Transition and search displays label practice; progress shows practice 1 / 2 or 2 / 2 as not counted. Both voice libraries include the practice-specific announcements.
- **Implemented:** enrollment now uses short visual-search and gaze commands, with an instruction to read naturally and finish recording when done.
- Verification: voice isolation and enrollment harnesses pass; final Android Build And Run succeeded in 44.3 seconds and launched the updated process. Visual and spoken headset QA remains pending.

### Update yellow star spoken instruction
- **Implemented:** the yellow star target now uses "Locate the star, yellow color." in both voice libraries and round announcements. Audio caching already includes the exact phrase text.
- Verification: voice isolation checks pass; Android Build And Run succeeded in 48.1 seconds and launched the updated app on the headset. In-headset audio QA remains pending.

### Pull upstream task wording and relaunch headset
- **Implemented:** fast-forwarded to `v2` commit `a5fe654`, including project-wide Locate wording and the updated audio content version.
- Verification: both voice test harnesses pass. Android Build And Run succeeded in 57.0 seconds; the updated process launched on the connected headset and passthrough initialized successfully. Full session QA remains with the researcher.

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
