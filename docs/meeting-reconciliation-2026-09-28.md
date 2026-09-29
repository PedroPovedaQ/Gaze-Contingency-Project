# Meeting reconciliation — received September 28, 2026

**Source:** researcher-supplied whiteboard photograph and typed meeting notes in this chat. Meeting date itself is unconfirmed. **Status: current decisions / protocol proposals and implementation backlog**, not a claim of new runtime behavior, validated instruments, completed power analysis, or institutional approval.

This record supersedes conflicting older trial-count, warmer/colder-only, deferred-guidance and mega-CSV-only requirements. Two counterbalanced voice blocks remain participant-preferred versus self-similar; the photograph's “generic” label does not silently change the comparison. Preserve actual voice IDs and historical condition labels.

## Decisions and unresolved details

- **Current decision:** 40 measured trials per block, 80 total; practice separate. Retain the five absolute wall-angle categories with eight assignments each per block. Nominal wall angle, signed turn, exact target bearing and actual head rotation are different quantities.
- **Current decision — clarified September 28:** the gaze-contingent agent knows the target identity and position; the participant is not told the identity or location. No color/shape announcement or target preview is provided. The participant follows guidance, points the right controller and presses its trigger to select; the application evaluates the choice against the agent's target. Remove identity leakage from target announcements, repeats, wrong-selection speech and participant-facing displays, while retaining target identity in researcher-only logs/replay.
- **Current decision — clarified September 28:** first guide the participant horizontally to the correct vertical wall/plane, then provide up/down guidance within that plane. Here “vertical plane” means the surrounding wall, not a height band. Use this sequence in both voice conditions. Fine target-level alignment/arrival feedback and return-to-area handling still need operational thresholds so the participant can distinguish the correct object without knowing its identity.
- **Current decision:** half the target height deltas should be positive and half negative: 20 above and 20 below the chosen reference per block. **Open decision:** the reference (readiness cross, calibrated seated eye height, or previous target height), zero/deadband treatment and feasible magnitude range. Candidate allocation: four positive and four negative deltas within each eight-trial angle category, with paired height difficulty across voices. That cross-balancing is a proposal, not something legible enough to attribute to the board.
- **Current decision:** more frequent, shorter, more varied and natural guidance, including fragments and subtle corrections. Examples to pilot: “A little more right,” “Right there,” “Go back,” “Look up,” and “A little lower.” These are candidate phrases, not a frozen phrase library.
- **Current decision:** distinguish larger body/chair turns from nearby looking using “turn left/right” versus “look left/right.” **Open decision:** what “more than one rotation away” means. A one-wall/45-degree cutoff is a candidate interpretation, not an accepted threshold. Keep this separate from the existing greater-than-90-degree “way left/right” rule; reconcile the rules explicitly.
- **Current decision:** improve success feedback and add varied short success voice responses. Use the active block's voice and version/log the selected variant. Post-success audio is outside active search time and must not carry into the next target's cues.
- **Current decision:** loading text should read `Loading…….` (as supplied). This requests visual copy; it does not by itself replace the previously selected spoken processing announcement or remove error/retry information.
- **Current decision:** teach participants to use/rely on the agent and rotate using the chair; introduce passthrough later in onboarding. Exact passthrough checkpoint remains open; a later introduction is not authorization to disable safety-relevant visual access.
- **Current decision — clarified September 28:** only the right controller is needed for this project. Make setup, voice previews/acceptance, menus, wrist settings, selection, pause and guidance-repeat actions reachable using the right controller. Do not require the left controller to be present, tracked or connected; verify a complete session without it. Preserve right-ray intersection highlight/haptics and trigger selection. The separate unfinished “Don't” note has no actionable meaning and remains unresolved.

## Whiteboard transcription and interpretation

The researcher clarified the previously uncertain headings: **Euclidean distance**, **number of instructions**, timing to first eye/controller contact, and **time to final selection**. The timing wording describes a family of measures rather than an exact transcription of every abbreviation. Operational mapping: separate first valid target-gaze intersection, first right-controller ray intersection with the target, and final correct trigger selection. Record each latency from active search onset, excluding pauses/readiness; preserve wrong selections separately and leave unobserved events missing rather than zero.

