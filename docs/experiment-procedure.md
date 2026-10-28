# Experiment Procedure: Gaze-Contingent Guidance and Self-Similar Voice

**Implemented — October 28 current setup:** use the [consent-first audio intake run sheet](guide/10-audio-intake.md). Reserve P### and assigned A/B order → prospectively record signed-consent/voice-authorization attestation → participant auditions and confirms Selected Voice → River's nine spoken eligibility/baseline questions with a speak tone → optional insufficient-speech fallback → one researcher eligibility review → background clone and full participant-bound desktop cache → verified headset transfer → wearer confirms P### and both audio samples → October 27 three-fit/five-verification gaze preparation → activation/centering → one practice → 40 + 40 measured trials with a break → comfort check and external **Selected Voice | Self-Similar Voice** final survey handoff. A = Self-Similar first; B = Selected first. Reading remains an explicit insufficient-speech fallback. Development rehearsals are labelled `study_data=false`. This supersedes older intake, shape-confirmation, five-degree and Qualtrics-on-hold statements below. Qualtrics is being prepared separately; no live URL or response collection is claimed by this runtime change.

**Open decision:** approved consent/screening wording, retention, final questionnaire deployment, and wearer/microphone/voice-quality rehearsal remain study-team acceptance items. Software checks and an APK install do not establish those outcomes.

**Implemented locally — October 25, hardware rehearsal pending:** Trial Viewer Live mode now offers a participant-answer-only spoken intake with River (a voice outside the study catalog), background desktop cloning, and a minimal participant assignment sent to the headset for wearer confirmation. **Configuration A = self-similar first; Configuration B = neutral first.** These explicit desktop configuration labels supersede older A/B wording below; existing saved participant orders are preserved. Calibration uses horizontal head orientation at eye height. Synthesized-audio transcript/content validation is bypassed by default, with provider-trusted provenance. See [setup, architecture, recovery and protocol status](guide/08-participant-intake.md).

