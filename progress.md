## 2026-10-25 — Cloned-audio verification recovery

- **Implemented:** fixed an observed on-headset setup failure where Scribe transcribed “Keep looking up and right” and “Go back up and right” as “...up and write” on all three attempts. Canonicalization now recognizes this acoustically identical spelling only in the up/down-and-right context; wrong directions, omitted words and fillers still fail.
- **Implemented:** preserve decimal points when comparing angle instructions and spell decimal numbers explicitly for synthesis. The old receipt for 47.5 degrees accepted “forty seven, five”; the new verifier rejects a missing “point”. Bumped receipt version so old cached clips are checked again instead of trusting the old numeric comparison.
- Regression test reproduced the directional failure before the patch; full voice-isolation/download checks pass after it. Scoped manual review and simplicity pass found no additional changes necessary. Unity Android Build And Run succeeded in 66.3 seconds, installed via USB and relaunched (update time 12:41:52 EDT). APK SHA-256: `41bdd13115581bc4a99d80e6544c60f31708dac5b2c04bd04a73b0177ec425bb`.
- The headset now uses Wi-Fi address 192.168.0.25 rather than the old 192.168.1.208; this session uses USB. Live video opened with scrcpy. Cache and saved clone retained. Post-install voice preparation/trial entry is awaiting wearer selection of the saved-clone quick test.

## 2026-10-25 — Further local Jev fine-tuning

Implemented a separate v4 continuation from saved v3 weights: 2,400 training rows, compact versioned input, all action/direction coverage, and an untouched 500-case fresh wording/gate-combination test. Validation-only selection retained epoch 7 of eight. Fresh offered-action agreement improved 77.4% → 98.4%; unchanged mandatory gates corrected the eight remaining failures. Actual saved-model inference matched all 500 final expected actions. Earlier production-case regression improved 7/10 → 10/10.

Paired warmed local HTTP measured 7.24 ms → 6.52 ms median; separate current local/cloud comparison measured 15.27 ms versus Jev 196.87 ms. Thirteen tests and independent review pass. These are synthetic authored-policy results, not participant helpfulness, headset FPS or acoustic timing. Parent model preserved; no Unity/headset deployment. Evidence and remaining decisions: `docs/testing/2026-10-25-local-jev-continuation.md`.

## 2026-10-25 — Local Jev action-model experiment and checkpoint

Implemented an isolated, reproducible MiniLM action-classifier fine-tune after saving study work as `76d7cda` (voice worker fix remains in `1e47031`). Trained 22.7M parameters on 1,200 synthetic authored-policy examples with actual library phrases; validation-only selection, separate diagonal and production-C# holdouts, local loopback server and paired current-Jev latency benchmark. Ten tests and independent review pass. No Unity runtime or headset change.

Measured local HTTP median 19.3 ms versus current Jev 247.9 ms (12.85× faster), but expected-action agreement was only 7/10 unique production cases versus Jev 8/10; unseen synthetic masked agreement was 68.4%. External gates improve safety but do not fix repeat/helpfulness errors. Authored rules matched all 10 production cases with 0.0048 ms median direct-call cost. Keep the learned model experimental; local rules are the stronger next candidate for this narrow action policy. No participant helpfulness, headset FPS or acoustic latency claim. Full evidence and remaining decisions: `docs/testing/2026-10-25-local-jev.md`; weights/data retained outside Git in `~/.local/share/follow-my-voice-models/20261025-minilm-action-v3/`.

## 2026-10-25 — Live Trial Review headset wall controls

Implemented a separate live Headset controls row for wall outlines and degree labels; Viewer walls remains local to the desktop. The existing ADB receiver sends explicit session-bound, short-lived requests and the headset publishes actual state and acknowledgments. Saved review, legacy builds, disconnected/stale connections and sleeping headsets disable the control. Requests expire across pause/focus boundaries. Changes retain a standalone audit and appear in active replay events, without modifying trial geometry, targets or timing.

Verification: all 80 Trial Review Python checks (including 21 receiver checks), C# headset command/heartbeat regressions and generated desktop Unity checks pass; Trial Review Headset Controls.app built. Independent subagent review found stale commands across brief sleep and acknowledgment corruption on malformed input; both fixed with regressions. Final Android APK build passed in 87.0 seconds (1163.5 MB), with launch disabled. User put the headset away and requested build/error checks only; remote controls have not been installed or exercised on hardware. Wearer voice-performance remeasurement and physical wall-toggle acceptance remain pending.

## 2026-10-25 — Cloned-voice preparation frame stalls

Implemented: moved the existing deterministic clone PCM treatment (filters, levelling and stereo FFT convolution) from Unity's frame thread to a background task. Unity audio-object access and publication remain on the main thread; cancellation rejects stale results and processing failures retain the verified provider cache. The full 562-phrase quick-test preparation and waveform/verification policies remain intact.

Observed on the Focus Vision: loading fell to approximately 9–15 FPS, with thermal throttling also logged. A production-coroutine benchmark with synthetic three-second PCM measured the longest resume at 884 ms before and 29 ms after during a concurrent Android build; these are desktop diagnostic measurements, not headset FPS claims. Regression first failed on main-thread DSP, then passed with worker execution, pending-result isolation, cancellation and error handling. Existing voice isolation/DSP/normalization checks and Unity compilation pass. Targeted simplification/manual review found no additional required changes. Android build succeeded in 351 seconds, installed and relaunched over Wi-Fi (fresh process verified); on-device loading remeasurement remains pending.

## 2026-10-25 — Theta exports, v7 validation and Trial Review readiness

Implemented exact pre-cue snapshot columns in full/per-trial CSV and key metrics in condensed CSV. Trial Review now displays all angle/height/position/clock/sequence values with explicit world-frame labels and raw JSON, correct backward-seek boundaries and finalized readiness transforms. Added strict missing/non-finite/contradictory measurement rejection and v7 jittered-cross geometry support.

Updated the session validator for full, shortened and quick v7 schedules, independent direction/height quotas, snapshot/manifest consistency, reset-aware pre-opening timing, and valid terminal timeouts. Three independent subagent reviews covered validator, viewer/export and runtime; fixed issues included timeout rejection, nested JSON defaults, and cue-order evidence. Verification passed: 24 validator, 45 trial-export, 3 snapshot, 9 zone and 53 desktop-adapter tests (the latter includes the trial-export suite), generated desktop checks, schedule/voice and rotational geometry checks. Final Android build succeeded in 78.4 seconds and installed/launched over Wi-Fi; the new Trial Review Theta v7 Ready.app built successfully. Receipt: `/tmp/theta-android-build-result.json`. Native screenshot inspection timed out; automated desktop parser/state/export checks passed. Wearer acceptance across full blocks, pause/retry and infeasible positions remains open. Wi-Fi ADB is verified at 192.168.1.208:5555; USB is not required.

## 2026-10-25 — Angle-controlled randomization and GitHub checkpoint

Implemented schedule v7 with independently balanced directions, randomized target identities/layouts, signed-height quotas and target placement from the valid pre-cue eye origin. Recorded assigned/realized geometry and finalized scene positions; retained spacing and technical-stop behavior for infeasible geometry. See `docs/research/theta-randomization-20261025.md` for assumptions, test checklist and export/thesis follow-up. This is development implementation pending headset QA, not a validated study release.

Software verification: schedule/voice, constrained geometry, timeout, six native adapter suites, Unity compilation and readiness integration pass. The first-cross regression exposed by integration was corrected; later crosses retain bounded jitter.

User requested GitHub Projects as the task system and removal of obsolete task-system references. Current tracking: issue #10 on Gaze Study — Participant Readiness. User then requested pushing the current work to main and a detailed Claude handoff for thesis work; runtime and latest synchronized thesis changes are being checkpointed together while preserving their distinct local checkouts.

## 2026-10-25 — Synchronized thesis checkpoint for main

User requested pushing the current work to main before a Claude thesis handoff. Verified the Overleaf importer reports sources and PDF up to date at remote revision a06157ad688f521e41f4956cfd65b3628f106c6f; preserved the existing conflict baseline. Checkpoint includes latest manuscript, figures, recorded-development analysis, review evidence and one-way sync tooling. Fourteen sync regressions and six voice-panel regressions pass. Current runtime theta v7 work is a separate development checkpoint; the manuscript still identifies exact pre-cue geometry/height balancing as gaps in its earlier aligned release, requiring explicit reconciliation rather than retroactive claims about recordings.

## 2026-10-23 — 25-second search and qualified-target fast feedback

Updated the shared search deadline to 25 seconds, with summary and replay metadata deriving from the same clock constant. Preserved legacy deadline-aware analysis; added a 25-second export regression. Qualified gaze/controller acknowledgments can bypass interruption advice under local timing/semantic guards, with unchanged hold qualification, 150 ms fade, cooldown and final relevance checks. Added onset/ending, stale-target, ordinary-direction and protected-context tests. Both voice conditions share the policy. Headset behavior remains a wearer check after deployment.

## 2026-10-23

- Thesis survey cleanup: removed pre/post exposure symptom batteries and the agent-gender setup preference question; removed visible question IDs while preserving response circles and scale anchors. Updated procedure, scoring, safety-reporting description, abbreviations, and the Python-generated survey timing figure. General comfort checks and pause/stop procedures remain. Qualtrics remains on hold. Published the scoped changes to Overleaf as 72e570a, preserving concurrent introduction and main-document edits. The revised snapshot compiled and rendered cleanly; the integrated local PDF rebuilt successfully with no sync conflicts. No GitHub commit or push.

## 2026-10-23 — 25-second deadline and faster qualified target feedback

Updated the thesis methodology, scoring, analysis, and development-figure annotations to the selected 25-second active-search deadline. Documented the qualified-target local interruption fast path, protected onset/ending, cooldown, unchanged gaze/controller holds, 150 ms fade, and final rechecks. Regenerated Python/Matplotlib panels from identical recorded inputs; no historical observation or timeout was relabeled. Scoped Overleaf changes preserve concurrent manuscript edits. Six figure tests and 42 export tests plus runtime clock/gaze/guidance/voice checks pass.

## 2026-10-23 — Voice-condition outcome panels

Implemented four reproducible panels with Neutral voice and Self-similar voice axes, using actual Run 30 evidence. First-selection accuracy is 4/5 completed searches; completion-time n=5; exploratory IPA trial-mean n=4. No self-similar observations or recorded 15-second deadline exist in this selected recording, so corresponding cells remain unavailable rather than zero. Preserved raw per-eye diagnostics, source hashes, plotted CSV, and summary inventory. Six scoring/join tests pass. Chapter 4 receives explicit definitions and limitations; no synthetic data or participant-condition inference.

## 2026-10-23 — Final title includes Virtual Reality

Author revised the title to **Follow My Voice: Assisted Gaze-Contingent Search with a Self-Similar Agent in Virtual Reality**. Updated current title references; existing Nerella bibliography entry and citations remain.

## 2026-10-23 — Paper title alignment

Updated active manuscript/protocol and local study-draft title strings to **Follow My Voice: Assisted Gaze-Contingent Search with a Self-Similar Agent**. No Qualtrics upload or external IRB submission; historical snapshots retained. Authoritative thesis and new Nerella citation are in the thesis-main-sync checkout/Overleaf.

## 2026-10-23 — Pupil retention audit and validation readiness

- Traced Run 30 loss to continuity/regularity screens, not SDK-reported pupil invalidity: per eye 68.74 s in short segments, 22.82 s rejected for timing, 13.93 s retained after resampling. Exported per-segment reasons, quality plots and independent unlabeled review traces; existing metric values preserved exactly.
- Tested four engineering screen variants without deploying relaxed thresholds; retention alone is not validation. Renamed figure labels toward the reference paper with explicit candidate/restricted qualifications. Added graph coverage mapping and wearer/independent annotation checklist.
- 36 existing eye tests, 7 diagnostic tests and 17 native timing regression tests pass. Android rebuild succeeded (14.8 s); wireless package update confirmed at 22:54:28 and app process running. No physiological validation or sensor 120-Hz claim; quick alignment records are not held-out validation. Headset connected and app running, no wearer session performed.

## 2026-10-23 — Candidate eye outcomes and missed-target measurement

Added offline per-eye I-DT/velocity candidates, pupil quality screening and wavelet IPA diagnostics, source hashing, independent annotation comparisons and threshold sensitivity. Computational eye tests (36) and trial export tests (41) pass; physiological detector validation remains open. Actual Run 30 data supports development-only figures; missing IPA is blank. Added UCF paper candidates without guessing the intended source. Missed Targets export distinguishes deadline expiry from technical/incomplete outcomes. See docs/research/eye-measures-validation-20261023.md.

## 2026-10-23 — Fifteen-second active-search deadline

- Implemented the user-selected 15-second ceiling for study, practice and quick-test searches, excluding explicit pauses and opening/feedback audio. Deadline rejects late trigger selection, cancels pending guidance, hides stimuli and advances after prepared “Time up” encouragement in the assigned voice. Separate break/final wording; no false success/chime/crumble or replacement trial.
- Added explicit timeout lifecycle/event/outcome; actual found count excludes timed-out attempts. Summary, replay, viewer and condensed CSV preserve unsuccessful outcome and exposure without fabricating a completion time. Previous recordings are unchanged.
- Clock/state checks and 39 CSV tests pass; Unity study-integration checks pass, including timeout, paused clock, one-shot event, next-wall reference, both phrase libraries and replay boundary. Android build succeeded (142 s); wireless install and launch succeeded on 192.168.1.208:5555 at 22:17, version 2.2.1. Viewer build passed; wearer pacing/audio remain unverified.
- Added per-trial Missed Targets plus by-block/voice/deadline counts and percentage, preserving unknown/technical/unstarted outcomes and excluding practice. 41 exporter tests pass. Thesis deadline changes published in scoped Overleaf commit ebbea3a6; local PDF rebuilt.

## 2026-10-23 — Compact vertical experiment layout and recorded replay bounds

- **Implemented:** reduced default wall height from 3.148 m to 2.2036 m (30%), with centers from 0.10–2.1036 m. Kept 168 objects/21 per wall, radius, horizontal width, object scale and 20 cm minimum spacing. Versioned the regenerated deterministic layout as `rotational_scatter_v5_compact_vertical`.
- Replay records exact local wall bottom/height with a presence flag; Trial Review uses those bounds and retains legacy inference for older recordings. No replay object transform is rescaled. Guidance height bands and readiness height derive from the compact runtime configuration.
- Layout checks passed across 90 radius/seed combinations, including counts, bounds, spacing and reproducibility. Added replay bounds roundtrip/invalid-data checks and viewer legacy/new/trial-switch/projection regressions. Unity feedback and replay checks passed, including default guidance-height scaling and saved bounds validation. Isolated Trial Review verification/build passed (legacy geometry, compact bounds, projection, switching, no recorded-data mutation); opened the new Compact Walls viewer in live mode. Android build succeeded in 53.2 s and was installed/launched wirelessly. Installed APK hash matches `cf2cd5b5f7fb4e4f92156c9d2bde32e81041d8b23792a77ec60775c6781628b2`; no fatal/null-reference errors in the inspected startup window. Wearer verification of the new spacing remains pending.

## 2026-10-23 — Entry calibration visibility and three-dot rejection fix

- **Implemented:** small centered countdown replaces the active fixation center/shape, which is revealed only when the countdown ends. Replaced radial fill with an outward-growing gold glow. Added one 0.4-second white reveal pulse distinct from gold gaze confirmation; no repeating flash during collection. Hidden shapes cannot count as seen highlights.
- **Observed:** report `a4e00490600443a69511c81012b3113f` rejected its third dot at about 5.28 degrees, although the completed three-dot mean is about 4.70 degrees. Guided fitting now applies the unchanged 5-degree cap to the completed mean, retaining stability, held-out accuracy/improvement and manual OK requirements. Oversized completed means still reject; explicit Skip (no offset) remains available.
- Added recorded-mean regression, oversized-average rejection, countdown visibility/size, glow progression and single-pulse lifecycle checks. Method version is `head_local_three_dot_mean_user_confirmed_v2`; attention cue duration is saved with the report. This remains a pilot local correction, not vendor calibration or validated accuracy.
- Verification: Unity guided/legacy calibration and entry presentation checks passed, along with the offline shared-gaze quality suite. Inspected real-component rendered previews of countdown, reveal pulse and expanding glow. Android IL2CPP build succeeded in 75.8 seconds and wireless install/launch succeeded on 192.168.1.208:5555. Installed APK SHA-256 matches `ee678144ee6b0a0e5f3a653942b5c3b115d271f71c4ff9a8490cf9fc5efe16d2`; app process runs. Inspected startup window has no fatal exception or calibration null reference, with the existing nonfatal Android Bundle metadata type warning. Wearer validation remains required. The HTC package emitted nonfatal spectator-feature prebuild null-reference diagnostics; this was not an app runtime failure.
- Task-board connection refused locally; work recorded here. No GitHub commit/push requested.

## 2026-10-23 — Native gaze timestamp diagnostics and independent review

- **Implemented:** passive Android ARM64 OpenXR interception of existing eye-action queries, preserving the shared gaze/calibration/guidance pipeline. Added bounded native records, timestamp/validity/space/epoch metadata, asynchronous JSONL recording and diagnostic status.
- Corrected the acquisition premise: `XrEyeGazeSampleTimeEXT` is a runtime pose-expression time that may be predicted/interpolated, not guaranteed sensor capture time. Physical 120 Hz remains unestablished; no polling-frequency change or fabricated sensor sequence was introduced.
- Six native fake-runtime suites pass under AddressSanitizer/UndefinedBehaviorSanitizer. Seventeen analysis tests pass, including independently found/fixed missing/reordered sequence and inconsistent counter cases. Unity ABI/serialization/writer/configuration, shared-gaze and entry-alignment checks passed.
- Independent agents reviewed the native design, bridge/recorder and analyzer. Android prebuild caught duplicate extension registration; adapter now consumes the existing Eye Gaze Interaction extension. Android IL2CPP build succeeded (104.1 s); wireless install/launch succeeded on 192.168.1.208:5555. Packaged native library matches the stripped source build. Device logs confirm feature initialization, one candidate eye action and recorded queries. Final copied unworn snapshot: 3,024 queries over 64.178 s, zero valid gaze poses, all returned times equal requested times, no native drops or missing sequences. No final end status because the app remains running; analysis correctly labels session completeness unknown. This is a diagnostic smoke test, not sensor-rate evidence. Sustained wearer validation remains open.
- Task-board service was unavailable locally; this log records the work. No commit or push requested or performed.

## 2026-10-23 — 120 Hz eye-acquisition feasibility audit

- **Observed:** inspected the connected headset's latest raw-eye recording and installed HTC/OpenXR wrappers. Some five-second setup windows exceed 120 input deliveries/sec, but no sensor capture timestamps are recorded; these are not proven distinct sensor observations.
- **Open:** genuine 120 Hz acquisition requires a native source-timestamp/sequence path and sustained wearer validation. Documented API constraints and evidence in `docs/eye-acquisition-120hz-audit-20261023.md`. No runtime changes, new build, or sensor-rate claim.

## 2026-10-23 — Explicit countdown calibration and shape acceptance

- Replaced the failed-to-charge entry interaction with Start calibration, red-centered world dots, a 3–2–1 countdown at each, and timed gaze collection independent of gaze-to-dot proximity. The candidate averages three per-dot mean head-relative offsets.
- Added three independent shape checks with candidate-only gaze highlighting and explicit OK acceptance before shared guidance uses an offset. Retained raw gaze, source/validity safeguards, retry/skip, and recording of assumed fixation, per-point sample identities/errors and user confirmation.
- Updated protocol/telemetry notes; this is a development local offset procedure, not HTC calibration or task-wide accuracy validation. Qualtrics remains on hold.
- Verified legacy and guided calibration tests, entry UI/acceptance tests and shared-gaze quality checks. Final Android build succeeded (60 s), installed/launched over wireless ADB, and installed APK hash matches `75f0f34cc6bd6c9378a2d21d6331b0ddba1dfe95f6eb1d054ff438d8df98dd0f`. No fatal/null-reference startup errors in the inspected window. Wearer validation of countdown visibility, shape highlights and acceptance remains pending.

## 2026-10-23 — Three-dot entry gaze check without offset

- Replaced automatic entry offset fitting with three simultaneously visible world-fixed dots, charged in left/upper-center/right order. Each dot fills gold on its own surface after one second of valid gaze within 2.5 degrees; invalid/stale tracking, focus loss, gaps and looking away reset the active dot. Completed dots stay filled.
- No offset is fitted or applied; existing diagnostic alignment remains a separate admin tool. Retained Skip/Retry and entry outcome recording (schema 2), and updated procedure/telemetry documentation. These are development interaction thresholds, not a calibration or accuracy validation.
- Sequence checks passed for charging, completion, tracking loss, repeated deliveries, gaps, world anchoring and skipped outcomes. Android build succeeded and was installed/launched wirelessly; installed APK SHA-256 matches `636069c750d912141854310e6ff2de55242cccf7a5fd93527056fc12aa80b60e`. Wearer visual/interaction validation remains pending.

## 2026-10-23 — Wireless latest-runtime deployment

- Built the current recovered Unity checkout with entry eye alignment, then installed and launched it on VIVE Focus Vision over wireless ADB at 192.168.1.208:5555. Unity reported success in 24.4 seconds; all five headset-selection tests passed.
- Verified the installed base APK matches the build SHA-256 `4251d5a43f1f422f607d66116cc399f51f562a7916af932d983a44042c2c2313`; application process was running after launch. No fatal crash in inspected startup logs. Android startup emitted a nonfatal loadingLogo metadata type warning; wearer interaction remains to be checked.

## 2026-10-23 — Automatic experiment-entry eye alignment

- Added the existing one-fit/two-held-out-dot offset check to normal and quick-test startup, before centering, practice or trial timing. Stationary gold targets have animated collection halos; world-fixed instructions offer right-ray Skip and failure Retry.
- Preserved raw gaze and shared corrected-ray ownership. Skips/interruption get entry outcome records linked to detailed alignment reports; live setup status includes alignment progress. Closed a diagnostic-panel cleanup bug that could clear another screen's validation target.
- Kept current thesis and Trial Review work separate. Verified: QuickGazeAlignmentChecks + EntryGazeAlignmentChecks, Unity gaze measurement/controller selection checks, offline gaze-quality and guidance-state suites, compiler check and clean diff whitespace. Android build succeeded (56 s); wireless ADB installed successfully and launched on Focus Vision at 192.168.1.208:5555. Wearer verification of target visibility, ring progress, skip/retry and held-out accuracy remains pending.

## 2026-10-23 — Replay body clearance and headset-origin display ray

- **Implemented:** moved the illustrative body, lap anchor and chair upper parts 12 cm backward; kept the chair base/layout center and recorded controller poses fixed.
- Third-person orange gaze line now connects the headset face to the analytical ray endpoint. `GazeRay`, vicinity analysis, raw/corrected gaze and first-person projection retain their original origins/directions. Updated explanatory tooltips and provenance.
- Visually verified P020 Run 1 at 88.76 seconds from front/side. Updated tests to separately verify analytical and display rays, endpoint preservation and first-person behavior. Full isolated desktop build/checks passed: `Trial Review Body and Headset.app`. No headset changes or commits.

## 2026-10-23 — P020 replay torso-intersection fix

- **Implemented:** reproduced the user's P020 Run 1 screenshot at 88.76 seconds. The controller was in front of the torso, but the IK elbow was only 1.8 cm from the torso centerline, hiding the arm inside the body.
- Increased the right elbow's outward/downward preference while retaining the backward component. In the same recorded frame, elbow lateral offset is now 20.0 cm and the forearm is visible around the torso. Controller/root pose, hand contact, seat and raw telemetry remain unchanged.
- Inspected front/side renders from the actual recording; added a chest-center controller regression across torso turns. Desktop build and arm/hand/left-lap checks passed. Output: `Trial Review Arm Clearance.app` in the side-conversation artifact folder. No headset changes or Git commits.

## 2026-10-23 — Forward-bending replay arm

- **Implemented:** changed the illustrative right-arm IK elbow preference from down/outward to torso-relative back/down/outward. Close-to-chest controller poses now place the elbow behind the hand so the forearm reaches forward. Left lap pose, controller root data and ray endpoint are unchanged.
- Visually inspected side views for close and raised poses. Added regressions covering close/raised/lateral positions, 90°/180° torso turns, rewind and palm-to-controller contact.
- Isolated desktop build and regressions passed, including `TRIAL_REVIEW_FORWARD_ELBOW_PASS`. Built `Trial Review Forward Arm.app` in the side-conversation artifact folder. No headset changes or Git commits.

## 2026-10-23 — Straighter replay grip and controller-tip ray

- **Implemented:** changed the illustrative right-hand grip from a sideways wrist frame to a forward grip. Rotated the vendor controller mesh inside its recorded-pose root so the handle extends back into the hand and the emitter faces forward.
- Attached the desktop ray to the model emitter while preserving its recorded endpoint. Raw controller pose, ray origin, direction and hit data remain unchanged.
- Visually checked low, raised and rotated poses; closed a visible emitter-to-ray gap. Added regression checks for tip attachment, preserved endpoints and hidden rays. Isolated desktop build and checks passed (`Trial Review Controller Tip.app`). No headset/runtime changes or Git commits.

## 2026-10-23 — Trial Review controller grip refinement (side conversation)

- **Implemented:** visually compared wrist frames, handle offsets, finger curls and thumb placement. The right palm now sits beside the handle, fingers wrap underneath, and the thumb rests over the controls. Left fist remains on the lap and rotates with the body.
- Checked close-up and full-body renders for low, raised and rotated controller poses. These are illustrative hand poses, not measured finger tracking; recorded controller transforms are unchanged.
- Added rotation regressions for palm contact, thumb attachment and controller-pose preservation. Isolated desktop verification and build passed (`TRIAL_REVIEW_HAND_EYE_PASS`, `TRIAL_REVIEW_RESTING_LEFT_ARM_PASS`, `TRIAL_REVIEW_BUILD_PASS`).
- Built `Trial Review Natural Grip.app` in the side-conversation artifact directory. No headset/runtime changes or Git commits made in this side task.

## 2026-10-23 — Replay hand poses

- Posed the left hand as a fist at a body-relative lap anchor. Curled the right hand around the controller, reduced oversized cartoon hands, and moved the illustrative palm attachment behind the handle to keep the controls visible. Controller transforms and source telemetry remain unchanged.
- Inspected full-body and hand close-up renders. Full isolated Trial Review build passed, including right grip position/orientation and left-lap stability across turns and rewind. Reopened `Trial Review Hand Poses.app` and verified the new viewer window.
- Finger articulation is illustrative rather than measured hand tracking. Preview images and the standalone app are in the side conversation's October 23 artifact folder.

## 2026-10-23 — Keep unused replay arm attached to the body

- Trial Review now keeps the illustrative left arm in its body-relative resting pose and hides the unused left-controller model. Recorded left-controller samples remain untouched; right-controller pose and hand IK remain active.
- Added replay checks for 90/180-degree body turns, backward seeking and a tracked left controller parked away from the body. These checks and the full isolated viewer build passed.
- Built the side-thread `Trial Review Neutral Arm Fix.app` and reopened the viewer. This is a desktop presentation change only.

## 2026-10-23 — Thesis title and gaze-based workload literature

User-selected title: **Follow My Voice: Assisted Gaze-Contingent Search with a Self-Similar Agent**. Reconciled title/source metadata and added Nerella et al. (2026, DOI 10.1145/3806040) to workload literature and future spoken-assistance adaptation, preserving the self-similar voice comparison. Updated current manuscript/protocol and local IRB/Qualtrics draft title strings; no Qualtrics upload, IRB submission, or GitHub push. Historical review snapshots remain unchanged. See docs/thesis/review/title-nerella for citation evidence and build verification.

## 2026-10-23 — Eye metrics, recorded graphs, and thesis wording

Published the reviewed Missed Targets definition, exploratory eye-measurement methods, and Figure 4.5 from actual Run 30 data to Overleaf; reconciled the previously local development graphs and voice-literature additions without discarding browser edits. Added Chiossi method attribution, Duchowski IPA source and a bounded Pfeil UCF eye/head connection; the exact UCF paper the user remembers remains unidentified. Updated nine chapters to present-tense methodological language and replaced condensed-export wording with trial CSV. Both scoped Overleaf builds passed; figure and table layouts inspected. 36 eye-analysis and 41 trial-export tests passed. Detector physiological validation remains pending independent labels; no study results or 120 Hz capture are claimed. No GitHub commit/push performed.

## 2026-10-23 — Thesis compact-wall methodology alignment

- Updated Chapter 3 application design, experimental-design height constraints, data collection, and Appendix B for the implemented 30% wall-height reduction (3.148 to 2.2036 m), 0.10–2.1036 m object-center range, unchanged 168 objects/21 per plane, default 1.5 m radius and 0.20 m minimum 3D center separation.
- Distinguished earlier recorded figures from the revised layout and documented per-recording replay wall bounds without rescaling historical object positions. Comfort, visibility, and proposed vertical balance remain pilot checks, not established benefits.
- Published only four scoped source files to Overleaf, preserving concurrent/unrelated edits; verified remote revision 2d3b13541fe26fc04b3a59b211f056839bb7cfd6. Remote snapshot and local PDF rebuilt successfully; no sync conflicts. Visually checked the geometry page and verified no final-pass undefined references/citations. No GitHub commit or push.

## 2026-10-23 — Remove thesis Appendices D, E and F

- Removed D (readiness/source register), E (decision contract) and F (supporting guidance implementation) from the compiled manuscript and TOC. Cleaned all live cross-references, preserving the Jev figure in Chapter 3 and Appendices A–C. Source files remain available as excluded working material.
- Published the scoped changes to Overleaf in `65a58bb7152f5dbb9b48e0ae9f7508eb6b51f73a` after successful compilation. Preserved Claude's remote text and cleanly merged the existing local voice-literature improvements and development-result graphs; no baseline reset or unrelated upload. Local rebuild passed and removed appendix headings are absent from the PDF.

## 2026-10-23 — Publish survey format and Jev playback-gate figure to Overleaf

- Published the bubble surveys, Failure-to-Perfect Performance item with scoring documentation, and Jev final playback gate figure in scoped Overleaf commit `0f4abbeb29595908924b75d52093569e667ec969`.
- Based the publish on fresh remote revision `8dbfa76529e6023d5dc46f10f07ace9f40889c0d`; retained Claude's chapter text and inserted only the requested figure/explanation. Seven intended files changed; no GitHub push or Qualtrics edits.
- Compiled the merged remote sources before publication and verified the remote commit. Subsequent one-way sync detected only an extra trailing blank line in Chapter 3; reconciled that whitespace, preserving the text. Sync reports up to date with no conflicts, and the local PDF rebuild succeeded.
- Review evidence: `docs/thesis/review/overleaf-survey-gate-publish/`.

