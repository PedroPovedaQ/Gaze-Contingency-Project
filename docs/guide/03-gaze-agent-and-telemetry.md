# Gaze agent and telemetry guide

**Implemented locally — October 1, 2026; Vive verification pending.** This guide describes the shared gaze pipeline, controller selection and horizontal-then-vertical guidance currently in the source. It supersedes the earlier hover-based, warmer/colder guidance description. The study has two voice blocks of 40 measured trials each, plus separate practice. Both voices receive the same participant-directed commands; the agent knows the target identity and guides the participant without announcing it.

## 1. One gaze observation for measurement and decisions

`SharedGazeStream` reads the OpenXR EXT Input System eye pose. Each observation preserves the uncorrected pose, converted world ray, delivery and receipt times, validity, head/tracking-origin poses, trial context and optional HTC per-eye signals. Guidance, readiness, trial intersection timing, compatibility CSV and replay consume this shared observation. No head-direction fallback is substituted for missing eye gaze.

The stream records input updates and per-frame heartbeats, including invalid and stale states. `newInputUpdate` means a changed Input System delivery timestamp/device; it does not prove a new physical sensor observation. The HTC queries have a separate receipt time and are not assumed synchronized with the EXT pose. A render rate or polling rate is not a sensor sampling rate. Native 120 Hz acquisition remains unestablished.

New guidance decisions require a tracked, valid pose, application focus, a known fresh delivery timestamp, and a usable tracking parent. Available HTC data can additionally invalidate the combined observation when both eyes are invalid. The default delivery-age ceiling is 100 ms. `TryGetValid` rechecks freshness at use time, so a previously valid snapshot cannot remain usable indefinitely.

Unity already converts the EXT pose; `GazeCoordinates` applies its tracking-parent transform once. The former late X mutation in `EyeGazeRayVisual` has been removed. Optional local-origin X mirroring is a recorded diagnostic, defaults off, and does not modify preserved raw data. It is not an automatically validated calibration correction.

**Protocol proposal / pilot settings:** freshness, stability and directional tolerances below need headset validation. They are not validated fixation thresholds or evidence of measurement accuracy.

## 2. Visuals and right-controller selection

`EyeGazeRayVisual` controls the optional orange gaze line independently of acquisition. Hiding the line does not disable the stream. `GazeHighlightManager` remains a serialized anchor/configuration component; physiological event detection does not depend on its hover events.

`ControllerRaySelector` highlights the right-controller ray's object intersection blue and gives one short entry pulse. A fresh right-trigger press selects during active search. Looking alone never selects. Held triggers cannot carry selection across pauses, trials or tracking loss. Foreground UI blocks object selection; search objects remain fixed rather than being grabbed.

`StudyRayControls` provides right-ray Settings, Pause and Repeat guidance controls. Opening Settings pauses active search/readiness, docks the existing wrist menu and hides the checkpoint. Closing Settings returns to Continue. The study disables left-controller, hand-poke and gaze UI activation; passive left-controller telemetry can still be present. Haptics, menu reachability and comfort require physical-device verification.

The compatibility `dwell_progress` column remains zero. New summaries identify `controller_ray_trigger_press_v1`. Historical hover/fixation/saccade fields remain interaction proxies, not validated physiological events.

## 3. Guidance from horizontal area to vertical alignment

`HintGenerator` uses the shared valid ray and the hidden target's actual position:

1. Determine the horizontal wall sector independently of gaze height. Looking up or down within that wall does not count as leaving/reentering the area.
2. Outside the target wall, give left/right guidance. Larger turns use chair-turn language.
3. Inside the wall, correct horizontal error beyond 5° before giving vertical guidance.
4. With horizontal alignment, use up/down for elevation error beyond 3°, otherwise an alignment phrase such as “Right there.”

Near-vertical gaze has an unstable azimuth and is treated as unavailable for area decisions, not as a manufactured exit. Invalid or stale gaze cancels pending guidance and suspends new decisions. Directional candidates must remain stable for 150 ms. Ordinary hints are eligible 1.25 seconds after the preceding request, when speech is available. This is an eligibility interval, not a promise of uninterrupted speech or a fixed acoustic onset interval.

