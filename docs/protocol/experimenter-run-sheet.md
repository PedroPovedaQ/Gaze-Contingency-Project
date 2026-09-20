# Gaze Contingency Search Study

Experimenter Run Sheet

Version: 0.1 | September 16 2026 

Researcher only: Two counterbalanced voice blocks, generic and self-similar. Both use gaze-contingent warmer/colder guidance. Planned trial start is the same fixed front direction. Say “first block” and “second block”; do not suggest that either voice should improve performance.

Session record: Participant ID ______  Run ______  Date ______  Researcher ______  Build ______  Voice order ______  Neutral profile ______  Survey version ______

## Before participant arrives

- Sanitize and test the Head Mounted Display (HMD) and controllers.

- Open Unity and Qualtrics.

- Enter coded ID and assigned condition.

- Prepare consent, baseline survey, both block surveys, final interview and session/event record. Use one defined TLX collection route; see the beta note in Section 7.

## 1 Welcome and consent

Say: “Thank you for coming. You will stay seated and turn to find virtual objects by their color and shape. A voice assistant will provide hints. You may pause, take a break or stop at any time without penalty.”

Say: “We will ask you to record a short voice sample to create a synthetic version of your voice. We also record eye-gaze and task data under a study code. Please read the consent form for what is collected, where it is processed and how it is handled.”
## QUESTION
Do we want to make them aware of the voice cloning?


ACTION: Obtain consent.

## 2 Initial Qualtrics

Say: “Please complete the background and current-comfort questions before we begin. Use only the study code already entered. Do not add your name or contact details. You may skip optional questions.”


## 3 Fit and calibrate the headset

Say: “Remain seated in this chair. You may turn the chair, your body and your head to look around, but do not stand or walk. Tell me immediately if you feel dizzy, nauseated, strained, uncomfortable or unsteady.”

ACTION: Fit equipment and verify tracking, perform automated eye callibration protocol.
## NOTE
Callibration step where the user looks at a dot in space, and we can use that as a callibration offset to boost accuracy.

Ask: “Can you see clearly? Does the equipment feel comfortable? Do you feel well enough to continue?”

## 4. Pre-Block Activity

Say: “First, I will explain the task. Your task is simply to locate objects that are around you. The objects that you'll need to find are specified by an agent. For example, the agent might say: "Locate the blue sphere". Once you find the object, hold your gaze on it for a few seconds to select it -- this completes the round. You will then continue this for other objects the agent specifies.

Say: “Next, read the displayed passage so the agent understands the way you speak". 
## QUESTION
Can we cover this in the consent step?

Say: “We will now connect a short sample of your voice, after the tone please read the script.” Then “Processing voice.” When ready: “Voice setup is ready. Let’s check the audio.”