## 2026-10-23 — Jev final-gate figure

- Added an editable vector figure to Chapter 3 showing Unity observations/filtering, Jev phrase and speak/wait advice, qualified local feedback, and the final fresh-state playback gate. Marked the broader action mode as development-only, retained bounded waiting/fallback caveats, and distinguished actual playback from proposals. Local thesis update; no runtime, Qualtrics or live Overleaf changes.

## 2026-10-23 — Development distributions and questionnaire layout

- Reformatted actual recorded development search and speech plots into lettered distribution panels with individual observations, sample counts, quartile boxes, and explicit log-scale labels where needed. Retained all long durations and separate unfinished-search counts; no synthetic data or significance claims. Astra updated printable survey response bubbles and positive NASA-TLX Performance wording. On user clarification, Performance displays 0 Failure to 10 Perfect, and only that response is reversed once before the workload mean. Rebuilt 190 pages and inspected representative figures/surveys with no final-pass overflow or missing-glyph warnings; Qualtrics and concurrent live Overleaf edits remain untouched.

## 2026-10-23

- Added four thesis development-results figures from actual headset DEV_NEUTRAL Runs 20–28, with immutable source hashes, derived CSVs, missingness and export cross-checks. Recorded 27 started / 21 completed searches; retained unfinished endpoints. Added request-to-playback statistics and the historical 2.19 s gaze-confirmation deferral sequence to prevent conflating delivered-request latency with full response delay. Gaze charts report rendered-frame usability, not sensor accuracy or 120 Hz. No synthetic observations, voice-effect estimates, or Overleaf upload; concurrent browser edits remain protected.

## 2026-10-23

- Pulled latest Overleaf introduction/abbreviations, then strengthened the self-similar voice motivation using verified full-text evidence on game performance, agent experience, voice resemblance, and avatar voice choice. Added Kao et al. 2022 Audio Matters Too; Guo et al. already existed. Preserved exact pre-edit sources and a patch for reconciliation with ongoing Claude browser edits. No Overleaf upload or GitHub commit/push.

## 2026-10-23

- Extended the Overleaf sync to rebuild changed thesis sources automatically using the existing LuaLaTeX/BibTeX toolchain. Builds use an isolated source snapshot, back up existing PDFs, publish only successful current output, retry failures, and skip unchanged successful builds. Updated the existing 10-minute automation and added build/recovery tests (14 total passing). A real pull retrieved an Overleaf introduction edit at revision 29bbe56e8dfad9ad25e484f0ac9867eb7ed316f6 and rebuilt/published the PDF successfully in 44.85 seconds.

## 2026-10-23

- Enabled one-way Overleaf thesis source sync every 10 minutes via Codex heartbeat `sync-overleaf-thesis-to-local`. Added a tested importer with upload-baseline comparison, local-edit preservation, conflict snapshots, backups, path restrictions, and status reporting. First authenticated pull confirmed sources match. PDF rebuilding remains separate; no automatic commit, push, or bidirectional sync.

## 2026-10-23

- Imported the current thesis source and figures into a separate Overleaf project for Visual Editor editing: https://www.overleaf.com/project/6adbf46b3f96e54f5b3cc805. Configured LuaLaTeX and automatic compilation; verified Visual Editor opens the introduction and the PDF preview renders. Documented that this copy requires reconciliation with local Git edits. Original protocol project preserved.

## 2026-10-23 — Full-paper Claude language pass

- Reviewed all current thesis chapters and appendices using three verified Claude Opus 5.5 batches with the abstract and study context. Integrated 47 wording suggestions plus four clarifications; rejected five proposals that were unnecessary or could change meaning. Kept self-similar voice central.
- Preserved the abstract, acknowledgments, citations, questionnaire/quoted wording, equations, tables, figures and study definitions. Exact before/after edits and rationale are in `docs/thesis/review/claude-full-language-summary-20261023.md` and linked JSON records.
- Rebuilt 174 pages; no unresolved citations/references or overfull boxes. PDF/UA-2 passed 483,477 checks with zero failures; checked structural tags/internal destinations and four representative changed pages. PDF and source delivery refreshed. No commit/push, runtime change or Qualtrics work.

## 2026-10-23 — Apparatus wording

- Removed the optional video-passthrough statement from the thesis apparatus description; retained the VR study configuration.

## 2026-10-23 — Thesis methodology and literature organization

- Consolidated platform, method, measurement and analysis into Chapter 3 Methodology; Chapter 4 is Results, with separate performance and experience sections. Preserved detailed definitions and pending participant-results status.
- Added XR application context, explicit gaze/speech/controller rationale, and bounded relevance to low-vision assistance. Verified HTC specifications, stimulus definitions and the 17-column condensed CSV. Shortened Figure 3.2 caption while retaining recorded-source provenance in review materials.
- Qualtrics, live Overleaf and runtime unchanged. No sample outcomes invented; GPS-paper identity remains unresolved. Rebuilt 174-page PDF, checked revised layout and internal links, and passed PDF/UA-2 automated validation (483,344 checks, zero failures). Refreshed PDF and source delivery.

## 2026-10-23 — Problem statement and proposed EEG extension

- Reframed the problem statement around attention-guidance and self-similar voice research, initially unnamed target search, and performance/experience outcomes. Retained existing two-sided research questions without adding a directional hypothesis.
- Added Yang et al. (IEEE VR 2026) to the bibliography and a future-work subsection proposing synchronized EEG alongside gaze, speech events, behavior and questionnaires. Kept neural measurements proposed and visual-flicker evidence distinct from the voice comparison. Metadata verified against Crossref and the supplied first page; full-text access remains unavailable. Qualtrics and runtime unchanged. Rebuilt 171-page PDF, checked changed pages and bibliography, and passed PDF/UA-2 automated validation (465,224 checks, zero failures); refreshed delivery.

## 2026-10-23 — Introduction application and guidance framing

- Revised Section 1.1.1 to introduce VR applications and connect instructional voice presentation to performance, perceived workload and enjoyment. Reframed the Binetti, Zhang and Chiossi examples while preserving their citations and distinguishing their studies from the present voice comparison.

## 2026-10-23 — Abstract eeriness wording

- Changed the abstract to “but can also elicit greater eeriness” as requested, preserving its citation.

## 2026-10-23 — Full-manuscript language polish and recorded figure refinement

- Completed the author-requested Claude Opus 5.5 review with full manuscript and project context. Integrated 150 language edits across nine chapters and six appendices, with synthesis to preserve claims; resolved seven final-review findings. Kept the approved abstract, instruments, citations, equations, study design and source snapshots intact. Qualtrics remains on hold.
- Removed the recorded figure footer, added outer padding and captured real zoomed object views in four recorded colors. Preserved the original telemetry and source screenshots; provenance stays in the caption and metadata.
- Corrected the stale bibliography inventory count to 67. No participant results or study-approval claims were introduced.
- Rebuilt the 170-page PDF; PDF/UA-2 automated validation passes with 461,202 checks and zero failures. Inspected the revised prose and figure page, checked internal destinations, and refreshed the PDF/source delivery.

## 2026-10-23 — Recorded scene figure

- Implemented a three-panel thesis figure from actual Run 26 headset data: recorded-head first-person view, overhead arrangement, and six magnified object examples. No synthetic demonstration or generated scene data was used.
- Preserved source screenshots and hashes, labeled the 168-object layout as an earlier development configuration, and added a caption distinguishing replay reconstruction from headset video and participant results. Capture-only camera/overlay settings leave runtime and telemetry unchanged.

## 2026-10-23 — Family acknowledgments

- Added the author’s thanks to his wife, Vanessa Zameza-Poveda, and mother, Carmen Quevedo, in the thesis acknowledgments.

## 2026-10-23 — Abstract spatial-hint examples

- Added “turn to the left” and “look up” as concrete examples of gaze-informed spatial hints in the abstract, retaining the requested present tense.

## 2026-10-23 — Abstract search-layout wording

- Clarified that seated participants search for colored geometric objects in a virtual environment, with the objects arranged on eight vertical planes around them. Retained present tense and the out-of-view rationale.

## 2026-10-23 — Abstract voice terminology and tense

- Changed the self-similar condition to a “synthetic cloned voice” and converted every future-tense study-description sentence in the abstract to the present tense. Kept prior-research findings in their appropriate tense and preserved the manuscript’s explicit distinction between study design and collected results.

## 2026-10-23 — Accessible spoken-guidance rationale

- Added Coughlan et al. (2020) to the abstract, literature review and bibliography as a blind-user precedent for verbal guidance to points of interest on physical objects. Replaced the proposed comparative claim about partial blindness with wording matched to the four-participant feasibility study.
- Preserved the distinction between an accessibility motivation and validation of our gaze-contingent system for blind or low-vision users; no recruitment or runtime change.

## 2026-10-23 — Audio attention-guidance grounding

- Strengthened the abstract opening with distinct examples of VR audio alerts, AR spatialized target sounds, and spoken AR search hints. Added Sabouni et al. (2025) to the thesis bibliography and linked its attention-guidance precedent in the introduction and literature review.
- Reviewed primary full-text cue methods/results; preserved the distinction between nonverbal alerts and speech, conditional performance benefits, and self-similar voice as the study focus. Applied the final dictated wording separating completion-time performance from workload, motivation and agent experience.
- Rebuilt and refreshed the 165-page PDF/source bundle; 46 cited keys, 66 bibliography entries, no missing/duplicate keys or unresolved references. Checked affected pages and bibliography. No protocol/runtime change or Qualtrics synchronization.

## 2026-10-23 — Dictated abstract and thesis navigation revisions

- Rebuilt the 165-page PDF; checked title, abbreviations and TOC layout, chapter/appendix numbering and internal links. PDF/UA-2 validation passed 451,191 checks with zero failures. Prepared all accumulated changes on this branch for the author-requested main sync.
- Applied author-directed abstract wording on tone/voice identity, agent assistance, self-similar naming, hidden target, gaze context, right-controller trigger selection and common hint guidance. Removed the requested similarity/debrief sentence and draft-results disclaimer from the abstract; draft status remains explicit elsewhere.
- Enabled chapter/section/subsection numbering, including letter-prefixed appendix headings, and added a 19-entry abbreviation list compiled from active manuscript terms. Retained cited works’ original reality settings and corrected an overlooked passthrough description of our planned VR task.
- Confirmed Searching Across Realities was in BibTeX but not cited in active chapters; added a bounded introduction connection to object search and attention measurement rather than voice-effect evidence.

## 2026-10-23 — Author-supplied front matter

- Added B.Sc. in Computer Science below the author name and Dr. Roshan Venkatakrishnan as major professor using the supplied UCF class commands. Used the professor spelling already present in the project bibliography.
- Added acknowledgments immediately after the abstract, thanking Dr. Venkatakrishnan for thesis guidance/support and Merav for IRB-process assistance, following the author's final instruction to include it. No surname, degree institution or degree year was invented.
- Confirmed that a dedicated list of abbreviations is not currently present; no such section added solely in response to the question.

## 2026-10-23 — Thesis terminology: virtual reality

- At the author's request during abstract review, changed this study's title, abstract and narrative from mixed reality to virtual reality. Revised the apparatus description to a virtual room while retaining truthful documentation that passthrough is an available software option; the final study configuration requires release verification.
- Preserved cited studies' original AR/MR contexts and the prior-headset-experience questionnaire. No Unity setting or device build changed. This is a manuscript decision, not evidence of runtime reconfiguration.

## 2026-10-23 — Complete thesis accessibility and independent review

- Completed independent full CE manuscript review against the supplied submission checklist; no actionable manuscript findings remained. Ran a separate follow-up review for PDF export repairs, with independent adversarial fallback after the Claude route failed before content transmission.
- Fixed five missing explicit CID font maps in three diagrams, duplicate imported transparency-group dictionaries, and low-contrast gold diagram labels. Documented figure-export dependencies. Preserved neutral/self-similar condition labels, pending participant results, and historical source snapshots.
- Rebuilt the 163-page thesis and passed veraPDF 1.30.3 PDF/UA-2: 1,728 rules and 431,298 checks passed, zero failures and no parser warnings. Verified all fonts embedded, 14 figure alternatives, 17 tables with headers, chapter structure and caption links. Performed sampled visual/reading-order checks; formal automated validation is not institutional approval or an exhaustive assistive-technology usability test.
- Saved validation and review receipts, refreshed PDF/editable-source delivery, and prepared the requested thesis-only local commit. Author metadata, actual results, final study validation, and institutional approval remain open; Qualtrics and live Overleaf remain unchanged.

## 2026-10-23 — Neutral-voice terminology and manuscript checklist

- Renamed the comparison condition to neutral voice throughout active thesis text, tables, captions, questionnaire options, and three editable diagrams. Catalog now describes the supplied selection pool; participant choice remains part of the design. Added an explicit definition so neutral does not imply affective neutrality or verified absence of perceived similarity.
- Preserved retrieved Overleaf snapshots and historical review records. Added the supplied UCF Manuscript Submission Checklist to the full-paper review context. Fixed table-header and chapter-heading tags, externalized longtable captions to remove merged header cells, and corrected the bibliography count. Rebuilt and checked PDF structure, caption destinations, citation keys and representative layouts. Final accessibility and institutional review remain outstanding.

## 2026-10-23 — Introduction follows the guidance-to-voice argument

- Reorganized Chapter 1 motivation from VR/out-of-view guidance to practical object search, auditory/verbal assistance, Guo et al.'s Doppelgänger findings, and the rationale for self-similar versus participant-preferred catalog voice.
- Added a dedicated auditory-guidance subsection and explicit transfer limits (AR versus desktop/VR; speech versus tones; embodied collaborator versus a voice without a visible body). Kept the study questions and shared guidance controls intact; aligned the problem statement with this argument.
- Saved exact motivation before/after in `docs/thesis/review/introduction-guidance-flow-20261023.json`. LuaLaTeX/BibTeX rebuild passed without undefined references or overfull boxes; refreshed delivered PDF, introduction source and source bundle.

## 2026-10-23 — Doppelgänger rationale in the abstract

- Added the approved Guo et al. (2024) connection between voice identity, likability/believability, and eeriness to the thesis abstract. The paper motivates the spatial-guide comparison without implying it established a search-speed benefit.
- Preserved the participant-selected catalog comparator, proposed-study tense, and other manuscript edits. Exact change saved in `docs/thesis/review/abstract-doppelganger-20261023.json`.

## 2026-10-23 — Guidance-first abstract

- Revised only the thesis abstract to open with the longstanding VR attention-guidance problem, out-of-view objects, and source-bounded evidence for auditory/verbal AR search assistance. Added existing Grogorick, HiveFive and See or Hear citations.
- Kept self-similar versus participant-selected catalog voice as the empirical comparison, with shared gaze guidance as apparatus. Retained proposed-study tense and explicit absence of participant results.
- Exact before/after preserved in `docs/thesis/review/abstract-guidance-first-20261023.json`; earlier Claude chapter edits preserved. LuaLaTeX/BibTeX rebuild passed: 159 pages, one-page abstract, no undefined citations or overfull boxes; delivered PDF refreshed. Built-in compiler remains incompatible with this template’s DocumentMetadata.

## 2026-10-23 — Apply Claude Opus narrative review

- Ran a dedicated Claude Opus 5.5 reviewer against the abstract, all nine chapters, and literature/narrative notes; recorded actual model provenance. Applied 18 exact-edit suggestions (two with documented synthesis), plus structural cleanup of analysis sections, count definitions, and contextual references.
- Preserved the candidate model, protocol parameters, instruments, figures, and pending-results status. Statistical and study-readiness decisions remain explicitly unresolved. Rebuilt the 159-page PDF; citation checks and changed-page/reference visual checks passed. Refreshed delivery; no commit, live Overleaf changes, or Qualtrics changes. Full changes in `docs/thesis/review/claude-narrative-review-20261023.md`.

## 2026-10-23 — Apply the self-similar assistance story throughout the thesis

- Revised abstract, all nine chapter openings/transitions, the introduction problem statement, and discussion/conclusion to connect useful object search with self-similar versus participant-preferred guidance. Kept technical architecture as supporting apparatus and mixed outcomes interpretable.
- Preserved all protocol parameters, statistical specifications, instruments, figure assets, bibliography, and pending participant-results status. Updated narrative-plan provenance; rebuilt the 159-page PDF, verified citations, and inspected the 41 changed pages plus references. Refreshed PDF/source package for the requested local commit. Qualtrics and live Overleaf remain unchanged.

## 2026-10-23 — Compare research narrative structures

- Reviewed introduction-to-discussion structure in Kao, Guo, Binetti, and Zhang using full texts. Saved a sourced narrative blueprint in `docs/thesis/review/narrative-architecture-20261023.md`.
- Proposed bringing the self-similar versus preferred-voice question onto the first page and reducing early implementation detail; preserved mixed-outcome possibilities and study boundaries. This is an editorial proposal, not a manuscript or protocol change. Compiled thesis remains at checkpoint `b377dbd`; Qualtrics remains on hold.

## 2026-10-23 — Carry the voice rationale through the introduction and discussion

- Reworked the introduction around practical AR/VR search motivations from Binetti, Zhang, HiveFive, and Two-Step Gaze Guidance; distinguished application examples from demonstrated deployment outcomes.
- Connected that motivation to the value of comparing self-similar and participant-preferred voices. Extended discussion of conditional design implications, engagement versus efficiency, and transfer from the controlled hidden-target task.
- Preserved protocol, instruments, analysis, and pending-results status. Source provenance is recorded in `docs/thesis/review/practical-motivation-and-voice-throughline-20261023.md`. Compiled and visually checked the 159-page draft; no missing/duplicate citation keys, undefined references, or overfull boxes. Refreshed PDF/source delivery and included the accumulated thesis work in the requested local checkpoint. Qualtrics remains on hold; no push.

## 2026-10-23 — Ground the self-similar voice rationale in full-text evidence

- Read the six selected voice/audio-guidance papers through primary full texts or author copies. Expanded Chapter 2 around self-similar versus participant-preferred voice, separating performance, engagement, favorable experience, and eeriness; extended the interpretation in Chapter 8.
- Distinguished gaze-contingent sonification, spatial audio, audiovisual cueing, and spoken opening instructions. Supporting design connections are explicit adaptations, not claimed replications or additional treatment factors.
- Added source links and evidence locations in `docs/thesis/review/audio-guidance-and-self-voice-20261023.md`. Preserved protocol, instruments, pending results, live Overleaf, and Qualtrics hold. LuaLaTeX build and citation audit passed; inspected revised pages and all references, and refreshed the 157-page PDF/source package. No missing/duplicate citation keys or overfull boxes. No commit or push.

## 2026-10-23 — Add RippleVision as bounded visual-guidance context

- Verified final RippleVision metadata against Crossref and PubMed; added `kudnick2026ripplevision` to the active thesis, manuscript, and protocol bibliography files, preserving historical snapshots.
- Added a Chapter 2 comparison that keeps self-similar voice central and distinguishes visual guidance from the shared spoken policy. Saved source links, transfer boundaries, and follow-up questions in `docs/literature/ripplevision-2026.md`.
- Full text could not be retrieved through the checked publisher/author routes. The discussion explicitly relies on the published abstract; detailed study methods, thresholds, statistical findings, and figures are not transferred. Full-text review remains open.
- Recompiled the thesis and refreshed PDF/source delivery: 155 pages; citation audit has no missing or duplicate keys. Inspected the added discussion and bibliography. No live Overleaf, Qualtrics, or runtime changes; no commit or push.

## 2026-10-23 — Center the thesis on self-similar voice

- Reframed the abstract, contributions, chapter openings, research-question wording and conclusion around self-similar voice compared with a participant-preferred catalog voice. Voice identity/experience now leads the literature; voice preparation leads the shared apparatus chapter.
- Moved detailed state-machine/memory/cancellation and Jev policy-pilot/diagnostic material into supporting Appendix F. Participant voice results now lead Chapter 7; pending results and all protocol/instrument decisions remain explicit.
- Read the supplied Yang et al. embodied AR navigation preprint as context. Added a bounded literature comparison of interface versus mechanism, without transferring embodiment findings or changing the study.
- Rebuilt and visually checked: 154 pages, approximately 28,625 words, 14 figures, 16 tables and six appendices. No undefined references/citations, LaTeX warnings or overfull boxes; original instruments and figure assets retained. Updated PDF/source delivery. Local manuscript edits only; not committed or pushed. Live Overleaf and Qualtrics remain on hold.

## 2026-10-23 — Synthesize Opus 5.5 thesis language review

- Applied 99 reviewed paragraph revisions across the abstract, Chapters 1–8 and decision-contract appendix, improving sentence connections and grammar while preserving instruments, counts, timing definitions and pending-results status.
- Added bounded synthesis on gaze-informed context (IEEE VR 2025), performance-specific feedback (ISMAR 2025), and task time versus system latency (IEEE VR 2026). The 2026 timing citation is limited to the inspected primary abstract; full-text access was blocked. Preserved provenance and exact accepted edits.
- Compile-checked source matches the integrated TeX inputs byte-for-byte: 148 PDF pages, 14 figures, 16 tables, no unresolved citations/references, LaTeX warnings or overfull boxes. Formal ETD/accessibility approval and empirical results remain pending.
- Runtime checkpoint passed automated verification and Android Build And Run installed/launched over Wi-Fi at 192.168.1.208:5555. Desktop viewer including the neutral mannequin builds successfully. This verifies software build/launch, not wearer perception or headset eye-tracking accuracy.

## 2026-10-23 — Align thesis structure with Truong reference

- Added stable research-question IDs and evidence mapping, a comparative literature table, explicit development/held-out evaluation separation, controlled policy comparisons, a failure taxonomy, worked diagnostic examples, and question-specific conclusion status.
- Added a fifth appendix for the inspected Jev contract, bounded memory fields, admission requirements, and reproduction manifest. Hashed three October 23 working-source files and distinguished that inspection from the October 20 deployment audit.
- Applied Claude's review, then rebuilt and visually checked the added material: 145 pages, approximately 27,006 words, 14 figures, 16 tables. Final references resolve and fonts are embedded; formal ETD/PDF accessibility checks remain pending.
- Participant outcomes and personal acknowledgments were not invented. Live Overleaf, Qualtrics, and runtime files remain untouched. Updated local PDF/source delivery and prepared the requested local checkpoint. The author identifies this as the October 23 format-review draft with participant results pending; official UCF guidance requires a near-final document, so format-review acceptance remains unconfirmed.

## 2026-10-23 — Publish thesis first pass

- Added the editable UCF-template thesis, compiled 124-page PDF, 14 figures, 11 tables, bibliography, preserved Overleaf source snapshot, and Claude review/verification records under `docs/thesis/`.
- Matched the reference contents style; distinguished synthetic demonstration figures, implementation snapshots, proposed measures, and missing participant results.
- Documentation-only checkpoint: no runtime deployment or live Overleaf/Qualtrics update. The draft references the recovered implementation audit, not a claim that all runtime changes are merged here.

## 2026-10-23 — Neutral Trial Review avatar

- Replaced the replay avatar's skater texture with a uniform gray mannequin material, retaining the existing rig, seated pose, headset and controller anchors. This remains an illustrative body, not a reconstruction of participant appearance.
- Isolated desktop build and its existing verification checks passed; rendered preview inspected. Output: `Trial Review Neutral Avatar.app` in the side conversation's October 23 artifact folder. The currently running viewer and headset build were left undisturbed.

## 2026-10-23 — Recovered runtime and protocol checkpoint for main sync

- Included recovered Unity, Trial Review, protocol, and offline Jev pilot work alongside the thesis, as requested. Kept Jev-led action policy restricted to development quick tests; Qualtrics remains on hold.
- Verified 275 analysis tests, 71 Trial Review Python tests, 16 script tests, spatial voice/state/gaze/preparation/enrollment/isolation/layout suites, desktop Trial Review build, and Unity StudyVerificationChecks. Fixed two obsolete test expectations and made the fake-Unity compile-hook fixture handle Python executable paths containing spaces.
- Unity integration verification requires graphics: the initial `-nographics` attempt crashed in a camera-render fixture; the graphics-enabled retry passed. No headset perceptual or acoustic-latency result is inferred from these checks.
- Preserved a backup of Unity-generated preloaded-assets cleanup outside the repository and restored its tracked configuration. Excluded the generated native crash dump. Historical IRB generator checks describe an older packet and do not establish current protocol approval.

## 2026-10-20 — Full protocol handoff and runtime alignment

- Reconciled the latest full handoff with fresh Overleaf sources, preserving Claude’s Section 3 and propagating reviewed corrections into both aggregate documents. Resolved unsupported memory/blink placeholders from actual code, qualified spatial/acoustic claims and wired the Bovo citation. Synchronized the 62-entry working bibliography. All 19 source/assets files match live revision `f9f49e450c7b64b34c319a557114f1fa4393f003`; dedicated Overleaf checkout/index preserved.
- User resolved practice voice: one practice using the first assigned block voice (Order A preferred catalog, Order B self-similar). Updated schedule version/start wall, progress counts and both audio-preparation loops. Block40 now goes to rest/comfort/tracking checkpoint without a survey; study completion hands off to final external questionnaires. Closing speech no longer directs participants to the old headset TLX. Development quick tests retain their separate legacy survey/identity.
- TLX scoring preserves explicit raw 0–10 or legacy 0–100 scales, native complete-six mean and separate labelled 0–100 conversion. Final retrospective block joins and mixed-scale plots are tested; raw items/missingness remain unchanged.
- Tests: voice/schedule/audio isolation, central guidance and gaze-quality shell checks pass; 38 trial-export and 7 TLX tests pass. Unity RayMenuChecks and full StudyVerificationChecks pass. Updated outdated reflection/cache-injection fixtures to the central state-machine architecture; integration also caught and fixed obsolete second-practice voice-library generation.
- Eight relevant standalone/aggregate documents compile; figures and resolved reference page visually checked. Tectonic bibliography passes still emit rerun/bbl-consistency notices, with resolved citation/output present. Updated existing GitHub tasks #7/#8/#10/#13/#16/#18/#20/#26/#27; retained history and left acceptance gaps open. Qualtrics was not opened or changed.
- Required build/deploy harness completed: Android build succeeded in 84.2 seconds (1161.4 MB), Wi-Fi streamed install returned Success, launch Status: ok, package update 20:58:15 and process 28196. This confirms packaging/install/launch, not wearer gameplay, native120Hz or acoustic validity.
- Remaining: exact pre-cue cross-to-target geometry; constrained20above/20below cross-height schedule after bounds/deadband pilot; automatic IMI/Guo subscale scoring; power/N/model and calibration/proficiency/stop criteria; consent/service lifecycle review; complete-session wearer/export acceptance. See `docs/protocol/2026-10-20-alignment-audit.md`. Unity repository edits remain uncommitted; only the authorized Overleaf document revisions were published.

## 2026-10-20 — Live speech preservation and detached review

- Fixed a reproduced catch-up path that skipped complete short spoken intervals. Live scene, subtitles and audio now advance together through received speech; missing WAVs get a bounded five-second wait with a visible unique-gap count. Buffered pause/end tails are labeled separately from fresh headset activity.
- Added **Wi-Fi settings → Review window** and equivalent launch options. The independent, initially muted window uses a frozen complete-record prefix plus available played WAVs; it can browse other saved runs while the main receiver continues.
- Added regression checks for skipped/late/unavailable audio, cancellation, record-prefix snapshots and independent cursors. Full isolated Unity viewer verification/build passed. A real recording under simulated 1.5-second network batching improved software-start coverage from 24/30 to 30/30 segments, with all 30 starting at offset zero. This is not measured headset/acoustic latency.
- Verified real application launch of the detached snapshot window, then a separate four-trial archive at trial 2. Closing the first detached process left the main receiver connected. The UI button and launch option share the same launch routine; native mouse automation did not reliably activate Unity controls.
- Fresh live wearer/audio verification remains open; the connected headset reports paused. No headset runtime or participant guidance policy changed in this viewer fix. Ran the repository’s requested refocus/build-device harness after editing: Android build succeeded, Wi-Fi install succeeded and launch completed. Desktop viewer verification is separate from wearer validation.

## 2026-10-20 — Development-only Jev action policy

- Added switchable quick-test action selection (orient/refine/correct/encourage/wait), preserving local confirmations, state/revision validity, prepared audio and final playback checks. Participant policy stays baseline; resetting participant clears the switch.
- Recorded policy/action identity in state and Jev telemetry; Trial Review displays policy and action choices; evaluator maps action IDs to wording without confusing a proposal with audio playback.
- Fixed an over-conservative first action prompt through real API probes. Final 40-request scripted comparison: 20/20 valid responses per arm, about 179 ms median each. This is Mac HTTP timing, not wearer or acoustic evidence. Added transport, eligibility, progress/recovery and evaluator tests.
- Android build installed/launched over Wi-Fi; installed APK hash matches. Updated Trial Review built and opened live; headset is currently paused/waiting for wearer signal.
- See `docs/testing/2026-10-20-runtime-jev-actions.md` for evidence and paired-headset checklist. Wearer benefit and participant-policy promotion remain open.

## 2026-10-20 — Fresh-clone quick test and live viewer audio controls

- Added a third development shortcut, **Quick test — new cloned voice**: record a fresh sample through the existing enrollment flow, create a new ElevenLabs clone, prepare that voice only, then enter development trials. It clears any in-memory clone identity before enrollment, blocks preparation while enrollment is pending, and records `new_clone_quick_test` with `study_data=false`. Existing saved-clone and neutral shortcuts remain available; startup buttons fit the panel.
- Added persistent desktop viewer mute and 0–100% volume controls in the Wi-Fi toolbar and transport. They affect viewer audio only (including recorded effects), not the headset. Gain changes do not seek or restart speech.
- Fixed live replay stopping audio at short received-buffer underruns despite a fresh live signal. Genuine pause, stale tracking/transport and disconnected states still stop playback. The reviewed recording had all 30 referenced spoken WAVs present; this does not establish that each arrived before its live playback interval. Late audio after an interval has elapsed remains a live-view limitation.
- Validation: voice isolation and enrollment suites passed; refreshed Unity VoiceChoiceChecks passed including fresh-clone button, arming and panel bounds. Viewer Python tests (70) and isolated Unity checks/build passed, including uninterrupted audio across brief live-buffer gaps. Android build succeeded in 55.2 seconds; Wi-Fi install returned Success and launch Status: ok (process 26728).
- Opened the updated Trial Review build in live mode, visually verified toolbar placement and Wi-Fi connection; headset currently reports paused. UI automation did not verify mouse toggling in the Unity player, so the wearer/operator should confirm mute/slider interaction and live spoken playback. New microphone capture and actual new-clone synthesis still require the user's recording. A viewer startup stall cleared after restarting the newly built app; no cause established.

