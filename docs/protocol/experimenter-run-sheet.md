# Experimenter run sheet

**Protocol proposal — October 20, 2026. Qualtrics on hold.** One practice trial; two 32-trial blocks; condition-specific questionnaires collected only in the final survey. See [current timing and reconciliation](2026-10-20-final-survey-decision.md). The authoritative formatted source is the corresponding file in docs/protocol/overleaf. Generated legacy forms are superseded drafts.

<div class="tcolorbox">

**EXPERIMENTER RUN SHEET**

**Follow My Voice**

Experimenter protocol / October 20, 2026

</div>

#### SAY / ASK

Participant-facing speech is set in dark upright type.

#### ACTION / RESEARCHER

Experimenter-only instructions are italic and muted gold-brown. Do not
read them aloud.

|                        |                    |
|:-----------------------|:-------------------|
| **Participant ID**     | **Run**            |
| **Date**               | **Researcher**     |
| **Build**              | **Voice order**    |
| **Preferred voice ID** | **Survey version** |

#### CURRENT DESIGN / RESEARCHER ONLY

Two counterbalanced voice blocks: participant-preferred catalog voice
and self-similar synthetic voice. Each block will contain 32 measured
trials (80 total); one practice trial will be separate. Use the saved
assigned order. Each trial will be one hidden-target search. Both
conditions will use the same gaze-contingent guidance policy and
right-controller selection. Say “first block” and “second block” without
suggesting either voice will improve performance.

# Before participant arrives

- Sanitize and test the HTC Vive Focus Vision and right controller.
  Check the rotating chair and clear the seated movement area.

- Open the frozen study build and the versioned questionnaires. Confirm
  the build ID, audio-library version, recording destination and
  available storage.

- Enter coded participant/run IDs and saved voice-block order. Verify
  one practice trial and 32 measured trials per block. Confirm
  researcher visual aids and passthrough are set consistently across
  conditions.

- Prepare consent, baseline survey, both block surveys, final interview
  and session/event record. Use one defined TLX collection route and
  avoid duplicate administration.

# 1 Welcome and consent

#### SAY

“Thank you for coming. Today you will be performing a search task in
mixed reality. In this task, you will stay seated and turn to find
virtual objects. A voice assistant will know the target and will guide
you toward it without naming its color or shape. You will be performing
this task for a number of trials over two task blocks, each using a
different assistant voice. You may pause, take a break or stop at any
time without penalty.”

#### SAY

“We will ask you to record a short voice sample to create a synthetic
version of your voice. We will also record eye-gaze and task data under
a study code. Please read the consent form for what will be collected,
where it will be processed and how it will be handled.”

#### ACTION

Obtain consent.

# 2 Initial Qualtrics and Voice Setup

#### SAY

“Please complete the background and current-comfort questions before we
begin. Use only the study code already entered. Do not add your name or
contact details. You may skip optional questions. We will also ask what
kind of assistant voice you would prefer. There are no right or wrong
preferences.”

#### ACTION

Confirm the coded participant and run IDs. Include the baseline
demographics and current-comfort items, followed by the separate
questions below. Keep skipped responses distinct from “no preference.”

#### SURVEY ITEM / AGENT GENDER PREFERENCE

“What gender presentation would you prefer for the agent’s voice?”

#### ACTION / CANDIDATE PREPARATION

Use the frozen candidate catalog and common preview text. Have the
participant preview voices, choose one and confirm the selection. Save
requested gender presentation separately from actual voice ID; record
unavailable requests and “no preference” distinctly. Do not infer or
match the agent’s gender from participant gender. Record the configured
catalog and presentation order.

#### VOICE RATINGS

During setup, rate each auditioned voice, including the self-similar
voice, from 0 (Do not like at all) to 100 (Like extremely). Record voice
ID and audition order. At the final survey, collect liking again
separately for each voice block and the 1–7 voice-similarity item with
Strongly disagree / Strongly agree endpoints. Keep setup and final
retrospective responses separate.

#### SAY

“We will now record the short voice sample described in the consent
form. After the tone, please read the displayed passage in your usual
speaking voice.”

#### ASK / AUDIO CHECK

“Can you hear the voice clearly and at a comfortable volume?”

# 3 Fit and calibrate the headset

#### SAY

“Remain seated in this chair. You may turn the chair, your body and your
head to look around, but do not stand or walk. Tell me immediately if
you feel dizzy, nauseated, strained, uncomfortable or unsteady.”

#### ACTION

Fit the headset, verify both eyes’ tracking and freshness/validity
feedback, and perform the headset’s calibration. Then use independent
validation targets across low/middle/high positions, wall boundaries and
relevant distances. Record the validation result. Revalidate after
headset adjustment and between blocks.

#### OPEN DECISION / RELEASE GATE

Freeze numerical validation/drift criteria, retry limits and a
tracking-failure procedure using headset/task pilots before confirmatory
collection. Head direction must not silently replace missing eye gaze.
Ray intersections are not validated fixations, and polling at 120 Hz
does not establish distinct 120 Hz observations.