## UPDATES
- Preferred accent
- Gender perception 
- Match Gender in preferred voice (to avoid cross-gender effect)
- Ask for 100 point rating on each voice (including self-similar)
- ("The agent's voice is similar to mine.", The agent's accent is similar to mine.) (1-7), Likert Scale
- Log time to first gaze
- Log time to first controller raycast/object intersect
- Log overall trial time (includes final selection trigger) 
- After action review tool (Participant Trial Replay) (Controller + Eye Gaze), is trigger held down (1/0) (60 HZ) + object position
- Total Gross Rotation + Head Movement (Y + X axis + Z), yaw/pitch/ per round'
- Drewes Gaze vs locomotion paper
- [10] J. Drewes, S. Feder, and W. Einh¨auser. Gaze during locomotion in virtual reality and the real world. Frontiers in Neuroscience, 15:656913, 2021. doi: 10.3389/fnins.2021.656913 3, 6
D. C. Niehorster, M. Nystr¨om, R. S. Hessels, J. S. Benjamins, R. Andersson, and I. T. C. Hooge. The fundamentals of eye tracking, part 7: 8Online Submission ID: 2013 Determining data quality. Behavior Research Methods, 58:183, 2026. doi: 10.3758/s13428-026-03039-4 3, 7
- use colors and italics to actions to perform without speaking
- update start of protocol, include mention of self-similar voice clone use in the experiment
## 5. Familiarization

ACTION: At the 360 search start checkpoint, seat the participant at the intended center facing the chosen room-front reference. Release the trigger, then press Trigger / Enter to center and begin. Record the reference; do not recenter to wherever the participant happens to face later.

Say: “Objects will appear around you, you can turn in your chair in all 360 degrees. Find the object with the announced color and shape. Turn while staying seated, then hold your gaze on the matching object until it is selected. You do not need to count rounds.”

## NOTE (USE IMAGE HERE 360 user figure)
Say: “The next rounds are practice, and are meant to simply get you familiar with the task.”

ACTION: Allow the user to complete the object selection practice rounds.

Ask: “Do you have any questions about the controls or task?”

Say: “When you are ready, we will begin the first task block.”

## 6 First task block
**INTERNAL SETTINGS**:
Practice | Real | 30 rounds

Researcher settings: Confirm the saved first voice and planned trial schedule. Keep object layout, selection method, guidance policy and the finalized prompt wording constant across voices. 

Say: “We will now begin the first task block. Find each target as quickly and accurately as you comfortably can. Stay seated and use the same gaze selection you practiced. Tell me if you need to pause.”

ACTION: Start block; monitor safety.

At completion say: “That task block is complete. Please stop and lower your hands comfortably.”

Say: “These questions ask about your workload, your experience of the activity and the agent. There are no right answers. Please answer based on the block you just finished.”

ACTION: Administer Survey Block #1.

Say: “You may take a short break of up to 5 minutes. Tell me when you are ready to continue.”

Before continuing ask: “Do you feel comfortable and well enough to continue?”

## Second Task Block
INTERNAL SETTINGS
| Assigned condition | 30 valid attempts


READ ONLY THE ASSIGNED CONDITION SCRIPT:

Say: “We will now begin the second task block. The task and selection method are the same. Find each target as quickly and accurately as you comfortably can, and tell me if you need to pause.”

ACTION: Start block; monitor safety.

At completion say: “The search task is complete. Please stop turning. We will finish the questions about this block, then I will help you remove the headset.”

Say: “These questions ask about your workload, your experience of the activity and the agent. There are no right answers. Please answer based on the block you just finished.”

ACTION: Administer Survey Block #2.
## 9 Final Qualtrics

Say: “Please complete the final questionnaire. It asks about your experiences with the two voices. It is fine to have no preference. We are interested in what you experienced, including anything distracting, uncomfortable or unhelpful.”

Say: “Some follow-up questions may appear depending on your answers and the timing condition you experienced. Answer based on your own experience and tell me when you have submitted it.”

Exploratory follow-up: After open responses, ask “Did either voice feel like your own thoughts, like another person helping, or neither?” Record this as an exploratory report, not proof of an internal-dialogue mechanism.

ACTION: Administer Post-Study Survey Block

## 10 Debrief and finish

Say: “Thank you. This study compares generic and self-similar voices during gaze-guided object search. Both voices provide gaze-contingent assistance. We are examining task performance and how participants experience the activity and assistant.”

Say: “Your data are stored under a coded participant ID. Please avoid discussing the timing condition or study details with people who may participate later, because knowing them in advance could affect their behavior.”

Ask: “Do you have any questions about the study or your experience?”

Say: “Thank you for participating. We are finished.”


## Immediate stop or pause criteria

PAUSE OR STOP: Participant request, dizziness, nausea, headache, eye strain, fatigue, pain, distress, unsafe movement or equipment failure. Help the participant remain seated and remove the headset when needed.

ACTION: Pause through the verified researcher control; the beta supports P on a connected keyboard and Trigger / Enter at its resume checkpoint. App focus loss can pause or technically stop a session. Log time, trial, reason and outcome. Resume only when the participant wants to continue and tracking/audio/alignment are valid; otherwise stop and preserve partial data.

## Final researcher check

- Confirm both block surveys and final interview/symptom records, or record why incomplete. Match participant, run, block and voice identifiers.

- Verify EyeData (Trial #, Participant ID), MotionData and TrialData (Most important)(Participant ID, Voice Type, Trial #/Block#/Distance to object, Rotation Theta, Gross Rotation (X/Y/Z), Gross Movement (X/Y/Z)) for all three blocks. Time to first gaze, Time to final gaze,time to first controller raycast intersection, final time to correct zone, final controller selection/overall completion time. # of zones visited, # of incorrect selections, # of main goal repeats requested, # of hints per round, include nasa-tlx survey and others etc include in the trial data file, repeat values if not measured on a per trial basis but instead measured on a block basis
- UPDATE: Think of more columns for trial Data
- Demographics Survey questions: what do you perceive your own accent to be?
- Secure data and sanitize equipment.

## Appendix Current recording passage

“Locate the red sphere. Look toward the upper shelf. Move your gaze slowly to the right. Check the blue cube near the center. Compare each object’s color and shape. Scan the lower row from left to right. Look beside the purple cylinder. Focus on the target and hold your gaze steady. Select the matching object. Take a short pause. Get ready for the next round.”