## 2026-10-20 — Wi-Fi-first Unity build/deploy harness

- Added `scripts/headset-adb.py`: reconnect a saved wireless endpoint, prefer an online Focus Vision over Wi-Fi, verify model, reject ambiguous/unauthorized devices and retain explicit serial for installation and launch. Single USB headset remains a fallback. Endpoint is local under ignored UserSettings; no shared ADB-server resets.
- Codex Build And Run now preflights the connection, builds, then installs and launches with the selected serial instead of Unity's remembered/default device. The refocus script checks wireless connectivity first. Updated build guide and agent preferences so future sessions try wireless before asking for a cable.
- Validation: five selection tests passed; shell syntax and changed-file whitespace checks passed. Real Unity build succeeded in 18.0 seconds, ADB streamed install returned Success over `192.168.1.208:5555`, and launch returned Status: ok. Verified running process 27501 and package update timestamp 2026-10-20 17:42:51. This verifies wireless build/install/launch, not wearer gameplay. Wireless debugging may need re-enabling after headset reboot; authorization is not bypassed.

## 2026-10-20 — Counterbalancing legend spacing

- Refined padding and row spacing in procedure figure v9; visually checked the result and saved project PNG/prompt and Desktop copy. Added a figure index and linked the current illustration from the protocol decision document. Preserved the existing distinction between illustrated object wording and the operational eight-trials-per-angle allocation. No runtime, protocol-decision or Qualtrics changes.

## 2026-10-20 — Figure typography and stimulus consistency

- Refined procedure figure v8 with consistent typography and aligned counterbalancing rows. Checked ChallengeSet's current red/blue/yellow/green palette and shape catalogue; replaced mismatched object colors and added stars/cylinders to wall illustration with more regular geometry. Inspected PNG and saved prompt, project asset and Desktop copy. No runtime or protocol changes.

## 2026-10-20 — Armless chair and unbranded headsets in figure

- Edited procedure illustration v7 to remove chair armrests and blue triangular icons from the standalone headset and participant's rear strap. Preserved physical straps, participant arms, controller, rays and procedure text. Visually inspected generated PNG; saved project asset, prompt and Desktop copy. No runtime changes.

## 2026-10-20 — Procedure figure icon cleanup

- Removed the cube badge and gaze/sound icon from the two lower-right information rows and aligned their text to a shared left inset. Saved inspected v6 PNG and generation prompt under docs/figures, with a Desktop PNG copy. All procedure content remains unchanged; no runtime or Qualtrics changes.

## 2026-10-20 — Final-only questionnaires and protocol handoff reconciliation

- **Protocol proposal / user decision:** one practice trial; retain two counterbalanced 40-trial blocks and a between-block rest/comfort/tracking check; move both condition-specific questionnaire sets to the final survey. Baseline demographics/SSQ and setup auditions remain. Final SSQ is collected immediately after headset removal, before retrospective sets. Qualtrics stays on hold and was not accessed.
- Preserved the side-thread battery: 6 TLX + 24 IMI + 21 selected Guo + 14 custom numeric ratings per referenced block (65 each, 130 total), identical within-set wording/order, retrospective scope/timestamps, individual Guo appealing/eerie/familiar ratings and removal of four accent questions. Documented recall/burden considerations and separate setup ratings.
- Read current Overleaf sources and preserved collaborator Section 3. Updated run sheet, full questions, scoring, protocol, Experiment/Analysis/Limitations and combined manuscript plus vector procedure figure. Published only these ten files to Overleaf as `5728aa0`; synchronized local sources, readable questionnaire/operator copies, study hub, measures register, decision/register notices and archival warnings. Old submitted/generated packet drafts remain historical, not current participant materials.
- Updated illustrated figure v5 on Desktop and in docs/figures: practice, Block 1, break, Block 2, final questionnaires/debrief. No runtime changes or headset deployment. Runtime single-practice/survey gates, exact pre-cue theta/export provenance, TLX scale metadata and vertical quotas remain implementation checks; practice voice assignment remains open.
- **Validation:** eight changed LaTeX sources compile with Tectonic; built-in compiler also passed run sheet and both survey sources. Fixed a wide survey table and visually checked administration page and updated vector flow. Source checks contain no remaining two-practice or immediate post-block administration instructions in current documents. Final-only timing is a documented protocol change, not evidence of institutional approval or device behavior.

## 2026-10-20 — Single practice trial decision

- **Protocol proposal / user decision:** change familiarization to one practice trial total, replacing one per voice. Updated study illustration to Practice / 1 trial (v4 in docs/figures and Desktop). Runtime schedule and run sheet have not been changed in this figure-edit turn; practice voice remains unspecified.

## 2026-10-20 — Figure caption and angle labels

- Removed the bottom seated-MR-search caption, retained removal of the bar-graph icon, and added the absolute angle list (0°, 45°, 90°, 135°, 180°) beneath the user-requested objects-per-theta label. Saved v3 in docs/figures and on Desktop. Illustration only; no runtime changes.

## 2026-10-20 — Study figure revisions

- Revised illustration: black headsets, gaze-contingent voice guidance label, removed shared-policy row and bar-chart icon. Bottom label now reads “8 objects at each θ” per user request. Saved v2 in `docs/figures/` and on Desktop; original retained. Figure edit only, no change to actual trial allocation or object spawning.

## 2026-10-20 — Experiment procedure illustration

- Generated a paper-style study-flow PNG from the current run sheet: consent/setup, two practice trials, two counterbalanced 40-trial voice blocks with block surveys/break, and final survey/debrief. Includes a schematic seated MR task cutaway, right-controller selection, and balanced absolute wall-angle counts. Saved image and generation prompt in `docs/figures/`, with a Desktop copy. Illustration only; no runtime or protocol changes.

## 2026-10-20 — Stateful Jev sequence pilots and scheduling comparisons

- **Implemented (offline only):** JSON-lines bridge executes the production C# guidance state machine and bounded speech memory against scripted observations. Added four-policy sequence pilots, recorded-response replay with identical parsed-payload checks, advisory whole-sequence Astra review, and paired early-repeat-gate/event-wake experiments. No participant runtime edits or headset deployment in this experiment.
- **Measured:** 302 real Mac-to-Vercel calls across three pilots, with no transport/schema failures; deadline overruns remain recorded separately. Initial 146-call comparison: all-Jev simulated confirmation median/max 0.80/1.40 s versus 0/0.60 s with local overrides. All six flagged all-Jev starts were valid at request time and stale at delivery, not six demonstrated wrong model choices. Four all-Jev qualification episodes remained unconfirmed.
- **Experimental improvements:** skipping already-blocked repeats reduced requests 31 to 10 with 14 simulated starts in both arms. Event-triggered cancellation/wake reduced all-Jev simulated median confirmation 0.80 to 0.40 s, flagged starts six to zero and unconfirmed episodes three to one, but increased calls 48 to 67. Its remaining missed gaze acknowledgment waited for speech, was cancelled on controller entry, then hit a request deadline before pointing superseded it. Longest delivered confirmation was 1.05 s: 0.80 s existing speech plus 0.25 s Jev.
- **Limits:** movements, audio starts/durations and scheduler timing are simulated; requests start only while audio is idle. The experimental polling interval is 600 ms versus the current app's 100 ms guidance interval. These pilots do not demonstrate equivalent production speedup, acoustic latency, naturalness or participant benefit. The two-second issue was not reproduced among delivered simulated confirmations; headset resolution remains unverified. Astra is advisory and reviewed the initial comparison, not the follow-ups.
- **Validation/artifacts:** 12 new sequence tests and 89 existing guidance analysis tests pass; pure C# bridge compiles. Results and representative timelines in `docs/testing/2026-10-20-jev-stateful-sequences.md`; source hashes and saved requests under `analysis/pilots/20261020-*`. Imported comparison into Trial Review's existing Evaluation tab (Refresh pilot comparison), preserving older variants and a backup; standalone report saved on Desktop. Task-board service was unavailable at localhost:3456, so task update remains pending. No commit or push this turn.

## 2026-10-20 — Jev payload audit, speech memory, audio gate and clone quick start

- **Implemented:** bounded decision memory records at most four actual software playback starts/outcomes and four phase transitions within 15 seconds; detached snapshots go to Jev, and guidance resets clear history. Queued/proposed lines are not recorded as spoken. The existing local spatial/overshoot/turn memory remains authoritative.
- **Implemented:** joint Jev requests factor identical candidate metadata without losing semantic fields. Explicit busy-audio and central `newGuidanceAllowed` gates are described in the payload. Unity enforces the central flag before automatic guidance requests, after asynchronous advice and before queued playback. Setup/success/repeat paths and existing interruption rules retain their own lifecycle. Snapshot v2 is recognized alongside v1 in analysis, and the viewer shows whether the new-guidance gate was recorded/open/closed.
- **Measured:** 150 synthetic production-payload HTTP probes across three paired batches. Final 54-request batch: baseline/compact/compact-plus-memory median 158/149/153 ms; p95 491/361/225 ms; all responses schema-valid. Explicit gate checks improved from 14/18 to 18/18 in both revised forms. One baseline response exceeded 450 ms; none in the revised forms. One-entry history fixtures were 5,322 versus 5,479 median bytes; full bounded histories can be larger. This is not a demonstrated latency advantage, participant helpfulness score or headset/acoustic measurement.
- **Evaluation correction:** rescored the earlier 314-network-request four-policy pilot by evidence of helpfulness, not exact expected-action matching. One all-Jev response clearly opposed the supplied +45° target; useful partial directions/trigger reminders are not wrong. Wait opportunities, uncertain usefulness and unnecessary speech remain separate. Fresh Astra sequence review is advisory and does not establish a full-menu advantage. Frozen histories and Python reference policies are explicitly distinguished from actual Unity execution.
- **Implemented:** startup includes “Quick test — last cloned voice.” Explicit development reuse of the last successfully enrolled ElevenLabs clone, clone-only verified preparation, `DEV_CLONE`, one 40-trial block, no practice and the same centering/readiness/developer controls. No-clone/audio-failure states remain blocked with setup or retry/cancel guidance. Normal participant setup does not reuse the saved development ID. Older builds may require one new enrollment to populate the shortcut.
- **Validation:** spatial voice contracts, semantic reconstruction, memory bounds/expiry/reset, gaze quality, central state and voice isolation checks pass; 89 guidance Python tests pass; enrollment/client tests pass. Fixed the pre-existing semantic export omission of the three already-authored between-round cross prompts. The isolated Trial Review build passed with recorded-run verification. Final Android Build And Run passed (127.3 seconds), installed and launched on the USB-connected Vive Focus Vision. Installed base APK SHA-256 matches the built APK; process running and the bounded AndroidRuntime error check was empty. Wearer timing/naturalness checks remain pending.
- **Artifacts:** `docs/testing/2026-10-20-jev-payload-memory.md`; synthetic requests/results in `analysis/pilots/20261020-payload-memory/`; updated desktop diagram under `~/Desktop/Guidance State Machine — 2026-10-20/`. Checkpoint remains `4cbc1c8`; these newer changes are not committed or pushed.

## 2026-10-20 — Live viewer audio transfer priority

- Diagnosed Run 27: 10 recorded playback events referenced WAVs not yet present locally, including controller reminders and success speech/chimes. The receiver copied the large unused library before the spoken clips; the viewer correctly did not replay those clips after their recorded intervals.
- Receiver now promotes recent recorded playback assets within the current package and reserves one urgent atomic WAV transfer even if replay ingestion exhausts the normal 250 ms batch budget. Restarts seed recent references from a bounded replay tail. Recorded times and source files remain unchanged.
- Regression reproduced starvation with 500 prepared clips and a 300 ms replay transfer, then passed after the fix; all 19 receiver tests pass. This improves transfer priority, not a guarantee of zero network latency or audible playback across dropouts.
- Isolated macOS viewer build and its renderer/audio/real-recording checks passed. Relaunch stalled in Documents stop-marker access; user asked to check the macOS permission dialog. Audible live verification remains pending.

## 2026-10-20 — Fixed seated human in Trial Review

- **Implemented:** anchored the illustrative chair to each trial layout center, with height fixed from the first valid head sample. Preserved swivel/head rotation and recorded controller/gaze data; orbit camera no longer follows head translation. Added regression checks for center stability and no mutation of raw poses. Isolated Mac build passes, including trial-switch/rewind stability, raw-pose preservation, arm/gaze regressions and 48 camera samples from Run 26. Visually checked the centered seated participant in Run 26 at 14.83s. Final app: `~/.codex/visualizations/2026/10/20/fixed-seat-final/Trial Review.app`. Restored live mode and visually verified Wi-Fi-connected Run 27 advancing with current poses and speech.

## 2026-10-20 — Checkpoint and autonomous Jev action comparison

- Saved requested local checkpoint **26c8b96** before experimentation. No push.
- **Implemented (offline only):** typed action-choice pilot with explicit eligible actions and shared sanitized current state/history; existing speak/wait baseline, interleaved repetitions, recorded retry sequences and mirrored scripted cases. Confirmation/selection/invalid-input checks remain local. No participant build change.
- **Measured:** 212 real Mac-to-Vercel requests across exploratory and revised batches. Revised batch: median235/236ms baseline/action policy; p95366/672ms; 2/58 versus3/58 beyond450ms. All116 revised responses valid. Revised model chose encouragement10/10 eligible scripted opportunities and the recorded post-acknowledgment return correction2/2; no human-perceived or playback improvement established. One retry sequence is counted as one episode, not seven failures.
- **Validation:** 11 focused tests pass. Detailed comparison, constraints and reproduction in `docs/testing/2026-10-20-autonomous-jev-pilot.md`; results in `analysis/pilots/20261020-actions/comparison.json`. Headset A/B and action-provider integration remain proposed; current device build stays at the tested checkpoint behavior.

## 2026-10-20 — Between-round cross instructions

- **Implemented:** replaced the generic round announcement with three deterministic rotating prompts to look at the cross and hold gaze until the next round begins. Both voice conditions prepare the cues; practice disclaimer and the existing five-second gaze-relative reminder remain. No trial timing or stimulus randomization changes.
- **Validation:** gaze-quality and voice-isolation suites pass, including prepared audio coverage for all three cues and deterministic rotation across 40 rounds. Android Build And Run succeeded in 154.1 s; Unity confirmed installation and launch on VIVE Focus Vision FA5AR3N00385. Wearer verification of the new cues remains.

## 2026-10-20 — Paired guidance pilots, controller ordering, and visible-reference consistency

- **Checkpoint:** saved the starting implementation locally as `7299312` before parallel runtime, pilot and evaluation work. Earlier comparison source is `492be9d`.
- **Implemented:** central guidance suppresses contradictory search actions on fresh controller-target evidence before the hold qualifies; final playback rechecks that evidence, and already misleading search speech fades over 150 ms. Gaze acknowledgment no longer consumes the separate pointing acknowledgment. Trigger selection remains separate.
- **Implemented:** Run 26 exposed a visual/spoken reference mismatch: the highlighted blue capsule is below the target cube while the raw gaze ray remains above both. Same-wall unique-highlight evidence now suppresses an opposing vertical component using apparent elevation, retaining a valid horizontal cue or staying quiet. Raw gaze, target confirmation, and height-magnitude bands are unchanged; reference and suppression evidence are logged.
- **Implemented:** upcoming acknowledgments use their intended 100 ms post-speech gap, rather than the preceding ordinary line’s 250 ms gap. Wrong-selection feedback is shortened to “Try another object.” Prepared audio and final transmission paths are covered.
- **Measured policy comparison:** 20 identical scripted trajectories produced 7/7/0 controller-conflicting eligibility episodes across earlier/checkpoint/fixed versions; synthetic conflicting starts were 6/9/0. Seventy-five frozen recorded-request probes produced 4/4/0 conflicts. These are software-policy comparisons, not participant outcomes or headset/acoustic latency.
- **Measured network probe:** 12 Mac-to-Vercel Jev requests, all valid choices; median 163 ms, p95/max 498 ms, one beyond the runtime’s 450 ms deadline. Small and full eligible sets are both five; medium 15 requires additional valid authored options and remains untested.
- **Evaluation correction:** the first-observed qualification metric finds two remaining Run 25 delays (1.957 s and 1.171 s) that request-only timing hid. Existing speech and scheduling caused them, not Jev waits. Only 6/21 episodes have sufficient earlier qualification evidence; 15 remain unknown. A bounded 12-case review plus four targeted Astra sequence reviews are advisory and preserve deterministic delay flags. Local review cases are separate from counterfactual Jev benchmark cases.
- **Implemented:** Trial Review’s Evaluation tab imports paired comparison results and shows first-observed timing separately. Live preparation uses a separate atomic heartbeat before any trial exists, with real progress when known, pause/error handling and freshness expiry. Provider-side clone progress is now explicitly unknown rather than 0% presented as measured completion.
- **Verification:** runtime state, gaze-quality, voice-isolation and spatial-voice suites pass; 65 guidance-analysis and 69 viewer Python tests pass; new preparation-heartbeat tests cover initialization, atomic replacement, progress, pause/resume and trial transition. Independent review found and resolved episode-boundary, apparent-elevation and final-guard issues. Final builds/deployment are recorded below when complete.
- **Deployment:** final Android Build And Run succeeded (103.4 s). Installed APK SHA-256 matches the local verified build: `651bc92b962de1c515bb0c64edb326264768c92840cf46897c358450b54877d6`; launched PID 11478. The connected headset reports asleep, so live preparation-heartbeat and wearer behavior are not yet observed on-device. Final isolated Mac viewer build also passed its Unity checks and real Run 26 reconstruction (48 sampled poses). After the user approved renewed macOS Documents-folder access, Run 25 opened successfully; playback/pause and the Evaluation sidebar with imported paired-policy comparison were visually checked. Restored Live mode at 192.168.1.208:5555 and verified Wi-Fi connected, 5392/5392 files hash-verified, zero transfer backlog. The viewer correctly shows “Headset app paused” instead of replaying stale motion; resumed wearer behavior remains to test. Automatic Astra jobs were disabled for this verification launch. No permission bypass was attempted.
- **Evidence and remaining wearer checks:** `docs/testing/2026-10-20-guidance-comparison.md` and `analysis/pilots/20261020/README.md`. New headset timing, calibration accuracy, acoustic onset, perceived naturalness and behavior remain unverified until a fresh wearer run.

## 2026-10-20 — Faster live receiver and bounded correction delays

- **Implemented:** replay-only polling, background archive inventories/hash checks, linear-time transfer ordering, unchanged-file reuse with periodic local rechecks, and once-per-second manifest checkpoints. Historical link discovery now reads at most 128 KiB per file per pass and persists complete-line offsets together with selected folders. Copied bytes remain fsynced; partial lines, source shrinkage, archive identity and hash mismatch protections remain intact.
- **Evidence:** a real Wi-Fi scan covered 5,502 files in 1.712 s versus 30 replay files in 0.253–0.484 s. Profiling exposed 82.6 s spent parsing historical links in one reconnect pass. After bounded parsing, measured receiver cycles were 1.070 s initially and 0.530/0.739/0.932 s afterward. A subsequent 12-second viewer observation remained connected with 12 status updates (median 1.075 s, maximum 1.447 s) and zero replay backlog. These are receiver timings, not end-to-end headset latency; a transient real Wi-Fi disconnect also occurred during testing.
- **Transport:** enabled capability-checked gzip only for JSONL/CSV chunks of at least 1 MiB. A 2 MiB headset replay chunk transferred as 231,161 bytes with identical SHA-256; two compressed reads took 0.733/0.337 s versus 1.052/1.880 s raw. A 256 KiB test was slower compressed, so smaller chunks and WAVs stay raw. Compression is transport-only; archive files remain unchanged.
- **Viewer:** built and opened `live-wifi-steady/Trial Review.app`. The waiting screen now distinguishes connection loss, catch-up, tracking loss, paused headset and completed/stopped runs. Visually verified archive catch-up completes and the latest paused run displays “Headset app paused” with Wi-Fi connected.
- **Guidance:** run 025 contained repeated Jev waits for “Look slightly left” around 85.69–88.36 s. Qualified target-departure, bearing-overshoot and wall-return cues now use the existing local priority path after audio is available. Ordinary transmission deferral is capped at one second instead of three. Interruption approval, phrase-ending protection, geometry revalidation and the separate three-second same-direction repeat guard remain in effect.
- **Validation:** 67 Python viewer/export/receiver tests pass, including slow hashing, slow archive scans, bounded/resumable historical parsing, restart and byte preservation. Desktop build and Unity viewer checks passed. Voice-isolation tests verify each return-correction reason reaches prepared playback within 0.2 simulated seconds when idle and reject stale evidence; the one-second fallback is tested for standalone and joint catalog decisions. Android Build And Run succeeded (611.5 s); installed APK matches SHA-256 `23fcb5fe6e8b1ba88e172826132bde79cbda1c1660d693d7dfbf958b36e03949`, app running on device (PID 14480). Real wearing/turning timing remains a device check.

## 2026-10-20 — Short conversational turn-progress cues

- **Implemented:** added prepared “More, more.”, “Keep turning.” and “A bit further.” lines to both voice conditions with authored progress semantics. The central turn state tracks recent measured progress and explicit-direction age. Short cues require a new unconsumed turn milestone, recent correct-route movement and a direction spoken within six seconds; revalidation rejects stopped/reversed/stale movement before audio starts. Explicit directional wording remains the fallback.
- **Validation:** new checks cover both routes, reversal, stopping, tracking recovery and expired direction context. Gaze-quality, guidance-state, spatial-voice and voice-isolation suites passed; Unity feedback checks also confirm every new line is in the starter and full preparation libraries. Combined Android build succeeded (533.2 s), installed APK matches SHA-256 `a87baf2595a02d123edfc7889a2d8dfd6ef05083c4313122ab1d5805df9d8e73`, and app is running on the Vive (PID 30402). New lines use the existing synthesis/verification preparation pipeline; wearer assessment of delivery remains open.

## 2026-10-20 — Faster guidance eligibility

- **Implemented:** reduced the ordinary hint interval, Jev wait-retry delay and post-acknowledgment pause from 0.6 to 0.1 seconds in both voice conditions. Updated Jev instructions and runtime documentation. Ordinary post-speech gap (0.25 s), stable-direction qualification, duplicate suppression and interruption gates remain unchanged; 0.1 seconds is eligibility, not a guaranteed acoustic response time.
- **Validation:** voice-isolation, guidance-state, gaze-quality and spatial-voice suites passed, including 90/110 ms timing-boundary checks. Timing-only build succeeded (346.6 s), then the combined short-progress build was verified installed as recorded above.

## 2026-10-20 — Correct-selection burst and confetti

- **Implemented:** correct targets explode into 100 fragments with full spherical coverage, plus 120 double-sided, five-color confetti pieces. Fast outward motion slows into a tumbling fall and shrinks away within 1.8 seconds. Both effects share the pause-aware lifetime, own and release their meshes/materials, have no colliders or rigidbodies, and leave stimulus randomization untouched.
- **Validation:** extended the existing Unity feedback checks for all six burst directions, confetti geometry/motion, and resource cleanup; Unity editor checks passed, including the existing feedback suite. Android Build And Run succeeded in 307.5 seconds. Installed APK matches local SHA-256 `c8bfb4a3c20de283aba0f483e4fd326e98fe4df9ceb2d613f9fb8a50c2dd26b8`; app running on headset (PID 14147). Visual feel remains a wearer check.

## 2026-10-20 — Voice setup loading progress

- **Wording:** changed the loading subtitle to “Preparing experiment,” preserving the loading bar and percentage.
- **Wording deployment:** whitespace checks passed; automatic Build And Run could not start because macOS could not find Unity’s Tools menu. The subsequent burst/confetti build includes this subtitle and is verified installed on the headset.
- **Implemented:** a left-to-right UCF-yellow loading bar with a numeric 0–100% label in regular voice setup and neutral quick test. Other setup/error screens hide it; graphics do not intercept controller rays.
- **Implemented:** progress comes from completed download/verification jobs, prepared clips, level matching, manifest persistence and replay-audio preparation. Stage weights are 45% download/check, 45% clip preparation and 10% finalization; this is work progress, not an estimate of remaining seconds. Provider-side clone creation stays at 0% because it exposes no measurable completion percentage. Regular setup still prepares its starter library first and loads remaining optional prompts afterward.
- **Validation:** voice-isolation/download regression suite passes, including added bounds, monotonicity, cached reset, intermediate progress and failed-audio-not-100% assertions. Unity Android Build And Run succeeded (436.1 s). Installed APK matches local SHA-256 `65e3a092d5e6dfb218035ca482157b8a49f20c20ae17e6d6eced608a0f865113`; running PID 2968. Headset visual inspection remains a wearer check.

## 2026-10-20 — Sequence-aware confirmation evaluation

- **Implemented:** evaluation follows recorded qualification requests through waits and playback, separates gaze confirmation from controller confirmation, and shows sequence latency in Trial Review. Tracking gaps, invalid observations, lifecycle changes and ambiguous IDs break episodes. One second is an engineering review target, not a validated perceptual threshold. Timing begins at the first recorded qualified request, not an inferred sensor onset.
- **Implemented:** corrected controller-confirmation phrase metadata: fresh gaze in the target vicinity plus a qualified controller hold; a separate gaze hold is not required. Historical recorded metadata remains intact.
- **Rerun:** all 86 Run 20 cases passed Astra result validation (11 batches, sequence rubric v2). There were 17 confirmation episodes: 12 played within one second, two exceeded it, and three lacked sufficient playback evidence. Delayed episodes took 1.382 and 2.175 seconds with two idle Jev deferrals each. Astra now identifies these sequence delays and no longer falsely rejects controller confirmations for missing the gaze hold. Seven timing-incorrect decision ratings describe two delayed episodes plus two densely repeated cues; they are not seven independent delays. Repetition remains flagged in 12 decision ratings. All interruption ratings remain unknown.
- **Validation:** 53 guidance-analysis/judge tests, 61 viewer/export tests, spatial-voice checks and voice-isolation checks passed. The existing priority-confirmation path starts prepared audio within 0.2 simulated seconds even if the advisor would wait; this is not an acoustic headset measurement. Final isolated Mac viewer build succeeded. Android Build And Run subsequently succeeded (803.1 s). Installed APK SHA-256 `ecb6bfe154f767ad06997b4223a65b941d1e8101dc55b987cb10d8ae8903e3fe` matches the local build; running PID 25029. Updated Trial Review is open in live mode, connected over Wi-Fi and archiving Run 21. Fresh wearer timing validation remains open.
- **Review:** completed CE review `20261020-120308-144741ae`, no remaining findings after invalid-tracking/gap and duplicate-identity fixes. Final evaluation package suffix `c781be5e82729732`; the live rerun's validated cache was reused after verifying all 86 sanitized model inputs were unchanged by those fixes.
- **Deferred at user request:** the reported controller/go-back ordering investigation was suspended after the user identified an older installed version. Its temporary test was removed; no runtime change was made for that report. Test the latest build before changing current pacing or interruption rules further.

## 2026-10-20 — Central guidance state, replay inspection and offline evaluation

- **Implemented:** one pure C# guidance state machine owns qualification, named phases, action priority and short-term turn/target history. Unity adapters observe once per frame; async callbacks revalidate without advancing the machine. Pending provider requests and unsounded cues share a scheduler; action consumption happens at actual playback start. Jev is behind a provider interface. Existing validity gates and pilot hold thresholds are preserved.
- **Implemented:** replay records versioned state and distinct direct-gaze, vicinity-winner, controller-hit and target identities. Trial Review shows the state, transitions and separate markers, supports backward seeking, and keeps legacy/invalid/stale data unavailable. Lifecycle states can survive the last pose sample without retaining stale hit markers.
- **Implemented:** local automatic evidence checks, blank human-review worksheets and frozen request-only benchmark inputs; optional, default-on-for-this-request Astra evaluation through saved Codex ChatGPT authentication; separate versioned/hash-linked results and an Evaluation tab. Exact selected/proposed wording remains distinct from recorded playback. Failed/unknown evidence never becomes a passing score. No automatic training or production candidate-count change.
- **Fixed during review/testing:** local decision snapshots read their actual nested field and require a nonblank phase; positive return-action priority/consumption regressions; independent guidance evaluation for incomplete legacy summary exports; long sidebar values/buttons; following an existing evaluation after viewer reopen. A failed/pending worker status does not assert another worker is alive without evidence.
- **Verification:** all four runtime suites passed; 61 viewer/export Python tests and 42 guidance-analysis/judge tests passed. Isolated Mac builds passed editor checks and reconstruction of 147 saved poses across seven trials. Android build-only succeeded in 104.4 seconds; APK SHA-256 `bf726e6b8cc470c85e9d1767abda7fb31ce09d80b7a869b90a534e392a294491`. Headset disconnected, so this build is not installed.
- **Model integration:** a real single-case smoke, a revised twelve-case Astra batch and the complete Run 20 evaluation passed result validation: 11 successful batches, 86 rated decision cases. Earlier full-run batches failed strict validation; the contract now requires exact batch cardinality and case-specific evidence, with fixed rejection categories. Pipeline success does not mean all hints were correct. Model judgments are advisory, not human ratings or eye-tracking validation.
- **Final desktop verification:** the final Trial Review build reconstructs the saved run, renders the evaluation sidebar without overflow, and supports keyboard playback/pause. GUI automation could not reliably activate Unity IMGUI mouse controls; seek/join behavior passed editor tests, while physical mouse interaction remains on the pilot checklist. No exception/error entries were found in the final viewer log.
- **Review:** completed CE review `20261020-105155-a01227e5`, nine lenses, no open actionable findings after two caller-applied fixes. External Claude transport failed before source egress; local adversarial fallback completed. Supplemental outgoing-prompt tests now cover sensitive strings inside retained nested state, candidate and answer fields. Read-only Codex sandboxing is not a proven data-isolation boundary; no-tools/no-files remains a prompt restriction.
- **Documentation:** Desktop state-machine diagram with editable Mermaid/source; research note with direct IEEE VR/ISMAR references and evidence limits; headset checklist in `docs/testing/2026-10-20-central-guidance.md`. Small/medium/full choice-set comparison remains a documented future engineering experiment. New wearer tests are still needed for timing, naturalness, actual calibration and audio delivery.

## 2026-10-20 — Jev choice-set comparison decision

- **Protocol proposal, approved for engineering evaluation:** compare current small, medium (up to 20), and full real catalog requests on matched recorded states. Preserve local hard validity and final playback checks; broaden Jev's conversational choices where supported.
- Recorded the paired evaluation, provider-limit check, latency/failure/invalid-choice metrics and human wording review in `docs/plans/2026-10-20-jev-choice-set-comparison.md`. No live comparison has run and no production choice limit changed. Prefetch remains a separate proposal.