`DirectionalHintPolicy` supplies 44 shared variants across ordinary directions, larger turns, area entry, return, vertical directions and alignment. Both voice conditions use the same wording policy. `RequestRepeat` repeats the current valid direction rather than an obsolete direction or the hidden object identity.

### Area entry, overshoot and interruption

`GazeZoneEntryGate` debounces horizontal arrival and suppresses repeated area messages during vertical scanning. `GazeReturnCueGate` requires a stable 100 ms visit followed by a 250 ms horizontal exit; return eligibility lasts two seconds, with a six-second cue cooldown. Tracking gaps clear return evidence. These are pilot interaction settings, not blink/fixation definitions.

Area/return corrections can interrupt ongoing guidance. Pending corrections recheck gaze and active-search relevance before delivery; a queued correction does not consume its cooldown until speech is committed. A stale directional instruction is interrupted after a stable change. Ordinary area entry and overshoot retain separate correction reasons in telemetry.

Correct selection produces a chime and varied positive reinforcement in the active voice. Trial recording stays open through this feedback, and transition waits for it. Search completion time ends at selection; feedback time does not extend the performance measure.

## 4. Recording layers and stable trial identity

| Output | Purpose and limitations |
| --- | --- |
| `EyeMeasurements/<UTC>_<recording_id>/gaze_observations.jsonl` | Continuous raw/used observations, delivery updates and heartbeats, validity/gaps, independent validation targets and optional HTC signals. Preserve this source for quality analysis. |
| `capabilities.json`, `end_status.json` | Device/source/clock limitations and clean-close/error receipt. An absent receipt leaves completeness unverified. |
| `GazeData/<participant>/<run>/gaze_log.csv` | Per-frame compatibility table linked to the raw observation sequence/file. Includes invalid rows; unmeasured gaze/event fields remain blank. It does not establish sensor rate. Preparation uses separate unique directories. |
| Trial summary, object manifest and trial/performance CSV | Search timing, controller/gaze intersections, outcomes, instructions, schedule provenance and linked subjective measurements. Block ratings repeated in trial exports remain block measurements. |
| Replay JSONL and motion/event exports | Scene reconstruction, object state, head/controller/gaze samples and event timing. Frame sampling is distinct from continuous raw eye acquisition. |

The manager's **presented trial** remains stable while success feedback plays, even though its next-objective counter has advanced. Raw gaze, compatibility CSV and replay preserve the completed trial's round and voice across that interval, including the 39→40 block boundary and final trial. Practice is explicitly marked and does not borrow a measured-trial ID. New observations are not retroactive corrections to historical files.

Compatibility logging appends when re-enabled; a conflicting existing CSV header fails visibly instead of overwriting data. The continuous eye writer is bounded: overflow/write errors stop recording with an error. Interrupted final JSON records can be recovered as explicitly incomplete prefixes; corrupt middle records, mixed recording IDs and unordered sequences are rejected by analysis.

