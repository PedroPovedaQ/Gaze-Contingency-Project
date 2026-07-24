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
- Hint meaning, wording, opportunity, volume, target knowledge, and system feedback should otherwise be matched across cells.

The primary analysis should test the main effects of guidance awareness and voice similarity and their interaction. Sample size remains an **open decision** and should be determined with an a priori power or precision analysis that is appropriate for the interaction.

### Current executable study

**Implemented.** The application currently runs 14 rounds with 56 objects per round. Each round contains one target, 13 same-color distractors, 13 same-shape distractors, and neutral distractors drawn from six shapes and four colors. Participant-facing odd rounds are gaze-unaware and even rounds are gaze-aware.

The current build is not yet the proposed 2 × 2 experiment:

- **Implementation gap:** only one fixed ElevenLabs voice is configured; there is no generic-versus-self-similar voice assignment.
- **Implementation gap:** condition order is fixed by round number. The participant number does not counterbalance order or target-condition assignment.
- **Implementation gap:** gaze-aware and gaze-unaware hints have different default delays and intervals (2 s/4 s versus 3.5 s/9 s). This confounds guidance awareness with timing and hint exposure.
- **Open decision:** 14 rounds cannot be divided equally among four conditions. A balanced design could use 16 rounds, four per cell, or use a larger counterbalanced schedule with a justified unequal allocation.

No data collected with the current build should be described as evidence from the full 2 × 2 design.

## Evidence basis and adaptations

The procedure adapts practices from related visual-search, gaze-assistance, and self-similar-agent studies:

- Chiossi et al. used informed consent, headset eye calibration, criterion-based practice, counterbalanced condition order, post-block raw **NASA-TLX**, and controlled visual-search displays. Their fixation thresholds were developed for a Vive Pro Eye sampling at 120 Hz and should not be copied into this study without validation on the Focus Vision and the exported data stream. See [Searching Across Realities](https://doi.org/10.1109/TVCG.2024.3456196).
- Zhang et al. used task familiarization, a controlled environment, explicit search-time boundaries, and **NASA-TLX** following augmented-reality book-search tasks. This study adapts those procedural controls but uses a conjunction-search display and gaze-contingent audio guidance. See [See or Hear?](https://doi.org/10.1109/ISMAR-Adjunct60411.2023.00031).
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

**Open decisions:** finalize inclusion/exclusion criteria, screening instruments, compensation, voice-provider disclosure, source-recording retention, synthesized-voice retention, deletion timing, and whether participants may complete only the generic-voice conditions if they decline voice synthesis. These choices require ethics approval before recruitment.

### 2. Self-similar voice preparation

**Protocol proposal.** Complete voice preparation before the experimental blocks so generation delays do not alter condition timing.

1. Record every participant reading the same neutral passage in the same quiet room with the same microphone placement and gain.
2. Check the recording for clipping, background noise, omissions, and minimum provider requirements. Repeat the recording when the technical check fails.
3. Generate the self-similar voice using a fixed provider, model, and parameter set.
4. Render both generic and self-similar conditions from the same text strings. Normalize playback level and use the same audio path and spatialization.
5. Conduct a brief technical intelligibility check without revealing experimental hypotheses.
6. Record the voice ID, model/version, generation parameters, and file or request checksum in the session manifest.

**Open decisions:** select the standardized passage, generic comparison voice, synthesis provider/model, similarity acceptance rule, and fallback for failed or delayed synthesis. A similarity rating may be used as a manipulation check, but rejecting participants based on that rating requires a preregistered rule.

### 3. Environment and equipment setup

**Protocol proposal.**

1. Use the same physical table, headset, application build, room arrangement, lighting range, and audio-output configuration for all sessions.
2. Seat or position the participant at a marked origin and explain the safe search area.
3. Fit the Vive Focus Vision securely and confirm that the participant can see the entire virtual display comfortably.
4. Start a session manifest containing an anonymous participant ID, build identifier, headset identifier, condition schedule, and session timestamps.

Room changes, interrupted tracking, headset restarts, and application restarts should be recorded as deviations.

### 4. Eye-tracking calibration and validation

**Protocol proposal.**

1. Run the headset vendor's eye-tracking calibration after fitting the headset.
2. Validate gaze at several locations spanning the intended search area.
3. Refit and recalibrate if validation fails.
4. Repeat calibration after a long break, a headset shift, or sustained loss of usable gaze data.

**Open decision:** define a device-appropriate validation task and acceptance threshold during piloting. Do not present XRI object hover as a validated fixation measure. The current logger records rendered frames and hover state, not a confirmed native eye-tracker sample stream with a validated fixation classifier.

### 5. Instructions and practice

**Protocol proposal.**

Give standardized instructions:

- Find the announced conjunction target as quickly and accurately as possible.
- Select an object by looking at it continuously until the dwell selection completes.
- Continue searching after an incorrect selection.
- Listen to the assistant, but use or ignore its guidance as desired.
- Request a break whenever needed.

Practice should use stimuli and targets not used in analyzed trials. Demonstrate target announcements, object selection, incorrect-selection feedback, correct-selection feedback, and at least one example of each guidance policy. Continue until the participant demonstrates task understanding and reliable dwell selection.

**Open decision:** preregister the practice criterion. A reasonable pilot candidate is two consecutive targets selected without experimenter intervention, but this is not an established threshold.

### 6. Condition order and blocks

**Protocol proposal.**

Use four blocks, one for each factorial cell. Assign block order with a balanced Latin or Williams square so that each condition occurs comparably often in each ordinal position and follows every other condition comparably often. Assign schedules before the session from participant ID; do not allow the experimenter to choose the next condition.

Within each block:

- Use an equal number of rounds.
- Balance target shape, target color, and display layout across conditions.
- Use deterministic, logged seeds so a target/layout can be audited.
- Do not show the same exact layout repeatedly to a participant unless repetition is itself controlled.
- Match hint opportunities and spoken content across guidance-awareness conditions. Only the use of gaze information should differ.

Provide a short rest and the post-block questionnaire after each block.

### 7. Round timeline

The following timeline describes verified current behavior and the event boundaries required by the proposed protocol.

1. **Goal presentation — Implemented.** A fixation cross is shown and the assistant announces the target.
2. **Transition — Implemented.** The cross disappears, followed by a randomized blank pause of 0.2–1.3 s.
3. **Display onset — Implemented.** The 56 search objects appear.
4. **Search onset — Protocol requirement.** Log a dedicated `search_onset` event when the full display is available and participant search may begin. The current objective timer is initialized too early for later rounds and can include transition/announcement time.
5. **Guidance — Partially implemented.** Gaze-aware hints use inspected regions, scanning behavior, or target proximity; gaze-unaware hints provide general encouragement. For the factorial study, opportunity times and semantic content must be matched, and every hint must log its condition, trigger time, text/intent, gaze evidence used, voice identity, playback start, and playback outcome.
6. **Selection — Implemented.** Looking continuously at an object for 1.6 s triggers a capture.
7. **Incorrect selection — Implemented.** The application plays an error sound and the same round continues.
8. **Correct selection — Implemented.** The application records success and advances through the next transition.

The 1.6 s dwell threshold is an interaction parameter, not a fixation threshold.

### 8. Post-block measures

**Protocol proposal.** Administer measures immediately after every condition block:

- Raw **NASA-TLX**: mental demand, physical demand, temporal demand, performance, effort, and frustration.
- Guidance manipulation check: whether hints appeared responsive to where or how the participant looked.
- Voice manipulation check: perceived similarity or familiarity of the voice.
- Brief experience measures covering helpfulness, trust, reliance, enjoyment, comfort, and perceived control or agency.
- One short open-ended prompt about what helped or hindered search.

Use identical items and ordering after every block. Where preference is collected, ask open-ended impressions before revealing the four condition labels or requesting a forced choice.

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
| Guidance response | Change in gaze direction, target-region entry, or selection after a hint within a preregistered window | Requires synchronized hint and gaze events; window remains an **open decision**. |
| Target-directed looking | Latency to first target hover and target dwell before capture | Hover-derived proxy; do not label as fixation without a validated detector. |
| Search organization | Unique objects/regions visited, revisits, angular path length, and scanpath efficiency | Define formulas and missing-data handling before analysis. |
| Fixations and saccades | Events produced by a validated offline classifier from sufficiently sampled gaze data | Not established by the current hover-event labels. Hardware-specific thresholds require pilot validation. |
| Workload | Six raw **NASA-TLX** subscales after each condition block | Current single end-of-run form is insufficient. |
| Voice similarity | Post-block similarity/familiarity manipulation-check rating | Instrument and scale remain open. |
| Trust and agency | Prespecified, consistently scored post-block items or validated short scales | Select instruments and primary/secondary status before preregistration. |

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

- the minimum usable-gaze proportion and how it is calculated;
- calibration-failure and recalibration rules;
- treatment of blinks, missing samples, duplicate timestamps, and headset removal;
- maximum interruption or round duration;
- handling of TTS/network failures and incomplete utterances;
- participant-, block-, and round-level exclusion rules;
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
| 2 | Fixed condition sequence | Implement a reproducible balanced Latin/Williams-square assignment and log it. |
| 2 | Hover events named “fixation”/“saccade” | Rename them as hover episodes/angular transitions or add a validated offline eye-movement classifier. |
| 2 | Incomplete gaze-quality metadata | Export actual timing and available validity/tracking/calibration indicators; quantify achieved sampling behavior. |
| 2 | Four-cell imbalance with 14 rounds | Select a balanced round count or a justified rotating allocation before preregistration. |

## Open-decision register

Resolve and version-control these decisions before piloting the final protocol:

- participant population, eligibility, compensation, and target sample size;
- confirmatory hypotheses, primary outcome, and multiplicity strategy;
- number of blocks and rounds per cell;
- counterbalancing schedule and target/layout allocation;
- matched hint opportunity schedule and exact scripts;
- generic voice, self-similar voice workflow, similarity check, and synthesis fallback;
- voice recording and synthesized-asset retention/deletion;
- eye-calibration validation and recalibration thresholds;
- practice criterion;
- gaze-quality and technical-failure exclusions;
- gaze-event classifier and scanpath formulas;
- post-block instruments and item order;
- rest duration, stopping rules, and maximum session duration.

Once resolved, move each item into the relevant procedural section and label it **Protocol approved** with the approval or preregistration version. Do not silently convert recommendations into established protocol.