**Implemented locally (October 23; hardware acceptance pending):** after voice setup, select Start calibration. Three dots each show a centered 3–2–1 countdown, then reveal the red fixation center for timed gaze sampling. A single brief white halo attracts attention when each countdown ends; a soft gold outward glow shows collection progress. Their three mean offsets are averaged before applying the unchanged 5-degree candidate limit. Three separate shapes then preview/check the candidate, and the participant confirms that highlighting follows gaze with OK. Skip/Retry remain available. Fixation during timed sampling is instructed and recorded as assumed, not inferred from proximity. This does not replace HTC calibration or task-wide validation. See [entry calibration details](guide/03-gaze-agent-and-telemetry.md#automatic-entry-eye-alignment--october-23-2026).

## Current protocol decision — October 20, 2026

**Protocol proposal:** one practice trial using the first assigned voice, two counterbalanced 40-trial blocks, a rest/comfort/tracking check between them, and all condition-specific questionnaires in the final survey. Two retrospective sets preserve the block/voice referent (65 numeric items each). Baseline demographics/SSQ and setup auditions remain. Qualtrics is on hold. See [reconciled procedure and measurement decisions](protocol/2026-10-20-final-survey-decision.md) and [current run sheet](protocol/overleaf/experimenter-run-sheet.tex).

## Runtime baseline — October 1, 2026 (new decisions not yet verified in app)

**Implemented locally; headset acceptance pending.** Two counterbalanced voice blocks contain **40 measured trials each**, plus one separate practice trial using the first assigned voice (Order A preferred, Order B self-similar). Each block has eight occurrences of each absolute previous-wall-to-target-wall angle: 0°, 45°, 90°, 135° and 180°. Every consecutive group of five contains each magnitude once; block two mirrors paired left/right assignments with an independent order. Schedule seeds, pairing, target identity and actual object geometry are saved. Short debug runs do not count as complete experimental pairs.

The agent knows the target's identity and location; the participant does not receive its color/shape or location in advance. Both voices use the same participant-directed imperative wording: horizontal guidance toward the wall, fine horizontal adjustment, then up/down within it. Success uses a chime and varied reinforcement. The participant selects with the right controller ray and trigger; setup, settings and pause also have right-ray controls. Looking alone does not select.

Voice setup offers six candidates per gender presentation (see [catalog](voice-choices.md)). Both catalog speech and self-similar synthesis use ElevenLabs. Recording instructions retain the participant's natural speaking voice. Shared wording removes the prior first-person wording manipulation, but the existing clone-only audio treatment and perceived voice/prosody differences still need listening and design review; a pure voice-identity effect is not yet established.

The readiness cross is fixed on the **previous target wall**, with front-wall initialization for the first practice; its fill and non-identifying announcement precede search onset. Breaks and questionnaires remain outside active-search timing. [Current editable run sheet](protocol/overleaf/experimenter-run-sheet.tex), [runtime flow](guide/02-gameplay-round-flow.md) and [gaze/telemetry guide](guide/03-gaze-agent-and-telemetry.md) provide the current operational account.

**Protocol proposal / open decisions.** Signed vertical balancing now uses readiness-cross height in the protocol; deadband, magnitude allocation and implementation still need verification; present floor-relative randomized heights do not satisfy a 20-above/20-below quota. Power, burden, survey wording/scales, later passthrough introduction and cloud processing/retention consent require research review. The pilot uses a 45° turn/look threshold, but it is not a validated behavioral boundary. Headset spatial accuracy, distinct native 120 Hz observations, held-out offset correction, drift and eye-event/workload measures remain unverified. See [gaze validation](gaze-measurement-validation.md) and [morning hardware checklist](overnight/2026-10-01-morning-verification.md). No institutional approval or participant deployment follows from this software checkpoint.

## Historical decision register — superseded where conflicting

The dated material below preserves earlier designs and contains obsolete counts, first-person prompts, fixed-front readiness and offline-provider assumptions. It is **not the current operator script or a submission-ready consent source**. Use the current account above; #27/#18 own reconciliation of remaining historical/submission artifacts.

**Historical decision / protocol proposal — September 28:** [Meeting reconciliation](meeting-reconciliation-2026-09-28.md) controls the new direction: 40 measured trials per voice (80 total), agent-led guidance without revealing target location, within-area up/down cues, balanced positive/negative target-height deltas, shorter varied guidance, and participant training to use guidance and the chair. Confirmed follow-up: only the agent knows the target identity/location; it first guides to the correct wall, then up/down within it. Participants select using the right controller only; all setup/menu/wrist/pause actions must work without a left controller. The vertical reference, turn/look threshold and later passthrough checkpoint remain open. This is not yet runtime behavior; do not use older instructions below as a frozen protocol. #27/#18 own run-sheet and submission-source reconciliation.

## Historical decisions and pilot notes

The entries below document earlier designs and are superseded where they conflict with the October 20 protocol. Use the current run sheet and [alignment audit](protocol/2026-10-20-alignment-audit.md) for current behavior and remaining gaps.

**Historical pilot implementation — 2026-09-27 (superseded):** researcher-requested directional guidance supersedes warmer/colder-only wording below. Both voices use gaze-relative “Look left” / “Look right” toward the target wall, followed by a debounced correct-area cue when gaze enters it. Self-similar speech uses first-person singular (“I need to find the red cube”; “I need to look left”; “I'm in the correct area. I need to keep looking”), while the other voice uses external instructions. Voice identity and wording therefore differ together; this is not a voice-only manipulation. The phrase/cache version is `perspective-v3-first-person-directional`, guidance policy `gaze-directional-v1`. Both directions and the area cue are startup-prepared (seven phrases per voice, fourteen total); tracking loss suppresses directional guidance, and obsolete direction cues are cancelled. Target instructions remain protected. These are pilot settings requiring headset validation.

**Protocol proposal — current requirements, September 22:** [Meeting reconciliation and delivery order](meeting-reconciliation-2026-09-22.md) supersedes conflicting older planning below: participant-preferred versus self-similar voice, passive gaze measurement with controller ray/trigger selection, early recording/candidate evaluation, external survey gates and provisional 30+30 measured trials plus two practice. The beta still uses neutral profiles and 14 measured trials. Controller ray/trigger selection is implemented locally on September 22; headset validation remains pending. #27 owns full operator-script/export reconciliation; this mixed-version document is not a frozen run sheet.

**Experimenter run sheet (2026-09-16):** [Word protocol](protocol/Gaze_Search_Experimenter_Run_Sheet.docx) · [Readable source](protocol/experimenter-run-sheet.md). Adapted from the supplied example and checked against current scripts; marked as a rehearsal proposal with unresolved study decisions.

**Measurement planning (2026-09-16):** see the [living measures register](measures-register.md) for all surveys, behavioral metrics, scoring, implementation gaps and open decisions. IMI is now included; final subscales/form are proposed.

**Implemented beta (promoted to `v2` at `4198cfa`, 2026-09-16):** the application starts from a seated center with eight surrounding planes, seven objects each. It retains the two practice trials and 14-round voice schedule. See [beta behavior and QA](eight-plane-beta.md). Shelf-specific descriptions below describe the retained legacy mode.

**Reconciliation in progress (2026-09-16):** the user confirmed a seated, rotating search task. The older four-cell, shelf and offline-provider sections below are historical planning inputs wherever they conflict with the current two-voice thesis direction. See [the alignment checkpoint and pending decisions](thesis-study-alignment.md). Do not use this mixed-version document as a frozen operator script.

## Purpose and status

This document defines the participant-facing procedure for evaluating gaze-contingent guidance and self-similar voice during mixed-reality visual search. It also records where the intended experiment differs from the current Unity implementation.

Status labels are used throughout:

- **Implemented**: behavior verified in the current Unity project.
- **Protocol proposal**: recommended procedure that still requires implementation or formal approval.
- **Open decision**: a choice that must be resolved before preregistration or data collection.
- **Implementation gap**: the current application cannot yet execute or measure the proposed protocol as specified.

The runtime authorities for the current application remain:

- [Gameplay and round flow](guide/02-gameplay-round-flow.md)
- [Gaze, agent, and telemetry](guide/03-gaze-agent-and-telemetry.md)

The [IEEE-style planned-study manuscript](manuscript/gaze-guidance-planned-study.tex) is the concise scientific account of the planned 360° direction and also requires reconciliation. This procedure distinguishes the implemented eight-plane beta from unimplemented scheduling, guidance and measurement proposals.

## Decisions confirmed 2026-09-16 — target protocol, not all implemented

**Protocol proposal — user-directed decisions:** the participant sits at the center, rotates to search, and returns to the same fixed front direction before every new trial. Guidance remains warmer/colder only in both voice conditions. The participant-study voice route is cloud processing; the current runtime uses ElevenLabs for both catalog speech and participant voice cloning. The researcher reports the IRB application has not been submitted.

**Implementation gap:** the beta currently presents its transition in the current viewing direction. Add a fixed-front alignment/readiness gate without rotating the world or resetting the session origin. Balance and log front-to-target angle; keep the return interval outside active search timing. Previous-target angle/distance are secondary geometric descriptors rather than the controlled turn distance.

**Protocol proposal — provisional user decision, 2026-09-20:** plan 60 experimental trials, split into 30 per voice block, plus two practice trials (62 trials including practice). The implemented beta still has 14 experimental trials. Alignment tolerance, practice/timeout criteria, angle allocation and final sample/repetition justification require validation. Offline and no-third-party-transfer statements retained below belong to the old draft and must be replaced before submission; they do not describe the selected cloud workflow.

## Study design

### Current decision: always gaze-contingent

**Protocol proposal — user-directed decision, 2026-09-10.** The agent is always gaze-contingent. Gaze awareness is no longer an experimental factor and there is no gaze-unaware control condition. Neutral versus self-similar voice is the comparison, with the same guidance policy in both voices.

**Implemented in code.** The application retains a deterministic 14-trial target schedule with 168 objects per trial, displayed on eight surrounding planes in the beta and arranged into two seven-trial voice blocks. Odd participant IDs receive neutral then self-similar; even IDs receive the reverse. The assignment is saved under the coded participant folder. Gaze-contingent hinting stays the same in both blocks.

**Implemented — preparation and progression.** Choose the male or female neutral profile, enroll the current participant's self-similar voice, and prepare/decode a starter set for both voices (audio check, introduction, first practice instruction, success response and priority area-correction phrase). Remaining authorized trial and hint phrases load silently in the background, starting during audio checks, and are cached separately by voice. Playback takes priority; a needed clip that is not ready yet is generated on demand. Round audio preparation and playback finish before search onset; first-use hints can incur network latency during search. Each additional clip targets the accepted starter audio level using a maximum gain of 4 and a peak cap of 0.9. If those limits prevent an exact match, it plays at the closest safe level instead of stopping the trial. Requested/achieved RMS and a limitation flag are saved in the participant and active-run manifests; an `audio_level_limited` event records the shortfall. The manifest labels this mode `starter_then_background`. Self-similar prompts use first-person plural wording ("let's", "we", "our"); neutral prompts retain their existing wording. Spoken setup announcements mark the start and completion of voice-library processing. The operator accepts a playback sample from each voice before proceeding. Two separate practice trials (one per voice) precede the 14 experimental trials. NASA-TLX follows each block; a researcher-confirmed break separates blocks. Practice is excluded from experimental counts and timing.

**Implemented — matching and isolation.** Each gender offers five neutral voices (see [voice choices](voice-choices.md)). Neutral and self-similar speech both use ElevenLabs `eleven_turbo_v2_5`. The conditions now differ in both voice identity and wording; a voice-only causal interpretation is not supported by this implementation. Starter samples use a common achieved RMS level (up to -20 dBFS, lowered uniformly for peak headroom), and both conditions use fixed playback rate. Later clips use best-effort matching under the same gain/peak limits (`safe-best-effort-v1`); identical loudness across all phrases is not guaranteed. Prepared clips are versioned by text, voice ID and model, with duration/gain/identity in a saved manifest. Experimental playback uses prepared audio only. Failed preparation blocks readiness; missing or failed playback stops the session without voice substitution. Device-wide saved clones are not reused.

**Implemented — measurement.** Objects remain hidden until setup and target speech finish. Search exposure starts at reveal and excludes transitions and explicit pauses. Stable trial/object IDs, per-trial voice, search onset, capture, outcomes, audio events and block questionnaire rows are recorded. Summaries use schema 2 and `always_gaze_aware`; the run label includes the saved voice order. Completed trials are retained on interruption without replacement trials.

**Implemented locally — October 23 user decision.** Active search ends after 25 seconds, excludes explicit pauses, and records a timeout rather than a successful selection. The assigned voice announces “Time up” and encourages the next round; a break or final-round line is used where appropriate. Feedback precedes the next readiness gate. This replaces the disabled timeout described by earlier builds; the limit is a protocol decision, not a literature-validated constant. Normalization does not establish perceived loudness, neutrality, similarity or prosodic equivalence between providers. Headset listening, gaze calibration, final block allocation/power, spatial/theta mechanics, provider/data handling and the complete revised protocol remain research work. Follow [the consolidated QA checklist](voice-study-qa.md) before judging study readiness.

## Evidence basis and adaptations

The procedure adapts practices from related visual-search, gaze-assistance, and self-similar-agent studies:

- Chiossi et al. used informed consent, headset eye calibration, criterion-based practice, counterbalanced condition order, post-block raw **NASA-TLX**, and controlled visual-search displays. Their fixation thresholds were developed for a Vive Pro Eye sampling at 120 Hz and should not be copied into this study without validation on the Focus Vision and the exported data stream. See [Searching Across Realities](https://doi.org/10.1109/TVCG.2024.3456172).
- Zhang et al. used task familiarization, a controlled environment, explicit search-time boundaries, and **NASA-TLX** following augmented-reality visual-search tasks. This study adapts those procedural controls but uses a conjunction-search display and gaze-contingent audio guidance. See [See or Hear?](https://doi.org/10.1109/ISMAR59233.2023.00128).
- Kwok, Kiefer, and Raubal separated large-space guidance into a fast approximate-direction stage and a fine exact-target stage. This study adapts that architecture using spatialized target-plane audio followed by gaze–target proximity speech; its original thresholds are not transferred. See [Two-Step Gaze Guidance](https://doi.org/10.1145/3536221.3556612).
- Guo et al. evaluated self-similar voice in a 2 × 2 within-participant design, generated voices from standardized participant recordings, conducted similarity checks, used tutorial exposure, counterbalanced condition order, and administered post-condition surveys. This study adapts the voice workflow but does not manipulate agent appearance. See [Collaborating with My Doppelgänger](https://doi.org/10.1145/3651288).
- Kaschub et al. motivate gaze as an efficient input for XR assistance and report workload and user-experience measures. Only the published proceedings material available to this project was used; it does not establish the detailed timing or data-processing choices below. See [IEEE VR 2026 proceedings](https://ieeevr.org/2026/program/papers/).

These sources justify design practices, not automatic reuse of hardware-specific thresholds or stimuli.

## Participant procedure

### 1. Recruitment, screening, and consent

**Protocol proposal.**

1. Screen for eligibility relevant to headset use, corrected vision, color discrimination, hearing the spoken guidance, and susceptibility to motion discomfort.
2. Explain the visual-search task, eye tracking, audio guidance, recorded data, compensation, breaks, and the participant's right to stop without penalty.
3. Obtain study consent before collecting any research data.
4. If a self-similar voice will be generated, obtain separate, explicit consent before recording or synthesizing the participant's voice.

**Current decision:** recruit adults age 18 or older who can consent in English,
hear the standardized prompts, safely use the headset while turning in a seated
swivel chair, distinguish the approved colors, complete eye calibration, and
authorize the study-limited voice procedure. Pregnancy and age over 65 are not
automatic exclusions. Use signed paper consent with a separate voice-authorization
initial. Anyone who signs consent and begins screening receives a $15 UCF-approved
gift card even if they withdraw or the session stops; no-show/cancellation before
consent receives no payment. Voice synthesis is required for the factorial study,
so declining it ends participation without penalty.

### 2. Self-similar voice preparation

**Protocol proposal.** Complete voice preparation before the experimental blocks so generation delays do not alter condition timing.

1. Record every participant reading the same neutral passage in the same quiet room with the same microphone placement and gain.
2. Check the recording for clipping, background noise, omissions, and the frozen local-model input requirements. Repeat once when the technical check fails.
3. Generate the self-similar voice with OpenVoice V2 pinned to an institutionally
   reviewed release/commit and running offline on a UCF-managed encrypted
   workstation. Do not send participant audio, embeddings, prompt text, or clips to
   a third party.
4. Render both conditions from the same fixed prompt manifest and base TTS settings.
   The generic condition uses the fixed neutral base voice; the self-similar
   condition adds the participant-derived tone-color conversion. Normalize wording,
   loudness, intelligibility, duration range, audio path, and spatialization.
5. Conduct a brief technical intelligibility check without revealing experimental hypotheses.
6. Record the local model commit/version, settings, prompt-manifest version, speaker-
   embedding alias, and file checksums in the restricted session record.

**Current decision:** use the neutral passage in the IRB voice-script document, a
fixed neutral generic base voice, at most two recording takes, and one prompt-library
regeneration. If an intelligible complete self-similar library cannot be produced,
stop before randomized blocks and provide full compensation. Similarity ratings are
manipulation checks and never exclusion criteria. Delete failed takes immediately
and all usable source audio, embeddings, generated clips, and headset caches within
24 hours after the session and verified record transfer.

### 3. Environment and equipment setup

**Protocol proposal.**

1. Use the same swivel chair, marked origin, headset, application build, room arrangement, lighting range, and audio-output configuration for all sessions.
2. Seat the participant at the marked origin and explain the safe rotational search area. The participant may rotate the chair, torso, and head but may not stand or translate.
3. Fit the Vive Focus Vision securely and confirm that all eight surrounding virtual planes can be inspected comfortably by seated rotation.
4. Start a session manifest containing an anonymous participant ID, build identifier, headset identifier, condition schedule, and session timestamps.

Room changes, interrupted tracking, headset restarts, and application restarts should be recorded as deviations.

### 4. Eye-tracking calibration and validation

**Protocol proposal.**

1. Run the headset vendor's eye-tracking calibration after fitting the headset.
2. Validate gaze at nine directions: forward plus the centers of the eight search
   planes. The formative gate requires detection at 8 of 9 targets, median angular
   error no greater than 3 degrees, and 90th-percentile error no greater than 6
   degrees.
3. Refit and repeat calibration/validation up to two times. Stop with full
   compensation if the third attempt fails.
4. Refit and recalibrate between trials after a material headset shift or less than
   70% valid gaze availability over the preceding 60 seconds. Permit one
   mid-session refit/recalibration before a technical stop.

**Current decision with validation gate:** these numeric criteria are starting
acceptance limits, not literature-derived universal thresholds. Verify them with
nonparticipant Focus Vision testing and freeze them before confirmatory collection.
Do not present XRI object hover as a validated fixation measure.

### 5. Instructions and practice

**Protocol proposal.**

Give standardized instructions:

- Locate the announced conjunction target as quickly and accurately as possible.
- Point either controller ray at the object; its blue highlight confirms the ray intersection. Release, then press that controller’s trigger to select it. Looking alone never selects.
- Continue searching after an incorrect selection.
- Listen to the assistant, but use or ignore its guidance as desired.
- Request a break whenever needed.

Practice should use stimuli and targets not used in analyzed trials. Demonstrate target announcements, object selection, incorrect-selection feedback, correct-selection feedback, and at least one example of each guidance policy. Continue until the participant demonstrates task understanding and reliable controller ray/trigger selection.

**Current decision:** require three consecutive targets selected without
experimenter intervention within a maximum of eight practice trials. Failure ends
the session with full compensation; it is recorded as a practice-criterion failure,
not poor task performance.

### 6. Condition order and blocks

**Protocol proposal.**

Use four condition blocks, one for each factorial cell, with a standard schedule of 16 trials per block and no more than 64 experimental trials total. Define A as noncontingent/generic, B as noncontingent/self-similar, C as gaze-contingent/generic, and D as gaze-contingent/self-similar. Assign block order with a four-sequence balanced Latin square (Williams design): A--B--D--C, B--C--A--D, C--D--B--A, and D--A--C--B. These sequences balance both block position and immediate first-order transitions. A pre-generated schedule contains 15 assignments per sequence. Enroll up to 60 people and stop when 48 complete datasets include 12 per sequence or the ceiling is reached. Participant ID reveals the next assignment; the experimenter must not select it.

Within each block:

- In the standard 16-trial manifest, place the target twice on each of the eight planes.
- Balance target shape, target color, and display layout across conditions.
- Use deterministic, logged seeds so a target/layout can be audited.
- Do not show the same exact layout repeatedly to a participant unless repetition is itself controlled.
- Match hint opportunities, utterance duration, playback level, and audio path across guidance-awareness conditions. The responsive target information is part of the intended guidance manipulation and therefore differs from the general noncontingent prompts.
- End a block or session early only for the 75-minute ceiling, withdrawal, safety, or a documented technical failure. Retain completed trials and log the reason; do not change trial count in response to task performance or condition results.

Provide a minimum two-minute seated rest after blocks 1--3, and longer whenever
requested, followed by the post-block questionnaire.

### 7. Round timeline

The following timeline describes verified current behavior and the event boundaries required by the proposed protocol.

1. **Goal presentation — Implemented.** A fixation cross is shown and the assistant announces the target.
2. **Transition — Implemented/current.** The cross disappears, followed by a randomized blank pause of 0.2–1.3 s. **Current decision:** implement and freeze a 0.5–0.9 s interval for the factorial study.
3. **Display onset — Implemented.** The 168 search objects appear.
4. **Search onset — Protocol requirement.** Log a dedicated `search_onset` event when the full display is available and participant search may begin. The current objective timer is initialized too early for later rounds and can include transition/announcement time.
5. **Guidance — Partially implemented.** Opportunities occur at 4, 10, 16, 22, 28, 34, and 40 s after `search_onset`; the trial times out at 45 s. Noncontingent conditions provide duration-matched general prompts. Gaze-contingent conditions use a two-step policy: spatialized audio first directs attention toward the target plane, then the fine stage computes angular error between the valid gaze ray and the eye-to-target-center ray. Let `alpha` be the target's angular half-size and set `theta_max` to 45 degrees. Normalize proximity as `p = 1 - clip(max(0, theta - alpha) / (theta_max - alpha), 0, 1)`. The plane becomes attended after valid gaze remains within +/-22.5 degrees of its center for 300 ms. Relative to the preceding valid fine-stage opportunity, an angular improvement of at least 5 degrees produces **warmer**, worsening of at least 5 degrees produces **colder**, smaller change produces **about the same**, and `theta <= alpha + 2 degrees` produces **very close**. Fine evidence requires at least 80% valid gaze over the preceding 500 ms; otherwise the policy logs an abstention and plays a duration-matched neutral fallback. Fine stage returns to coarse after the plane is outside the attended sector at two consecutive opportunities. Every opportunity logs condition, evidence, state, utterance, voice, timing, outcome, and abstention. These are formative starting limits that must pass nonparticipant validation before being frozen; no cited source establishes universal thresholds for this headset and task.
6. **Selection — Implemented.** Looking continuously at an object for 1.6 s triggers a capture.
7. **Incorrect selection — Implemented.** The application plays an error sound and the same round continues.
8. **Correct selection — Implemented.** The application records success and advances through the next transition.

**Implemented:** gaze dwell selection is removed. Historical 1.6 s dwell runs used an interaction threshold, not a fixation threshold.

### 8. Post-block measures

**Protocol proposal.** Administer measures immediately after every condition block:

- Raw **NASA-TLX**: mental demand, physical demand, temporal demand, performance, effort, and frustration.
- Guidance manipulation check: whether hints appeared responsive to where or how the participant looked.
- Voice manipulation check: perceived similarity or familiarity of the voice.
- Nine fixed study-created ratings: gaze responsiveness, helpfulness, distraction,
  voice similarity, familiarity, trust, comfort, uncanniness, and reliance.
- Two additional mechanism checks after gaze-contingent blocks only: usefulness of
  coarse directional guidance and usefulness of proximity guidance.

Use a fixed item order after every block; the two mechanism items appear only when
the participant actually received the mechanisms. Analyze each item separately and
do not claim a validated composite. Do not administer SUS or UEQ because general
usability is not a focal outcome. Where preference is collected, ask open-ended
impressions before revealing the four condition labels or requesting a forced
choice. Administer the complete 16-item Simulator Sickness Questionnaire before
headset use and after removal, with brief safety checks between blocks.

**Implementation gap:** the current application administers **NASA-TLX** only once after the alternating 14-round run and labels it at the session level. It cannot estimate workload by factorial condition.

### 9. Completion and debrief

**Protocol proposal.**

1. Remove the headset and check for discomfort.
2. Administer final comparative questions and a hypothesis-aware debrief only after all condition-specific responses are complete.
3. Explain the gaze-awareness and voice manipulations.
4. Confirm compensation and provide researcher contact information.
5. Apply the approved retention or deletion procedure to the participant's source voice recording and synthesized voice.
6. Record withdrawals, early stops, technical failures, and other deviations without pressuring the participant to continue.

## Outcomes and operational definitions

| Construct | Operational measure | Status and caution |
|---|---|---|
| Search speed | Time from logged `search_onset` to correct controller-trigger capture | **Primary proposal.** Current objective timing can include transitions and must be corrected. |
| Selection accuracy | First-attempt correctness, incorrect captures, and total captures per round | Available from current events/summary logs. |
| Guidance exposure | Number of hint opportunities, playback starts, and completed utterances | **Implementation gap:** hint timing/content is not currently logged. |
| Guidance response | Change in gaze/head direction, target-plane entry, or selection from prompt offset until 3 s, selection, or the next opportunity, whichever occurs first | Requires synchronized hint, gaze, head, and selection events. |
| Target-directed looking | Latency to first target hover and target dwell before capture | Hover-derived proxy; do not label as fixation without a validated detector. |
| Search organization | Unique objects/regions visited, revisits, angular path length, and scanpath efficiency | Define formulas and missing-data handling before analysis. |
| Fixations and saccades | Events produced by a validated offline classifier from sufficiently sampled gaze data | Not established by the current hover-event labels. Hardware-specific thresholds require pilot validation. |
| Workload | Six raw **NASA-TLX** subscales after each condition block | Current single end-of-run form is insufficient. |
| Voice similarity | Separate 1–7 post-block similarity and familiarity manipulation checks | Study-created items; never an exclusion rule or validated composite. |
| Trust and experience | Separate 1–7 helpfulness, distraction, trust, comfort, uncanniness, and reliance items | Secondary/exploratory study-created items analyzed separately. |

The analysis plan should name one primary performance outcome and limit confirmatory secondary outcomes. Exploratory gaze measures should be labeled as such unless their extraction and exclusion rules are frozen before data collection.

## Data quality and exclusions

**Protocol proposal.** Store raw logs and derive exclusions reproducibly. Do not delete a trial merely because performance is poor.

Record at minimum:

- participant and session identifiers;
- application build and configuration;
- assigned block and round schedule;
- target, distractor counts, and deterministic layout seed;
- calibration and validation attempts;
- gaze sample or render-frame timestamps and available validity indicators;
- display onset, search onset, hint, hover, capture, questionnaire, pause, and application-state events;
- voice identity/model and playback status;
- interruptions, tracking failures, and experimenter notes.

Before data collection, preregister:

- the selected 70% trial-level usable-gaze rule and its exact computation;
- the calibration-failure and recalibration rules above;
- treatment of blinks, missing samples, duplicate timestamps, and headset removal;
- the 45 s trial timeout and interruption rules;
- handling of TTS/network failures and incomplete utterances;
- the requirement for at least 12 technically valid trials per cell for a complete
  dataset, while retaining observable assignment-policy outcomes where possible;
- whether failed trials are repeated and how repetitions are labeled.

The current per-frame logger includes gaze pose, hover metadata, eye openness when available, blink state, objective state, and condition-related fields. It does not provide a complete per-sample validity/calibration record, and transitions are skipped. The acquisition rate therefore must be measured from timestamps rather than assumed to equal the headset's native eye-tracking rate.

## Required implementation work before the factorial study

| Priority | Gap | Required change |
|---|---|---|
| 1 | No 2 × 2 scheduler | Represent both factors explicitly; assign balanced block orders and balanced target/layout sets from participant ID. |
| 1 | No self-similar voice condition | Add per-condition voice assets/IDs, standardized rendering, fallback behavior, and voice metadata logging. |
| 1 | Guidance timing/content confound | Match hint opportunities and scripts; allow gaze awareness to alter only the prespecified responsive component. |
| 1 | Invalid reaction-time boundary | Log display/search onset and compute response time from that event to correct capture. |
| 1 | No hint audit trail | Log trigger evidence, intended text, condition, voice, playback start/end/failure, and target state. |
| 1 | Session-level workload only | Administer and label **NASA-TLX** and manipulation checks after each condition block. |
| 2 | Fixed condition sequence | Implement and log the four-sequence balanced Latin square (Williams design) and its randomized participant-allocation schedule. |
| 2 | Hover events named “fixation”/“saccade” | Rename them as hover episodes/angular transitions or add a validated offline eye-movement classifier. |
| 2 | Incomplete gaze-quality metadata | Export actual timing and available validity/tracking/calibration indicators; quantify achieved sampling behavior. |
| 1 | Current 14-round bookshelf schedule | Implement four condition blocks with a standard 16-trial manifest, up to 64 trials total, eight surrounding planes, two scheduled target appearances per plane within each full cell, and prespecified early-stop logging. |

## Decision status

The scientific and operational choices above are frozen as **current decisions** in
[the IRB decision register](irb/decision-register.md). They remain subject to
nonparticipant feasibility validation, preregistration, PI review, and UCF approval.
The remaining inputs are administrative facts or institutional determinations:
official contacts and roles, lab/room, funding and gift-card mechanism, dates,
Huron number, risk/review classification, required ancillary reviews, and final
institution-approved workstation/storage configuration. Implementation gaps listed
above must be closed before participant collection.


### Standard participant gaze preparation

**Implemented (2026-10-27):** every rotational experiment launch, including development quick tests and Trial Viewer participant handoff, waits for participant/voice setup, then runs this preparation before centering or starting trial clocks:

1. Confirm participant identity, headset fit and recorded participant/headset IPD in the existing intake. Missing IPD stays explicitly unrecorded; this procedure does not infer it.
2. Select **Start calibration** once and follow three timed fit dots. There is no separate shape-confirmation screen in standard setup. The current researcher-requested correction cap is 8°; each participant receives a fresh measured correction, never another person's offset.
3. After a three-second transition, the independent five-dot accuracy check starts automatically (center, left, right, down, up; 2.5 s each, 2.4 m forward distance). This check does not fit or adjust the offset. The result screen reports mean 3D angular error with equal weight per target, excluding the first 500 ms uniformly and deduplicating source queries/deliveries. Each target requires at least 30 observations, median error ≤2° and P90 ≤3°. For corrections of at least 0.25°, verification also requires at least 0.25° mean improvement in per-dot medians, with no per-dot worsening exceeding 0.1° median or 0.25° P90. These are implemented **pilot acceptance settings**, not validated Focus Vision manufacturer specifications. The fitted correction is temporary (`awaiting_accuracy`) until this check passes; verification samples never refit it.
4. On success, **Continue** proceeds to the look-and-trigger activation exercise below. A failed, interrupted or incomplete check offers **Retry** or **Skip (no offset)**. Skip remains available at calibration, the accuracy instruction screen, and its result screen; it clears the correction and records an explicit skipped outcome, never a pass.
5. Complete activation, then **Center and begin**. The accepted correction stays active into the experiment, scoped to this participant and gaze source. If it becomes invalid before centering, preparation repeats. Returning from activation's Recalibrate runs the full preparation again.

`EyeMeasurements/study_preparation_<id>.json` records each attempt's participant/run, IPD metadata, calibration ID, validation ID/path, per-dot counts/errors, pilot thresholds, outcome and offset status. The independent check retains its usual immutable `validation_<timestamp>_<id>/` manifest, samples and events; it is excluded from scored trial data. Standalone Trial Viewer validation remains separate, retains its existing optional shape-confirmation sequence, and still clears its validation-only correction at completion. See [the calculation and thesis handoff](research/standard-gaze-accuracy-method.md).

**Open decision:** retaining Skip allows proceeding without a passed accuracy check. Analyze/exclude such recordings according to the approved study protocol; do not label them calibrated. Passing this spatial preparation does not validate saccade detection, IPA, sensor sampling rate, or whole-field accuracy.

### Post-calibration activation exercise

**Implemented (2026-10-25):** after completing or explicitly skipping the standard gaze preparation, three objects appear at eye height in front of the participant: a red cube, blue sphere and yellow capsule. In prompted order, look at the object until the study's gaze-vicinity highlighting responds, keep looking, point with the right controller and pull the trigger. A continuous 0.35-second response qualifies the gaze step; it is a familiarization debounce, not a fixation detector or research accuracy threshold. Wrong-object selections, ambiguous/unavailable gaze, interruptions and stale observation gaps do not advance. Each next object requires a new gaze qualification and trigger release/press.

All three checks must complete before Continue enables centering and the existing uncounted practice trial. Recalibrate returns to the entry procedure and then starts a fresh exercise. The exercise does not change calibration offsets, trial order, trial clocks or scores. Settings/focus interruptions hide the exercise objects and clear gaze qualification.

Separate `EyeMeasurements/activation_<id>.json` reports retain participant/run, alignment and eye-recording identifiers, intended/gaze/selected object indices, attempts, completed count and outcome. Eye rows use `phase=activation_exercise`, `activationStep`, and an empty trial ID. **Open decision:** this central three-object functional rehearsal does not establish whole-field eye-tracking accuracy or replace held-out validation.
