# Rotational AR Search Experiment Direction

## Motivation

The current Gaze Contingency Project uses a table-anchored bookshelf layout. This gives us a controlled conjunction-search task, but it keeps the search space largely in front of the participant. A stronger gaze-contingency direction is to place the participant in a chair at the center of the room, restrict locomotion, and require them to rotate to search for virtual objects distributed around them in mixed reality.

This redesign keeps the core research question intact:

> How does gaze-contingent assistance affect performance, attention, and workload during mixed reality object search?

The main change is the spatial structure of the task. Instead of searching a bookshelf, the participant searches an egocentric field around their body. This makes the experiment more directly about gaze, head rotation, field of view, out-of-view targets, and attentional guidance.

## Related Work

The closest precedent is Warden et al.'s AR visual search study. Participants used a HoloLens 2 while seated in a chair at the center of a room and searched for real-world objects distributed across a 180-degree field. The task included 32 objects placed horizontally and vertically around the participant, and AR target cues were compared across cue type and cue location. The system used gaze direction to determine which object the participant was looking at, and a bounding box appeared during fixation before the participant confirmed a response. This is highly aligned with a seated, rotation-based AR search setup.

[Warden et al. paper link](https://nuilab.org/wp-content/uploads/2023/06/5.2.pdf)

![Warden et al. seated AR visual search setup](assets/warden-ar-visual-search-setup.png)

Figure note: Warden et al.'s setup places the participant at the center of a room with colored objects distributed across desks, shelves, floor positions, and side surfaces. This is very close to the redesigned direction for this project: the participant remains seated, searches by rotating/head-turning rather than walking, and must inspect objects distributed across a wide egocentric field.

Key details to borrow or adapt:

- Use a seated participant at the center of the room as the spatial origin.
- Place objects at multiple horizontal angles around the participant rather than on one frontal shelf.
- Include vertical variation, such as floor, table, and shelf-height targets.
- Use gaze to identify the currently attended object.
- Give participant-visible feedback when the system believes an object is being selected.
- Compare assistance/cue conditions using time, accuracy, workload, and head/gaze movement.

Key differences from our proposed project:

- Warden et al. used real-world physical objects, while our first redesign can use virtual AR objects so object identity, target position, and distractor composition remain deterministic.
- Their task covered a 180-degree search field; our selected design expands this setup to a full 360-degree seated search field.
- Their cue conditions focused on AR target cue type and cue location; our primary independent variable remains gaze-aware versus gaze-unaware assistant feedback.
- Their response used a confirmation button after gaze targeting; our current project can preserve gaze-dwell selection.

Relevant citation:

- Warden, Wickens, Mifsud, Ourada, Clegg, and Ortega. "Visual Search in Augmented Reality: Effect of Target Cue Type and Location." HFES 2022. PDF: https://nuilab.org/wp-content/uploads/2023/06/5.2.pdf

Kelley et al. extend the cueing problem to a full 360-degree visual-search environment with multiple targets and distractors. They compare 2D Wedge, 3D Arrow, and Gaze Line cues against a no-cue baseline. This line of work is useful because it treats the full egocentric search field as the task space and measures search performance under different cueing aids.

Relevant citation:

- Kelley, McMahan, Wickens, and Ortega. "The Importance of Cueing While Visually Searching a 360 Degree Environment for Multiple Targets in the Presence of Distractors." 2025. Project page: https://mountainscholar.org/items/6005168d-3212-40e1-af86-ba8b1df74ae9

Binetti et al. study out-of-view object search in head-mounted AR and compare visual cueing with visual-plus-spatial-audio cueing. Their work is relevant if the redesigned task uses objects outside the current field of view or if the assistant gives audio proximity cues to reduce visual clutter.

Relevant citation:

- Binetti, Wu, Chen, Kruijff, Julier, and Brumby. "Using Visual and Auditory Cues to Locate Out-of-View Objects in Head-Mounted Augmented Reality." Displays 2021. PDF: https://discovery.ucl.ac.uk/id/eprint/10130700/1/Displays_UCL_OA.pdf

Gruenefeld et al.'s EyeSee360 work addresses how to visualize out-of-view objects in the 360-degree space around the user. It is useful for thinking about egocentric direction and distance encodings when the target may be behind or beside the participant.

Relevant citation:

- Gruenefeld, Ennenga, El Ali, Heuten, and Boll. "EyeSee360: Designing a Visualization Technique for Out-of-view Objects in Head-mounted Augmented Reality." SUI 2017. PDF: https://uwe-gruenefeld.de/pub/eyesee360-paper.pdf

Bork et al. study visual guidance in limited-field-of-view head-mounted displays and analyze head-rotation trajectories. This is relevant because the Vive Focus Vision display still imposes a limited field of view, and a rotational search task will make head-turn behavior one of the main dependent variables.

Relevant citation:

- Bork, Schnelzer, Eck, and Navab. "Towards Efficient Visual Guidance in Limited Field-of-View Head-Mounted Displays." TVCG 2018. IEEE: https://ieeexplore.ieee.org/document/8456525/

Recent visual-search work in extended fields of view is also useful even when it is VR rather than AR. In one study, participants sat in a 360-degree swivel chair and completed visual search while eye and head movements were recorded. This validates the chair-based search posture and gives precedent for treating head rotation and eye movement as separable search measures.

Relevant citation:

- "Eye and head movements in visual search in the extended field of view." Scientific Reports 2024. https://www.nature.com/articles/s41598-024-59657-5

Finally, wide-area AR navigation-aid studies are useful as cautionary literature. One CHI 2023 study found that navigation aids improved AR search performance but could shift attention toward virtual annotations and away from physical-world recall. That tradeoff matters for gaze-contingent assistance because the assistant may improve target finding while reducing awareness of the broader real environment.

Relevant citation:

- Lee et al. "The Impact of Navigation Aids on Search Performance and Object Recall in Wide-Area Augmented Reality." CHI 2023. PDF: https://attentionlab.psych.ucsb.edu/sites/default/files/images/publications/2023_TheImpactOfNavigationAidsOnSearchPerformanceAndObjectRecallInWide-AreaAugmentedReality.pdf

## 360-Degree VR Search Patterns to Incorporate

Several VR studies are useful even though the proposed task is AR, because they isolate the search behaviors that become important once the target can appear outside the current field of view.

Kelley et al.'s 360-degree visual-search work is especially relevant because it studies a full effective field of regard with multiple targets and distractors. Their task compares a no-cue baseline against 2D Wedge, 3D Arrow, and Gaze Line cues. The important takeaway for our design is that the 360-degree search field should not only contain one isolated target; it can include multiple relevant targets, distractors, and competing objects so that cue usefulness is tested under real search pressure.

Relevant citation:

- Kelley, McMahan, Wickens, and Ortega. "The Importance of Cueing While Visually Searching a 360 Degree Environment for Multiple Targets in the Presence of Distractors." VRST 2025. PDF: https://vtechworks.lib.vt.edu/bitstreams/0f57ce52-721c-4167-846d-1e7ad0d1e867/download

Stein et al.'s extended-field visual-search work is useful because it separates eye movements from head movements during VR search. Participants completed search in a wider-than-screen field, and the study tracked how head movements bring new regions into view while eye movements handle finer inspection once objects are visible. This maps directly to our seated AR design, where gaze-aware help may reduce unnecessary head rotation or improve the timing of head turns.

Relevant citation:

- Stein et al. "Eye and head movements in visual search in the extended field of view." Scientific Reports 2024. https://www.nature.com/articles/s41598-024-59657-5

Harada and Ohyama's 360-degree visual-guidance work is useful for comparing guidance styles around the body. Their framing treats the limited field of view of an HMD as an out-of-view problem and evaluates visual guidance effects for surrounding directions. This supports our plan to compare no cue, visual sector cue, voice cue, and gaze-aware coverage cue.

Relevant citation:

- Harada and Ohyama. "Quantitative evaluation of visual guidance effects for 360-degree directions." Virtual Reality 2022. https://link.springer.com/article/10.1007/s10055-021-00574-7

Audio and tactile 360-degree search work is useful for a more creative assistant condition. Studies of spatialized auditory, vibrotactile, and audio-tactile cueing in dynamic 3D VR search suggest that non-visual cues can reduce visual-search time without adding more visual clutter. For our AR task, this suggests a condition where the assistant's spoken content stays non-directional, but a spatial audio cue points toward an under-searched sector or likely target direction.

Relevant citations:

- Fincannon. "Spatialized Auditory and Vibrotactile Cueing for Dynamic Three-Dimensional Visual Search." Dissertation. https://www.proquest.com/openview/4748bba66352b07dfe7b3d51a60c4111/1
- "Spatial Sound in a 3D Virtual Environment: All Bark and No Bite?" Big Data and Cognitive Computing 2021. https://www.mdpi.com/2504-2289/5/4/79

These VR papers suggest several concrete changes to the experiment:

- Add an initially-out-of-view target factor rather than treating all target positions as equivalent.
- Add head-rotation measures as primary dependent variables, not only secondary logs.
- Consider blocks with multiple targets, where participants must find all objects matching a criterion.
- Compare cue styles that are meaningful in 360 degrees: gaze line, sector cue, spatial audio, coverage feedback, and no cue.
- Track search path quality: sector revisits, unsearched-sector neglect, cumulative yaw travel, and whether users scan past the target sector.
- Preserve a no-cue or generic-assistant baseline so the value of gaze-aware assistance is not confounded with simply having any cue.
- Use target/distractor clustering to create hard rounds, because a sparse 360-degree scene may be too easy once participants learn to rotate systematically.

## Proposed Task

The participant sits in a swivel chair at the center of a tracked room. They are instructed to remain seated and not translate through the space. They may rotate the chair, turn their torso, and move their head naturally. The task is to find target virtual objects distributed around them in a full 360-degree passthrough AR search field.

Objects are placed at fixed angular positions around the participant across the complete egocentric field. Some targets should begin in front of the participant, while others should begin to the side or behind the participant. Objects should appear at multiple vertical bands, such as low, mid, and high positions, to make the task require both yaw and pitch exploration.

The simplest stimulus set can keep the current shape-color objects:

- Shapes: sphere, cube, pyramid, cylinder, star, capsule.
- Colors: red, blue, yellow, purple.
- Target: one shape-color conjunction per round.
- Distractors: objects sharing the target color, sharing the target shape, and sharing neither.

The object-finding task should remain objective. This is preferable to paintings or artwork for the core experiment because search performance, wrong selections, and target recognition are easier to measure. Artwork could become a later variant for memory, preference, or semantic search, but the first redesign should preserve the current shape-color conjunction logic.

## Selected Layout

### Primary Setup: 360-Degree Search With Sectors

Objects are placed around the participant in a full circle and assigned to explicit angular sectors. Participants must rotate the chair to inspect targets behind and beside them. This 360-degree sector-based layout is the selected design for the study.

Advantages:

- Stronger egocentric search problem.
- Makes head/body rotation central to the task.
- Creates genuine initially-out-of-view targets.
- Better fit for out-of-view guidance, spatial audio, and gaze-aware coverage feedback.
- Differentiates the project from frontal shelf or 180-degree search designs.

Disadvantages:

- Higher physical fatigue.
- Harder to keep object distance and visibility consistent.
- Requires careful chair, cable, and space management, even on a standalone headset.

### Sector Structure

The 360-degree field should be divided into sectors, for example front, front-right, right, back-right, back, back-left, left, and front-left. Objects are distributed evenly across sectors and vertical bands. This makes coverage and neglect easier to measure.

Advantages:

- Clean dependent variables for sector coverage.
- Easy to implement gaze-aware hints such as "you have not checked behind you yet."
- Supports counterbalancing target location by sector.
- Allows explicit analysis of initially visible versus initially out-of-view targets.

Disadvantages:

- The sector structure may become obvious to participants if hints name sectors too explicitly.

### Reduced Debug Variant: 180-Degree Search

If the 360-degree layout creates usability or tracking problems during pilot testing, a 180-degree version can be used as a temporary debugging variant. It should not be treated as the final experimental design unless the full 360-degree task proves unsafe or unreliable.

## Candidate Experimental Conditions

The strongest assistance design is a 2 x 2 factorial crossing gaze awareness with voice type:

| Condition | Gaze awareness | Voice type | Description |
| --- | --- | --- | --- |
| Neutral, gaze-unaware | No | Neutral TTS or neutral recorded voice | Generic timed encouragement without gaze or coverage knowledge. |
| Neutral, gaze-aware | Yes | Neutral TTS or neutral recorded voice | Uses current gaze, recent gaze history, and sector coverage to provide warmer-colder or coverage feedback. |
| Self-similar, gaze-unaware | No | Participant-like, familiar, or self-similar cloned voice | Same generic hint logic as the gaze-unaware baseline, but delivered in a personalized/self-similar voice. |
| Self-similar, gaze-aware | Yes | Participant-like, familiar, or self-similar cloned voice | Gaze-aware assistance delivered in a personalized/self-similar voice. This is the highest-engagement but highest-privacy-risk condition. |

This design separates two questions that are easy to confound:

- Does gaze awareness improve search behavior and performance?
- Does self-similar voice change trust, engagement, compliance, or discomfort?

The most interesting outcome is the interaction. A self-similar voice may make gaze-aware feedback feel more personally relevant and coach-like, but it may also make the gaze monitoring feel more invasive. If the self-similar voice increases reliance, it could help when the assistant is correct and hurt when the assistant is vague or wrong.

If voice cloning is not feasible or not approved for the study, use a lower-risk personalization manipulation:

- neutral synthetic voice
- warm human-like voice
- familiar but not cloned voice
- participant-selected preferred voice

These options preserve the voice-type question without requiring biometric voice cloning.

The gaze-aware condition can be implemented at several levels:

- Current-object awareness: "You are off target" or "You are getting closer."
- Sector coverage awareness: "You have not checked much to your left."
- Revisit awareness: "You keep returning to the same kind of object."
- Recovery awareness: after an interruption, "Before that, you had mostly checked the front-right area."

Secondary conditions can be added later:

- Voice-only versus visual cue.
- Voice-only versus spatial audio cue.
- Warm/cold proximity feedback versus coverage feedback.
- sector count or target eccentricity within the 360-degree field.
- Perfect hints versus imperfect hints for trust calibration.
- Self-similar voice with transparent privacy disclosure versus minimal disclosure, if the ethics protocol allows studying perceived surveillance.

## Measures

The redesign should preserve the current performance and gaze logs while adding rotation-specific measures.

Performance measures:

- Time to find target.
- Wrong captures.
- First-try accuracy.
- Number of hints delivered.
- Time from hint to correct capture.
- Time from first target fixation to capture.

Gaze and attention measures:

- Fixation time on target.
- Fixation time on distractors.
- Number of unique objects inspected.
- Percent of objects inspected before success.
- Repeated fixations on non-targets.
- Gaze coverage by sector.
- Gaze coverage by vertical band.
- Time to first target fixation.

Rotation and search-path measures:

- Total head yaw rotation per round.
- Total chair/body yaw rotation if available.
- Maximum angular distance searched from start orientation.
- Number of sector transitions.
- Time spent facing each sector.
- Search path entropy or systematicity.
- Overshoot events where gaze passes the target sector but does not inspect the target.

Workload and subjective measures:

- NASA-TLX.
- Perceived helpfulness.
- Trust in the assistant.
- Reliance on the assistant.
- Compliance with hints.
- Annoyance.
- Eeriness or uncanniness.
- Voice familiarity and naturalness.
- Voice ownership or self-similarity.
- Perceived intrusiveness or surveillance.
- Privacy concern.
- Preference for gaze-aware versus gaze-unaware rounds.
- Preference for neutral versus self-similar voice.
- Perceived physical effort from rotation.

Optional memory and awareness measures:

- Recall of non-target objects.
- Recall of object sectors.
- Detection of occasional peripheral events.
- Awareness of the physical room versus virtual objects.

## Research Questions

Primary question:

> Does gaze-aware assistance improve performance and reduce workload in a seated AR object-search task where targets can appear outside the participant's initial field of view?

Secondary questions:

- Does gaze-aware assistance reduce unnecessary head rotation?
- Does gaze-aware assistance improve sector coverage or make search more systematic?
- Does self-similar voice increase trust and compliance with assistant feedback?
- Does self-similar voice make gaze-aware feedback feel more useful or more invasive?
- Does gaze-aware assistance help most when targets begin outside the participant's initial field of view?
- Does assistance improve target finding at the cost of environmental awareness?
- Is coverage feedback more useful than direct warmer-colder proximity feedback in a search space surrounding the body?

## Hypotheses

H1: Participants will find targets faster in gaze-aware rounds than in gaze-unaware rounds.

H2: Participants will make fewer wrong captures in gaze-aware rounds than in gaze-unaware rounds.

H3: Gaze-aware assistance will reduce redundant rotation, measured by lower total head yaw and fewer repeated sector visits.

H4: Gaze-aware assistance will increase search coverage efficiency, measured by faster inspection of previously unsearched sectors and fewer repeated fixations on already inspected distractors.

H5: The benefit of gaze-aware assistance will be larger for targets outside the participant's initial field of view than for targets near the starting forward direction.

H6: Gaze-aware assistance may reduce mental demand but could increase perceived intrusiveness if participants feel that the system is monitoring their eyes too explicitly.

H7: Self-similar voice will increase perceived familiarity, social presence, and compliance with hints relative to neutral voice.

H8: Self-similar voice will increase privacy concern and eeriness, especially when paired with gaze-aware assistance.

H9: The self-similar gaze-aware condition may produce the strongest subjective engagement but not necessarily the best objective performance if participants over-attend to or over-trust the assistant.

## Implementation Notes

The current shelf system should be treated as a special case of a more general spatial layout system. The redesigned layout should introduce a new rotational spawn manager that places objects using polar coordinates around a seated participant:

- center: participant/chair origin
- radius: fixed object distance, such as 1.2 to 2.0 m
- azimuth: object angle around participant
- elevation: vertical band relative to seated eye height
- sector: front, right, back, left, or finer bins

Each spawned object should keep the existing `SpawnableObjectInfo` metadata and add new angular metadata:

- `azimuthDeg`
- `elevationBand`
- `sectorIndex`
- `distanceFromUser`

The current gaze-dwell capture system can remain mostly unchanged because objects are still gaze interactables. The main changes would be:

- Replace `ShelfSpawner` with a `RotationalObjectSpawner` or add a second layout mode.
- Replace shelf row/column metadata with sector/elevation metadata.
- Update `AgentContext` and `HintGenerator` to reason over sectors instead of shelves/bookcases.
- Update logs to include target azimuth, target sector, current gaze sector, total head rotation, and sector coverage.
- Update the UI so target instructions appear near the participant's forward start direction or as a small head-locked/table-fixed panel that does not bias search toward one sector.

The participant should start each round facing a known forward reference. A fixation cross can be placed at the forward direction before each round, just as the current shelf design uses a fixation cross between the bookcases. This gives a clean starting orientation and makes target eccentricity from the start direction analyzable.

## Recommended Next Step

The first pilot should use the selected 360-degree seated layout with a reduced object count, such as 32 to 40 objects, to verify comfort, rotation behavior, gaze targeting, and sector coverage. After the mechanics are reliable, increase density to 48 to 64 objects and run the full gaze-awareness by voice-type design.

The most compelling final version is likely:

- seated participant in a swivel chair
- 360-degree virtual object field
- no locomotion
- gaze-dwell selection
- alternating gaze-aware and gaze-unaware rounds
- sector-aware warmer-colder or coverage hints
- measures of time, errors, gaze coverage, head rotation, workload, and trust

This version keeps the current project's strengths while making the task more spatially demanding and more clearly tied to out-of-view AR search.