## 2026-10-20 — Run 20 voice-guidance checkpoint and pacing corrections

- Saved all preceding project changes in local checkpoint `5b3e8fd` before these fixes; no push.
- **Implemented:** locally qualified gaze/controller confirmations, stable wall arrivals, turn milestones and sustained-gaze reminders bypass an additional idle Jev transmission decision. Current evidence, prepared voice-scoped audio, lifecycle cancellation and playback gaps remain mandatory. Interruption approval remains separate.
- **Implemented:** same semantic ordinary direction/magnitude requires three seconds after actual directional playback completion, including provider timeout and joint selection. New direction/event and new-trial resets remain eligible. Opening speech participates in this history.
- **Implemented:** track actual completion of all speech contexts for Jev's elapsed-silence field; unknown history is null. Selection reminders use trigger-only wording when already aimed, and cancel if controller state changes before commitment. Shortened arrival leads, preserving vertical/diagonal geometry. Exported phrase semantics regenerated from production policy.
- **Evidence:** Run 20 had a 2.19-second qualified-confirmation delay, three timeout prompts 1.76 seconds apart, and three aiming reminders while already aimed. These motivate the regressions; they are not proof of improved acoustic latency on the new build.
- **Validation:** voice-isolation and gaze-quality regressions pass, including priority event rejection after stale evidence, post-playback gaps, both advice routes, timeout repetition, changed directions, new trials and actual speech-end context. Spatial semantics/networking checks also pass, including every prepared phrase matching its exported metadata. Android build-only APK succeeded in 64.6 seconds. Build And Run was attempted but deployment was blocked by a disconnected headset; the new APK is not installed. Wearer evaluation remains open.

## 2026-10-19 — In-app eye-tracking failure feedback

- **Implemented:** automatic status card in the study prompt style for unavailable gaze. Uses the same `SharedGazeStream.TryGetValid` decision as readiness/guidance; identifies missing left/right/both eyes only from fresh native data, and separately reports stopped updates, unavailable data or head tracking. Appears after one second of continuous loss, clears after 0.35 seconds of stable recovery, and resets while the application is inactive. Two-second startup grace avoids initialization warnings.
- The non-interactive card follows below the center of view and cannot block controller ray input. Existing trial timing and gaze validity remain unchanged; the card does not invent gaze or auto-pause a measured trial. Warning/recovery transitions are logged.
- **Verification:** gaze quality regression suite passes, including brief blinks, repeated gaps, stable recovery, focus/resume and stale native flags. Android Build And Run succeeded (42.6 s for final build); installed and launched PID 19450. Android immediately paused/stopped the activity, so in-headset presentation and live warning/recovery remain wearer checks. Startup-order review removed a dependency on another unordered AfterSceneLoad callback. No commit/push; unrelated pending changes preserved.

## 2026-10-19 — Readiness cross blocked by unavailable left-eye tracking

- Inspected live DEV_NEUTRAL run 020 replay and raw eye observations after the cross would not charge. Audio announcement was ready, readiness was active and unpaused, and progress remained zero. Across 9,276 observations at app time 60–110 seconds, EXT reported untracked; HTC reported left eye invalid and right eye valid throughout, with valid head tracking and application focus. The sampled left-eye openness was zero. Later samples reported both eyes invalid.
- The existing binocular validity gate therefore rejected the gaze; no valid ray reached the cross. This identifies the immediate blocker, not the cause of the headset's left-eye tracking failure. Next wearer check: refit headset, ensure both eyes are visible to sensors, run HTC eye calibration, then retry. No validity bypass, monocular fallback, code change, or redeployment performed.

## 2026-10-19 — Repair look/slightly audio preparation failure

- Device setup stopped at 551/551 after three rejections of Eric's “Look slightly right.” Scribe returned “Looks slightly right” on each attempt. Reproduced with the existing voice/model at final-repair stability. Adding a comma pause after “Look” produced the exact expected transcript without altering words or weakening verification.
- Final repair attempt now inserts that punctuation boundary for `look slightly` in the shared neutral/clone synthesis request builder. Earlier attempts, voice/model identity, cache keys, filler rejection and exact-word verification remain unchanged. Added left/right regression assertions; confirmed failure before the change and passed the full voice-isolation/download suite afterward.
- Generated one corrected Eric clip, verified its real Scribe transcript with production `SaveReceipt`/`HasReceipt`, and installed the MP3/receipt in the headset cache. Device hash matches `a9921867488a165b598f8e4cd6e01cb2777ea51f2186614c1354bcc947bc840d`. No participant recordings or other cached clips were deleted. Unity reopened; Android Build And Run succeeded in 234.9 s and launched PID 7266. APK SHA-256 `dcda7cf6d1d031657df273ead43a619a96ca0d308eccfd198c09ad501c6e99c8`. Wearer setup continuation/listening remains to be checked. No commit/push; unrelated edits preserved.

## 2026-10-16 — Condensed whiteboard CSV in Trial Review

- **Implemented:** automatic desktop run export now writes `trials_condensed.csv` with 17 whiteboard performance/subjective columns and stable trial/block IDs. It projects the existing authoritative export without changing recordings, measurements, missing values or source participant identity. Full CSVs remain available for outcome/quality flags and catalog reassignment. Height is explicitly target minus head at initial tracked pose; the separate subjective-performance column stays blank until its instrument mapping is confirmed.
- **Implemented:** separately attached catalog file, an Open condensed CSV action, schema/column definitions in manifest and checksum in the export receipt. Reopening/regenerating creates a versioned export; prior packages remain unchanged. Live generation keeps the existing run-end/verified-transfer gate.
- **Verification:** 60 desktop Python tests pass, including projection, missing/zero/signed/quoted values, source identity and corruption detection. Unity desktop checks/build pass, including persisted/deduplicated catalog attachment. Packaged-app testing caught and fixed a missing bundled column dictionary. Rebuilt app under `visualizations/2026/10/16/trial-review-condensed/Trial Review.app` automatically generated P020's 22-row/17-column CSV and persisted its catalog link. All mapped cells match the full export; this historical run has no NASA-TLX scores. Native pointer automation did not reliably activate viewer buttons, so manual button interaction remains unverified.
- Ran the requested Unity refocus/Android Build And Run command; dispatch succeeded, device deployment not yet verified. This change is desktop-only. No commit/push; other pending edits preserved.

## 2026-10-12

- Implemented up to five eligible navigation choices. Added 75 catalog rows (15 new spoken variants across existing distance bins), preserving the four tones and all geometric/prepared-audio gates. Catalog is now 375 rows; completion feedback retains its separate 2–4 eligibility filter.
- Added request-ID-linked Jev replay traces: exact state/candidates, accepted answers and supplied probabilities, latency, timeout/error/cancellation. Headers and credentials are never recorded. Playback and application outcomes remain separate from model selections.
- Trial Review now includes a Jev lane and inspector, probability bars, hover explanations for view and transport controls, and metadata on phrase hover. Legacy recordings explicitly mark unrecorded candidates/probabilities; joins never infer playback from matching text. Added `--jev` to open the inspector directly.
- Verification: standalone networking/voice tests and 58 Python reader tests passed; Unity spatial checks cover 375 phrases and five-candidate coverage across the signed grid. Desktop build checks cover late/out-of-order responses, exact playback IDs, malformed/legacy telemetry, timeouts and stale outcomes. Four synthetic live Gateway requests returned five probabilities and correct speak/wait choices; cold first response 826 ms, subsequent 335–387 ms (not a headset latency measurement; existing deadlines/fallbacks unchanged).
- Deployment verified: final Android build-and-run succeeded, installed APK hash matched (5e80c357…), PID 28119, no startup Unity/AndroidRuntime errors. Built and opened updated desktop viewer; synthetic Jev panel/probability bars inspected. Native pointer automation was unreliable, so a human hover/click smoke check remains. Details: docs/testing/2026-10-12-jev-viewer/README.md. Preserved all pre-existing work; no commit or push.

## 2026-10-11 — Explicit round-completion voice feedback

- **Implemented:** expanded the completion catalog to 13 lines, including explicit correct-object/round-complete feedback and three quick-find variants. Jev receives `round_complete`, post-selection phase, correct right-trigger selection, search-finished status, trial/block position, previous feedback, pause-aware duration, errors, instruction/repeat counts and planned turn magnitude.
- **Implemented pilot rule:** quick wording is eligible only for a measured non-practice trial with positive search time at most 10 seconds and zero incorrect selections. This is a configurable code constant/wording heuristic, not a validated performance norm. Other rounds retain recovery, persistence, first-selection or generic feedback. Candidate filtering, prepared/verified clips, exact-choice responses, nonrepetition, 600 ms deadline and local fallback remain.
- **Verification:** 2,688 fact/history combinations and all 8,192 prepared-clip subsets pass; 54 synthetic live Jev scenarios produced 53 timely eligible responses and one cold-start timeout handled by local fallback. No generic answer when a preferred specific candidate was available. Unity StudyFeedbackChecks and voice-isolation checks passed, including completion snapshot identity/history and inclusion of all 13 lines in quick/full voice preparation. After an editor/player layout mismatch, restarted Unity and reran the newly compiled feedback checks successfully. Android Build And Run passed in 75.5 seconds; installed/launched at 14:46:47 with local/device APK SHA-256 matching `12c7ae0e6482c60ed8ab06448fd98a8d2ae679707dd0b048440fbe3e165238e1`. New voice clips enter existing per-voice generation/verification during setup; headset listening remains to be checked. No commit/push.

## 2026-10-11 — Reduce Jev guidance latency

- **Implemented:** coarse turns combine phrase selection and speak/wait into one typed Jev request, eliminating the second serial network round trip. Local geometry, prepared audio, voice/session identity, stale-result rejection, wait cooldown and bounded deferral remain authoritative. Fine guidance and interruption behavior are preserved. Joint telemetry marks its path so the same network request is not counted twice.
- **Implemented:** one cancellable synthetic connection warm-up after voice preparation. It does not block trials, transmit participant data or authorize audio. Deadlines remain unchanged.
- **Measured:** local snapshot segment median 0.206 ms (p95 0.962 ms, one 98.576 ms spike). Final desktop live A/B median 454 ms sequential versus 224 ms combined; 12/12 joint decisions correct, no timeouts in that warmed sample. Earlier cold run had one first-call timeout. This is not yet an Android failure-rate result. Detailed experiments in `docs/testing/2026-10-11-jev-latency/`.
- **Verification:** spatial/voice-isolation regression checks and Unity spatial checks passed. Android Build And Run passed in 112.8 seconds; installed/launched on Vive at 14:38:53. Local/device APK SHA-256 matches `e689a973639cb7ebde190d2e9835ebe42bebc65b13b4e8741b2d6086ab0ac47f`; no Unity/AndroidRuntime errors in the initial new-process log. New headset latency/failure-rate measurement awaits a trial. No commit/push.

## 2026-10-11 — Prevent silent guidance deferrals and add viewer subtitles

- **Diagnosis:** latest archived DEV_NEUTRAL run (17:36 UTC) ended its last guidance at 70.54 s, followed by repeated successful Jev wait responses through 97.70 s. At 98.26–98.30 s eye tracking became untracked/both eyes invalid; guidance then correctly suspended. This distinguishes the initial advisory silence from the later tracking loss. Some catalog calls timed out, but timeout fallback did produce audio.
- **Implemented:** clarified the Jev idle-speech criteria, explicitly separated a pending decision from busy playback, and bounded repeated valid deferrals to three seconds before a logged, revalidated local fallback. No override for invalid gaze, stale geometry, changed voice/session, or unprepared audio. The original prompt alternated low-confidence wait/speak in a synthetic long-silence correction; revised wording selected speak in three repeated probes. Voice regression checks pass; provider latency can still exceed the 400 ms deadline.
- **Implemented:** Trial Review subtitles overlay the reconstructed scene, enabled by default with a Subtitles toggle. They use recorded phrase text and start/end/cancel/pause events, work without WAV assets, ignore chimes, and follow scrubbing/rewind. They describe authored speech, not a microphone transcription. Subtitles are hidden while waiting for live signal.
- **Verification:** isolated Trial Review 2.17 build and subtitle/replay checks pass; voice isolation and spatial contracts pass. Native viewer screenshot confirms the recorded subtitle at 69.70 seconds. Android Build And Run succeeded in 156.8 seconds; installed/launched on Vive at 14:04:05, with matching local/device APK SHA-256 `20b5b875710b597c50e4a080b2f26cbef79b32319cfddd47f7f4fb326667a22a`. Startup logs show existing unsupported lip-tracking and AR plane-subsystem diagnostics, with no voice exception observed. Viewer reopened in live mode; subjective headset listening remains to be checked. No commit/push.

## 2026-10-11 — Prepare live replay voice clips before playback

- **Implemented locally:** export the final treated/normalized voice library during Loading, then admit new background/on-demand clips before playback; reuse each exported clip in replay start events. Preparation keeps the recording header/origin/clock deferred until the first trial transition.
- WAV publication is atomic. Wi-Fi prioritizes prepared audio before bulk telemetry and yields between batches so new playback/stop events are revisited; the viewer refreshes its audio plan immediately on new events. Guidance never waits for desktop acknowledgement.
- Transfer priority and bounded-batch regression checks pass (13 Python bridge/adapter checks). Added Unity coverage for preloaded audio, empty preparation streams, cursor offsets, uninterrupted refresh and cancellation, plus recorder header/export reuse. Desktop build and headset compile/deploy verification in progress; end-to-end listening remains a hardware check.

## 2026-10-11 — Faster guidance cadence for headset pilot

- **Implemented:** ordinary hint interval 2.0→0.6 seconds; post-speech gap 0.55→0.25 seconds; post-confirmation gap 1.0→0.6 seconds; direction/wall-entry settling 0.35→0.25 seconds; offline stalled-turn reminder 4→3 seconds. Jev catalog choice deadline 900→450 ms with the existing prepared local fallback.
- Applies equally to both voice conditions. Speech playback rate, 150 ms fade, interruption protections, gaze validity, target confirmation/controller hold, milestone/repetition gates and audio verification remain unchanged. These are pilot pacing choices, not validated measurement thresholds.
- Added a state-aware Jev speak/wait decision before idle automatic guidance (400 ms deadline), alongside the existing interruption decision. Wait consumes no hint/milestone state; late or stale approvals are discarded; provider failure uses a locally revalidated prepared cue. Requests record their current pending phrase and recheck catalog angle/distance bounds before committing.
- **Verification:** voice isolation/transmission, gaze quality and spatial contract checks pass; Unity Study Integration and Spatial Navigation checks pass after fixing a C# local-name collision. Live Jev probes returned the expected wait choice for 3 scenarios; 5/8 calls exceeded the 400 ms budget and used deadline handling, so live decision coverage is incomplete. First Build And Run stopped because no device was connected; reconnected Vive over Wi-Fi. Retry succeeded in 628.2 seconds and installed/launched at 13:33:13 (PID 26416); local/device APK SHA-256 matches `7cd73c920a32a15e5e734fc0e0cd8afb57a8da5d17f8f92681b5c16c98f0888c`. Startup Unity/Android error query was empty. Pilot listening should compare response speed and boundary chatter.

## 2026-10-11 — Simplified live waiting message

- **Implemented locally:** removed the “Your live trial will appear here when the headset connects” subtitle and recentered the pyramid/title group. Retained the rotating pyramid, Waiting for connection title and UCF logo.
- **Verification:** Trial Review 2.16 isolated build and existing replay checks passed. Native light-theme live screen confirms subtitle removal and recentered pyramid/title. Resumed the existing headset endpoint after waiting for the prior receiver to stop; launchers updated. No headset changes, commit or push.

## 2026-10-11 — Live mode switch

- **Implemented locally:** replaced the top-right Follow live action with a labeled Live mode switch. Gold/right indicates live following, gray/left indicates off; the full control is keyboard-focusable. On connects/resumes; off pauses the cursor and audio while preserving background archiving. The Wi-Fi panel retains Stop receiver and now calls its resume action Resume live. Seeking continues to turn following off.
- **Verification:** isolated Trial Review 2.15 build and existing replay checks passed. Native light-theme screenshot confirms label, switch proportions and off state. Updated three launchers, left P020 open in 2.15. Restored the prior headset endpoint from the normal archive after the previous isolated localhost waiting-screen preview had persisted its test address. No headset build, commit or push.

## 2026-10-11 — Repair quick-test audio verification failures

- **Root cause confirmed on device:** Eric/Eleven Turbo v2.5 returned incorrect speech for “Next, turn left approximately 165 degrees” and “Okay, look left.” The strict verifier blocked setup after three identical-configuration retries. Re-transcription of the rejected cache reproduced a wrong angle and unrelated speech/fillers.
- **Implemented:** bounded repair retries now spell integer angles, specify English and progressively increase stability (0.85, then 1.0 with speaker boost disabled). Neutral and cloned voices share the repair request builder; voice identity/model and exact-word verification stay unchanged. Exhausted failures name the affected phrase and log bounded transcript diagnostics. No unchecked audio fallback.
- Live repair checks: angle line passed at stability 0.85; short left cue still contained a filler at 0.85 and passed at stability 1.0 without boost. Production C# verification/receipt checks passed for both; repaired bytes and matching receipts installed in device cache. Automated voice isolation, retry payload and ElevenLabs client/enrollment checks pass. Live 36/36 phrase checks passed across all 12 catalog voices (short cue, numeric angle, directional continuation); Eric reproduced both faults and recovered at attempts 2/3. New participant clone audio remains subject to per-clip verification; no claim of exhaustive library or human listening validation. Android Build And Run succeeded (291.4 seconds); installed/launched on Vive FA5AR3N00385 at 12:12:07. Installed/local APK SHA-256 matches `31273a7e3e779090fd980bdc26cb42af68a6518d5242a97fef7b9882780b4ef5`.
- Refreshed the generated guidance semantics export to match the current directional continuation phrases.

## 2026-10-11 — Gold pyramid live waiting screen

- **Implemented locally:** replaced the live no-signal text block with a shaded gold pyramid rotating at 12 degrees/second and gently floating above “Waiting for connection.” Added the unmodified official stacked Bold Gold UCF logo at the viewport’s bottom-right; official image/page provenance recorded alongside the asset. Dark/light palette behavior retained.
- Animation runs independently of replay time, is drawn only in the waiting state, and uses an isolated camera with its prop deactivated immediately after each render. No changes to transfer, acquisition, recorded data or headset UI.
- **Verification:** isolated Trial Review 2.14 build and existing replay checks passed. Two native screenshots of a deliberately disconnected localhost receiver confirm distinct pyramid orientations, legible text and logo placement. Used a temporary receiver archive; no headset network settings changed. Updated launchers to 2.14 and restored P020 saved replay after the preview. No Android build, commit or push.

## 2026-10-11 — Signed wall labels centered on forward

- **Implemented:** main simulation and desktop trial viewer label forward as 0°, the right side +45°/+90°/+135°, the left side −45°/−90°/−135°, and the shared rear wall ±180°. This changes presentation only; wall IDs, spawn positions, recorded azimuths, theta scheduling and guidance geometry are preserved.
- **Verification:** isolated Trial Review 2.13 build/replay checks passed; native P020 overhead view visually confirms all eight labels. Updated all three desktop launchers and README. Unity Build And Run succeeded in 177.7 seconds after retrying the refocus script; installation and launch on Vive FA5AR3N00385 confirmed (process 24431). No commit or push.

## 2026-10-11 — Larger overlay indicators and reconstructed detection feedback

- **Implemented locally:** doubled scene-toggle circle typography to 26 points while retaining gray backgrounds and bold selected labels. Added independently toggleable target detection ring (cyan outside / green when the target is the unique closest candidate) and hue-preserving 1.6x gaze shine with a gold rim; right-controller blue rim has precedence. Ring visibility and shine are independent. Invalid/stale gaze clears feedback and backward seeking rebuilds the appropriate scene colliders.
- Reuses the live angular comparison and collider recipe through the desktop preparation adapter. Bounds are explicitly estimated: recordings lack saved collider dimensions. Latest preceding measured guidance padding is used within each trial, otherwise 6 degrees; footer labels provenance. Desktop rim is a lightweight shader approximation of the headset material. Original replay hits, guidance and source files remain untouched.
- **Verification:** 3 adapter tests and isolated Trial Review 2.12 build/replay checks passed, including new overlap/tie, invalid/stale, ring-toggle, controller-rim, backward-seek and source-immutability assertions. Native P020 screenshot confirms larger indicators, cyan target ring and object shine. Updated launchers to 2.12. No headset build, commit or push.

## 2026-10-11 — Neutral gray scene-overlay toggles

- **Implemented:** the eight scene-overlay controls keep the same neutral gray background when enabled or disabled in both themes, including hover, focus and press states. Enabled controls use a bold label and filled radio-style indicator (◉); disabled controls use a normal label and outlined circle (○). Each remains independently toggleable; other UCF gold selection controls are unchanged.
- **Verification:** Trial Review 2.10 isolated build and existing replay checks passed. Native dark-theme screenshot confirms bold/normal labels, filled/outlined indicators and gray backgrounds. Same style construction follows the existing light-theme palette. Opened 2.10 on P020 and updated the three launchers. No headset deployment or commit/push.

## 2026-10-11 — UCF dark and light viewer themes