**Number of instructions:** map this to task-guidance utterances whose playback actually starts during active search, using #21's event stream. Keep delivered repeats identifiable, preserve interruption/partial-playback flags, and do not count prefetch or playback requests that never start. Setup, success feedback and non-guidance utterances have separate categories. #8/#20 must map existing hint-count aliases rather than create duplicate columns with ambiguous meanings.

Other visible headings include “Trial Performance & Subjective,” “Motion Stuff,” PID, condition, zone angle, +/−, actual angle/yaw (Y axis), next-object height delta +/−, time to search, controller, workload/NASA-TLX and performance. **Ignore the small photographed To-do section**, as requested; its transcription is no longer an open action. Redacted content is not reconstructed. Handwritten numbers are illustrative, not participant observations or a statistical result.

## Export A — one performance/subjective CSV per trial

**Current decision:** write one small `trial_<trial_id>_performance_subjective.csv` per logical trial, with a header and one aggregate row. It is not a separate file for every sample. A session-wide concatenated analysis table can additionally be generated. Preserve incomplete, interrupted and practice outcomes with explicit status; do not invent zero values for unavailable measures.

**Proposed schema, to freeze under #8/#20:**

| Family | Fields and interpretation |
|---|---|
| Identity/provenance | Participant, session/run, block, trial, practice flag, condition/order, actual voice ID, schema/build/schedule/prompt versions, seeds, outcome and missingness/quality flags. |
| Geometry | Start/target wall and nominal absolute/signed theta; actual start/target bearing and yaw difference with reference/time; target ID/color/shape/position; reference position; signed target height delta in metres and its reference; Euclidean eye/head-reference-to-target distance at search onset in metres. Separate realized geometry from planned strata. |
| Timing/performance | Search onset/end, pause duration, active completion/trigger-confirmation time; first valid target-gaze intersection, first target controller-ray intersection, final gaze and final correct-zone arrival only after their boundary rules are frozen. Store seconds and explicit null/not-observed status consistently. Hover is not validated fixation. |
| Counts | Number of delivered task-guidance instructions (mapped to existing hint-count fields with defined repeat handling), repeat requests and deliveries separately, incorrect selections, unique zones visited and revisits separately, and relevant cue-stage counts. Define thresholds, deduplication and denominators. |
| Movement aggregates | Gross head rotation and translation totals and X/Y/Z components, with units, axis/quaternion convention and missing-gap rules. These are cumulative paths, not merely final minus initial pose. |
| Subjective | Selected NASA-TLX dimensions/score, finalized IMI and voice/reliance items, and other approved survey fields. Carry survey response ID, source scope (block/session/trial), instrument/item version, timing and missing reason. |

Block scores are collected once after the block and repeated into its trial files with their provenance. They are **not** 40 independently collected subjective observations. Initial trial exports can mark surveys pending; an idempotent join/re-export fills them after collection without altering immutable raw inputs. Preserve original raw survey responses and derived scoring separately. For questionnaires with session scope, retain that scope rather than pretending they describe a specific trial.

## Export B — separate reconstruction data

**Current decision:** keep high-frequency motion/replay data separate from Export A. Retain synchronized gaze, head/XR-origin and controller poses, ray/hit/trigger/tracking states, timestamps/frame/source IDs, coordinate frame and validity. Also retain object/scene snapshots (stable IDs, mesh/shape, scale, pose, target, visibility), lifecycle/selection/readiness events, exact speech/playback/interruption events and audio references. A path-only motion file cannot recreate the full trial by itself.

Use the same trial/session identities as Export A and a manifest linking the components. Keep existing replay JSONL/audio and raw gaze/event/object sources; a CSV representation is an analysis/interchange layer, not authorization to discard the original recording. Distinguish recorded states, derived values and interpolated display frames. Validate partial/truncated runs and gaps rather than silently manufacturing continuity. The prior mega CSV may remain optional; it is no longer the only requested analysis deliverable.

**Existing evidence, limited:** P020 has a local timeline demonstration and split exports, with two practice and six complete measured trials and an unfinished seventh search. This is not an 80-trial acceptance fixture or evidence of the new guidance policy. Its recordings remain local and are not uploaded with these task updates.

## Subjective additions — drafts, not validated scales