Replay samples/selection records carry the exact raw `gazeRecordingId` plus `gazeObservationSequence` under `gaze-observation-link-v1`. This stays unambiguous if raw logging opens another file mid-replay. `analysis/recording_links.py` audits explicitly supplied run/raw/replay files, verifies trial/object/selection links and transforms valid replay-local rays back to raw world coordinates. It records unavailable observations and incomplete/legacy evidence separately. File consistency is not validation of sensor rate, gaze accuracy or eye events; see the [audit command and limits](../../analysis/README.md#exact-replayraw-eye-link-audit).

Repeat requests and delivered repetitions have separate versioned events. `instruction_count` includes delivered repeats, while `hint_count` excludes them. A missing historical measure remains unknown rather than becoming zero; repeated CSV/replay evidence is not counted twice. See [trial export definitions](../trial-performance-csv.md).

## 5. Independent accuracy and eye-event validation

Use **Eye check** during setup or explicit pause. The diagnostic panel shows raw gaze, validity/reason, delivery age, head pose, rates and error to a visible independent dot. It never treats the hidden search target as an accuracy reference. Validation covers low/middle/high elevations, different bearings/distances and a wall seam; separate offset-fit, held-out and drift sets are available.

`analysis/eye_quality.py` reports delivery intervals, valid coverage/gaps, per-target angular error and dispersion. It can evaluate a proposed angular offset on a disjoint held-out set without applying it live. Candidate fixation/saccade/blink detection is explicitly unvalidated; tracking loss alone never becomes a blink, and events do not bridge trial/target/gap boundaries. Pupil availability is recorded without inventing a workload score.

Follow the [measurement and validation procedure](../gaze-measurement-validation.md) for target counts, commands, annotation comparisons and hardware gates. Quality must be established before interpreting eye events or comparing exploratory eye measures with NASA-TLX. Repeated block scores are not independent trial outcomes.

## 6. Voice isolation and delivery evidence

Self-similar speech uses its clone provider/cache only. Missing enrollment, keys or synthesis success never silently falls back to a neutral voice. Neutral caches include the actual provider voice ID. Setup now offers six voices per gender presentation; [voice choices](../voice-choices.md) documents the catalog, persistence and remaining rating decisions.

Audio request/ready/start/end/failure events and clip manifests provide software delivery evidence, not measured acoustic onset. Both audio checks use the same Accept Audio instruction in their respective voices. Confirmation requires successful completed playback. The candidate inventory snapshot records availability, not proof that a participant auditioned or rated every candidate.

## 7. Debugging and verification

- Check observation validity, delivery age and tracking-origin pose before interpreting a wrong direction. Separate a missing signal from an incorrect coordinate conversion.
- Inspect target wall, actual target position and gaze azimuth/elevation. A target's exact bearing can differ from its balanced wall-center theta.
- Compare requested, started and interrupted audio events. A requested hint is not necessarily delivered speech.
- Verify presented-trial ID/voice during capture, success feedback and the next transition.
- Check right-trigger release/re-press, foreground UI blocking and pause state for selection failures.
- Run `StudyVerificationChecks.Run` for desktop integration checks, plus the focused C# and Python suites. They do not verify headset accuracy, rate, acoustic levels, haptics or fit/recenter behavior.

Main source files: `SharedGazeStream`, `GazeCoordinates`, `GazeQualityPolicy`, `GazeDiagnosticView`, `HintGenerator`, `DirectionalHintPolicy`, `GazeZoneEntryGate`, `GazeReturnCueGate`, `ControllerRaySelector`, `StudyRayControls`, `GazeDataLogger`, `TrialDataLogger`, `TrialReplayRecorder` and `VoiceSynthesizer`.

## Guidance decision audit (implemented October 1)

`guidance-decision-v1` is recorded in the replay header and trial summary. A `guidance_decision` event contains a frozen snapshot: unique decision ID, stage, reason, semantic category, exact phrase, policy version, raw-eye recording ID/observation sequence, original input-delivery and receipt times, decision time, delivery age/availability, trial identity and current gating state. `uncertaintyStatus=not_estimated_pilot_thresholds` is deliberate; thresholds are not validated uncertainty estimates.

Ordinary and repeated guidance records its request before synthesis. Area/return corrections record the original request and a separate committed snapshot after relevance is checked again; both use the same decision ID. Abandoned fades emit `guidance_decision_aborted`. Invalid gaze logs a suspension when its reason changes, rather than flooding a row every frame. The repeat request counter remains separate from delivered-repeat counts.

Audio request, playback start/end and cancellation carry `decision_id` for the corresponding guidance request. The structured replay also exposes `guidanceDecisionId` and the nested `guidanceDecision` snapshot. Trial event CSV stores the same snapshot JSON in its escaped detail cell. Joins use eye recording ID + observation sequence, not a nearby rendered frame. `decisionTime`, gaze receipt/input times use Unity realtime seconds; replay `time` is relative to its fixed recording start. Audio start remains a **software playback call**, not verified acoustic onset or sensor-to-audio latency. Old recordings have no inferred decision IDs.

**Implemented — October 1 offline audit:** `analysis/guidance_audit.py` joins decision IDs to exact raw-eye recording/sequence pairs and software audio events. It exports per-decision outcomes and inconsistency rows, preserving unavailable/legacy/incomplete states. See `analysis/README.md` for the command, clock definitions and limits. This enables morning data-integrity checks; it does not validate the physical gaze or acoustic response.