- **Implemented:** black/charcoal default interface with UCF gold (#FFC904) selected controls, headings, timeline markers and header rule. Added top-bar Light mode / Dark mode switch and persistent local ViewerLightTheme preference; optional --theme light|dark launch override does not overwrite the preference. Light mode uses warm white surfaces, dark text, darker gold headings and black text on gold selections. Fields, hover/focus states, browser/catalog styles and timeline tracks share the palette; rebuilding styles releases old textures.
- Recorded object colors, gaze display color, data and 3D scene lighting remain independent of interface theme. Dataset names, replay time and playback state are not reset by the toggle.
- **Verification:** Trial Review 2.9 isolated build and existing replay checks passed. Both palettes visually inspected on P020 via launch overrides. Automated mouse clicks still do not activate Unity UI controls, so actual button-click/persistence interaction remains a manual check; persistence path reviewed in code. Left the app in default dark mode and updated all three launchers. No Android build, commit or push.

## 2026-10-11 — First-person wall-projected gaze dot

- **Implemented:** primary first-person gaze is now an orange dot with a dark outline at the nearest forward intersection with the reconstruction's finite octagonal panels. Uses the same saved/corrected eye ray and inferred wall geometry; does not snap to target/object centers or fabricate intersections beyond panel bounds. Invalid/stale gaze and unsupported layouts show no dot. The gaze toggle still controls visibility; third-person gaze remains a ray and optional raw/pre-alignment diagnostics retain their thin line.
- **Verification:** isolated Trial Review 2.8 build and P020/replay checks passed, including new projection tests for all eight walls, translated/rotated layout frames, out-of-height and vertical misses, and unknown layouts. Visually confirmed orange dot on P020's wall area at study 01:03.38, with the first-person orange line removed. Source recordings and headset behavior unchanged.
- Viewer opened on P020 and three existing launchers updated to 2.8. No commit/push or Android restart. Scoped manual review retained validity gating and kept the projection strictly in the desktop presentation path.

## 2026-10-11 — Explicit directions in continuation cues

- **Implemented:** replaced directionless keep-going cues with explicit left/right wording at turn milestones and up/down/diagonal wording during within-wall progress. Removed the three generic progress lines from preparation; updated Jev semantics/export and policy version v23.
- Preserved route lock, measured-progress eligibility, cadence and anti-repetition limits; retained the separately requested small-progress “A bit more” alternatives. Regression checks caught and avoided a collision with ordinary “Keep turning” variants.
- **Verification:** offline gaze, rotational layout and spatial voice checks pass; full Unity study integration passed on rerun after one transient CSV ray-intersection assertion failure (no production freshness changes). Android Build And Run succeeded; installed and launched on Vive FA5AR3N00385 at 11:26:38. Installed/local APK SHA-256 match: `569976797952364134a6e3da2632513469d446bf6772d701a2aab80f9d9358cd`; startup scan found no fatal/null-reference/missing-method/type-load errors. Wearer listening validation remains; new cues use the normal verified-audio preparation pipeline.

## 2026-10-11 — Thin first-person gaze ray in Trial Review

- **Fixed:** the corrected/recorded and raw/pre-alignment gaze overlays now use a 2.5-render-pixel width in first person. The former fixed 8–9 mm ribbon was magnified near the camera into a large orange wedge (8 mm at 1 cm projects to ~314 pixels in a 500px-high, 65-degree viewport).
- Presentation clips along the original ray at the camera near plane and scales ribbon width by camera depth. It does not alter recorded eye origins/directions, shift the gaze toward objects or mutate source data. Third-person styling resets each frame; behind-camera segments are hidden.
- **Verification:** new Unity checks cover rotated cameras, 240/500/1200px viewports, unchanged ray centerline/endpoint, near-plane crossings and behind-camera rays. Isolated Trial Review 2.7 build and all replay/P020 checks passed. Inspected live P020 first-person playback at study 01:03.38: thin orange line visible instead of the triangle. Launchers updated; viewer left paused there. No Android deployment, commit or push.
- Pre-fix HEAD 0c56efd; unrelated study edits and earlier desktop catalog changes preserved. Fix scope: TrialReviewGazeLine, TrialReviewGazeLineChecks, actor width reset, desktop View render hook, generated renderer render hook and build verification call. Scoped manual correctness/simplicity review completed; independent code review skipped because side-conversation delegation is prohibited.

## 2026-10-11 — Emerald stimulus palette

- **Implemented:** replaced purple with emerald green (#00A878, label Green) in the spawn palette and deterministic trial definitions. Updated object metadata documentation and the enrollment reading passage to use green. Red, blue and yellow remain; the 30% capsule increase is preserved. User's emerald choice supersedes the proposed hot pink.
- Verified both palette definitions match, active palette/enrollment files no longer refer to purple, and replay rendering uses recorded RGBA rather than today's palette. Historical recordings remain visually unchanged. Local source changes only; headset deployment and wearer color validation remain pending to avoid disrupting main-thread study work.

## 2026-10-11 — Rich current-state context for Jev

- **Implemented:** shared gameplay/playback snapshots for catalog wording and interruption advice: exact gaze/controller hit categories, unique vicinity winner and ambiguity, stable wall/gaze/controller gates, trigger, input freshness, geometry, recent departure/history and actual AudioSource utterance/timing/fade.
- Corrected `targetGazeQualified` to mean the completed gaze hold; immediate vicinity acceptance is separate. Evidence updates after gates, clears on objective reset/cancellation, and serializes missing geometry as null. Requests freeze state before await, refresh coarse geometry after the initial frame defer, record context telemetry and retain existing live revalidation/local limits.
- Offline spatial/voice suites and Unity study integration pass; includes new state/transport/hold/distractor/tracking-loss regressions. Jev remains choice/advice only; no wearer naturalness result claimed. Android Build And Run succeeded; Vive updated at 11:18:02, process 15434, installed APK matches `2d1b6976056b7a02aaf8b9e5ccf52403508bcf7fc0a813bfc13d78018ba51edb`. No fatal/null-reference/missing-method/type-load errors in inspected startup logs.

## 2026-10-11 — Increase pill stimulus size

- **Implemented:** capsule/pill spawn scale increased uniformly by 30%, from 0.035 to 0.0455 on each axis. Its existing collider scales with the object; other shapes are unchanged. Replay recordings already save object scale, so future recordings retain the new size and historical replays keep their original geometry.
- Verified the scale arithmetic, spawn path and recorded-scale replay path; diff whitespace check passed. Local source edit only in this side conversation; no Android build/relaunch to avoid disrupting the main thread's active study work.

## 2026-10-11 — Desktop participant datasets and whole-session scrubbing

- **Implemented locally:** Dataset dialog with participant IDs/optional names, per-recording run names, saved dataset navigation and reasoned/timestamped reassignment history. Source participant IDs and recording bytes remain unchanged; viewer assignments do not change headset enrollment. Catalog persists locally, detects conflicting writes and reloads after failed saves.
- **Implemented locally:** one attachment entry per file, file picker, Finder reveal and run-folder opening. Package/audio, explicitly linked study-folder and generated export files are indexed individually. Links do not copy files; missing paths remain visible. CSVs add assigned_participant_id, participant_name, dataset_id and dataset_name while retaining original participant_id; label/assignment changes create new export packages.
- **Implemented locally:** whole-session timeline by default, drag from any track or playhead with a stable range across trial boundaries, continuous reconstruction/data seeking and separate study/trial clocks. Spacebar toggles playback, ignores key repeat and text/modal input, resumes after scrubbing and restarts at the end. Existing event/sample stepping and selected-trial range remain available.
- **Verification:** 50 Python export/transfer tests passed; five adapter checks rerun after adding source-folder receipt verification. Isolated desktop Unity build and catalog/keyboard/replay checks passed, including original-byte preservation, assignment audit, write conflicts, copied/replaced recordings, cross-trial seeks, shortcut repeat/text guards and P020 replay fixtures. Native Spacebar play/pause and typing-space behavior verified in the running app. Mouse automation moves/hover-highlights but does not activate controls, so actual drag, dataset-dialog and Finder-click verification remains open; not represented as passed. No runtime exceptions in inspected player log.
- **Delivery:** Trial Review 2.6.app built and opened on P20; three existing viewer launchers updated. Main-study/Android build and deployment intentionally untouched for this side conversation. No commit or push.
- Code review: skipped (ce-code-review unavailable) — independent reviewer dispatch is prohibited in this side conversation. Scoped inline review reused the existing exporter/browser/seek pipeline and fixed failed-save rollback, recording-path identity reuse and the trial clock during cross-trial dragging.

## 2026-10-11 — Same-wall pre-trial guidance

- **Implemented:** when valid gaze at the readiness cross is already in the target wall, the hidden-target opening uses vertical or diagonal refinement, including existing little/normal/way height bands. Fresh thresholds avoid previous-trial hysteresis; other-wall openings retain the left/right orientation cue.
- Existing prepared phrases serve both voice conditions. The cue still finishes before object reveal and search timing; invalid/stale gaze cannot supply a guessed direction. Policy telemetry advances to v22.
- Validation: added magnitude/diagonal/prepared-phrase coverage and a Unity fixture for hidden-target openings, other-wall starts and invalid/stale gaze. Offline gaze tests and Unity gaze measurement checks pass. Android Build And Run succeeded; installation confirmed at 10:55:44 on Vive FA5AR3N00385, process 11243, matching APK SHA-256 `ec87b5c4b6bc6a5cfc8f0759e7191bab4ff2311822d1d85daa707e22cebeffa8`. No fatal/null-reference/missing-method/type-load errors in inspected startup logs; wearer listening remains open.

## 2026-10-11 — Parallel voice preparation and verified audio repair

- **Implemented:** five concurrent voice-download/verification jobs with identity-scoped deduplication, cancellation, bounded transient HTTP retries and atomic completed-file publication. Unity decoding, normalization and library admission remain sequential.
- **Implemented:** verbatim ElevenLabs Scribe v2 checks new and legacy cached MP3s against the approved script before playback. Added/missing words, fillers and changed directions reject; mismatches regenerate up to three times. Verification outages and exhausted attempts cannot admit unchecked clips. Receipts bind policy, script, transcript and exact audio hash; verified caches work offline. Applies to neutral and self-similar preparation, auditions and on-demand synthesis.
- **Diagnosis:** two cached Eric neutral clips contained unwanted trailing speech despite clean script text. Known hashes are quarantined; fresh replacements passed live Scribe verification. Local Whisper audit flagged 56/528 for review (includes benign ASR/formatting differences; not 56 confirmed defects).
- **Verification:** voice-isolation/download tests and Unity study integration pass. First preparation has extra speech-verification latency; ASR is an imperfect safeguard and wearer listening remains open. Android Build And Run succeeded in 218.9s; Vive installation updated at 10:45:30 with process 9248 running. Installed APK SHA-256 matches the new local build (`8050499527ea3442ff4df527559c873dca98854311ae0c18a2e6077faec4cb03`); no fatal/null-reference/missing-method/type-load errors in inspected startup logs. Two verified replacement clips and matching receipts were installed with prior cache bytes backed up; session data preserved.

## 2026-10-11 — Latest local build installed and verified

- Built the current recovered Unity project, including uncommitted round encouragement and right-controller settings changes, through the default Android Build And Run flow. Build succeeded in 39.7 seconds.
- Vive FA5AR3N00385 installation updated at 10:14:11; app process 31193 running. Installed APK SHA-256 exactly matches the newly generated local APK (`b468c1baf0be8a873045c2432d56275710965aa58bd16d3aaf8f37e4dcebd863`).
- No fatal, null-reference or missing-method exceptions in the inspected startup logs. This confirms deployment and launch; wearer audio/interaction validation remains open. No session data cleared.

## 2026-10-10 — Participant names and automatic round/run CSVs

- **Implemented:** participant display-name editor beside the session selector and in Final data, saved locally by original participant ID. Names appear in the header and as an additive participant_name CSV column. Raw replay files and stable IDs remain unchanged.
- **Implemented:** saved-run opening automatically runs the bundled authoritative analysis/trial_csv.py exporter in a background process. Current live run generation waits for an end record and a connected fully copied/hash-verified archive. Each package includes one performance/subjective CSV per scheduled/recorded round and combined trials.csv. Missing source folders have a matching-folder picker; failures have a regenerate action. No invented missing measurements/surveys and no claim that an export proves completion.
- **Integrity:** exact linked participant/run-folder resolution, source fingerprints before/after export, atomic new package publication, cache verification, additive names and preserved original IDs. Renaming creates a new package; edited prior exports are never silently overwritten. Saved-view summary attachment also rejects source changes since opening.
- **Compatibility:** added postponed annotations to analysis/trial_zones.py for the Mac system Python 3.9; no calculation changes. Bundled only the five existing analysis modules needed by trial_csv.py. The reader and exporter still flag P20's incomplete replay tail.
- **Verification:** 55 Python checks pass under /usr/bin/python3, including four new adapter tests and existing trial-export cases. Unity 2.4 isolated build/replay checks pass and the new participant-name column successfully joins through the existing summary validator. Launched 2.4 on P20 without a preloaded export: it automatically produced 22 per-round files and a 22-row full-run CSV from its source schedule/summary (nine replay snapshots; missing rounds remain status-marked). Native layout inspected; normal mouse interaction remains unverified by automation. README and three launchers updated; only the new viewer launched, no headset restart/build or commit/push.
- Code review: skipped (ce-code-review unavailable) — side-chat restrictions prohibit independent reviewer dispatch. Scoped inline reuse/quality/efficiency review retained the existing scientific exporter, bounded source lookup and asynchronous process; no external reviewer receipt claimed.

## 2026-10-10 — Expanded round-encouragement scenarios

- **Implemented:** 15 named live scenarios × three consecutive selections, 1,408 offline fact/history combinations and all 1,024 prepared-clip subsets. Added malformed/unoffered response, HTTP 401/429/500, response-limit, missing-key and caller-cancellation checks.
- **Unity checks:** snapshot immutability, practice-versus-study identity, repeat playback counts, decision telemetry, missing-client bypass, repeated stop, restart and disable. Expanded study feedback checks pass.
- **Live result:** 45/45 responses eligible and nonrepeating; zero deadlines; 177–490ms (median 210ms). Missing/invalid metrics remained generic. See `docs/testing/2026-10-10-jev-encouragement/report.md` and saved case results. No runtime wording or eligibility thresholds changed; wearer validation remains open.
- **Device:** default Build And Run attempted after verification, but Unity reported no Android devices connected and ADB listed none. Dismissed the prompt; no reinstall/launch confirmed this turn. Prior installed encouragement behavior is unchanged by these test-only edits.

## 2026-10-10 — Performance-aware round encouragement

- **Implemented:** a separate Jev end-of-round choice, bounded to 600ms and overlapped with the success chime, selects among locally qualified prepared phrases. Missing/failed/late/out-of-set advice falls back locally; no live TTS generation or free-form claims. Both voice conditions use the same policy and their own normalized clips.
- **Inputs:** frozen pause-aware search seconds, incorrect selections, actual guidance starts, repeat requests, practice status and available planned absolute turn angle. No participant IDs, raw gaze, inferred feelings/workload, speed baseline or voice identity sent to Jev. One first-selection claim requires zero incorrect selections; recovery requires errors. Persistence eligibility after 30 seconds is a pilot wording rule, not a performance norm.
- **Lifecycle/logging:** capture presented trial identity before next-round advancement, recheck voice/trial before speech, cancel on stop/disable/restart/next presentation, and protect final completion speech from restarting after a session stop. Record input/selected phrase/fallback status/version in trial events and replay.
- **Verification:** ten live synthetic decisions returned eligible nonrepeating responses in 194–528ms. Offline policy/transport/deadline and voice-isolation suites pass. Unity study integration passes, including counter/reset/identity/cancellation/preparation tests. Wearer listening and the research impact of adaptive reinforcement remain unverified.
- **Device:** Android Build And Run succeeded in 50.0s; installation confirmed on Vive FA5AR3N00385 at 19:06:37, with process 8679 running. No matching fatal/null-reference/missing-method exceptions in the inspected startup logs.

## 2026-10-10 — Right-controller settings shortcut and larger controls

- **Implemented:** hold right-controller A while clicking its thumbstick twice within 450ms to toggle settings. Distinct rising edges are required; holding the stick, releasing A, focus loss and disconnect cannot complete a stale chord.
- **Implemented:** settings content opens 0.8m away (was 1.1m), at 130% of its original scale. The launcher is closer and larger too. Reopening reanchors to the current view without cumulative scaling; menus remain world-fixed while interacting.
- **Implemented:** dedicated right-ray/trigger Close settings button beneath the panel, existing Close settings control, and the same hardware chord all dismiss it. Closing an active-trial pause exposes Continue and never silently resumes the timer.
- **Verification:** Unity controller-selection checks pass, including shortcut edge/timeout/reconnect cases, placement/scale/idempotence, world-fixed menus and readiness/search pause lifecycle. Device button feel and readability still require wearer confirmation. Android Build And Run succeeded in 66.7s; installation confirmed at 18:57:49 on Vive FA5AR3N00385, process 6964 running, with no matching fatal/null-reference/missing-method startup exceptions.

## 2026-10-10 — Semantic Jev speech and near-target continuations

- **Implemented v21:** explicit intent/axes/magnitude/stage/prerequisites/interruption policy for the 300 catalog phrases and all 215 guidance texts. Jev receives structured current/proposed meaning, playback, event and compact gaze/geometry context. The existing study playback, voice identity, normalization and replay archive remain in use.
- **Implemented:** “A bit more.” / “Just a little more.” require actual same-direction progress (at least 2°), at most 10° remaining and at most 20% plane-height error when vertical; one continuation per unchanged direction. Stalling/reversal uses explicit directions.
- **Interruption tuning:** protect the final one second, unknown/finish-only phrases, ordinary progress and redundant same-wall arrival locally. Keep 650ms onset protection, 400ms advice deadline, one advice attempt per utterance and 150ms fade. These gates contain observed incorrect raw Jev approvals.
- **Live verification:** 43 synthetic interruption cases × 3 = 129 calls; 117 completed responses matched expected effective behavior after local gates, 12 deadline fallbacks safely finished (8 missed desired cuts). Completed-response median 203ms, p95 333ms. 16 navigation selections returned eligible nonrepeating phrases. Nine synthetic Eric audio clips generated for listening previews; no participant recordings sent.
- **Offline verification:** voice isolation/fade, gaze quality/history, transport/PCM, rotational layout and enrollment pass. Unity catalog and study integration pass, including JSON/asset semantics and all 2,888 angle/distance grid checks. See `docs/testing/2026-10-10-jev-speech/report.md` for evidence and limitations.
- **Review:** Code review: skipped (ce-code-review unavailable) — its independent reviewer/finish-agent dispatch conflicts with this session's inline-only AGENTS rule. Manual diff review and simplification completed; no independent receipt claimed. Unity's transient unrelated preload changes were not intentionally edited.
- **Device:** Android Build And Run succeeded in 94.9s; installed on Vive FA5AR3N00385 at 18:52:31, running PID 5896. No matching fatal/null-reference/missing-method/metadata exceptions in current-process startup logs.
- **Open validation:** headset listening and perceived naturalness, particularly neutral versus cloned prosody and fast gaze transitions. Synthetic audio previews are timing demonstrations, not headset recordings.

## 2026-10-10 — Checkpoint before primary checkout synchronization

- Saved the accumulated headset guidance, gaze diagnostics, spatial voice pipeline and desktop replay work as a local checkpoint for merging into the primary project folder.
- Preserved the primary checkout's existing modified/untracked files in an external backup before synchronization; its older run-sheet copy must not replace the newer tracked protocol. API credentials remain ignored and outside the checkpoint.
- This checkout synchronization does not constitute new headset validation; the v20 build and focused test evidence remain recorded below.

## 2026-10-10 — Bounded Jev interruption advice and 150ms fades

- **Implemented v20 pilot:** stable corrections can ask Jev in parallel while speech continues, once per utterance with a 400ms deadline. Fresh approval plus local gates is required; missing/error/late/stale advice finishes the phrase. Protects the first 650ms, last 600ms, confirmations and trigger reminders; 2.5s interruption cooldown. Inspector toggle allows reverting to finish-phrase scheduling.
- **Implemented:** linear unscaled-real-time 150ms fade reaches zero before Stop, restores playback gain afterward, and revalidates the replacement. Sustained tracking loss fades locally after the existing 250ms grace; lifecycle/setup cancellation remains immediate. Repeated invalid ticks cannot restart a fade. Unity frame scheduling limits exact timing to the first frame at/after 150ms.
- **Telemetry/replay:** advice request/result/discard and fade-start events; source replay renderer reconstructs the fade envelope and handles seeking/live refresh. Existing standalone viewer binaries are not rebuilt in this task.
- **Live Jev evidence:** four synthetic cases returned expected approve/reject choices in 247/243/199/175ms (arrival and overshoot approved; redundant direction and jitter rejected). No credentials or participant data printed. This does not validate perceived naturalness.
- **Verification:** production fake-audio regression covers half gain at 75ms, no early Stop, zero gain at Stop, prepared correction, stale/late advice, one request per utterance, protected endings/confirmations and lifecycle cancellation. Gaze-quality, voice-isolation and transport suites pass. Refreshed Unity study integration and replay fade reconstruction pass. Review also protected the clone's speech window rather than its effect tail and guarded replay refresh when a segment is inactive. Android Build And Run succeeded in 95.7s; installed on Vive FA5AR3N00385 at 18:26:06, running PID 31267. No matching fatal/runtime exception lines in current-process startup logs. Wearer listening and subjective naturalness remain unverified. Existing work preserved; no commit/push.

## 2026-10-10 — Independent replay walls and degrees

- **Implemented:** separate Walls and Degrees visibility controls. Angle labels remain available when outlines are hidden, for both live and saved replay. Updated eight-toggle layout and README.
- **Verification:** isolated macOS Trial Review 2.2 build and existing Unity replay checks pass, including P20 validation. Updated launchers and replaced the running 2.1 viewer with 2.2. No headset changes or commit/push.

## 2026-10-10 — Trial Review desktop interface redesign

- **Implemented locally:** charcoal/mint desktop interface with persistent first-person, third-person and overhead buttons; reset view; layout-radius-based orbit framing; large visibility chips; searchable/filterable trial sidebar; practice ordinals separate from study rounds; session/file and Wi-Fi controls. Preserved the shared replay geometry, pose, gaze correction, audio and live-follow pipelines.
- **Implemented:** selected-trial/whole-session timeline with saved speech segments, validity-aware gaze spans, event markers, exact-sequence seeking, stable-range scrubbing, sample/event steps, speed, volume and mute. Gaze spans show usable replay samples under the existing 100 ms display freshness rule, not fixations. Audio remains 1x only.
- **Implemented:** At cursor, Events and Final data panes; speech jump, tracking/sample age and expandable decoded observation. Per-trial export creates new samples/events/objects CSVs, supported decoded JSONL and source-hash manifest. Only a loaded identity/hash-matched summary is exported; unavailable questionnaires/results stay absent. This subset does not replace the full replay/audio or raw eye-acquisition package.
- **Verification:** 14 Python tests pass; isolated Unity build passes all interface/export, live, audio, renderer, correction, alignment and reconstruction checks, including P20's 98 sampled poses across nine trials. P20 has a pre-existing incomplete tail, omitted by the reader. CSV tests cover quoting/newlines, missing poses, trial isolation, unchanged source hash and rejection of changed recordings. `git diff --check` passes. Native layout and keyboard play/pause inspected on P20; automated mouse input did not activate Unity buttons, so normal mouse camera/toggle interaction remains a user confirmation, not a claimed pass.
- **Delivered:** Trial Review 2.1.app in the existing side-chat visualization output folder; closed the previous instance, launched only 2.1 on P20 and updated the three launchers to reuse the app rather than open duplicates. Updated tools/trial-review/README.md. No Android build, headset restart, commit or push; unrelated main-thread changes preserved.
- Code review: skipped (ce-code-review unavailable) — independent reviewer dispatch prohibited in this side conversation; manual scoped diff review performed. No matching replay task was found in the available board snapshot; no duplicate task created.

## 2026-10-10 — Signed-angle catalog and navigation speech adapters

- **Implemented:** 300 editable navigation entries (15 angle × 5 horizontal distance × 4 tone), JSON exchange/import, deterministic signed-angle matching and a Jev candidate-only adapter using Vercel's verified TypeSafe compatibility endpoint and `typesafe-ai/jev` model.
- **Implemented:** standalone ElevenLabs PCM WebSocket coordinator with cancellation, bounded fragmented-message handling, audio-thread ring buffer, configurable 60ms prebuffer and timing/underrun diagnostics. No zero-latency claim.
- **Implemented:** ordinary coarse study hints can select already-prepared catalog clips; existing valid-gaze, route lock, sentence pacing, calibrated loudness, voice identity and replay playback remain authoritative. Stateful vertical/diagonal, return, confirmation and milestone cues remain in the current policy.
- **Validation:** production transport/PCM tests pass, including gateway model/endpoint, exact choices, timeouts, socket fragmentation, cancellation and concurrent ring ordering. Refreshed Unity catalog/geometry/local-fallback checks and full study integration pass. Gaze-quality and voice-isolation regressions pass. Final Android Build And Run succeeded in 227.5 seconds; installed on Vive FA5AR3N00385 at 18:13:14, running PID 28795. Verified packaged gateway configuration matches the ignored local file without printing values; no matching fatal/navigation exceptions in current-process startup logs. `git diff --check` passes. No commit/push; side-chat replay work preserved.
- **Live verification:** user-authorized `jev_key` loaded from ignored local configuration. Three authenticated Vercel/Jev checks selected a valid candidate in 1,357/214/213ms; the study retains a 900ms decision deadline and deterministic fallback. Key values were not logged. One authenticated ElevenLabs socket smoke test returned 22,291 decoded 24kHz samples in 692ms (desktop transport, not headset audible latency).
- **Open:** Vive streaming listening/latency. Streaming is callable in a standalone preview; recorded study playback remains calibrated cached audio, because streaming normalization/effect/replay parity is not yet validated.

## 2026-10-10 — Finish guidance sentences and shorten cues

- **Evaluation:** confirmation bypassed the prior correction pacing, permitting “Go back — YEP”; request-time cadence allowed a fresh direction immediately after a longer acknowledgment finished. Invalid gaze also cancelled speech immediately, including brief gaps. User chose complete phrases with shorter wording instead of mid-sentence semantic interruption.
- **Implemented v19:** all automatic corrections and repeat requests wait for natural completion and a playback-end gap: 0.55 seconds ordinarily, one second after all gaze/controller acknowledgment variants. Removed stale-direction interruption. The 0.1-second gaze qualification remains, but waits its turn to speak; latest valid evidence is reevaluated rather than enqueuing obsolete cues. Pause/completion/session cancellation remain immediate. New decisions stop immediately on invalid gaze; already-playing speech gets 0.25 seconds before sustained tracking loss cancels it.
- Shortened large turns, area leads, diagonal refinements, target/wall return wording and the right-controller trigger reminder. Preserved distance tiers and shared policy across both voice conditions. Added the awkward-transition evaluation and current speech policy to `docs/guide/voice-guidance-states.md`.
- **Verification:** gaze/history and rotational-layout suites pass. Production synthesizer fake-audio regression now rejects interruption of held playback, enforces actual-end pauses, cancels obsolete pending cues without consuming acknowledgment, verifies all confirmation variants, and preserves cached/offline playback/decision linkage. Voice-isolation suite passes. Fresh Unity study integration passed. Build shortcut menu dispatch failed, so the same Build And Run was invoked through Unity UI; Android build succeeded in 69.4 seconds, installed at 17:30:05 on Vive FA5AR3N00385, running PID 13786.
- **Open validation:** wearer listening for return → confirmation → next-direction pacing, tracking-gap behavior and naturalness of the shorter phrases. No claim of automatic linguistic boundary detection or measured human naturalness. Unrelated work preserved; no commit/push.

## 2026-10-10 — Gaze-ring shine and right-controller hover rotation

- **Implemented:** valid gaze within the unique closest object's padded vicinity brightens its surface and adds a gold emissive edge. Applies equally to targets and distractors, regardless of dev-ring visibility. Uses the existing padding/overlap arbitration; ambiguous/invalid gaze does not highlight. Restores the previous base color on departure/reset and preserves the controller's blue rim when both point at the same object. No gaze selection was introduced.
- **Implemented:** a fresh tracked right-controller hit spins the object at 60 degrees/second around world up during active search. FixedUpdate applies rotation before physics, keeping visible geometry and the actual hitbox synchronized with automatic transform sync disabled. Stops in place on departure/tracking loss/pause; hidden objects and disabled hitboxes are excluded. Position, scale and identity stay fixed; existing replay transform changes capture rotation. Symmetric spheres may look unchanged; live shine is not a new replay material channel.
- **Verification:** Unity controller checks passed, including new material hue/alpha preservation, cleanup, simultaneous-controller rim priority, rotation/position/scale, hidden-object and disabled-hitbox tests. Final Android build with physics-timed rotation succeeded in 51.6 seconds; generated IL2CPP includes ControllerRaySelector.FixedUpdate. Installed on Vive FA5AR3N00385 at 17:23:15, running PID 3872. `git diff --check` passes.
- **Remaining wearer check:** shine visibility, overlapped-ring arbitration and hover stability near spinning corners. Existing voice/eye/replay work preserved; no commit/push.

## 2026-10-10 — Conversational turn progress and recent-target returns

- **Implemented locally:** guidance v18 chooses turns from actual object bearing (including lateral wall offset), preserves the opening route across the +/-180 seam, and releases that rear-seam preference below 150 degrees or on wall entry. Exact seated 0-to-180 and 180-to-0 ties resolve right and left respectively. Turns of at least 90 degrees remaining say “Turn way to your left/right.”
- Outside-wall encouragement now follows new 45-degree remaining-error milestones, with three rotating variants. Consumed milestones cannot repeat on backtracking; fast turns coalesce them. Ordinary cadence is two seconds; same-direction reminders without new milestones wait four seconds and stay explicit. Within-wall refinement retains its bounded continuation and plane-height tiers.
- A qualified recent target-vicinity visit followed by sustained valid departure can produce “You were just there. Go back down and left” (all eight directions prepared). Two-second departure-memory expiry, six-second spoken cooldown, invalid-gaze/lifecycle reset and post-fade relevance checks bound the cue. Wall-return speech now names the actual direction back. Added decision fields for remaining turn degrees and the latest qualified target visit's receipt time/observation sequence.
- Adding/removing diagonal detail no longer cancels a sentence. Busy ordinary reversal/arrival/return corrections require 650 ms settled evidence and at least 1.5 seconds since the preceding request. Fast 0.1-second gaze confirmation, controller hold and immediate invalid-gaze/lifecycle cancellation remain.
- **Verification:** gaze/history, rotational-layout and voice-isolation suites pass; refreshed Unity study integration passes, including runtime opening-route retention, milestone consumption and lifecycle memory reset. Android Build And Run succeeded in 103.4 seconds; verified Vive FA5AR3N00385 install at 17:16:08 and running PID 27382. No commit/push. Preserved side-chat replay and other WIP.
- **Open validation:** wearer pacing and spoken directions for opposite walls, milestone backtracking, high/low/diagonal search, recent target departure, wall overshoot and blinks. New pilot rules and both Mermaid state diagrams are documented in `docs/guide/voice-guidance-states.md`; no eye-event or accuracy validation is claimed.

## 2026-10-10 — Waiting message copy cleanup

- **Implemented locally:** removed the two supporting sentences below “Waiting for headset signal” and centered the heading alone. No live acquisition or recording behavior changes.

## 2026-10-10 — Replay walls and degree labels

- **Implemented locally:** renamed Study walls to Walls / degrees; it controls outlines and screen-facing 0°–315° markers together, matching the headset toggle. Labels use the saved layout origin/rotation and the headset's 0.43 m layout-relative label height; available in saved/live views, with 0° marked FORWARD. These are viewer overlays, not recorded participant guide visibility.
- **Verified:** desktop build 1.19 and environment checks pass, including rotated/translated marker placement and clearing on missing layouts. Visually verified all eight labels and the toggle in the synthetic demo overhead view. Native UI automation could not click the IMGUI control (noWindowsAvailable), so no manual click verification claimed. Updated README and local launchers; no headset deployment, commit or push.

## 2026-10-10 — Larger live waiting message

- **Implemented locally:** centered the live waiting message in the replay viewport, increased its heading from 14 px to a responsive 32–64 px, and enlarged supporting text to 18–24 px. Wrapped text uses measured heights to keep the block centered.
- **Verified:** isolated desktop build and existing replay checks pass; visually confirmed the large waiting message in the running Trial Review 1.18 app. Updated local replay launchers. Desktop-only change; no headset deployment, commit or push.

## 2026-10-10 — Live follow latency and inactive-headset waiting

- **Implemented locally:** desktop Trial Review 1.17 reduces its live buffer from 3 s to 200 ms, status/audio refresh to 100/200 ms, and receiver sleep between transfer passes from 500 to 100 ms. Cached eye-folder lookup avoids scanning the whole remote inventory for every replay row. Full-file hash verification yields while the newest replay is growing; archiving continues.
- **Inactive source behavior:** receiver exposes observed source-replay growth independently of connection/archive/audio activity. Follow requires current tracked head samples, no terminal/pause marker, caught-up transfer/parser, and source growth within 2 s. Otherwise the viewport displays “Waiting for headset signal” and stops speech/motion. Repeated Follow clicks no longer rewind a following view. Rejoin/catch-up uses the 200 ms edge; manual historical playback remains available.
- **Verification:** regression reproduced missing source-activity distinction; all 14 Python tests pass. Isolated Unity desktop build and follow-policy tests pass (historical first copy, stale source, disconnect, pause, tracking loss/recovery, completed run, backlog, low-lag cursor and no backward following), alongside existing replay/audio/P20 checks. Built Trial Review 1.17 and updated the local launch shortcuts. No study Unity/device restart, commit or push; main-thread WIP preserved.
- **Live follow-up:** accepted Documents access and restored Wi-Fi; visually confirmed stable waiting during offline state and archive catch-up. Real transfer exposed parser-chunk flicker, now fixed: ongoing following tolerates a partially parsed fresh chunk while all source/tracking/pause checks remain enforced. An initially offline headset now retries automatically instead of exiting; identity is checked again after each reconnection. Added regression checks.
- **Remaining manual check:** fresh wearer latency/audio and pause/resume. User accepted macOS Documents access; final 1.17 app and its bundled receiver are running over Wi-Fi. Visually verified the final waiting screen while no fresh samples arrive, with 100/100 files verified, about 803.1 MB archived and zero pending bytes. Fresh wearer latency/audio still needs a trial. End-to-end live latency is not measured or promised to be 200 ms; source writes/network transfer still contribute.
- **Review:** targeted manual review due to unrelated branch work; independent agents prohibited in this side chat. Simplification limited to this fix, preserving overlapping prior changes.

## 2026-10-10 — Wireless live replay and automatic run archive

- **Implemented locally:** Trial Review live Wi-Fi mode uses authorized ADB TCP/IP to copy existing headset replay/audio, linked raw eye observations and trial files, plus current/new preparation folders. It resumes byte offsets across disconnects/restarts, automatically discovers new runs, and keeps a SHA-256 manifest. Headset source recordings stay intact; no separate study/server build is needed.
- Added incremental live JSONL ingestion using the same validator as offline replay; dynamic meshes/trials, first-person/third-person rendering, rewind with ongoing archiving, buffered follow/catch-up, late WAV playback and audio cancellation. UI distinguishes disconnected/waiting/catching-up states. Root README now links the detailed live/replay instructions.
- **Verified:** 12 Python tool tests; Unity live checks (partial tails, exact records, mesh/trial append, rewind, late WAV, uninterrupted audio refresh, cancellation, malformed data), existing real P020 replay/correction/CSV join checks; standalone macOS build `Trial Review 1.14.app`; current study runtime/editor C# compilation (zero errors, existing runtime warnings). `git diff --check` passes.
- **Device evidence:** Wi-Fi endpoint `192.168.1.208:5555` identifies the Focus Vision `FA5AR3N00385`. Copied 148,250,314 bytes across 46 files into `~/Documents/Trial Review Live/data`; all 46 match source SHA-256 and a second local hash check. Explicit ADB disconnect produced connected → disconnected → connected with no pending bytes afterward. Viewer rendered the real received run and archive status. USB remained physically attached, but these transfers explicitly used the TCP/IP endpoint.
- **Limits / remaining wearer check:** run one fresh neutral demo to evaluate live delay, continuous audio and new-run transition under actual wearing. Existing completed/prefix recordings and synthetic incremental tests do not establish that human check. This is telemetry reconstruction, not passthrough video; external surveys/microphone/video are not manufactured. Hash equality verifies copied bytes, not study completion. Initial connection selects latest/current folders, then all new ones; does not bulk-import older history. Receiver must remain open/Mac awake, or catch up later before headset sources are deleted.
- Inline simplification/manual review fixed archive-path resolution on macOS, bulk-copy live lag, final-event draining and unnecessary audio-plan rebuilding. Code review: skipped (ce-code-review unavailable) — its required independent reviewer dispatch is prohibited in this side conversation; no independent review is claimed. No commit, push, Android deployment or study-session restart performed. Main-thread changes preserved.

## 2026-10-10 — UCF readiness charge colors

- **Implemented locally:** replaced the aqua readiness panel fill with opaque UCF yellow (#FFC904). The cross now fills left-to-right from gray to black using the same charge progress, keeping its original size and world position. Readiness duration and valid-gaze requirements are unchanged.
- Visual-only change; refreshed Unity readiness checks pass. Build shortcut failed to address Unity, so the same Build And Run command was dispatched through its menu. Android build succeeded in 54.0 seconds; verified install at 16:13:58 and running PID 8135 on Vive FA5AR3N00385. Headset visual check remains. Preserved unrelated edits; no commit/push.

## 2026-10-10 — Vertical guidance by wall-height distance

- **Implemented locally:** vertical phrasing now uses absolute target-minus-gaze-intersection height divided by the full configured wall height. At or below 20% uses “a little higher/lower”; above 20% through 50% uses “look higher/lower”; above 50% uses “look way higher/lower.” Each tier has three variants and applies to ordinary, diagonal, area-entry and repeat guidance; vertical overshoot return wording also respects the tier. Both voice libraries prepare all variants.
- The ray intersects the target's object plane using its actual radius and seated rotation; height includes the same top margin as the drawn frame. No clamping of off-wall gaze. Existing valid-gaze, horizontal-then-vertical and alignment rules remain. Band changes must settle and receive an explicit next cue rather than an ambiguous “keep going.” Pending area/return speech rechecks the tier after its audio fade.
- Added optional guidance-decision measurements (signed metres, plane height, signed fraction, availability), and advanced policy version to v17. Boundary/phrase-library tests pass with gaze/history, layout and voice-isolation suites. Fresh Unity study integration passes, including translated/rotated wall intersections, height normalization, parallel/backward rays and off-wall measurements. Android build succeeded in 59.7 seconds; verified install at 16:04:48 and running PID 4101 on Vive FA5AR3N00385. Wearer validation remains; no commit/push.

## 2026-10-10 — Faster gaze confirmation

- **Implemented:** reduced the gaze-only target-vicinity confirmation hold from 0.5 to 0.1 seconds, including reacquisition. Controller hold, closest-object arbitration, validity checks, 0.15-second exit rearm and selection reminder remain unchanged. Bumped the logged guidance-policy version and updated runtime documentation.
- Updated the boundary/reacquisition checks; the 0.1-second assertion failed before the threshold change and passed afterwards. Gaze/history script checks and freshly compiled Unity gaze checks passed. Android build succeeded in 44.5 seconds; verified install at 15:56:22 and running PID 31903 on Vive FA5AR3N00385. No commit/push.

## 2026-10-10 — Selection and cross reminders; 100-fragment success feedback

- **Implemented locally:** after target acknowledgment, three continuous seconds of valid target-winning gaze makes a right-controller/trigger reminder eligible once per objective. Existing closest-vicinity arbitration, invalid-gaze suspension, 0.5-second confirmation and 0.15-second reentry remain. Both voice libraries prepare the same new wording.
- **Implemented locally:** after five seconds away from the readiness cross following the setup announcement, one gaze-relative directional reminder asks the participant to find and hold gaze on the cross. The prompt stops when the cross is acquired, direction becomes obsolete, gaze is invalid, or readiness is paused/cancelled. Search timing still starts only after readiness and the opening direction.
- **Implemented locally:** a correct selection replaces the visible target with 100 independently animated faceted fragments in one mesh for 1.2 seconds. Timing stops before the effect; metadata remains for capture observers, the effect survives target cleanup, and no fragment adds a physics body or selectable collider. A local RNG preserves stimulus randomization.
- Verification: focused timer/wording tests first failed before implementation, then passed with existing gaze/history, layout and voice-isolation suites. Added Unity checks for gaze-relative cross geometry, phrase preparation, fragment count/motion, cleanup and RNG isolation. Refreshed Unity study integration passes, including fragment lifecycle checks (edit-mode fixture explicitly invokes runtime cleanup). Android Build And Run succeeded in 68.8 seconds; installed on Vive FA5AR3N00385 at 15:50:29 and running as PID 29803. Wearer validation of reminder timing/direction and the visual effect remains.
- Preserved existing eye-alignment, replay and other uncommitted changes. No commit or push.

## 2026-10-10 — Quick eye alignment (side conversation)

- **Replay follow-through:** additive replay fields preserve source/alignment provenance and both selected pre-alignment and final used rays in the fixed recording frame. New recordings use the measured eye origin without avatar recentering or double correction. Desktop controls now distinguish Recorded gaze / Before alignment, with alignment status/angles/ID at the cursor; Unity replay inspector shows the same metadata. Built `Trial Review 1.11.app`; end-to-end synthetic alignment/capture/JSON/render/seek/gap checks and all 8,134 tracked P020 legacy correction checks pass. Six desktop Python checks and 13 recording-link tests pass. Existing data retained.
- **Implemented locally:** Eye check now offers automatic three-dot quick alignment: one fit point and two independent checks. It estimates head-relative yaw/pitch bias from the actual usable EXT/native gaze source, requires stable valid delivery windows, and only enables a bounded correction after both checks pass. Gaps, insufficient data, unstable gaze, excessive offset, timeout, cancellation and worsening checks cannot accept a correction.
- Preserves raw EXT and native measurements plus the selected pre-alignment ray. Final used rays feed guidance, readiness, CSV/replay and the optional orange visual. Per-attempt JSON saves source, participant, input sequences, before/after errors and pilot settings; each eye row includes the alignment ID/status/applied angles. Save failures disable correction. Source/participant/device changes and app focus loss/pause clear it; no automatic reuse across restarts or mandatory trial gate.
- **Verification:** reproduced the missing quick-alignment implementation with an isolated Unity compile, then ran Unity synthetic fit/held-out, rotated-head, unchanged-raw, native fallback, source/participant change, duplicate, invalid-gap, timeout, noisy/large/zero-offset and JSON-result checks. Runtime and editor study assemblies compile using the current Unity compiler references; 22 eye-quality Python tests pass. No study/device restart, commit or push from this side chat; main work preserved.
- **Open validation:** wearer UI/collection behavior, full-layout accuracy and drift, pilot acceptance limits and actual installed build. This is offset alignment after HTC calibration, not a research-grade accuracy certification or proof of 120 Hz. See `docs/gaze-measurement-validation.md` for controls, fields and limits.

## 2026-10-10 — Wrist mixed-reality toggle

- **Implemented locally:** connected the existing wrist passthrough toggle to HTC's native underlay, labeled it Mixed reality, synchronized template environment fading, and applied transparent camera clear on / opaque off. Startup now explicitly reveals passthrough instead of relying on skipped template onboarding. Disable/destroy releases the native layer and restores camera settings; reenable reconnects the settings listener.
- Controls remain in the fixed panel left/below the initial heading. Right-ray Settings opens the former wrist menu; no left-hand gesture is required. Fresh runtime/editor assemblies passed study integration, including the new mixed-reality bridge tests. After resolving the qualified HTC handle type and waiting for concurrent eye-alignment source imports, Android Build And Run succeeded in 160.6 seconds. Verified install at 15:36:27, running PID 25705 and native underlay creation XR_SUCCESS on Vive FA5AR3N00385. Wearer on/off visibility check remains. No commit/push.

## 2026-10-10 — Closest-object gaze confirmation and prompt reentry

- **Implemented locally:** removed the gaze-confirmation six-second cooldown and shortened valid departure rearming from 0.5 to 0.15 seconds. Returning still requires a continuous 0.5-second hold; lingering, brief boundary jitter and blinks do not repeat acknowledgment. Wall and controller gate defaults are unchanged.
- Overlapping vicinities now compete by gaze-to-collider-center angular distance across active spawned objects with enabled colliders. Confirmation requires a unique target winner; near ties are withheld. Looking at a closer distractor counts as target departure even within the target ring. Both gaze-only and controller-plus-gaze cues use this winner, rechecked before playback. Dev ring green state matches eligibility.
- Added optional guidance audit fields for winner ID, candidate count, ambiguity and closest angular error. Raw geometric intersection remains separate from vicinity assignment and neither is a validated fixation.
- Reproduced reentry failure in script tests and overlap ambiguity in Unity before fixes. Added three-ring, ordering, inactive/disabled object, runtime gate, invalid-gaze and jitter regressions. All script suites and refreshed Unity study integration passed; gaze-only Android build succeeded in 94.4 seconds. Vive reconnected. Combined build with the subsequently requested mixed-reality toggle succeeded in 160.6 seconds and is running on the Vive (PID 25705). Earlier uncommitted changes preserved.

## 2026-10-09 — Bounded continuation and half-second vicinity confirmation

- **Implemented locally:** first-trial recording from development run 007 showed five consecutive “Keep going.” requests at roughly 1.25-second spacing. Each 2-degree improvement requalified because the policy lacked continuation history. Now only one generic continuation is permitted per uninterrupted direction; subsequent cues use specific direction variants.
- Changed gaze confirmation to a 0.5-second continuous valid hold inside the existing padded target vicinity, including its development wrist-adjustable radius. Added four prepared confirmation variants. Holding does not repeat; invalid gaze/region departure resets incomplete holds. Exact ray intersections remain separately logged and are not relabeled as fixations. Matching eye/controller evidence suppresses a redundant second acknowledgment.
- Reproduced the recorded-angle loop in an existing regression suite (failed before fix, passed after). Script suites for gaze/history, layout and voice isolation pass. Refreshed Unity study integration also passed, including actual runtime gate and continuation-history checks. Android Build And Run succeeded in 69.4 seconds; verified updated install at 12:27:50 and running PID 24741 on Vive FA5AR3N00385. Existing recordings retained. Wearer confirmation of pacing and vicinity behavior remains. Earlier uncommitted work preserved; no commit/push requested.

## 2026-10-09 — Pre-search opening, direct gaze feedback and diagonal refinement

- **Implemented locally:** cross readiness now precedes an audible opening direction while objects remain hidden and the search clock is stopped. Matching completed playback reveals objects and starts timing. Pausing cancels the opening and resets readiness; stale/cancelled audio cannot start search. Existing active-search instruction counts exclude this pre-search cue, which is retained in audio/readiness events with its decision ID.
- Added “You're looking right at it.” for stable direct target gaze without a target controller hit. Kept the one-second controller confirmation and larger gaze-vicinity circle distinct. Within-wall refinement now combines both axes into four diagonal directions, with three variants each and axis hysteresis.
- Tests first reproduced the premature onset and missing diagonal behavior. Gaze policy/history, layout and voice-isolation checks pass; refreshed Unity study integration passes, including direct-hit vs vicinity/occlusion and opening onset/pause/stale-trial guards. Final refreshed Unity integration and all three script suites passed. Android Build And Run succeeded in 215.1 seconds; verified install at 10:03:28 and running PID 27938 on Vive FA5AR3N00385. Existing device data preserved. Wearer validation of cue timing, diagonal usefulness and direct-gaze reliability remains. No commit/push.

## 2026-10-09 — One-second pointing confirmation

- **Implemented:** reduced the sustained right-controller target hold for “You're on it!” from 1.5 seconds to 1 second. Valid nearby gaze, tracking freshness and exit/rearm behavior remain unchanged. Updated guidance policy version so recordings identify the new timing.
- Updated boundary, reacquisition and tracking-recovery checks and runtime documentation. Gaze policy/history script and refreshed Unity gaze checks passed. Android Build And Run succeeded in 414.8 seconds; verified new install and running PID 21996 on Vive FA5AR3N00385. Existing device data preserved. Wearer timing check remains.

## 2026-10-08 — GitHub checkpoint

- Prepared all current recovered-study changes for GitHub in two commits: headset guidance/readiness/prompt/menu fixes, and trial analysis/desktop replay with supporting documentation. Retained the existing build and test evidence below.
- Checked changed files for credential patterns, oversized files and whitespace errors; ignored API configuration, build outputs and local participant recordings remain outside the commit. Target: `origin/codex/recovered-study-20261007`.

## 2026-10-08 — Stationary eye and settings controls

- **Implemented locally:** removed per-frame head following from the Eye Check launcher, diagnostic panel and left settings controls. Menus anchor in world space and stay upright while the head turns, tilts or translates. Opening/reopening places panels near the current view once; starting a validation check moves its panel below the target grid once, using the same fixed anchor as the dots.
- Pause/Continue checkpoints also stay world-fixed and reposition only when shown again after Settings closes. Right-ray input, hover haptics, pause behavior and diagnostic target measurements remain unchanged.
- Explicitly refreshed Unity scripts and passed the full study integration suite, including a new head-translation/yaw/pitch/roll regression for launchers, open panels and Continue, plus reopening placement. Android Build And Run succeeded in 63.7 seconds; verified fresh install and running PID 32016 on Vive FA5AR3N00385 with activity status ok. Existing device data retained. Wearer comfort/reach check remains. No commit/push.

## 2026-10-08 — Sustained target confirmation and continuing guidance

- **Implemented locally:** “You're on it!” after a 1.5-second tracked right-controller target hold, with valid nearby gaze at speech onset. Blinks do not erase controller evidence; confirmation finishes through a later pointer departure, while invalid gaze and lifecycle stops still interrupt.
- Added “Keep going.” for same-direction progress; retained explicit directions when stalled, moving away, changing direction or manually requesting a repeat. Added 0.35-second speech settling and within-wall hysteresis, and suppressed redundant same-direction overshoot interruptions.
- Inspected the latest development recording: pointing speech was cancelled about 0.06 seconds after commitment and a downward hint was immediately replaced by a downward return cue. Original recording retained on device; read-only local copy at `/tmp/gaze-guidance-followup/recent.jsonl`.
- Gaze-policy, rotational-layout and voice-isolation tests passed; first new tests failed against missing behavior before implementation. Unity integration passed including the runtime 1.5-second hold and controller evidence during a blink. Final foreground Unity integration passed. Android Build And Run succeeded in 86.1 seconds (`/tmp/gaze-prompt-editor-20261008.log`); verified fresh install, running PID 30761 and activity status ok on Vive FA5AR3N00385. Existing data retained. Scoped inline review per AGENTS (no subagents): reused the entry gate, consolidated direction/error logic and checked speech priority after controller departure. No commit/push. Wearer validation remains.

## 2026-10-08 — Auditable P020 gaze-coordinate correction

- Added an exact-recording correction profile keyed by SHA-256, recording ID and build GUID. It reverses the historical local-X origin reflection using each saved XR-origin pose; direction is unchanged and the actual eye–head offset is retained.
- Replay distinguishes Corrected gaze from original Raw gaze. Missing valid reference data suppresses correction, without substituting a head-anchored ray. No recording, hit ID, summary or trial metric is rewritten.
- Added a separate derived CSV/audit exporter: 9,703 sample rows, 8,134 tracked corrections, explicit invalid gaps, source hash and profile. Median gaze–head offset 1.655 m → 0.0241 m. Readiness cross angle median 20.66° → 8.40°; remaining error is not calibrated accuracy, and readiness membership is not independent ground truth.
- Added synthetic reflection/frame-transform tests and real-file regression checks for every tracked sample, original-data preservation and reference validity. Current study Assets remain untouched.

## 2026-10-08 — Larger replay display selectors

- Replaced tiny viewport checkboxes with 42-pixel-high clickable controls, 16-point labels and 22-pixel state boxes. Seven display options wrap into two to four columns based on available width.
- Reserved space below the viewport for all controls and the room-context note; display toggles retain existing behavior.

## 2026-10-08 — Seated replay chair clearance

- Moved the complete illustrative chair 14 cm backward relative to the avatar to reduce seat/knee clipping; body and recorded head/controllers are unchanged.
- Built Trial Review 1.8, passed the existing Unity verification suite and real P020 checks, and visually inspected seating at 30 s. Updated both P20 launchers. Researched alternative Kenney appearances; no avatar replacement selected or installed.

## 2026-10-08 — Remove irrelevant left-controller replay ray

- Desktop replay now omits the left-controller ray while retaining right-controller and eye-gaze rays. Historical left-hand data and controller/hand model remain unchanged.
- Change is confined to the standalone renderer adapter; added a both-controllers-tracked regression check. No main study Assets changes.

## 2026-10-08 — Gaze visible before cross charge completes

- Fixed the desktop replayer eye-origin ray being gated only by object visibility. It now also appears during fresh, active, unpaused readiness, including zero and partial cross charge.
- Valid head/gaze and freshness checks still apply; pause, transition and stale samples suppress the ray. Study recording data and main Unity project are unchanged.
- Added regression coverage for readiness visibility, recovery, toggles and rewind, and a real P020 charging-sample check (0.5474 s, 8.34% charge).

## 2026-10-08 — Replay wall and room toggles

- Implemented locally in Trial Review 1.5: independent Study walls and Room background toggles. Eight-wall rotational snapshots get inferred octagon outlines; the generic backdrop adds a neutral floor/grid and cutaway outer walls. Both default on and are labeled illustrative/inferred.
- Context uses snapshot origin/rotation and object extents; no study object, tracking, or measurement data changes. Unknown layouts do not get invented octagonal study walls.
- New Unity checks pass for independent toggles, radius/origin/rotation, trial switching/rewind, empty/unknown snapshots and data preservation. Existing replay checks plus 98 P020 camera samples and three Python tests pass. Built and visually checked the P020 room/walls; updated launchers to 1.5. Main study Assets/device untouched.

## 2026-10-08 — Replay hands and eye-origin presentation

- Implemented locally in the isolated Trial Review desktop viewer: both hands follow recorded controller grip anchors using deterministic two-bone IK; elbows/arm length adaptation remain illustrative. Controllers retain exact recorded transforms. Extreme reach is bounded and disclosed; invalid tracking resets the affected arm.
- Added default eye-origin gaze display and separate Raw gaze toggle. The display preserves recorded direction and discloses raw-origin distance; no recorded samples, hit IDs, or metrics change. P020 at 30 s has about 1.52 m raw-head origin separation, requiring a separate coordinate audit.
- Built and visually inspected Trial Review 1.4 on P020. New Unity checks pass for both hands, grip orientation/position, limits, rewind, gaps, invalid tracking, eye origin/direction and raw preservation. Existing replay/audio/CSV checks, 98 real camera samples and three Python preparation checks pass.
- Updated P20 launchers. Main study Assets, Unity session and headset build were not changed by this replay work. Finger gripping and participant-specific body proportions remain illustrative/untracked.

## 2026-10-08 — immediate vertical arrival and controller/gaze confirmation

- **Implemented locally:** area arrival includes immediate up/down refinement in one prepared clip; pointing confirmation requires an exact right-controller target hit plus valid gaze in an expanded circular angular region. Fresh controller telemetry and audio-fade revalidation prevent stale confirmations; stable-entry/latching avoid continuous repeats.
- Added development-only admin wrist controls for the target vicinity ring and 0–20° extra radius (1° steps, default 6°). Study sessions hide the ring and retain the default; selection colliders and raw gaze remain unchanged. Added angular/ray evidence and admin-setting events to guidance decision snapshots.
- **Verification in progress:** the new phrase-library regression first failed for the missing arrival clip, then passed. Rotational layout, gaze policy/history and voice isolation scripts pass. Unity integration passed for angular vicinity, correct/distractor target hits, stale controller data, and development-only settings. A scoped inline correctness/reuse/quality/efficiency review kept the radius separate from selection/fixation data, removed redundant opening state, and aligned follow-up guidance with vertical-first arrival. Final foreground Unity integration passed, including rendered-ring geometry verification; inspected `/tmp/gaze-vicinity-ring-preview.png`. Development ring previews remain live while settings pause the trial, without advancing speech or guidance gates. Final Android Build And Run succeeded in 407.7 seconds (`/tmp/gaze-prompt-editor-20261008.log`); verified install timestamp, running PID 28816 and activity launch status ok on Vive FA5AR3N00385. Existing app data retained. Hardware tolerance validation remains open.

## 2026-10-08 — human, Vive controllers and swivel chair in replay (side chat)

- **Implemented locally:** CC0 Kenney human in third-person/overhead, seated on an illustrative swivel chair; official Focus 3 controller meshes from the installed SDK follow independent recorded controller poses. First person hides the body; tracking gaps hide unavailable models.
- Chair seat follows estimated torso yaw while recorded head rotation remains independent. A labeled display-only neck allowance/turn-rate estimate supports deterministic scrubbing; actual torso/chair motion was not recorded. Static hands do not imply tracked full-body pose.
- Trial Review 1.3 Mac build and new actor/torso checks passed, as did P020 pose/CSV checks and three isolation tests. Opened and visually verified P020 third person. Vendor notices/provenance included. No study/device modifications, commit or push.

## 2026-10-08 — standalone Trial Review desktop app (side chat)

- **Implemented locally:** separate macOS Unity app with first-person recorded-head reconstruction, orbit/overhead, saved audio, trial/event/sample navigation, timeline and decoded sample inspector, plus exact-hash-matched final performance/subjective CSV context. Reuses shared replay code in an isolated generated project; no live-study Assets/settings changed.
- Universal app built successfully; three isolation tests and Unity state/audio/CSV checks passed. Verified 98 P020 head poses across nine recorded trials and matching actual CSV joins. App launched with copied P020 package; truncated ending is prominently labeled. Native UI mouse automation was unreliable; hands-on controls/audio validation remains.
- **Open:** MP4 export, passthrough footage and embodied third-person avatars are outside this version. No device deployment, commit or push. See `work_log/2026-10-08-trial-review-desktop-side-chat.md` and `tools/trial-review/README.md`.

## 2026-10-08 — shared coaching-style study prompts

- **Implemented locally:** one `StudyPromptCard` resource/component supplies the original coaching Inter font, dark surface, rounded template sprite and shared button/text styling across voice setup, recording/loading/audio checks, start/readiness instructions, trial status and block/completion screens. Rewrote participant instructions as a brief heading plus an action. Survey item wording, scoring, hidden-target rules and gaze-cross stimulus remain unchanged.
- Removed the redundant original Coaching UI scene instance and its wrist-menu relaunch button; kept the shared style source prefab. Clarified that surface selection is obsolete: removed surface-tap copy from study setup; current eight-wall trials start through seated centering, without a surface tap. Checkpoint cards now also use the shared component. Preserved background ray blocking in the shared card.
- Original CoachingCardRoot sprite reference was missing in this recovered project; the shared card uses the surviving rounded sprite from the same MR template. Resource generation validates both sprite and font. Reused original screen actions/layouts, right-ray trigger confirmation, haptic hover, six avatars and selection borders.
- **Verification:** first full Unity integration and ray-menu checks passed; inspected rendered voice menus and survey. Corrected underlying participant-status text showing through survey headers. Final ray-menu and full study-integration checks passed (`/tmp/gaze-prompt-final-ui-20261008.log`, `/tmp/gaze-prompt-final-integration-20261008.log`); rendered setup/voice/survey/completion cards inspected. Final ray-menu checks also passed in the foreground Editor after preserving card ray blocking. Final Android Build And Run succeeded (233.4 seconds; `/tmp/gaze-prompt-editor-20261008.log`), installed and launched on Vive FA5AR3N00385 (PID 20376; activity status ok). Existing data retained; wearer check of comfort/readability remains. Scoped inline reuse/quality/efficiency review removed obsolete color parameters; review stayed within prompt edits, preserving pre-existing research/runtime/analysis work.

## 2026-10-08 — clarified trial measurements and NASA-TLX scoring (side chat)

- **Implemented locally:** v2 offline export uses final correct selection timing, unique zones plus stable transitions/revisits, and planned target height relative to the recorded readiness-cross/plane midpoint. Valid gaze and source-aware continuity gate observed zone counts; debounce is a pilot export policy, not fixation validation. Removed the proposed incomplete-trial pause calculation.
- Added explicit six-dimension raw NASA-TLX mean (0–100), validated scale/direction metadata, corrected legacy totals and aligned subscale plots while preserving raw responses. Historical ambiguous surveys remain unscored.
- Full analysis suite passed (178 tests); independent reviews completed and synthetic workbook/CSV package refreshed. **Open:** headset validation, runtime survey endpoint labels and scale metadata, and any height-balancing quotas. No Unity/device/build, commit or push actions. Handoff: `work_log/2026-10-08-trial-demo-export-side-chat.md`.

## 2026-10-08 — readiness cross rejected valid HTC eye data

- **Diagnosed:** DEV_NEUTRAL run 002 never charged because EXT `isTracked` remained false; 4,264/4,999 inspected rows had at least one valid native eye, including 4,235 binocular rows. Native-binocular and EXT directions differed by a median 0.36 degrees; this agreement is not independent accuracy validation. The later headset-off rows correctly showed tracking loss.
- **Implemented locally:** require fresh, explicitly valid binocular HTC rays plus tracked head/focus for a labeled derived-ray fallback when EXT is untracked. Retain raw EXT flags/poses; no fabricated validity, head fallback, loosened charge threshold or rate claim. Added regression for the observed flag disagreement, blinks, monocular loss, stale sources, absent tracking parent/head, invalid rays and preservation of valid EXT.
- Red regression reproduced the bug in fresh Unity (`/tmp/gaze-native-red-20261008.log`). Final full Unity integration passed (`/tmp/gaze-native-final-check-20261008.log`) and focused gaze policy/history checks passed. Also separated offline raw EXT spatial/event/offset analysis from valid native-derived rows, with explicit source labeling/counts; preserved CSV/replay and used-ray availability. Native derived-ray accuracy and eye-event validation remain open. Targeted manual review kept unrelated branch/side-chat edits out of scope. All 179 analysis tests passed, including the source-separation regression. APK build succeeded (207.9 seconds; `/tmp/gaze-native-build-20261008.log`), installed with `adb install -r`, and cold-launched successfully on Vive FA5AR3N00385. Unity reopened, existing data retained. Physical cross charging and independent known-target accuracy still require the wearer.

## 2026-10-08 — trial demo sheets and separate motion export (side chat)

- **Implemented locally:** added optional per-trial/combined motion CSV export, explicit synthetic/development exclusion labels, and pre-read/final source fingerprint checks. Added reproducible three-case synthetic demo sheets, block survey examples and a full trial-column dictionary. Raw replay remains necessary for object/event/audio reconstruction.
- Two independent reviewers checked joins, missingness, source provenance and all 156 example motion records. Fixed source-change and symlinked-survey regressions. Full analysis suite passed (162 tests); workbook inspected and rendered successfully.
- **Open:** fresh headset export validation, final-zone/zone-count definitions, signed-height quota reference, and downstream filtering of development data. No Unity/device/build, commit or push actions here. See `work_log/2026-10-08-trial-demo-export-side-chat.md` for files, artifacts and main-chat handoff.

## 2026-10-08 — quick-test first-trial readiness fix

- Device run `DEV_NEUTRAL/run_001` stopped with `readiness_start_wall_mismatch` before search (0 seconds): the skipped practice sequence left the actual start at wall 0 while the saved schedule expected wall 3. Preserved the stopped run.
- **Implemented:** quick-test schedules now anchor to the centered forward wall 0, with a distinct `quick-origin0-v1` schedule suffix and mode-aware cache invalidation. Normal study schedules still continue from the final practice target. All planned signed thetas and subsequent previous-target transitions remain unchanged.
- Reproduced the defect with a failing regression, then passed the fixed schedule/voice tests, including same-ID mode switches. Added a Unity regression exercising the real readiness coroutine for all 40 quick-test transitions. Fresh Unity integration passed, including the actual readiness coroutine for all 40 quick-test transitions and a forward first cross. APK build succeeded (706.5 seconds), `adb install -r` succeeded, and cold launch returned `Status: ok` on Vive FA5AR3N00385. Existing data/audio retained. Verification/build logs: `/tmp/gaze-quick-origin-check-20261008.log` and `/tmp/gaze-quick-origin-build-20261008.log`. Cleared only unused LMDB lock files (preserved under `/tmp/gaze-unity-stale-locks-20261008-1791505598`) to recover a stalled Editor startup; asset databases remained intact.

## 2026-10-08 — restore neutral voice configuration and relaunch

- Diagnosed device failure: the recovered checkout had no ignored `Assets/StreamingAssets/api_keys.json`, leaving neutral synthesis without a provider credential. Linked the original project's existing local file into this checkout; the source remains unchanged, and the link is Git-ignored. No credential values were printed or committed.
- Verified actual Eric speech synthesis for “The object is to the left.” with HTTP 200. Rebuilt and ran on the connected Vive successfully (113.5-second incremental build). Fresh device process 14532 reports `[VoiceAssist] Loaded API keys` and `Ready. Key=present`. Existing app data remains intact; Unity stays open.

## 2026-10-08 — deploy neutral quick test to Vive

- Corrected the Android build helper to set both the selected build-target group and BuildPlayer target group to Android. The installed OpenXR validator reads the selected group; stale desktop selection incorrectly rejected the already-enabled Android Composition Layers feature. No XR feature settings were changed.
- Vive Focus Vision `FA5AR3N00385` is connected and authorized. Android APK built successfully in 572 seconds; `adb install -r` succeeded and Android launch returned `Status: ok` with the app process running (PID 12770). Existing app data was preserved. The headset was asleep at launch; sent a wake event and reissued launch successfully to the existing top-most app. Headset interaction still needs wearing/waking the Vive. Build log: `/tmp/gaze-neutral-quick-build-20261008.log`.

## 2026-10-08 — neutral quick-test shortcut

- **Implemented:** added a right-controller ray button on the first setup screen to enter a session-only neutral development mode. Eric's neutral library is prepared without clone enrollment; study intro, audio acceptance and practice are skipped, while centering, trial readiness, guidance and loggers remain active.
- Runs contain one 40-trial block (or the existing shorter debug override), with matching schedule flags. `DEV_NEUTRAL`, the run-folder prefix, session-mode sidecar and trial-summary mode distinguish test data. New-participant reset restores normal study behavior. Audio preparation failures stay blocked with a neutral retry/cancel path.
- Verification: focused voice checks pass for neutral-only preparation, missing clone, development identity, schedule and reset. Unity offline study integration passed, including the start-screen ray button, unarmed/no-audio guard and blocked quick-test failure path. Required Build And Run was dispatched but failed the existing OpenXR Composition Layers Support validation; no APK was deployed and no Vive is connected. Unity remains open. No paid/provider calls were made during development.

## 2026-10-08 — initial target direction and gaze-history return cues

- Implemented the first directional cue as “The object is to the left/right,” with truthful above/below/ahead cases when already horizontally aligned. Practice/new trials reset the opening; same-trial resume does not. Both voices prepare all 53 guidance phrases.
- Added a bounded three-second target-relative gaze history with source IDs/timestamps and same-wall horizontal/vertical passage detection. Invalid/stale gaps, duplicate deliveries, device/recording changes and pauses cannot manufacture a crossing. Direction-specific return cues revalidate before interruption; frozen history is retained in guidance-decision telemetry. Thresholds are pilot settings, not validated fixation or perception evidence.
- Focused gaze-history, rotational-layout and voice-isolation checks pass; all 156 Python analysis tests pass. Unity 6000.3.10f1 offline study integration passed, including opening-cue trial/resume lifecycle and history serialization (`/tmp/gaze-target-history-unity-verified-20261008.log`). No provider audio was requested during development.
- Opened the recovered project in Unity and dispatched the required Build And Run; Android preprocessing failed OpenXR validation because Composition Layers Support is required by the installed Composition Layers package (`/tmp/gaze-target-history-editor-final-20261008.log`). No APK was deployed. No Vive is connected; headset behavior remains unverified. Unity remains open.

## 2026-10-07 — recover the saved study implementation

- Restored tracked source at `8da2eea` into the managed `gaze-recovered` worktree on `codex/recovered-study-20261007`; original edits remain untouched. The two older missing protocol/material checkpoints are already included, and no extra staged changes remain in their missing worktree indexes.
- Re-ran all 156 analysis tests successfully and verified shared Python environment helpers. Preserved surviving P020 preview HTML and embedded nine-trial data with hashes as derived, incomplete evidence. Original raw P020 files, generated results and APK were not found in checked local locations; headset copies remain unchecked because no device is connected.
- Documented voice hint preparation, local geometry/phrase selection, cache playback, interruption and timing. Identified the stale self-similar `FirstPerson` audio metadata label for follow-up; no runtime logic changed. No build, deployment, paid audio calls or push. See `docs/overnight/2026-10-07-recovery.md`.

## 2026-10-01 — overnight continuation

- 06:00 EDT: overnight work window complete. Final handoff reconciled: approved three-script restore is complete; #11/#23/#24 issue bodies read back exactly with remaining device/research gates and In Progress status. Corrected phrase inventory to 44 guidance variants plus four success responses. Current APK fingerprint remains verified; Build And Run was dispatched, but no terminal result or connected Vive is present. No push or headset acceptance claim.

- Re-exported the original incomplete P020 package under the new motion method: 22 stable measured/practice IDs, one row per file, eight changed motion rows and matching source hashes; local ignored comparison artifacts retain warnings and leave originals unchanged. Added explicit three-directory (`GazeData`, `EyeMeasurements`, `GazeReplays`) transfer instructions because the older analysis pull fetches only trial data. No device copy was performed.

- Reproduced omitted trigger-time motion in trial CSVs (synthetic path 1.0 m exported instead of the observed 1.4 m). Motion method v2 now includes tracked correct/incorrect selection poses, preserves invalid/gap/pause breaks, excludes post-capture movement and identifies a selection-only geometry reference. Frame gaze-quality denominators remain unchanged. Four regressions and all 156 Python tests pass; new method/count fields preserve export provenance.

- Extended strict import to shared replay parsing after reproducing a textual `complete: "false"` footer accepted without warning. Reject non-finite JSON and malformed declared schema/sequence/time, booleans and tracking states before downstream metrics. All 152 Python tests pass; P020 retains exactly 10,119 records / 9,703 samples with its existing incomplete-prefix warnings. Corrected #20 documentation: repeat request/delivery counting is implemented; zone-arrival/revisit definitions remain open.

- Reproduced a raw-eye import false pass: JSON `valid: "false"` was treated as true and yielded 100% valid time; a complete receipt without a row count was also accepted. Added strict declared-boolean/numeric identity checks and typed clean-close row counts, preserving missing-count receipts as incomplete. All 151 Python tests pass, including the combined audit. Original raw data remains unchanged.

- Added `analysis/audit_recording.py` to run session, exact raw/replay/audio links, guidance and eye-quality validators from explicit local inputs. It retains failures and incomplete evidence, source hashes/change checks, command logs and declared short/full-session context; seven real-command regressions pass and the full Python suite now passes 149 tests. No hardware, survey or acoustic acceptance is inferred. Corrected the Unity refocus helper's misleading blanket permission advice for a locked/unavailable menu.

- Audited HTC requested-pose time and Khronos EXT expressed-pose time against the primary specifications and installed SDK. Documented the proposed native diagnostic route, its limits and connected-device evidence matrix; corrected the measurement plan's stale pre-implementation/current-state wording. Native capture time/sequence and true 120 Hz remain unknown, not unlocked by a timestamp field.

- Added an original editable setup/session schematic (SVG, vector PDF, PNG, caption/alt text and builder) for #30, with 40+40, separate practice, hidden target and right-controller selection. Replaced stale table/dwell/old-count runtime and octagonal guides; historical generated artifacts are explicitly labeled stale and preserved. Removed the unsupported blanket accent claim from the run sheet; native compilation passes. Figure rendered and inspected; final venue sizing, protocol ordering and hardware/research acceptance remain open.

- Verified the actual incomplete P020 source in both replay viewers: all 44 sampled/event cursor comparisons agree on original sample fields, source lines, freshness and 168 object positions. Original bytes remain unchanged; desktop/mobile pages were inspected. Fixed browser event jumps to preserve sequence boundaries, including multiple events at the same timestamp; both browser suites pass. P020 lacks the current gaze-link contract and matching video, so this is historical reconstruction evidence only.

- Native full-session replay now seeks from saved trial snapshots while retaining absolute source lines. Full-prefix equivalence passed across 80 trials/exact boundaries/random rewinds. In the 652 MB synthetic fixture, 240 late/mid/early seeks improved from p95 111.6 ms to 1.35 ms; no GUI/headset performance claim. Reproduced and fixed missing selection-time object movements in both capture and reconstruction; full Unity integration passed. Saved reproducible benchmark evidence.

- Reconciled the existing survey references with 40+40 trials, separate practice, six choices per gender and hidden-target shared guidance. Added separately labeled draft reliance/use-percentage items and clarified neutral debrief probes, timing and response provenance. Both native LaTeX compilations pass; final layout, anchors/burden and capture/coding choices remain researcher review. Updated/read back #26; no live survey or import restoration.

- Reworked native replay audio to preserve overlapping success chimes and speech, per-event volume for reused clips, natural/recorded stop limits and seek offsets. Playback owns disposable preview-scene sources; malformed WAV lengths/rates and invalid playback volumes are rejected. Full Unity integration and the silent real-engine check pass, including independent sources, seeking, cleanup and unchanged study-scene state. Acoustic listening remains a morning check.

- Native Unity replay now gates poses/highlights on freshness and known tracking, clears prior poses at phase boundaries, labels recorded-head fallback, steps exact equal-time samples and exports source-linked cursor snapshots. Full Unity integration and a graphical 168-object render passed; browser phase-boundary regression passed. Source records remain inspectable through gaps. Real headset comparison and interactive rehearsal remain open.

- Full-session timeline stress checks reproduced a JSON parsing failure in the old 570 MB single-payload page. Split raw-import HTML into per-trial payloads, parse only the selected trial, preserve all source observations, publish atomically and label headset/editor/synthetic/unknown origin. An 80 × 60-second synthetic run (432,000 samples, 168 objects/trial, 758 MB HTML) passed all trial-identity checks; observed load 2.68 s, p95 switch 34 ms and scrub 2.4 ms on this Mac. All 142 Python tests and both browser suites pass. Saved reproducible scripts and measurement evidence; hardware/video comparison remains open.

- Added direct replay JSONL import to the browser timeline (#37), exact source/sample inspection and stepping, recorded object transformations/visibility, pause-aware search lanes, stale/invalid pose handling, event/object inspection and cursor JSON export. Optional trial-summary joins require exact replay hash and identity and keep final/survey context separate. All 139 Python tests and both Chromium suites pass, covering synthetic 80-trial context, duplicate timestamps, backwards seeking and existing local-video controls; full-session browser performance and actual headset/video comparison remain morning checks. No runtime/APK changes in this unit.

- Extended recording-links v2 with referenced PCM16 audio validation, missing-clip/recorder-failure findings, bounded local asset reads, traversal/symlink rejection, source hashes and reusable-clip event inventories. All 130 Python tests pass, including corrupt header/length/rate cases, path protections, duplicate references and CLI mixed incomplete-report output. No participant audio was uploaded or regenerated; listening, actual headset playback and video validation remain open.

- Advanced #16 with a reproducible, explicitly hypothetical 40+40 voice sensitivity package: participant-level order-adjusted log-time contrasts, trial correlation/geometry, heterogeneous voice effects, random/informative loss, paired benchmarks and recruitment assurance. Saved 120 scenarios × 2,000 simulations, tables/figure/configuration/hashes. All 123 Python tests pass. Final primary outcome, effect threshold, model/missingness rules and enrollment remain researcher decisions; no participant data or protocol approval used.

- Added exact replay-to-raw gaze recording identity on every sampled/selected observation and a versioned link contract. New recording-links audit checks run/trial/voice/object/selection consistency, timestamps and world/local coordinate transforms; preserves invalid/legacy/incomplete evidence and hashes explicitly supplied inputs. Real production-header/reopened-stream Unity tests and all 115 Python tests pass. Fresh Android APK packaged successfully in 84.3 s (71,125,376 bytes); device installation/measurement validation remain pending. Updated replay documentation to the current 40+40/shared-command/right-only flow.

- 02:08 EDT: Android APK rebuilt successfully with the completed-trial preservation fix (269.7 s; 71,124,916 bytes; fingerprint in morning report). Updated current system overview/study hub to remove obsolete dwell/shelf guidance; current overview/QA links pass. No device deployment.

- 02:03 EDT: saved validator/QA fixes in `d9a8aa1` (103 Python tests). Reproduced stop-during-success corruption in actual Unity summary output, then preserved completed trial outcome/duration while retaining the session technical stop. Regression covers rounds 0, 39 and 79 plus unfinished controls; full Unity integration passed (`/tmp/gaze-unity-stop-integrity.log`). New APK rebuild running; previous package predates this fix.

- 01:59 EDT: reproduced and fixed session-validator false passes for unfinished/duplicate trials, contradictory outcomes, NaN/missing/negative timing and impossible pause/resume sequences. Readiness pauses no longer distort active duration. Interrupted trials retain timing/audio checks. Full current 80-trial packages pass in both voice orders; all 103 Python tests pass (`/tmp/gaze-python-session-integrity-final.log`). Found a separate runtime stop-after-capture outcome edge for follow-up.

- 01:53 EDT: launched the correct graphical Unity project. Refocus/Build And Run helper was attempted and exited 1 because no Unity menu bar was available while the Mac was locked; ADB lists no devices. Offline APK remains valid, but no device deployment is claimed. Replaced stale QA instructions (14 trials, shortcuts, countdown, distinct wording) with the current 40+40/right-ray/shared-guidance and progressive audio workflow.

- 01:50 EDT: saved the reviewed runtime, controller, voice, replay and validator changes in local commit `77f85f6`; the Git compile hook completed a fresh successful Unity compile. No push. Scoped commit excludes all unrelated deletions and Unity-generated ProjectSettings change. Task #27 update read back successfully.

- 01:50 EDT: reconciled the open run sheet with 40+40 measured trials, two separate practices, hidden target, shared guidance, right-controller controls and six voices per gender. Native LaTeX compilation succeeded. Added an explicit current/historical boundary to the procedure register; height balancing, hardware validation, final survey/interview/consent details and live publication remain pending.

- GitHub #34 moved to **Ready for headset QA** and read back in Project 2; only the completed software checklist items were checked. The issue remains open/unverified. Test the complete right-controller-only flow with the left controller off; exact steps are in the updated issue.

- 01:38 EDT: saved schedule-aware descriptive voice comparisons and unavailable-blink preservation in local commit `932f516`. The old hardcoded 14-trial pairing now supports the saved 40+40 schedule, rejects short/debug/restarted/malformed pairs and retains historical 7+7 support. All 95 Python tests passed. Fresh Android APK with the diagnostic panel fix built in 216.3 s (71,122,616 bytes); current SHA-256 is in the morning report. No device install/launch.

- 01:30 EDT: saved the seven analysis/report files in local commit `64d8325` (91 tests passing). Other index entries and unrelated deletions were excluded. Diagnostic panel clearance and full Unity integration passed (`/tmp/gaze-unity-diagnostic-clearance.log`); rebuilding Android with that fix. Native rate and headset checks remain open.

- 01:28 EDT: added per-phase delivery/capability/clock tables and timing figures; inspected a labeled synthetic outage plot. All 91 Python tests pass. HTC query counters deduplicate copied fields by host query time, and native-rate/loss conclusions remain unknown. Found and repaired diagnostic panel occlusion of right-side fit dots; full Unity geometry/integration verification is running. This small runtime change requires another APK before handoff.

- 01:18 EDT: Android packaging succeeded (305.2 s; current APK 71,124,672 bytes, SHA-256 in the morning report), no device install. Added offline exact-observation guidance audit and ten regressions for joins, two clock origins, invalid/stale claims, revalidation, cancellation, phrase mismatches, legacy/incomplete recordings and CLI safety. GitHub #15/#22/#24 reflect the implementation and remaining hardware checks.

- 01:07 EDT: added guidance decision/source-observation audit linkage, correction commitment/abandonment and correlated software playback events. Immutable snapshot, replay round-trip and voice lifecycle tests passed; full Unity integration passed (`/tmp/gaze-unity-guidance-provenance-final.log`). Python quality CLI regression brings the suite to 72 passing tests. New APK packaging is running; no headset validation claimed.

- 00:57 EDT: completed sequential self-review artifact (no independent peer credit), retaining two concrete measurement-plan gaps. Implemented per-trial search-only quality CSV and per-target direction bias/angular spread/time missingness. Invalid heartbeats split precision segments. All 71 Python tests passed; guidance decision provenance is the next remaining review item. Hardware acceptance is unchanged.

- Persistent Android APK packaged successfully (280.4 s, 71,123,184-byte APK); no device installed. Subsequent settings lifecycle repair prevents readiness/search running behind already-open Settings; ordered pause flush preserves the last eye/replay rows without blocking on storage. File-level stalled-writer and setup→readiness/search→Continue regressions passed in the full Unity suite (`/tmp/gaze-unity-pause-durability.log`). Rebuild required after review. Marked old beta-menu documentation explicitly historical and linked current behavior.


- Android packaging succeeded in 676.4 seconds (1145.9 MB reported), no device deployment. Found Temp output disappears at editor exit; switched helper to ignored persistent `Builds/CodexAndroid/` for next build. Expanded catalog to six per gender by appending Liam/Laura, preserving previous indices; all twelve cards now have fictional portraits, eight generated with built-in image tool and saved with prompt provenance. Catalog inventory snapshots are versioned per participant/run.
- Fixed shared eye/CSV trial linkage across capture/feedback and 39→40/79→80: a presented-trial context stays stable until the next actual transition. Practice remains separate; replay's voice routing is independent of listener order. Canonical Unity integration including new context fixture passed, pure voice/cache/saved-choice tests passed; both six-card graphical checks passed and were visually inspected. Latest full Unity integration passed (`/tmp/gaze-unity-six-final.log`), and all 66 Python tests passed after fixing empty-context annotation matching and cross-device event/precision continuity. Rewrote the authoritative gaze guide to match the new pipeline and guaranteed distinct layout seeds with collision retry (schedule suite passed). Persistent APK packaging is running.


- Full 40+40 schedule and right-controller startup/modal-input regression passed canonical Unity checks; all 63 Python tests passed. Graphical voice-menu checks passed and preview inspected. Updated #10/#13/#28 and project overview with actual local schedule progress; height quotas and headset acceptance remain open.
- Added offline APK build entry point with explicit batch failure propagation. Android packaging started without AutoRunPlayer (`/tmp/gaze-android-overnight.log`); no headset attached. No participant data deleted, external audio requested, or study approval claimed.

## 2026-09-30 — overnight run (ends October 1 at 06:00)

- Late-evening continuation: implemented 40 measured trials per voice (80 total), eight per absolute angle, generalized stratified randomization, same target-identity counts across voices with no adjacent repeats, and explicit pending-height-policy metadata. Voice/schedule checks passed across 50 participant IDs and both voice orders. Updated replay fixture for the actual new block boundary; full Unity integration passed. Python suite now 63 tests, including versioned manifest geometry/quota/identity and summary consistency. GitHub evidence refreshed on 13 tasks; 12 relevant cards In Progress, #11 no longer deferred. Right-ray policy now recovers from hand-mode startup; latest integration rerun passed (`/tmp/gaze-unity-40-trials-final.log`).


- User authorized restoring the three required scripts and autonomous in-scope task work until 06:00. Restored the dependencies; migrated the gaze CSV logger to SharedGazeStream, preserved invalid gaps and unavailable eye events, and made re-enable append-safe. Added an actual Unity CSV integration fixture and downstream missing-blink regressions.
- Fresh canonical Unity batch compilation and gaze checks passed, replacing the earlier temporary dependency-fixture evidence. Full offline study integration checks passed (gaze/CSV, real physics ray filtering, trigger gates, readiness, boundaries, voice choices and replay). Python analysis: 54 tests passed. Device accuracy/rate, physical haptics and listening remain pending.
- Added right-ray-only settings/pause/repeat controls, disabled left/hand/gaze UI activation, and retained existing settings content via front docking. Added versioned requested/delivered repeat counts and non-repeat hint counts, final-success event retention, and raw-eye interrupted-prefix/identity/row-count validation. Latest analysis suite: 60 tests; updated Unity right-only integration suite passed. Physical UI/voice/haptics remain in the morning verification list.
- Fixed stale-lock handling in the compile helper and shared-imperative voice metadata. Working report and verification list: `docs/overnight/2026-10-01-morning-verification.md`. Continuing with right-controller-only access and other unblocked project tasks; no issue is closed on the strength of implementation alone.

## 2026-09-30

- Implemented a shared validity-aware EXT gaze adapter with independently flagged HTC eye/pupil/openness probes, raw/native/world coordinate records, input delivery timestamps, invalid/stale heartbeats and bounded continuous JSONL recording. Removed late visual X flipping; replay/readiness/guidance/trial intersection summaries now consume the shared ray.
- Added right-ray researcher eye diagnostics and separate 54-point spatial validation, nine-point offset-fit, independent held-out and five-point drift checks. Added raw CSV/spatial quality reports, held-out offset evaluation and explicitly unvalidated fixation/saccade/blink candidates with annotation comparison. Native 120 Hz and hardware accuracy remain unestablished.
- Implemented shared imperative conversational guidance, within-wall vertical cues, validity cancellation, hidden-target trial/readiness wording, `Loading...`, and chime/varied success speech with transition ordering. Preserved existing five-per-gender catalog and clone audio treatment.
- Verification: runtime C# compilation passed in an external fixture using Git versions of three pre-existing deleted legacy scripts; this is conditional compilation, not a successful build of the current checkout. Focused gaze, layout, voice isolation and enrollment checks and 7 eye-quality / 22 trial-CSV tests passed. All 50 analysis tests passed using the repository's shared Python environment after system Python lacked pandas; runtime and editor assemblies compiled in the temporary dependency fixture. See `docs/gaze-measurement-validation.md` for operation and hardware gates.
- Updated and read back GitHub #14/#35/#22/#15/#11/#23/#24/#12/#20 with local implementation evidence and explicit hardware/validation blockers; no issue closed. Full independent/code-review delivery gate and actual Unity Build And Run remain pending the canonical dependency repair.
- Pending: response to restoring GazeDataLogger/GazeHighlightManager/ShapeObjectFactory; the deleted scripts remain untouched. No ADB headset was connected. No hardware validity/rate claims, task completion, commit or push.

## 2026-09-30

- Saved researcher decisions in `docs/meeting-reconciliation-2026-09-30.md`: shared participant-directed imperative wording, more conversational/less silent contextual guidance, 6–8 voices per gender, `Loading...`, varied success chime/reinforcement, descriptive filenames and separate trial-performance/subjective versus motion/reconstruction data. Retained hidden-target, right-controller-only and 40+40 decisions.
- Updated the study hub and added precedence notes to the September 28 reconciliation, voice-choice baseline and measures register. Extra cloned-voice prosody/personality remains an open pilot proposal; natural recording instructions remain, and possible voice-identity/style confounding is explicit.
- Updated and read back 15 existing GitHub issue bodies (#7, #8, #11, #12, #16, #19, #20, #21, #22, #23, #24, #25, #27, #29, #33), preserving older history and task status. No new duplicate tasks or implementation/completion claims. Local link and whitespace checks passed. Runtime/LaTeX sources were not changed for this notes update; no build, commit or push.

## 2026-09-28

- Researched ten primary/methodological gaze references and saved `docs/plans/2026-09-28-vive-eye-gaze-measurement-plan.md`, with direct DOI/full-text links, method-transfer limits, current source audit and implementation/test gates mapped to #14/#35/#22/#8/#15/#20/#16.
- Confirmed HTC documents standalone Focus Vision per-eye support and nominal binocular 120 Hz; installed wrapper's predicted-display-time usage does not establish native sampling. Found active X-position flip in the study scene with Update/LateUpdate measurement-order risk; requires known-target validation, not an untested removal.
- Proposed shared validity/freshness-aware acquisition, independent spatial validation/drift checks, head-aware event analysis, horizontal-then-vertical assistance and separately validated workload measures. No gaze runtime, protocol thresholds, GitHub status or participant data changed. Existing voice/guidance edits preserved.

## 2026-09-28

- Replaced runtime Voxtral cloning/TTS with ElevenLabs instant voice cloning and `eleven_turbo_v2_5`; both conditions share the model, retaining natural recording instructions and clone-only treatment. New cache namespace prevents old Voxtral audio reuse; manifests explicitly record voice condition.
- Expanded selection to five male and five female voices, preserving original saved option positions. Authenticated catalog lookup verified the supplied local key and voice IDs (including the Janet alias); no participant sample was uploaded.
- Added production-client transport checks for multipart WAV/authentication, verification and malformed responses, access/quota errors and non-audio rejection. Expanded selection/isolation checks across ten voices.
- Live TTS returned HTTP 200/audio for all ten choices. Unity voice-choice and ray-menu checks passed, including five cards per gender. Enrollment/client, voice-isolation and layout tests pass; Unity and LaTeX compilation pass. Build And Run could not install because no Android device is connected.
- Updated session validation to use explicit voice condition and the saved neutral ID with legacy-manifest compatibility; recorded round/object counts replace outdated defaults where present.
- Updated runtime documentation and the run-sheet choices. Open: fresh headset enrollment/auditions, microphone clone quality, and matching cloud-provider consent/retention details. No approval or live cloning success implied.

## 2026-09-28

- Applied researcher clarification: retain the headset timing/progress/status panel while removing its target name/color content. Readiness status now refers to the agent, and wrong-selection timeout restores only generic status rather than a target label. Preserved survey controls, timers and trial progress; added assertions to existing ray-menu Editor checks. Browser timeline unchanged; full hidden-target conversion (including separate readiness labels and spoken identity) remains pending. Changes remain local. Unity live-editor compilation passed; Android Build And Run was attempted but no headset was connected. Editor UI regression assertions were added; their menu execution was unavailable in this session.

## 2026-09-28

- Began #24 follow-up after v3/main integration: expanded guidance to 32 source phrases with explicit self-similar first-person mappings; normal directional cue eligibility shortened from four to two seconds without overlapping speech. Added varied area/return phrasing without changing the existing relevance/debounce/cooldown rules. Expanded regression coverage for phrase inventories, cycling, direction preservation and first-person coverage. Layout/gaze and voice-isolation tests pass; hardware listening remains pending.
- Identified the headset bottom goal/status/timer panel in FindObjectUI and requested clarification against the browser timeline's bottom data/cue panel. Panel removal is pending the user's answer. These follow-up changes remain local and uncommitted.

## 2026-09-28

- Prepared all current Unity interaction/guidance, protocol/meeting/survey, per-trial CSV and timeline-video changes for the requested v3 then main integration. Both remote target histories are ancestors of the current work, so fast-forward integration is available without discarding commits. Participant recordings, credentials, generated analysis outputs and Unity performance-test build metadata remain local/ignored.
- Pre-integration checks passed: rotational layout/gaze entry-return regression suite; voice isolation suite; 32 CSV tests; 2 timeline packaging tests; prior real-browser timeline integration tests; Unity live-editor compile check. Android Build And Run was blocked by the missing headset; dismissed the device notice and verified Unity restored temporarily removed simulation settings. Device validation remains pending.

## 2026-09-28

- Added local headset-video support to the browser run timeline (#37): reusable page packager, run-scoped local video selection, manual recording-clock alignment, linked play/pause/speed/scrub/trial/event controls, missing coverage, media errors and downloadable/restorable alignment metadata. Preserved the original P020 preview and raw data; generated a separate updated local page. No recording or media upload started.
- Validation: two Python packaging tests and real Chromium integration checks passed with synthetic video, including trial switching, pause/speed, coverage edges, offset restore/wrong-run rejection and corrupt media recovery. Desktop/mobile screenshots inspected. Real P020 page loaded all 9 recorded trial snapshots and retained telemetry playback; actual P020 video remains unavailable/unverified. Manual offset is approximate and requires later-event drift checking; raw footage cannot be reconstructed from CSV alone. Changes remain local and uncommitted.
- Ran the requested Unity refocus/Build And Run shortcut; menu dispatch succeeded and the editor began building. Completion/device launch is not confirmed (latest log remained in Android prerequisite checks). The timeline feature itself runs in the browser; build-generated Unity files were left untouched.

## 2026-09-28

- Implemented the local post-session portion of GitHub #20 in `analysis/trial_csv.py`: one performance/subjective CSV per logical trial, combined table and source-hash manifest; strict replay/survey identity joins, scoped questionnaire enrichment, pause-aware timing, tracked contact snapshots, actual audio instruction counts, motion paths and realized geometry. Raw sources remain unchanged, missing measures remain blank, and practice/incomplete records remain explicit. Added usage/method documentation and regression tests; changes remain local and uncommitted.
- Validation: 32 CSV tests pass and Ruff checks pass. P020 exported 22 rows/files (20 planned measured + 2 practice; 6 measured completed), with each individual file verified against the combined table. Original incomplete recording/tail and missing surveys are flagged; participant artifacts remain under ignored analysis/results. No claims of validated fixation, native eye sampling frequency, finalized zone/repeat metrics or acceptance of the new 40+40 design. #20 stays In Progress pending definitions, finalized survey joins and fresh-session/device integration.
- Requested Unity refocus/Build And Run script was attempted; menu dispatch failed because System Events could not access Unity's menu bar (invalid index). No headset build or runtime validation completed for this Python export change.

## 2026-09-28

- Incorporated the researcher's whiteboard transcription: Euclidean distance, number of instructions, first eye/right-controller contact timing and final selection timing. Mapped these to separate distance/count/active-search latency fields, with actual-playback counting, partial/missing flags and final correct-trigger completion; detector details remain specified separately. Updated meeting/measures notes and GitHub #8/#19/#20/#21. Removed the small photographed To-do section from pending transcription, as requested. Planning/schema clarification only; no runtime, headset or survey change.

## 2026-09-28

- Incorporated researcher clarification: right controller only throughout setup/menus/wrist/pause/repeat and trigger selection; the agent alone knows target identity/location; guide horizontally to the correct vertical wall, then up/down within it. Updated the meeting record, procedure/measures/decision pointers and related GitHub requirements/project overview. Target identity and controller allocation are no longer open questions. Runtime implementation remains pending; target announcements/repeats/error speech and readiness dependencies need a coordinated change, not simply silencing the introductory clip.
- Cropped three unredacted whiteboard regions for researcher transcription: count-column heading after distance, timing headings between search/controller, and small To-do notes. Preserved original image/redactions and kept crops local. No headset build, runtime or live survey/Overleaf change for this clarification/task update.

## 2026-09-28

- Processed the supplied whiteboard and typed meeting notes into `docs/meeting-reconciliation-2026-09-28.md`, separating decisions, candidate schema/items, unreadable headings and unresolved wording. Updated 21 existing GitHub issues and project #2 overview without duplicate issues; activated #11 coarse-to-fine/vertical guidance (Ready), renamed #10 for 40-per-voice scheduling and #20 for one performance/subjective CSV per trial. No tasks marked Verified or closed.
- Captured 40+40 trials, five balanced absolute-theta strata, 20 positive/20 negative height deltas per block, separate reconstructable motion/scene streams, block-survey provenance, short varied guidance and success speech, exact loading copy, reliance/chair instruction and later passthrough introduction. Linked draft willingness/reliance/independent-motivation/use-percentage items to #26 and preserved the unfinished semantic-differential and controller notes as open. Target identity availability, vertical reference and turn/look threshold still require researcher clarification.
- Propagated current planning pointers to procedure, measures, angle schedule and decision register; preserved runtime evidence and older requirements as history. Runtime, headset build, live Qualtrics/Overleaf and generated IRB sources unchanged; this session reconciled planning/tasks only. Raw P020 recordings and the meeting screenshot were not uploaded. Current-design power remains unestablished (#16).

## 2026-09-27

- Recorded the researcher’s new forty-trial-per-voice decision (80 measured total; eight assignments per absolute angle per block) and guidance without a spoken target description. Checked GitHub #16 and legacy protocol: current-design power remains unfinished; the older 48-participant sensitivity statement concerns a superseded design. Flagged target identification within a wall as unresolved before removing announcements. No runtime, open run-sheet, or headset change in this decision audit.

## 2026-09-27

- Split P020’s mega CSV into separate gaze, replay-gaze, head/XR motion, controller, trial, event, selection, object, session and voice files. Added 29 exclusive record-type partitions retaining geometry and all original populated fields; verified exact reconstruction of every original row and column. Kept source exports intact, retained incomplete-session warnings and documented overlapping convenience views. Added an index/manifest and ZIP under ignored analysis/results/p20-20260927/separate-data; no Unity runtime change or device rebuild needed.

## 2026-09-27

- Generated a local interactive timeline from actual P020 run 001 data, with recorded layout, gaze/controller rays, trigger/hit snapshots, cue text and authoritative selections. Recovered two practice and six completed measured trials; trial 7 never reached search and the replay ends without a footer. Preserved original headset copies and documented the one truncated CSV row omitted only in a derived copy. Exported 21,089-row / 387-column combined CSV, including 7,376 exact frame joins. Unity accepted the original replay prefix; browser playback, event jumping, scrubbing and incomplete-trial display verified. Artifacts and participant recordings stay under ignored analysis/results/p20-20260927; no runtime code or headset state changed. Full 3D/audio replay remains available in the existing Unity viewer; inline overview is reduced for display.

## 2026-09-27

- Changed the spoken processing announcement to “Give us a moment while we process your voice.” Voice-isolation checks passed. Android Build And Run succeeded in 49.8 seconds and launched on Vive FA5AR3N00385; listening verification remains on device.

## 2026-09-27

- Added the standalone survey questions-by-block companion to Overleaf (`survey-questions-by-block.tex`, revision 0.1). Includes every baseline, post-block and final draft question/response option, all 42 Guo candidate ratings plus Q43, requested accent/voice preferences and similarity/liking items, and the proposed exploratory debrief. Maps administration to two counterbalanced ten-trial voice blocks, distinguishes existing drafts from proposals, and flags stale wording, applicability, duplicate items and scoring/scale decisions. Verified all source question text and IDs; built-in LaTeX compilation succeeded. Published only the new companion to Overleaf at `476eed1` and verified the remote bytes match. Live Qualtrics forms, run sheet, Unity code and headset workflow unchanged.

## 2026-09-27

- Replaced the audio-level check with the requested Accept Audio/controller/trigger instruction in both voices. Added a preloaded “Go back. That was the correct area.” cue and its first-person counterpart after a stable horizontal visit/exit, with a two-second relevance window and six-second cooldown. Canceled correction fades no longer consume cooldown; stable arrival stops stale directional hints even during area-cue cooldown. Setup and target announcements remain protected.
- Fixed the reported upward-scan reannouncement path: guidance area membership now ignores wall-height bounds and uses the horizontal wall sector. Nearly vertical gaze abstains rather than producing an exit. Object interaction and telemetry unchanged. Pure geometry/entry/return tests, voice-isolation/interruption checks, Unity compilation and Editor checks across all eight walls with +/-89-degree pitch passed. Android Build And Run succeeded in 73.9 s and launched on Vive FA5AR3N00385 (PID 11641); the initial Unity/Android error query was empty. Physical listening and edge-jitter acceptance remain open.

## 2026-09-27

- Side-conversation change: wrong controller selections now speak the actual target again in the active condition: neutral “Locate the blue cube.” / self-similar “I'm looking for the blue cube.” Existing wrong-selection visual/tone feedback and ongoing trial timing remain unchanged. Correction utterances have their own source wording and `wrong_selection` audio context so normal announcements retain their current wording and directional guidance cannot interrupt the correction. Included corrections in practice starter, full phrase inventory and ordered background preparation. Voice-isolation checks passed for exact wording, both provider paths and preserved condition. Headset playback remains unverified here; kept the main thread’s device workflow undisturbed.

## 2026-09-27

- Side-conversation change: removed the two-second initial directional-hint delay. OnNewObjective now makes the first hint eligible on the first valid search frame; target announcement/readiness, gaze validity, and speech-busy gates remain in place. Later directional hints retain four-second minimum spacing; target-area entry still uses its existing 0.1-second debounce. Updated telemetry guide and passed the rotational/directional policy checks. Device timing remains unverified; no build was launched from this side conversation to avoid disrupting the main thread’s headset workflow.

## 2026-09-27

- Side-conversation change: the study introduction now explicitly uses the selected neutral voice and external, participant-directed wording, regardless of the first block’s assigned condition. Pedro confirmed this applies only to the introduction; trial target announcements and hints still follow their voice condition. Added an optional per-utterance voice argument without changing session assignment. Voice-isolation regression passed for all four neutral identities, external wording, and preserved self-similar/enrollment state. Left the main thread’s active headset build/testing workflow undisturbed; installation of this small follow-up has not been verified here.

## 2026-09-27

- Added stronger “Look way left/right” hints (first person for self-similar) when the current horizontal gaze-to-object angle is strictly over 90°, interpreting two thetas as two 45° wall steps. Uses actual target bearing; balanced trial theta quotas are unchanged. Included both lines in audio preparation, updated wording/policy versions, and passed threshold, wraparound, invalid gaze, near/far phrasing and voice-isolation checks.
- Removed “I’ll try looking right” from the self-similar hint rotation at Pedro’s request. Self-similar right hints now cycle three options; neutral/right and left options remain unchanged. Legacy source wording resolves to an allowed first-person line. Updated wording/policy versions and passed the rotational and voice-isolation checks.
- Changed the avatar picker to controller-hover voice audition, trigger selection with a persistent gold border, and a Back-to-Confirm button on the same screen. Hover does not mutate the selected or committed voice. Previews capture their own provider identity, cancel prior audio, and enable confirmation only after a successful completed sample of the chosen voice. Stable cards avoid hover restart loops; reentry retries, gender changes reset selection, and focus loss cancels previews. The updated ray-menu regression first failed against the old separate-preview screen, then passed after implementation. Voice-isolation checks also passed for all four explicit audition identities, unchanged session voice, completion timing and failed samples. Unity voice-choice and ray-menu checks passed; inspected the rendered picker with a persistent gold border and disabled Confirm for unavailable audio. Review ran inline per the project’s no-subagent instruction; no remaining in-scope correctness findings. Final combined Android Build And Run succeeded in 75.3 s and launched on Vive FA5AR3N00385 (PID 8872). Initial Unity/Android error query was empty. Physical hover/listening, selection confirmation and far-turn hint acceptance remain for headset testing.

## 2026-09-27

- Renamed the existing wrist Settings degree-guide control to **Guidance boundary**, retaining one toggle for all eight wall outlines and degree labels. Added shared ray hover/haptics and synchronized the toggle with public visibility changes and menu reopening. Visibility continues across trials/recreated frames within the session; objects, search zones and gaze guidance remain active when guides are hidden. Moved the control to the top of Settings. Unity compiled and GuidanceBoundaryChecks passed (eight walls/labels, pre-spawn preference, on/off, reopened/API synchronization, ray feedback attachment and unchanged zone geometry). Android Build And Run succeeded in 60.0 s and launched on Vive FA5AR3N00385 (PID 4163). Startup logs reported unsupported lip tracking, a null plane subsystem, and VIVE plane detection timeout; no boundary-toggle exception was observed. Physical wrist targeting, label readability and haptic feel remain for headset acceptance.

## 2026-09-27

- Added Zimmerer, Bartels and Latoschik (published IEEE ISMAR 2025) as the rationale for IMI in the survey inventory and measures register. Preserved the proposed four-subscale battery and distinguished methodological precedent from a predicted voice effect. Survey revision 0.2 compiled successfully and synced to Overleaf at `abaace7`; updated the shared sync baseline.

- Fixed the approved Unity compile-hook environment leak: only the Unity subprocess clears inherited repository/index/worktree/object-store/config Git variables; parent Git context is retained. New isolated fake-Unity regression tests first reproduced the leak, then passed clean-environment and nonzero-exit propagation checks; Bash syntax validation passed.
- Imported approved `eb477b0` soft-close v2 from `origin/v3`, rebasing the saved mega-CSV and ray-menu/voice-choice/stratified-schedule work on top. Resolved the prior DSP-removal conflicts by retaining the upstream treatment unchanged, stereo clip replacement, speech-window RMS, manifest fields and DSP tests while preserving all newer setup and trial features. Pedro confirmed natural-voice enrollment instructions will remain unchanged and approved the hook environment fix.
- Combined voice-isolation checks passed, including new exact manifest window checks (full neutral clip / clone words excluding its 0.4-second tail). Unity compile and Editor ray-menu, voice-choice and replay checks passed. Android Build And Run succeeded in 66.2 s and launched on Vive FA5AR3N00385 (PID 30510); initial Unity/Android error check was empty. Hardware listening, interruption-tail acceptance and preparation timing require a real setup run. The latest of 14 device voice manifests predates this install (15:02:05 UTC) and still contains 61 dry clips without speech_samples; fresh on-device manifest validation remains pending, not passed.

## 2026-09-27

- Replaced the self-similar inner-thought treatment with `inner-thought-v2-soft-close`, chosen by listening from six prototypes built on Voxtral clones of the neutral voice. It applies close-mic EQ, gentle 2.5:1 levelling and a -24 dB dark stereo room with a 0.4 s tail. Words start at sample 0; neutral bypass stays exact. VoiceSynthesizer now swaps in the stereo clip and level-matches over the words only (`speech_samples` in the manifest). Voice-isolation checks pass. The C# port matches the prototype within 0.5 dB. Open protocol decision: the selected sound assumed a soft enrollment reading, but the run sheet still says "natural voice"; instructions are unchanged. Headset listening and on-device timing are pending. See docs/inner-thought-voice.md.

- Updated `agents.md` here and in the side-conversation worktree to always include direct links for reported research papers, plus accessible full-text/PDF links when available.

- Uploaded the survey inventory to Overleaf and verified its rendered preview. Confirmed the researcher-created dedicated Overleaf Git clone is clean and its survey matches the local file byte-for-byte at `f397ee4`. Recorded mapping/common baseline and conflict-aware sync steps in `docs/protocol/overleaf/SURVEY-SYNC.md`. Subsequent noninteractive Git fetch requires authentication; token persistence remains a user-local step. No runtime or run-sheet files changed.

- Implemented controller-ray buttons across voice setup, audio checks/recovery, centering, checkpoints, survey submission and completion; shared blue hover feedback and one pulse on the hovering controller, including existing wrist controls. Removed global menu shortcuts and clone-page keyboard suffix. Added four generated fictional avatar cards, retained both neutral options per gender and dry self-similar voice.
- Kept beta at 10 measured trials per voice (20 total, two separate practices). Schedule v4 stratifies each block into shuffled five-trial halves, each covering all absolute theta categories; preserves mirrored pairs, random target heights, seeds and full schedule logs. Updated run sheet and angle documentation.
- Added three left/right alternatives per original phrase with first-person equivalents and startup preloading; retained correct-area interruption and stale-direction cancellation. Voice/layout/enrollment suites and Editor ray-menu/voice/readiness/controller checks passed; run sheet compiled. Android Build And Run succeeded (92.5 s); installed and launched on Focus Vision FA5AR3N00385, PID 29279; initial error-log check was empty. Physical hover/haptic, held-trigger transitions and full 10+10 beta validation remain open.

## 2026-09-27

- Created standalone `docs/protocol/overleaf/survey-instruments.tex` in the survey side conversation: current survey inventory, source-paper bibliography, administration/scoring notes, custom voice/debrief questions, unresolved adaptations and burden, and a manual Overleaf round-trip workflow. The desktop LaTeX compiler successfully compiled the initial document. This is a planning reference; live Overleaf/Qualtrics contents and participant forms were not changed.

- Added the requested post-session mega-CSV exporter while retaining separate recordings: exact trial/frame gaze+replay joins, all source columns, trial/schedule/target context and scoped survey repetition, object/mesh/event rows, voice manifests, explicit unmatched/missing data and a provenance/count manifest. Ten synthetic regression tests pass. No saved measured-run summary was present on the connected headset, so a real session export remains pending. Added GitHub project Backlog #36 (reconstruct a saved trial and rerun it interactively) and #37 (timeline playback with synchronized data snapshots); linked #28 and the two follow-ups. Updated #20 with exporter progress and remaining real-session validation. Generated a clearly labeled synthetic CSV example; final Build And Run succeeded in 21.3 seconds and the Vive process was verified. Requested a complete two-practice plus 10+10 beta session for real export/replay reconciliation.

- Replaced the rejected inner-thought DSP with a gender-first neutral voice choice: Eric/Roger or Janet/Sarah, followed by a sample and explicit confirmation; switch voices or return to gender before enrollment. Verified provider metadata (the legacy Rachel endpoint currently resolves to Janet). Both conditions now use dry audio. Saved exact choices with legacy gender-only migration, preserve voice-block order on re-selection, clear old preparation/prefetch state on change, and record voice name/ID/option in readiness and summary output. Voice isolation/routing/cache/persistence checks and the Unity voice-choice UI-state check pass; the updated open run sheet compiles. Android Build And Run succeeded in 76.3 seconds, installed and launched on the connected Vive (process verified); physical audition and end-to-end enrollment acceptance remain pending.

- Committed all accumulated study, replay and protocol changes as `d9868a7` and pushed the checkpoint to `origin/v3`. Added the requested self-similar-only inner-thought voice pilot: softened high frequencies and quiet 24/52 ms reflections, applied to PCM before existing level matching; neutral bypass is exact. Raw caches remain untouched, replay saves the treated clip, and manifests/playback events record the effect version. Generated a local dry/treated preview from a cached clone phrase; both are 2.2204 seconds and RMS 0.1000, with safe peaks. Voice-isolation/DSP and level-matching checks pass. Android Build And Run succeeded in 47.9 seconds; installed and launched on the Vive (process verified). Physical listening acceptance remains pending. See docs/inner-thought-voice.md; the treatment is a pilot manipulation awaiting perceptual/protocol review.

- Implemented the selective trial replay port from `origin/v2-app-demo`: current stimulus snapshots, head/gaze and both controller poses/rays/triggers, authoritative controller-selection events, voice playback assets, and isolated Unity playback. Removed dependencies on object `DwellTarget`/`DwellProgress`; new schema keeps legacy object dwell at zero while reading old recordings. Added bounded background writing, incomplete-file recovery and failure isolation. Video export remains deferred; telemetry cadence is render-frame based, not native 120 Hz.

- Integrated the requested world-fixed, previous-target-wall readiness cross: configurable one-second continuous valid gaze, generous whole-cross bounds and left-to-right fill; gaze loss, tracking loss and pauses reset charge. Both charge and target announcement must finish before objects/search timing begin. Changed theta to signed shortest turn from the previous wall, retaining two each of absolute 0/45/90/135/180 per ten-trial block, mirrored relative directions and independent seeded order, including practice and block boundaries. Added separate readiness replay/events and start-wall CSV metadata; updated schedule documentation and the open run sheet (compilation passed).

- Verification: Unity replay serialization/selection integration/failure isolation, bounded-queue overflow, render smoke, readiness/audio gating, and both-controller selection checks pass. Voice/schedule (50 participant schedules) and randomized-layout shell checks pass. Fixed an introduction/target-announcement coroutine cancellation race found during integration. Final Android Build And Run succeeded in 71.9 seconds; installed and launched on the connected Vive Focus Vision (process verified). Physical gaze-gate behavior, replay comparison, audio timing and headset performance remain acceptance checks.

- Cleared old recordings from the connected Vive at the researcher's request: nine run folders, eighteen participant preparation folders, twenty-three global preparation logs and the legacy root gaze CSV (85 files total). Verified all targeted files were removed while preserving voice setup metadata and audio caches; relaunched the installed app and verified its running process. Reviewed current trial/gaze exports; native 120 Hz acquisition, controller/head motion streams and dedicated first/final gaze/raycast latency fields remain outstanding.

- Added plain-language explanations to every ChallengeSet function and computed property, including participant seeds, signed/absolute angles, mirrored walls, shuffling, distractor counts, target placement and schedule exports. Verified that executable code is unchanged; this is documentation only.

- Updated the measured schedule to ten trials per voice block (twenty total, practice separate), with two occurrences each of absolute wall angles 0/45/90/135/180 degrees. Bumped the schedule version, moved the voice boundary to trial index 10, and aligned the open run sheet, decision register and angle-schedule documentation. Regression verifies all twenty manifest rows and quotas across 50 participant schedules; run-sheet compilation passes. Android Build And Run succeeded in 41.4 seconds and launched on the Vive Focus Vision. Physical session acceptance remains pending.

- Removed the enrollment 3–2–1 countdown while retaining the recording instructions and tone. Implemented participant-seeded, mirrored and independently shuffled target-wall angle schedules with identical absolute-angle quotas per seven-trial voice block. Added full planned-trial CSV and summary angle/seed metadata; retained randomized heights and object coordinates. See docs/balanced-angle-schedule.md for quotas, angle interpretation and the open 30-trial schedule decision. Regression tests pass for 50 participant schedules, randomized layout and enrollment. Android Build And Run succeeded in 90.0 seconds and launched on the connected Vive Focus Vision; physical listening/gaze and trial-start-facing acceptance remain pending.

- Saved all remaining protocol/run-sheet edits and Unity recovery/crash artifacts at the researcher's request for the v3 checkpoint. The saved run sheet compiles successfully; legacy IRB packet reconciliation and headset acceptance remain open.

- Recovered the temporary Unity checkout Git link and missing tracked files from its existing HEAD, preserving local protocol edits. Implemented first-person-singular clone speech and external neutral speech with versioned per-voice formatting through preparation, background synthesis, playback and correction-cache lookup. Replaced warmer/colder hints with gaze-relative left/right target-wall guidance and the existing debounced interrupting area cue; preload all three hints, cancel stale direction cues and suppress invalid gaze. Voice-isolation and rotational-layout suites passed, covering provider payloads, offline cache reuse, directional handedness and entry gates. Unity restarted and compiled without C# errors. Required Build And Run was attempted but failed during prerequisites with `No Android devices connected`; headset installation and listening/gaze acceptance remain pending. Preserved Unity scene backups in Assets/_Recovery/.

## 2026-09-27

- Revised participant-facing run-sheet explanations into future tense for the upcoming task, voice blocks, practice and data collection. Preserved direct instructions, current comfort questions and post-task completion/debrief wording.

## 2026-09-26

- Updated the open run sheet from the researcher-supplied revision: two counterbalanced voice blocks, trial/block definitions, between-trial rest and immediate-pause timing, consistent planned 30-trial wording, and removal of obsolete timing-condition references. Recorded the design in the IRB decision register and flagged legacy protocol/consent sources as unreconciled; no Huron submission or generated-packet update.

## 2026-09-25

- Prepared the complete pilot checkpoint for v2: randomized 168-object layout, floor-relative heights, wrist degree-guide toggle, immediate gaze-area speech interruption, finalized run-sheet source/PDF and prior controller/audio fixes. Layout/gaze-gate and voice-isolation regression suites passed again; prior Android Build And Run succeeded. Physical headset acceptance remains pending.

## 2026-09-24

- Made gaze-area corrections more immediate at researcher request: 100 ms stable entry and 40 ms fade, down from 300/150 ms. Search speech is cut off mid-sentence; instructions remain protected. Added regression proving unfinished audio is stopped rather than awaited. Geometry/gate and voice tests passed. Android Build And Run succeeded (296.4 s), installed and launched on the connected Vive; physical timing/listening QA remains pending.

## 2026-09-24

- Implemented 168-object scatter (21 per octagon wall) with trial seeds, random heights/width positions, 20 cm minimum spacing and recorded layout_seed. Exactly one target and scaled conjunction distractors retained. Added eye-gaze target-wall entry correction: 300 ms dwell, 150 ms speech fade, 500 ms exit rearm and 6 s cooldown, with tracking/search revalidation and protected instructions. Preload correction in both voices (ten startup clips). Scatter/gate and voice tests pass, including stale correction cancellation, progressive volume reduction and network-free playback. Android Build And Run succeeded (141.8 s); updated package and running headset process verified. Physical gaze/audio/performance QA still required.

## 2026-09-24

- Expanded octagonal object centers to 0.10–3.048 m above the tracked floor with configurable bounds and a new layout tag. Scene requests floor tracking; wrist Settings now includes Degree guides to toggle outlines and labels together across rounds. Geometry checks pass for bounds, determinism, spacing and connected corners. Android Build And Run succeeded (56.4 s); verified updated package and running headset process. Physical floor alignment and wrist-toggle QA remain pending.

- Fixed trial aborts from later voice clips exceeding accepted-sample RMS headroom: retain progressive loading, clamp gain to 4 / peak to 0.9, and record requested/achieved RMS, limitation flag and audio_level_limited telemetry. Starter matching remains strict; missing/corrupt audio and voice-isolation checks remain enforced. Peak-limited and quiet-clip regressions failed before the fix and pass afterward; targeted manual review (unrelated branch work) and inline reuse/quality/efficiency review found no additional changes needed. Committed fix as 83785d0. Android Build And Run passed after restarting Unity to clear stale editor assemblies; verified package update and running headset process. Human playback/trial acceptance remains pending.

- Added controller-ray object-entry haptics to ControllerRaySelector: per-controller 40 ms / 0.2 amplitude pulses, no sustained-hover repeats, reset on inactive search/tracking loss/selection reset. Documented behavior and updated #34 device QA. Unity batch compilation and ControllerSelectionChecks passed (exit 0). Required refocus/build script attempted but macOS could not connect to Unity (-609); no device build or physical haptic validation completed.

## 2026-09-23

- Updated GitHub #26 with a draft exploratory own-voice debrief interview and finalization criteria: self-recognition, perceived performance, liking/eeriness, use cases and context preferences. Linked operator delivery in #27. Verified both issue bodies; live survey and finalized run sheet remain unchanged pending question-set finalization.

## 2026-09-23

- Removed the metadata cover page from the live Overleaf run sheet at the researcher’s request and synced the local source, preserving current live edits. Verified the remaining source exactly and compiled six pages with zero errors or warnings.

## 2026-09-23

- Synced the local run-sheet source to the researcher-finalized attachment verbatim. Verified the live Overleaf experimenter-run-sheet.tex already matches all 13,391 characters exactly; recompiled successfully to seven pages with zero reported errors or warnings. This finalized edition supersedes the earlier ten-page local draft. No protocol wording changed during sync.

## 2026-09-23

- Verified the run-sheet meeting checklist against GitHub tasks. Updated #33/#26 with explicit participant-chosen agent gender/accent (superseding automatic gender matching), 100-point individual ratings including self-similar, and separate requested 1–7 voice/accent similarity items. Updated #27 with the local ten-page run-sheet revision and publication limits. Existing #20/#22/#14/#16/#18 already own timing, motion/replay, literature/quality and disclosure requirements. Read back and verified the updated issue bodies.

## 2026-09-23

- Revised the researcher-supplied Overleaf run-sheet source: initial Qualtrics records own perceived accent separately from preferred agent gender presentation and accent, including no-preference/missing handling. Added preference-aware previews/ratings, explicit participant voice choice and mismatch handling, early sample collection, readiness/identity checks and separate voice/accent-similarity items. Replaced superseded gaze-dwell instructions with the confirmed controller-ray/trigger method. Survey/application implementation and unresolved scale/allocation decisions remain explicit. Compiled a 10-page review PDF; changes are local, not a live Qualtrics or Overleaf publication.

## 2026-09-22

- Created GitHub #35: eye-measurement validity gaps and true 120 Hz acquisition evidence gate; added to Participant Readiness as Ready. Defined source-route feasibility, native-versus-predicted timing, render-independent acquisition, buffered persistence, quality/calibration/detector validation and hardware proof. Linked tailored responsibilities in #8/#14/#15/#16/#17/#19/#20/#22/#26/#28. No acquisition capability or workload validity claimed from advertised frequency; implementation remains pending. Verified issue bodies and project membership.

## 2026-09-22

- Replaced gaze dwell capture with controller ray + fresh trigger press. Blue highlight follows controller intersection; gaze remains observational for telemetry and hints. Search objects cannot be grabbed. Pause/round/tracking resets require trigger release; UI hits take precedence. Added controller selection verification and selection-method metadata. Updated spoken instructions in both perspectives and enrollment wording. Unity compilation, controller checks and octagon regression pass. Required Build And Run attempted: existing OpenXR Composition Layers Support validation blocks Android build; no adb device connected. Headset QA remains pending.

# Project Progress Log

Daily progress on the Gaze Contingency Project ("Follow My Voice: Gaze-Contingent
XR Search with a Self-Similar Agent"). Newest entries on top.

## 2026-09-22

### Implement the connected octagon and wider object coverage
- **Confirmed design:** retain eight faces around the seated user and spread objects across the connected faces; the researcher clarified that solid backing panels are not requested. Kept 56 objects, stable plane/slot IDs, existing vertical placement and shape/target composition.
- **Implemented locally:** derive outline width from the actual outline radius so all eight seams close; expand horizontal object placement with 15 cm of corner clearance. Added `rotational_octagon_v2` manifest provenance and updated the geometry guide to describe wider spacing and changed radial distances.
- Verification: observed the expected failing old-layout coverage test, then passed extended geometry checks at 1/1.5/3 m, Unity batch compilation, and a temporary integration harness invoking actual frame creation at a translated origin and 137° heading. Runtime corners join, frames add no colliders, and existing through-wall gaze physics checks pass. Code review found no actionable issues.
- Build And Run was triggered in Unity for this checkout. Android preprocessing failed because OpenXR Composition Layers Support is required by the installed package but not enabled; geometry changes do not modify XR settings. ADB also reports no connected headset. Device rendering/targeting and build configuration remain unverified; no successful APK/device deployment is claimed.

### Scope connected search walls under existing geometry task
- Confirmed current remote v2 uses eight separate outlined frames (0.84 m by 0.72 m), with seven objects clustered on each tangent plane and no solid backing. Added the requested connected-corner extension and geometric/interaction verification criteria to #9, preserving its project status.
- **Open decisions:** eight joined faces versus six, and solid panels versus expanded object coverage versus both. Requested clarification before changing the study geometry; no code or device build yet. Existing local user changes remain untouched.

### Lock controller ray as the selection method
- **Confirmed design decision:** researcher explicitly locked controller-ray object selection, with trigger confirmation, replacing gaze-dwell completion in the intended study. Gaze remains independently logged and supplies gaze-contingent guidance.
- Recorded the confirmation in #7 and #34 and verified the saved issue bodies. Press-versus-hold timing and other input details remain separate open decisions; no runtime implementation or headset test is claimed, and task statuses are unchanged.

### Map PeriSpike screenshot measures and citations into existing GitHub tasks
- **Protocol proposal:** reviewed the supplied measures/reference screenshots from anonymous submission 1752 and extended #26 (susceptibility/SSQ/IPQ selection), #27 (exposure and stop procedure), #20 (exposure events), #14 (gaze validity and projection), #15 (optional BCEA/transitions/SSQ-per-minute analysis), and #16 (source verification and interpretation). Preserved existing scope, issue history and project statuses; advanced analytics remain deferred.
- Checked the Golding MSSQ bibliographic/abstract source, Golding/Rafiq/Keshavarz VIMSSQ full text, and Castet/Crossland eye-stability bibliography/abstract. SSQ/IPQ exact form/scoring checks remain explicit tasks. The public PeriSpike manuscript, Zhao reference [49], mapping of [9], and two unseen gaze metrics remain unresolved; no invented citations or publication status.
- Specified transfer limits for seated passthrough MR: source-study 90 Hz, 20-minute exposure and normalized viewport radius are not local validated parameters. Preserve exposure and symptom outcomes separately, avoid bridging gaze gaps, and do not interpret free-search dispersion as fixation stability or cognitive load. Planning/GitHub changes only; no runtime, live survey, Overleaf or device changes.

### Add paper-informed eye-gaze implementation requirements to GitHub
- Verified the screenshot reference as Zagermann, Pfeil and Reiterer (2016), *Measuring Cognitive Load using Eye Tracking Technology in Visual Computing*, BELIV, pp. 78–85, DOI `10.1145/2993901.2993908`, using the open full text at <https://d-nb.info/1128594803/34>. It is a position paper; its proposed ocular/workload relationships do not validate this headset or task.
- **Protocol proposal / implementation backlog:** expanded and renamed existing #14 to own eye-gaze acquisition, calibration and quality validation, with concrete sensor-capability, timing, validity, coordinate-frame and device-test requirements. Added conditional fixation/saccade/blink and pupil criteria plus confound controls; linked telemetry #22 and outcome interpretation #16. Preserved existing issue bodies and project statuses; #14 remains Ready.
- **Implemented evidence:** inspected `GazeDataLogger.cs` at remote v2 `3cd250b`; the current render-frame logger and heuristic blink output are not sufficient evidence of validated physiological measures. GitHub bodies and project membership were read back and verified. No runtime changes or headset tests in this task.

### Format the annotated run sheet for Overleaf
- Created a standalone LaTeX review edition with UCF-inspired black/gold styling, distinct Say/Ask speech, italic researcher actions, highlighted meeting questions, session fields, page numbers and a seated 360-degree schematic. Preserved the supplied script and annotations with a cover note identifying the newer preferred-voice/controller requirements.
- Added the source to the requested Overleaf project alongside the manuscript and verified its eight-page PDF after a clean Overleaf build. Saved the reproducible generator and review PDF; local Tectonic compilation, text-coverage checks and page-by-page visual review passed without overfull boxes. Protocol choices remain open under #27; no runtime or participant-readiness claim.

### Reconcile meeting requirements with v2 and the GitHub project
- Audited latest `v2` at `6457c1f`, all 29 GitHub issues/project cards and the older local task lists. The supplied annotated run sheet exactly matched the committed source; preserved its notes and saved the meeting prompt verbatim.
- **Protocol proposal:** updated the planning entry points to preferred versus self-similar voice, controller selection with independent gaze, and pilot-first delivery. Added a complete 27-section owner map, evidence table, next-step sequence and open method decisions in `docs/meeting-reconciliation-2026-09-22.md`.
- **Implemented evidence:** the beta remains 14 measured gaze-dwell trials with neutral profiles; shared script modes, progressive preparation, practice/blocks and survey drafts exist. Replay work on `codex/trial-demo-replay` is separate and unmerged. Planning updates do not complete any pilot acceptance gate.
- Reused existing issues, preserving acceptance criteria/history; added concrete progress/next actions and run-sheet details for survey-to-trial joins, final-zone timing definitions, measured sampling rates and distinct operator actions. No duplicate issues or issue closures.
- Documentation/project update only: no runtime, live Qualtrics, generated PDF/Word or device-build changes. Existing unsaved workspace work was preserved by preparing this update in a clean checkout from `origin/v2`.

## 2026-09-20

### Checkpoint all workspace changes
- Merged the complete workspace snapshot, including the latest inline protocol questions, calibration and telemetry notes, survey wording and break updates. Existing generated PDFs remain their earlier review editions; they have not been regenerated for these latest source edits.

### Save remaining study materials
- Checkpointed survey sources, generators and review PDFs, the UCF protocol review edition, current run-sheet edits and the provisional 60-trial planning decision. Preserved the existing published protocol and study-flow figure.


### Publish formatted experimenter protocol
- Added the current run sheet, verified UCF-themed PDF and self-contained LaTeX sources. Added prominent README links to the protocol and editable sources. The cover identifies trial-count and voice-order inconsistencies for review.

### Add provisional study procedure figure
- Added the UCF-inspired study-flow image and reproducible ImageGen prompt under `output/imagegen/`.
- Depicts two counterbalanced 30-trial voice blocks, two practice trials, survey timing and seated eight-plane gaze search. The figure labels the procedure provisional; it does not change runtime trial counts.

### Link the experimenter protocol from the README
- Added a prominent PDF link and editable source links, retaining the review/rehearsal status. Copied the complete formatted protocol and Markdown into the local v2 checkout so its README links resolve.
- Opened the three existing Qualtrics survey builders for review.


### Format current experimenter run sheet as PDF
- Converted the current Markdown run sheet to a UCF-themed PDF with editable LaTeX under `docs/protocol/formatted-run-sheet/`. Preserved source wording and flagged the trial-count and voice-order inconsistencies in a cover note.
- Compiled using Tectonic and checked extracted sections and rendered pages.

### Set provisional study trial count
- Subsequent user revision: increased the provisional plan to 60 experimental trials, 30 per voice condition, plus two practice trials (62 total). Updated both planning documents; runtime remains unchanged. Thirty trials per block cannot divide equally across eight planes, so the location-balancing schedule remains open.
- Recorded the researcher’s provisional choice of 48 experimental trials: 24 per voice condition, plus two practice trials (50 including practice), in the protocol decision register and alignment document.
- Runtime and dated protocol exports remain unchanged; angle allocation, pilot feasibility and power justification remain open.

### Save reusable UCF academic PDF styling and render protocol
- Created the personal `ucf-academic-pdf` skill with a reusable LaTeX theme, local compilation fallback and PDF/text/visual verification instructions, based on the requested Hermes references.
- Produced a UCF-inspired protocol review edition under `docs/protocol/ucf-themed/` with editable LaTeX, a charcoal/gold cover, session overview, and explicit review notes for wording, TLX duplication, survey linkage and open decisions. The original run sheet remains the dated source; this is a styled review edition, not a protocol freeze.
- Validated the skill and compiled with Tectonic because pdflatex is unavailable; checked extracted content and rendered pages.

## 2026-09-19

### Create Qualtrics draft survey battery
- Built repeatable Qualtrics Advanced TXT imports from the questionnaire and measures register: baseline/pre-task, post-voice-block and post-session/safety.
- Imported the three drafts into the UCF Qualtrics workspace, preserved repository item IDs as question export tags, and removed each blank starter block. Draft IDs and builder links are recorded in `resources/surveys/qualtrics/README.md`.
- Included the full proposed 24-item IMI bank, NASA-TLX, baseline/post SSQ, voice manipulation and technical checks, and the Guo Table A1 source bank. IPQ remains on hold for the passthrough MR design; final wording, burden, scoring, requiredness, IRB reconciliation and publication remain open.

### Print the Qualtrics drafts for protocol review
- Rendered one print-ready PDF per draft under `output/pdf/`, with survey ID, administration timing, export tags, response choices, writing space and page numbering.
- Visually checked representative first, middle and final pages for clipping, readable text, matrix response scales and interview writing areas. The PDFs are static review copies; response collection remains in Qualtrics.

### Add source annotations to the survey PDFs
- Rendered annotated companion PDFs with a yellow provenance note after each item group and a references page covering NASA-TLX, SSQ, IMI, Kao et al. (2021), Guo et al. (2024), and the original Guo Table A1 attributions.
- Marked demographics, technical checks, preference questions and interview prompts as study-created instead of implying they came from a validated paper. Visually checked representative annotated pages and reference pages.

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