Collect the same wording after each voice block, using “this voice/agent” rather than revealing the condition or suggesting which should work better. Candidate 1–7 agreement items (strongly disagree to strongly agree):

1. “I was willing to follow instructions delivered by this voice.”
2. “I relied on the agent's guidance to complete the task.”
3. “I was motivated to complete the task without the agent's guidance.”

Replace the absolute statement “I used the agent's guidance 100% of the time” with a candidate percentage question: “During what percentage of this block did you use the agent's guidance?” (0–100%, plus unable to estimate). This is self-report, not a measured compliance percentage; freeze the denominator/wording after piloting. Retain the original statement in the source notes, not as a second redundant item.

The unfinished note “I relied more on the agent [because it was] my own — (Semantic …)” could mean a comparative reliance item, a self-voice attribution probe, or a semantic differential. **Open decision:** intended construct and endpoints. Suggested optional final probe: “What, if anything, about the voice affected your willingness to follow the guidance?” Ask before specifically probing own-voice attribution. Do not invent semantic endpoints or combine these draft items into a validated reliance scale. If guidance becomes necessary to identify the target, interpret reported reliance and independence in that task context; neither proves greater trust in one voice.

## Guidance and protocol acceptance

- #11/#23: define gaze validity, horizontal and vertical state transitions, hysteresis, stable arrival, overshoot/return, priority, cadence and abstention. Looking up within the correct wall must not repeatedly retrigger wall arrival. Nearly vertical/invalid gaze must not produce arbitrary directions. “Right there” must mean target proximity under a defined rule, not simply any point on its wall.
- #24/#21: use versioned phrase pools and reproducible/logged selection; record actual start/end/cancel and reason, not just queued text. Increase cadence without overlapping, repeating stale cues or speaking through a pause. Match guidance information and policy across voices; first-person versus external wording remains a co-manipulation to document.
- #27: candidate training wording: “The agent will know which object you need to find and will guide you toward it. You will first follow its directions to the correct wall, then look up or down as it guides you. Use the chair to turn while remaining seated. Point your right controller at your choice and press the trigger to select it.” Apply equally in both blocks; do not imply a preferred survey answer. Pilot target-level guidance before freezing participant speech.
- #10: generalize schedule generation, quotas and target-identity reuse beyond the current 24 shape/color combinations; the current twenty-trial no-repeat assumption cannot hold for eighty trials. Save signed/absolute horizontal and vertical assignments, realized deltas, reference poses, seeds, pairing and version. Do not achieve balance by silently replacing failed trials.
- #16: update power/sensitivity and duration/fatigue planning for 40+40 and the revised guided task. The older 48-participant/four-condition sensitivity calculation does not justify the current design. Repeated trials are not independent participants; P020's named-target pilot is not an estimate of this revised policy's performance.
- #17/#28: verify both voice orders, all horizontal/vertical strata, interruptions, errors, incomplete sessions, one-file-per-trial output, correct survey joins, and reconstruction against recorded events. Do not mark new requirements implemented because a prior beta passed.

## Existing GitHub owners

| Work | Owner |
|---|---|
| Decisions, target information, references and thresholds | #7 |
| Data package/schema | #8 |
| Forty-trial blocks and vertical/horizontal balancing | #10, #13 |
| Active coarse-to-fine and vertical guidance | #11 |
| Loading copy / readiness UX | #12 |
| Power, burden and outcome interpretation | #16 |
| Full headset/recording rehearsal | #17 |
| Participant/IRB source reconciliation | #18 |
| Epic and delivery sequence | #19 |
| Per-trial performance/subjective files | #20 |
| Guidance, success and interruption event logging | #21 |
| Raw motion/scene reconstruction support | #22 |
| Cue transitions, interruptions and return handling | #23 |
| Short varied phrase pools and success responses | #24 |
| Reliance/willingness/motivation questionnaire drafts | #26 |
| Reliance instruction, chair, later passthrough and operator flow | #27 |
| File validation and later survey joins | #28 |
| Controller allocation/access and repeat semantics | #34 |
| Reconstruction and timeline/data inspector | #36, #37 |

This update changes planning and tasks only. Runtime, headset build, live Qualtrics, Overleaf and submitted/generated IRB documents are not changed by this reconciliation.
