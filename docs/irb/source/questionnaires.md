# Participant questionnaires

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](../../balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

**Protocol proposal — October 20, 2026. Qualtrics on hold.** One practice trial; two 40-trial blocks; condition-specific questionnaires collected only in the final survey. See [current timing and reconciliation](../../protocol/2026-10-20-final-survey-decision.md). The authoritative formatted source is the corresponding file in docs/protocol/overleaf. Generated legacy forms are superseded drafts.

**Survey Questions by Referenced Block**

# Administration

Two counterbalanced voice blocks will contain 40 measured trials each,
with one separate practice trial. The final survey will administer two
separately labelled retrospective questionnaire sets, one referring to
each voice block, using identical item wording and order. No
questionnaires will be administered between blocks. The break and
immediate comfort/tracking checks will remain. Surveys and rest breaks
will occur outside active-search timing.

| Stage | Measures |
|:---|:---|
| Before headset exposure | Demographics, experience and baseline SSQ |
| Voice setup | Agent-gender preference, audition ratings and voice choice |
| Between voice blocks | Rest break, immediate comfort and tracking checks; no questionnaire |
| Final survey after both blocks | Post-exposure SSQ; two condition-specific sets (6 workload, 24 IMI, 21 agent-perception and 14 voice/assistance ratings each), technical checks, optional comments, voice comparison and debrief |

Each condition-specific set in the final survey contains 65 numeric
responses (130 across both sets) before technical checks and optional
text. Setup will include a separate liking rating for each auditioned
voice, including the self-similar voice. Baseline and final SSQ together
contain 32 symptom ratings.

# Baseline

**`BASE_STUDY_CODE`**

Study code (assigned by the researcher): *Response:* Coded identifier;
no name or contact details.

**`BASE_AGE`**

Age range: *Response:* 18–24; 25–34; 35–44; 45–54; 55–64; 65+; Prefer
not to answer.

**`BASE_GENDER`**

Gender identity (select all that apply): *Response:* Woman; Man;
Nonbinary; Another identity (optional description); Prefer not to
answer.

**`BASE_HANDEDNESS`**

Which hand do you usually use for everyday tasks? *Response:* Right;
Left; Both equally; Prefer not to answer.

**`BASE_CORRECTION`**

Do you use glasses or contact lenses? *Response:* Neither; Glasses;
Contact lenses; Both; Prefer not to answer.

**`BASE_MR`**

How often have you used virtual- or mixed-reality headsets? *Response:*
Never; Once or twice; Several times; Monthly; Weekly or more.

**`BASE_EYE`**

Have you used eye-tracking technology before? *Response:* No; Yes;
Unsure.

**`BASE_VOICE`**

Have you used a synthetic or cloned version of your own voice before?
*Response:* No; Yes; Unsure.

**`BASE_AI_FAMILIARITY`**

How familiar are you with AI voice assistants? *Response:* 1 = Not at
all familiar; 2–6; 7 = Extremely familiar.

## Current symptoms

**`BASE_SSQ`**

Rate each symptom now, before headset use. *Response:* One severity
rating per symptom.

1.  General discomfort

2.  Fatigue

3.  Headache

4.  Eyestrain

5.  Difficulty focusing

6.  Increased salivation

7.  Sweating

8.  Nausea

9.  Difficulty concentrating

10. Fullness of head

11. Blurred vision

12. Dizziness with eyes open

13. Dizziness with eyes closed

14. Vertigo

15. Stomach awareness

16. Burping

*Response:* 0 = None; 1 = Slight; 2 = Moderate; 3 = Severe.

# Voice setup

**`SETUP_AGENT_GENDER`**

What gender presentation would you prefer for the agent’s voice?
*Response:* Masculine; Feminine; Another presentation (optional
description); No preference; Prefer not to answer.

**`SETUP_CANDIDATE_LIKING`**

How much do you like this voice? *Response:* 0–100, with 0 = Do not like
at all and 100 = Like extremely; Unable to rate. Repeat for every
auditioned candidate and the self-similar voice.

**`SETUP_CHOSEN_VOICE`**

Which of these voices would you prefer to use for the task? *Response:*
Select from the voices actually offered. Ties in liking ratings will be
resolved by this explicit choice.

**`SETUP_AUDIO_CHECK`**

Can you hear the voice clearly and at a comfortable volume? *Response:*
Yes; No. Resolve volume or audibility problems before continuing. Record
the offered catalog/version, voice IDs, audition order, selected voice
and any unavailable preference. Demographic gender, preferred agent
presentation and perceived voice gender are distinct variables. No
preference will be inferred from demographic gender.

# Final survey: condition-specific questionnaire sets

Complete this set twice during the final survey, first for Block 1 and
then for Block 2. Display the referenced block number and actual voice
condition before each set. Please answer only about that indicated
block. There are no right or wrong answers. You may leave an item
unanswered if you cannot answer it. The researcher will associate the
response with participant, session and block IDs, actual voice
condition/ID, condition order, survey version and actual administration
timestamp. Record administration phase as final and reference scope as
participant-by-block; the BLOCK item IDs describe the referent, not
collection timing.

## Workload

Rate each aspect of the indicated block.

1.  Mental Demand – How mentally demanding was this block?

2.  Physical Demand – How physically demanding was this block?

3.  Temporal Demand – How hurried or rushed did you feel during this
    block?

4.  Performance – How unsuccessful were you in accomplishing the task?

5.  Effort – How hard did you have to work to achieve your level of
    performance?

6.  Frustration – How insecure, discouraged, irritated, stressed, or
    annoyed did you feel?

*Response:* Integer ratings from 0 to 10. For demand, effort and
frustration: 0 = Very low; 10 = Very high. For performance: 0 = Perfect;
10 = Failure. Intermediate values are numbered without additional
anchors.

## Activity experience: IMI

Rate each statement about the search activity in the indicated block.
Use 1 = Not at all true, 4 = Somewhat true, and 7 = Very true; the other
numbered points are available.

### Interest/enjoyment

1.  I enjoyed doing this activity very much.

2.  This activity was fun to do.

3.  I thought this was a boring activity.

4.  This activity did not hold my attention at all.

5.  I would describe this activity as very interesting.

6.  I thought this activity was quite enjoyable.

7.  While I was doing this activity, I was thinking about how much I
    enjoyed it.

### Effort/importance

1.  I put a lot of effort into this.

2.  I didn’t try very hard to do well at this activity.

3.  I tried very hard on this activity.

4.  It was important to me to do well at this task.

5.  I didn’t put much energy into this.

### Pressure/tension

1.  I did not feel nervous at all while doing this.

2.  I felt very tense while doing this activity.

3.  I was very relaxed in doing this.

4.  I was anxious while working on this task.

5.  I felt pressured while doing this activity.

### Value/usefulness

1.  I believe this activity could be of some value to me.

2.  I think that doing this activity is useful for practicing visual
    search.

3.  I think this is important to do because it can improve my search
    performance.

4.  I would be willing to do this again because it has some value to me.

5.  I think doing this activity could help me to search for targets.

6.  I believe doing this activity could be beneficial to me.

7.  I think this is an important activity.

## Agent perception

Rate the voice agent you experienced in the indicated block. Use 1 =
Very strongly disagree and 7 = Very strongly agree; intermediate points
are numbered 2–6.

### Co-presence

1.  I noticed the voice agent.

2.  The voice agent noticed me.

3.  The voice agent’s presence was obvious to me.

4.  My presence was obvious to the voice agent.

5.  The voice agent caught my attention.

6.  I caught the voice agent’s attention.

### Perceived intelligence

1.  The voice agent was able to operate without my intervention.

2.  The voice agent was aware of the virtual environment.

3.  The voice agent was able to set and pursue tasks by itself in
    anticipation of future needs.

4.  The voice agent was able to complete tasks quickly.

5.  The voice agent was able to find and process the necessary
    information for completing the task.

6.  The voice agent was able to adapt/adjust its behavior based on prior
    events.

### Appealing, eerie and familiar impressions

1.  I found the voice agent appealing.

2.  I found the voice agent eerie.

3.  I found the voice agent familiar.

### Perceived anthropomorphism

1.  The voice agent is able to speak like a human.

2.  The voice agent can be happy.

3.  The voice agent can be friendly.

4.  The voice agent can be respectful.

5.  The voice agent can be funny.

6.  The voice agent can be caring.

## Voice and assistance experience

Rate each statement about this block. Use 1 = Strongly disagree and 7 =
Strongly agree; intermediate points are numbered 2–6.

1.  The agent’s voice is similar to mine.

2.  The hints seemed responsive to what I was looking at.

3.  The hints were helpful.

4.  The hints were distracting.

5.  The speaking voice felt familiar.

6.  I trusted the spoken guidance.

7.  I felt comfortable hearing this voice.

8.  The voice felt uncanny or unsettling.

9.  I relied on the spoken hints when deciding where to search next.

10. The agent’s voice sounded natural.

11. I was willing to follow the agent’s instructions.

12. I was motivated to complete the task without the agent’s guidance.

**`BLOCK_VOICE_LIKING`**

How much did you like the voice in this block? *Response:* 0–100; 0 = Do
not like at all, 100 = Like extremely; Unable to rate.

**`BLOCK_GUIDANCE_USE_PCT`**

During this block, what percentage of the time did you use the agent’s
guidance to decide where to search? *Response:* 0–100%; 0 = None of the
time, 100 = All of the time; Unable to estimate.

**`BLOCK_PERCEIVED_GENDER`**

How would you describe the gender presentation of the agent’s voice?
*Response:* Optional free text; Unsure.

## Technical and comfort checks

**`BLOCK_HEARD`**

Did you hear all prompts clearly? *Response:* Yes; No; Unsure.

**`BLOCK_FAULT`**

Did any prompt unexpectedly repeat, cut off, arrive late, or use the
wrong voice? *Response:* No; Yes; Unsure. If Yes, optional description.

**`BLOCK_BREAK`**

Did you take an unplanned break or experience an interruption?
*Response:* No; Yes. If Yes, optional description.

**`BLOCK_COMMENT`**

Is there anything else you would like to share about this block?
*Response:* Optional free text.

# Final survey: symptoms, comparison and debrief

## Post-exposure symptoms and comfort

Administer these current-symptom items immediately after headset
removal, before the retrospective condition-specific sets. Rate each
symptom now.

1.  General discomfort

2.  Fatigue

3.  Headache

4.  Eyestrain

5.  Difficulty focusing

6.  Increased salivation

7.  Sweating

8.  Nausea

9.  Difficulty concentrating

10. Fullness of head

11. Blurred vision

12. Dizziness with eyes open

13. Dizziness with eyes closed

14. Vertigo

15. Stomach awareness

16. Burping

*Response:* 0 = None; 1 = Slight; 2 = Moderate; 3 = Severe.

**`POST_STOP`**

Did you stop or pause because of symptoms? *Response:* No; Yes.

**`POST_READY`**

Do you feel ready to leave the lab? *Response:* Yes; No. A No response
requires researcher follow-up.

## Voice comparison and debrief

**`POST_VOICE_PREF`**

Which voice did you prefer? *Response:* The catalog voice I selected;
The synthetic version of my own voice; No preference; Unsure.

1.  Why did you choose that response?

2.  How did you approach the task, and how did the guidance affect your
    strategy?

3.  What differences, if any, did you notice between the two voices?

4.  Which moments were helpful, confusing, delayed or distracting?

5.  Did either voice seem to affect your performance? What makes you say
    that?

6.  What was it like hearing the synthetic version of your own voice?
    What did you like or dislike about it?

7.  Did anything feel comfortable, unfamiliar or eerie? What contributed
    to that feeling, if anything?

8.  In what situations, if any, would you prefer a voice like your own?
    When would you prefer another voice or no voice?

9.  Did either voice feel like your own thoughts, like another person
    helping, or neither?

10. Do you have any concerns about how your synthetic voice was created
    or could be used?

11. What would you change about the guidance, voices or task?

Responses are optional. Ask the broad experience questions before the
specific eeriness and inner-dialogue probes. Use follow-ups when needed
rather than repeating a question already answered. Record perceived
performance separately from logged performance.