#### ASK

“Can you see clearly? Does the equipment feel comfortable? Do you feel
well enough to continue?”

# 4. Pre-Block Activity

#### SAY

“First, I will explain the task. In each trial, the agent will know
which object you need to find. It will guide you toward the object
without telling you its color or shape in advance. You will use the
agent’s directions to reach the correct area and then make smaller
adjustments up, down or to either side.”

#### SAY

“You will point the right controller at your choice to highlight it,
then press the right trigger to select it. Looking alone will not select
an object. Selecting the correct object will complete the trial. If you
select an incorrect object, you will continue searching for the same
target.”

#### SAY

“Before each search, you will look at the readiness cross until it
charges. You will hear the first direction before the objects appear.
When they appear, the timed search will begin. You will not need to
select a room surface.”

#### SAY

“If you would like a rest, let me know. We will normally take it between
trials, before the next search begins. You can also ask us to pause or
stop immediately at any time.”

#### BREAKS AND TIMING

Requested rest breaks normally occur after a trial ends and before the
next search begins. Do not start the next search until the participant
is ready. Breaks and questionnaires are outside active-search time. If a
participant needs to pause during a trial, pause immediately and record
the interruption and its duration separately. Verify that the frozen
build excludes paused time from active search; if it cannot, mark that
trial interrupted and retain the raw timing for the prespecified
analysis. Retain the interrupted-trial record; do not silently replace
it or count break time as search time.

# 5. Familiarization

#### ACTION

Seat the participant at the intended center and capture the designated
room-front reference using the right-controller start control. Do not
use keyboard shortcuts in participant instructions. Keep the same room
reference across the session. The readiness cross for later trials will
follow the saved schedule’s starting reference, currently the previous
target wall; it will not require a fixed front direction on every trial.
Verify this in the frozen build.

#### SAY

“Objects will appear all around you. You will use the chair to turn
while staying seated and follow the agent’s directions. You will point
with your right controller and press its trigger when you choose an
object.”

<div class="center">

Seated rotation schematic: see the formatted run sheet.

Schematic, not to scale. Illustrates seated rotation only.

</div>

#### SAY

“The next trial will be a practice trial to help you become familiar
with the task.”

#### ACTION

Complete one practice trial before measured collection. Verify
understanding of the cross, guidance and right-trigger selection. Record
the practice voice and any extra explanation or repeated practice. The
practice will use the first voice in the saved order: preferred catalog
voice for Order A, self-similar voice for Order B. Apply the same predefined proficiency rule to every
participant; freeze that rule before collection.

#### ASK

“Do you have any questions about the controls or task?”

#### SAY

“When you are ready, we will begin the first task block.”

# 6 First task block

#### PLANNED SCHEDULE

32 measured trials in this voice block; the practice trial is separate.
Verify the configured count before participant use.

#### RESEARCHER SETTINGS

Confirm the saved first voice and planned trial schedule. Keep object
layout, selection method, guidance policy and the finalized prompt
wording constant across voices.

#### SAY

“We will now begin the first task block. Find each target as quickly and
accurately as you comfortably can. Stay seated and use the same
right-controller ray and trigger selection you practiced. Tell me if you
need to pause.”

#### ACTION

Start block; monitor safety.

#### AT COMPLETION SAY

“That task block is complete. Please stop and lower your hands
comfortably.”

#### SAY

“You may take a break now. Please tell me when you feel ready to
continue.”

#### ACTION

Record break duration and current comfort. Recheck headset fit and
independently validate tracking before the next block; recalibrate if
required by the frozen acceptance rules.

#### BEFORE CONTINUING ASK

“Do you feel comfortable and well enough to continue?”

# 7 Second task block

#### PLANNED SCHEDULE

32 measured trials using the other assigned voice. Retain incomplete or
interrupted trials with their outcomes; do not add replacement trials
merely to reach 40 valid attempts.

#### SAY

“We will now begin the second task block. This block will use the other
assistant voice. The task and selection method will remain the same.
Find each target as quickly and accurately as you comfortably can, and
tell me if you need to pause.”

#### ACTION

Start block; monitor safety.

#### AT COMPLETION SAY

“The search task is complete. Please stop turning. I will help you
remove the headset, then you will complete the final questionnaires
about both blocks.”

# 8 Final questionnaire

#### SAY

“Please complete the final questionnaires. You will answer one set about
the first block and another about the second block. Each set will
identify which voice you used. Please answer about the indicated block,
rather than combining both experiences.”

#### SAY

“Some follow-up questions may appear depending on your answers about the
two voices. Answer based on your own experience and tell me when you
have submitted it.”

#### EXPLORATORY FOLLOW-UP

After open responses, ask “Did either voice feel like your own thoughts,
like another person helping, or neither?” Record this as an exploratory
report, not proof of an internal-dialogue mechanism.

