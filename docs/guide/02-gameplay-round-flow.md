# Gameplay round flow


**Implemented (2026-10-10):** correct right-trigger selection stops search timing before a 1.2-second, 100-fragment crumble effect begins. Fragments are cosmetic, share one mesh, have no colliders or rigidbodies, and use a separate deterministic RNG. The original target metadata remains available to capture observers. Existing chime and positive reinforcement still play; wrong selections do not crumble objects.
**Implemented locally — October 1, 2026; headset acceptance pending.** The current study has two counterbalanced voice blocks of 40 measured trials, plus two separate practice trials. Targets stay hidden from participant instructions/UI. Both voices use shared gaze-contingent commands, and only the right-controller ray/trigger selects an object. Earlier table-tap, shelf, dwell, 14-/20-trial and first-person descriptions are historical; see Git history for those implementations.


## Neutral quick test — implemented development shortcut

On the first voice setup panel, point the right controller at **Quick test — neutral voice** and press the trigger. This sets the session-only `SessionConfig.NeutralQuickTest` flag. The standard **Start setup** path stays unchanged. Quick test uses Eric (neutral male catalog option 0), clears any previous participant/clone state, prepares only the neutral phrase library, and skips voice selection, recording, sample acceptance, the study introduction and the two practice trials. Missing audio must load successfully before proceeding; a failed preparation offers retry/cancel and never starts silent trials. Existing cached clips are reused.

After loading, select **Center and begin** while seated and facing forward. This preserves floor-origin validation, normal per-trial readiness, hidden-target guidance and right-controller selection. The quick-test schedule starts from wall 0 (the centered forward direction), rather than the last practice target. Its version ends in `quick-origin0-v1`; all planned signed turn angles are preserved, and every later trial starts from the previous target wall. The run contains the first 40 planned trials (or a smaller existing debug override), all neutral, with ordinary gaze, motion, event and replay recording. The schedule's `enabled_in_run` flags match that limit. Runs use participant `DEV_NEUTRAL`, a `dev_quick_test_` folder label, `session-mode-v1.txt` with `study_data=false`, and `session_mode=neutral_quick_test` in the trial summary. Exclude them from study analysis. No two-voice readiness acceptance is fabricated. Resetting for a new participant clears the flag and restores the study schedule; it is not a saved launch preference.


## Session setup and practice

The active scene is `GazeContingencyStudyScene`. `VoiceModeSelector` handles presentation and preferred-voice choice: six candidates in each offered gender category, hover preview, selection and confirmation. Natural-voice enrollment prepares the self-similar condition. `VoiceAssistantController` and `VoiceSynthesizer` coordinate preparation and accepted playback levels for the assigned voices; failures must not silently replace a condition.

In rotational mode, the right-ray **Center and begin** checkpoint captures the seated position and horizontal room-front reference. Floor tracking must be available. No room-setup table tap is required. Head turning after centering does not redefine the fixed scene reference.

Two practice trials introduce the same task and selection method, one per assigned voice. They are explicitly labeled practice and remain outside measured-trial counts. After successful practice feedback, a researcher checkpoint confirms readiness. `FinishPractice` starts the measured run and emits `OnGameStarted`; `TrialDataLogger` then opens a single coded run folder for both blocks. Replay/raw-eye recordings can also include practice and setup, with their own explicit phase identities.

**Protocol proposal:** consent, background, equipment fitting, natural sample, voice choice, calibration and independent validation occur before measured trials. The [run sheet](../protocol/overleaf/experimenter-run-sheet.tex) owns operator wording; final survey/capture choices and setup sequencing need researcher review. The setup figure groups related activities rather than prescribing a new acquisition order.

## Reproducible objects and angles

`ChallengeSet` creates 80 measured definitions and records participant/schedule/layout seeds. Each block has eight trials in each absolute-angle category: 0, 45, 90, 135 and 180 degrees. Eight shuffled five-trial groups each contain all five categories. Paired directions are mirrored across blocks; their order is independently randomized. Theta uses the previous target wall as its reference, not fixed room front. The first practice initializes at the front wall.

Each trial has 168 objects, 21 on each octagonal face, with exactly one target identity. Layout sampling randomizes horizontal position and floor-relative height. Actual mesh transforms are saved for reconstruction. Target wall/slot allocation and repeated-identity counts are controlled separately from scatter. See [schedule definitions](../balanced-angle-schedule.md) and [geometry](../eight-plane-beta.md).

**Open decision:** current floor-relative heights do not enforce equal above/below quotas. Choose a vertical reference, deadband and allowable magnitudes before implementing that balance. A short debug run truncates the full schedule; it does not preserve completed-trial balance or establish full-session behavior.

## One trial: transition to selection

