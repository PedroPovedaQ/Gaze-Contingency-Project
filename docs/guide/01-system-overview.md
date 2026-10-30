# System overview

**Implemented locally; headset acceptance pending — October 1, 2026.** This Unity 6 study targets the standalone HTC Vive Focus Vision. It uses seated 360° mixed-reality search, two counterbalanced voice blocks, gaze-contingent guidance and right-controller selection. The active scene is `Assets/Scenes/GazeContingencyStudyScene.unity`.

This overview replaces the earlier table/shelf, dwell-selection and warmer/colder description. The retained shelf mode and compatibility classes are not the current experimental procedure. [Round flow](02-gameplay-round-flow.md), [gaze/telemetry](03-gaze-agent-and-telemetry.md) and the [run sheet](../protocol/overleaf/experimenter-run-sheet.tex) give the detailed current account.

## Session flow

1. Use the right controller ray and trigger for setup. Choose a gender presentation, audition six voice options and confirm the preferred voice. Record a natural voice sample through the authorized self-similar enrollment workflow. Both conditions use ElevenLabs; accept starter audio levels and record the selected identities.
2. Fit/calibrate the headset and establish the seated scene reference. Complete one practice trial using the first assigned voice.
3. Run 32 measured trials in the first voice, take a comfort/tracking break outside active-search time; then run 32 in the other voice. The saved order controls the run; condition-specific questionnaires follow both blocks.
4. Each trial begins with a world-fixed readiness cross on the previous target wall (front initialization for the first practice) and a non-identifying spoken introduction. Reveal objects/start search only when readiness and speech gates pass.
5. The agent alone knows the target identity. It guides horizontal turning toward the correct wall, finer horizontal alignment, then vertical adjustment. Both voices use the same participant-directed wording policy. Invalid/stale gaze suspends new decisions.
6. Point the right controller ray at an object for highlight/haptic feedback and press the trigger. Wrong choices retain the search without revealing the target. A correct choice records capture, plays a chime and varied active-voice reinforcement, then advances. Looking alone does not select.
7. Preserve timing, outcomes, source observations, scene geometry, guidance/audio and survey provenance for post-session checking. A stop during success feedback retains the captured trial and marks the session stop separately.

## Runtime ownership

| Component | Responsibility |
|---|---|
| `FindObjectGameManager` | Search/transition/practice/block state, presented-trial identity, pause clock, object lifecycle and checkpoints. |
| `ChallengeSet` | Seeded 32+32 allocation, four of each signed step and two above/two below per step and recorded object/layout assignments. |
| `ShapeObjectFactory` / `SpawnableObjectInfo` | Object meshes/appearance, stable object identity and shape/color/plane metadata. |
| `ControllerRaySelector` | Right-controller ray intersection, highlight, haptic entry feedback and fresh trigger selection; no dwell capture. |
| `StudyRayControls` / `StudyCheckpoint` / `VoiceModeSelector` | Right-ray settings, pause/continue/repeat and setup/voice controls. |
| `TrialReadinessGate` | Fixed previous-wall gaze readiness and announcement gate before search exposure. |
| `SharedGazeStream` | One validity/freshness-aware EXT gaze observation stream, raw recording and independent optional HTC signal probes. No head-gaze substitution. |
| `GazeCoordinates` / `GazeDiagnosticView` | Explicit coordinate transforms and researcher-only independent target checks, fit/held-out validation and drift recording. |
| `HintGenerator` / `DirectionalHintPolicy` | Horizontal-then-vertical cue decisions, short phrase variants, stable direction/area/return gates and cancellation. |
| `GuidanceDecision` | Immutable decision and source-observation provenance linked to requests, commitments and software audio events. |
| `VoiceAssistantController` / `VoiceSynthesizer` | Trial speech and feedback ordering; progressive preparation, voice-specific cache, playback/cancellation and audio telemetry. |
| `ElevenLabsVoiceClient` / `VoiceEnrollment` | Current voice enrollment/synthesis transport, captured microphone audio, timeout/verification/error handling. |
| `GazeDataLogger` | Compatibility CSV sampled from the shared stream, with validity/gaps and raw-record references; not validated fixation/blink detection. |
| `TrialDataLogger` | Trial summaries, event CSV, schedule/object manifests, voice/order/outcome context and search duration. |
| `TrialReplayRecorder` | Separate reconstructable scene/head/controller/gaze/audio records with trial and raw-observation linkage. |
| `SessionConfig` | Coded participant/run identity, persisted voice choices/order, file paths and policy versions. |

Many components attach/discover their dependencies at runtime. Follow the assembly boundaries in `AGENTS.md`; `Assets/Scripts` does not share the same assembly as root `Assets` scripts. VIVE plane support is bridged through `VivePlaneProvider` because the device does not supply the template's assumed AR Foundation plane subsystem.

## Measurement and analysis boundaries

Raw observations live in `EyeMeasurements`; reconstruction recordings live in `GazeReplays`; trial/event/object/compatibility tables and voice/survey records belong to the coded run folder. [Per-trial export](../trial-performance-csv.md) creates one performance/subjective CSV per trial, plus a combined table and provenance manifest. It does not replace raw input files. Repeated block ratings retain block scope.

The EXT input-event timestamp measures delivery, not proven hardware exposure. HTC query times are separate. A 120 Hz polling loop, 120 file rows/s or identical coordinates cannot establish native 120 Hz observations. Blink/saccade/fixation candidates remain unvalidated; object intersections are interaction proxies. See [validation and diagnostics](../gaze-measurement-validation.md).

**Open decisions / verification:** signed-height reference and quotas, hardware spatial/rate/drift performance, physical menu/haptic behavior, listening/prosody/treatment equivalence, full-session burden/power and final instruments/consent. Floor-relative randomized heights currently do not implement 20-above/20-below balancing. Software tests and APK packaging do not establish headset or study acceptance.

## Verification entry points

- `StudyVerificationChecks.Run` / **Tools → Codex → Check Study Integration** exercises offline Unity integration.
- `python3 -m unittest discover -s analysis -p 'test_*.py'` exercises analysis and recording validation using the configured Python environment.
- `scripts/verify-study-session.py`, `analysis/trial_csv.py`, `analysis/eye_quality.py` and `analysis/guidance_audit.py` inspect different parts of an exported package; read [analysis definitions](../../analysis/README.md) before interpreting them.
- Use the [headset QA procedure](../voice-study-qa.md) and [morning verification report](../overnight/2026-10-01-morning-verification.md) for remaining physical checks and current evidence.