#### ACTION

After headset removal, administer the post-exposure SSQ and safety check
first. Then administer the two condition-specific sets in block order:
six workload ratings, 24 IMI items, 21 selected Guo items and 14 custom
numeric ratings per set, plus technical checks and optional text. Use
identical item wording and order. Preserve the referenced block/voice ID
separately from the actual final-survey timestamp. Finish with
comparative preference and open debrief questions. Use the balanced
follow-up guide only after open responses, avoiding duplicate questions.

# 9 Debrief and finish

#### SAY

“Thank you. This study compares participant-preferred and self-similar
voices during gaze-guided object search. Both voices provide
gaze-contingent assistance. We are examining task performance and how
participants experience the activity and assistant.”

#### SAY

“Your data are associated with a coded participant ID. Your voice may
still be recognizable; it will be handled as described in the consent
form. Please avoid discussing the voice conditions or study details with
people who may participate later, because knowing them in advance could
affect their behavior.”

#### ASK

“Do you have any questions about the study or your experience?”

#### SAY

“Thank you for participating. We are finished.”

# Immediate stop or pause criteria

#### PAUSE OR STOP

Participant request, dizziness, nausea, headache, eye strain, fatigue,
pain, distress, unsafe movement or equipment failure. Help the
participant remain seated and remove the headset when needed.

# Final researcher check

- Confirm trial counts and practice labels; retain
  incomplete/interrupted trials and reasons. Match participant, run,
  block, trial, voice and build identifiers.

- Confirm the per-trial performance/subjective CSV and separate
  time-series motion/gaze/event/replay data. Check raw timestamps,
  validity, target/object positions and survey joins; distinguish
  missing values from zero.

- Preserve object-reveal, first target-gaze intersection, qualified gaze
  confirmation, first target-controller intersection, controller
  confirmation, selection attempts and correct-trigger timestamps as
  distinct events. Do not label one as another.

- Record completion time, starting theta, signed height delta, target
  distance, instruction count, incorrect selections and interruption
  duration. Repeat block-level survey values on trial rows only with
  scope/block labels; they are not independent trial responses.

- Confirm both condition-specific sets of the final questionnaire and
  the interview/comfort records, or why incomplete. Secure exports and
  voice assets according to the approved retention plan; sanitize
  equipment.

#### AUDIO AND READINESS CONFIGURATION

Archive the output device, volume/spatialization settings, microphone,
recording passage, synthesis model and audio-library version. Freeze the
cross charging duration and validity region, object collider/vicinity
definitions and incorrect-selection feedback; apply the same settings
across voice conditions.

# Difficulty and timing record / researcher only

#### CONFIRMED OUTCOME

Active trial completion time will be the primary outcome, accounting for
horizontal theta toward the target from the starting reference. Record
the opening cue separately: it will precede object reveal and the
active-search timer. Completion will be the correct right-trigger
selection.

#### OPENING-CUE CHECK

Log cue onset/end, object reveal and head pose at cue onset and reveal.
Participants may turn before the timer starts. Proposed sensitivity
outcome: cue-onset-to-correct-selection time, alongside the primary
active-search time. Starting theta alone will not account for movement
during the cue.

#### THETA

Keep nominal wall-angle category, exact starting target bearing, and
actual head rotation distinct. The schedule will allocate eight trials
at each absolute nominal angle (0, 45, 90, 135 and 180 degrees) per
block in a randomized order. Save signs, positions, reference
definition, seed and realized schedule. Calculate pre-cue theta from the
horizontal origin-to-cross and origin-to-target vectors. Retain the sign
and absolute angle; do not divide completion time by degrees.

#### CONFIRMED HEIGHT REFERENCE

Signed height delta will be target-center world Y minus
readiness-cross-center world Y, in metres. Positive means above the
cross; negative means below. Save both positions. The intended quota is
20 above and 20 below per block; verify implementation and freeze
feasible ranges/deadband before claiming the quota is met.

#### BEFORE CONFIRMATORY RECRUITMENT

Complete the theta-adjusted power analysis and recruitment plan; set
tracking acceptance, practice proficiency, timeout, interruption and
missing-data rules; verify the vertical schedule and acoustic checks;
reconcile consent with actual service processing and retention.

# Appendix: Self-similar voice recording passage

#### ACTION

Use the recording passage displayed by the frozen build and archive its
version. The text below is the retained passage from the source run
sheet; verify it matches the release before use. Its color/shape
examples are recording material, not target instructions during measured
trials. Read in a usual, natural voice without deliberately adding a
whisper or inner-thought style.

#### CURRENT RECORDING PASSAGE

“Locate the red sphere. Look toward the upper shelf. Move your gaze
slowly to the right. Check the blue cube near the center. Compare each
object’s color and shape. Scan the lower row from left to right. Look
beside the purple cylinder. Point the controller ray at the target and
check its highlight. Press the trigger to select the matching object.
Take a short pause. Get ready for the next trial.”