1. `BeginFirstRoundTransition` clears announcement readiness, resets controller confirmation, sets the assigned voice and the stable **presented trial** context, then emits `OnRoundTransitionStarted`.
2. `TrialReadinessGate` places a world-fixed cross on the previous target wall. Valid continuous gaze charges the gate; tracking loss, leaving the padded cross, pausing or an excessive unobserved frame gap resets it. This is a readiness interaction, not object dwell selection or a validated fixation.
3. `DoSpawnRound` creates/configures the planned objects, colliders, shape/color/plane metadata and non-grabbable interactables. Objects remain visually hidden. `OnRoundReady` publishes the snapshot; the voice coordinator marks announcement completion independently.
4. **Implemented October 9:** after the gaze cross completes and setup audio is ready, the agent says the opening direction (for example, “The object is to the right.”) while objects remain hidden and the search clock is stopped. A matching natural audio-playback completion permits `BeginSearch` to reveal the objects, start the pause-aware `StudyTrialClock`, reset trigger confirmation, hide the cross and emit `OnSearchStarted`. Cancelled/failed opening audio cannot start search. A readiness pause cancels that opening and requires a fresh cross charge after Continue. No 3-2-1 countdown or extra timed wait is added.
5. `SharedGazeStream` supplies validity-aware observations to guidance and logging. `HintGenerator` guides toward the correct horizontal wall, then smaller vertical, horizontal or combined diagonal adjustments. A 0.5-second valid gaze hold inside the target vicinity, with the target uniquely closest among overlapping object rings, can say “Yep, that's it!” or another confirmation variant; the right-controller target hold plus nearby gaze can say “You're on it!” Invalid or stale gaze suspends new decisions; changes can cancel obsolete speech. The agent does not name the target's color/shape.
6. `ControllerRaySelector` highlights the intersected object, sends an entry haptic and accepts a fresh right-trigger confirmation. Wrong selections emit `OnWrongCapture` and retain the current target/search. Looking alone never captures an object.
7. On correct selection, replay captures the selection/controller/object state before the manager advances its counter. The search clock stops; `OnObjectFound` writes the completed trial. A chime and varied reinforcement play in the active voice before the next transition. Success-feedback time is outside active-search time; logs remain open through it.

The timing/progress panel remains visible without target identity. A debug log or researcher replay can show target metadata; participant speech/UI cannot.

## Pauses, block boundaries and completion

`PauseSession` hides objects, suspends selection/speech and pauses the search clock. `ResumeSession` requires explicit Continue and retains the same trial. A readiness pause resets the charge and replays the readiness announcement; it does not resume an old search timer. Opening Settings during readiness/search invokes this pause behavior; closing Settings does not automatically continue.

After measured trial 40, the app waits for the first block questionnaire and a researcher break/continue checkpoint before applying the other voice. After trial 80, it finalizes the measured run and presents the final block questionnaire. The final interview/debrief is an operator procedure. Do not administer the same workload questionnaire twice through both app and external survey routes; select the route during protocol review.

Withdrawal, audio failure, timeout and other technical stops retain the partial run and reason. A stop during success feedback does not reverse a recorded correct capture. No replacement trial is silently inserted. A replay/raw-eye file without a clean footer/receipt remains incomplete even if some trials were completed.

## Data and verification

`TrialDataLogger` writes trial events, object manifest, schedule and run summary. `GazeDataLogger` is the compatibility CSV consumer of the shared gaze stream; continuous raw observations and replay remain separate. The offline [trial exporter](../trial-performance-csv.md) produces one aggregate performance/subjective CSV per logical trial, a combined table and provenance manifest. Repeated block ratings retain block scope.

Use [study QA](../voice-study-qa.md), [measurement validation](../gaze-measurement-validation.md), [telemetry definitions](03-gaze-agent-and-telemetry.md) and the [morning verification report](../overnight/2026-10-01-morning-verification.md). Offline integration checks and successful Android packaging do not verify physical haptics, acoustic behavior, eye accuracy, native 120 Hz acquisition, participant comfort or full-session burden.

### Shared instruction cards (implemented October 8, 2026)

`StudyPromptCard` and `Resources/UI/StudyPromptCard.prefab` provide the coaching-style surface and Inter typography for setup, audio checks, recording/loading, start instructions, trial status, surveys and completion. `RayMenuFeedback` uses the same rounded treatment for ray-selectable buttons. Instructions use a short heading and next action; the voice-card grid, survey questions, trigger/haptic behavior and hidden-target rules remain in their existing screen controllers. The readiness cross retains its prior stimulus typography and geometry. `Tools > Codex > Refresh Study Prompt Style` regenerates the shared prefab from the original coaching assets (using the surviving rounded template sprite when the legacy background reference is missing).

The original template Coaching UI and its Relaunch Coaching wrist control are disabled in the study scene to avoid duplicate startup instructions. The shared study setup card owns participant instructions.

**Implemented:** the current eight-wall study requires no surface selection. Voice setup proceeds to seated **Center and begin**, then the existing introduction/readiness/trial sequence. Room-floor tracking and gaze-readiness checks still apply.
