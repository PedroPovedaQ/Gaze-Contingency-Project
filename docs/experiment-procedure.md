# Experiment Procedure: Gaze-Contingent Guidance and Self-Similar Voice

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

The [IEEE-style planned-study manuscript](manuscript/gaze-guidance-planned-study.tex) is the concise scientific account of the approved 360° direction. This procedure remains the detailed decision register and must not imply that the planned eight-plane system is already implemented.

## Study design

### Intended factorial design

**Protocol proposal.** Use a 2 × 2 within-participant design. Rows manipulate whether the assistant uses gaze information; columns manipulate whether its synthesized voice is generic or self-similar.

| Guidance awareness | Generic voice | Self-similar voice |
|---|---|---|
| **Gaze-unaware** | General guidance in a generic voice | General guidance in the participant's self-similar voice |
| **Gaze-contingent** | Gaze-responsive guidance in a generic voice | Gaze-responsive guidance in the participant's self-similar voice |

The factors must be independently manipulable:

- **Guidance awareness** changes only whether the hint policy may use the participant's gaze behavior.
- **Voice similarity** changes only the rendered voice identity.
- Voice identity changes only the rendered voice. Scripts, timing, spatialization, playback level, and system feedback are held constant across voice columns.
- Hint opportunities and audio exposure are matched across guidance rows. Gaze contingency and its task-specific information are the intended guidance manipulation; the noncontingent prompts cannot be semantically identical to proximity guidance.

The approved planning target is **48 complete participant datasets**, with 12 participants randomly allocated to each order of a four-sequence balanced Latin square (Williams design). The standard schedule contains **up to 64 experimental trials**: four condition blocks with up to 16 trials each. The full 16-trial manifest places the target exactly twice on each of eight vertical search planes surrounding the seated participant at 45° intervals. A complete dataset requires at least 12 technically valid trials per factorial cell. Prespecified time, withdrawal, safety, or technical stops may produce fewer completed trials; the study may not adapt trial count to participant performance or emerging condition results. A simulation-based sensitivity and power analysis using pilot variance components remains required before preregistration.

### Current executable study

**Implemented.** The application currently runs 14 rounds with 56 objects per round. Each round contains one target, 13 same-color distractors, 13 same-shape distractors, and neutral distractors drawn from six shapes and four colors. Participant-facing odd rounds are gaze-unaware and even rounds are gaze-aware.

The current build is not yet the proposed 2 × 2 experiment:

- **Implementation gap:** only one fixed ElevenLabs voice is configured; there is no generic-versus-self-similar voice assignment.
- **Implementation gap:** condition order is fixed by round number. The participant number does not counterbalance order or target-condition assignment.
- **Implementation gap:** gaze-aware and gaze-unaware hints have different default delays and intervals (2 s/4 s versus 3.5 s/9 s). This confounds guidance awareness with timing and hint exposure.
- **Implementation gap:** the 14-round scheduler must be replaced by four condition blocks with a standard 16-trial manifest (up to 64 experimental trials), exact scheduled target-plane balance, and logged prespecified early-stop reasons.

No data collected with the current build should be described as evidence from the full 2 × 2 design.

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

- Find the announced conjunction target as quickly and accurately as possible.
- Select an object by looking at it continuously until the dwell selection completes.
- Continue searching after an incorrect selection.
- Listen to the assistant, but use or ignore its guidance as desired.
- Request a break whenever needed.

Practice should use stimuli and targets not used in analyzed trials. Demonstrate target announcements, object selection, incorrect-selection feedback, correct-selection feedback, and at least one example of each guidance policy. Continue until the participant demonstrates task understanding and reliable dwell selection.

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
3. **Display onset — Implemented.** The 56 search objects appear.
4. **Search onset — Protocol requirement.** Log a dedicated `search_onset` event when the full display is available and participant search may begin. The current objective timer is initialized too early for later rounds and can include transition/announcement time.
5. **Guidance — Partially implemented.** Opportunities occur at 4, 10, 16, 22, 28, 34, and 40 s after `search_onset`; the trial times out at 45 s. Noncontingent conditions provide duration-matched general prompts. Gaze-contingent conditions use a two-step policy: spatialized audio first directs attention toward the target plane, then the fine stage computes angular error between the valid gaze ray and the eye-to-target-center ray. Let `alpha` be the target's angular half-size and set `theta_max` to 45 degrees. Normalize proximity as `p = 1 - clip(max(0, theta - alpha) / (theta_max - alpha), 0, 1)`. The plane becomes attended after valid gaze remains within +/-22.5 degrees of its center for 300 ms. Relative to the preceding valid fine-stage opportunity, an angular improvement of at least 5 degrees produces **warmer**, worsening of at least 5 degrees produces **colder**, smaller change produces **about the same**, and `theta <= alpha + 2 degrees` produces **very close**. Fine evidence requires at least 80% valid gaze over the preceding 500 ms; otherwise the policy logs an abstention and plays a duration-matched neutral fallback. Fine stage returns to coarse after the plane is outside the attended sector at two consecutive opportunities. Every opportunity logs condition, evidence, state, utterance, voice, timing, outcome, and abstention. These are formative starting limits that must pass nonparticipant validation before being frozen; no cited source establishes universal thresholds for this headset and task.
6. **Selection — Implemented.** Looking continuously at an object for 1.6 s triggers a capture.
7. **Incorrect selection — Implemented.** The application plays an error sound and the same round continues.
8. **Correct selection — Implemented.** The application records success and advances through the next transition.

The 1.6 s dwell threshold is an interaction parameter, not a fixation threshold.

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
| Search speed | Time from logged `search_onset` to correct dwell capture | **Primary proposal.** Current objective timing can include transitions and must be corrected. |
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
