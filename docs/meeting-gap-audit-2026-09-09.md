# Spatial search and voice study: gap audit and work order

Audit date: **2026-09-09**. Baseline: remote `v2` and workspace HEAD at `38222695928db189881c8506b6be190bac6b7588`. Remote `main` is older. The existing voice PR #5 targets main and remains open even though voice work is present on v2. No existing GitHub issues were found before this backlog was created. The meeting source is the user-supplied cleaned summary, not an independently verified transcript; its calendar date is unknown.

**Implemented:** 14 fixed-seed bookshelf rounds, alternating gaze-awareness, 56 objects per round, dwell capture, trial/gaze logs, voice enrollment, saved clone ID and cached playback.

**Protocol proposal:** 360-degree search; balanced 45/90/135/180-degree transitions; an always-aware agent; two counterbalanced generic/self-similar voice blocks; coarse-to-fine directional coaching. These meeting directions have not yet replaced the older protocol or runtime.

**Open decision:** formal factor selection, theta reference and transition behavior, posture, provider/data flow, counts/duration, instruments and power. The older packet's four blocks, warmer/colder hints and offline OpenVoice conflict with both the meeting and the current cloud voice implementation. Repository text is not evidence of institutional approval.

## Highest-impact findings

- `TrialDataLogger` starts objective timing before the initial intro and at the preceding capture for later trials. Search-time estimates therefore include transition overhead.
- `VoiceSynthesizer` uses ElevenLabs for generic and Voxtral for self-similar, and can play generic fallback during a self-similar run. Provider differences and mislabeled actual audio can compromise the intended voice comparison.
- `VoiceModeSelector` reuses a global saved clone ID. Participant binding and verified deletion are needed for the study workflow.
- `GazeDataLogger` lacks separate head pose and explicit gaze-validity fields; `TrialDataLogger` calls hover segments fixations and hover transitions saccades. Repeated distractors share DisplayName identifiers.
- The old analysis tests gaze-aware versus gaze-unaware, rather than voice and theta. Trial schema, outcome definitions and analysis must migrate together.
- The runtime has no theta scheduler, rotational layout or two-block counterbalancing controller.

## Research grounding

Guo et al. (2024) studied appearance × voice during collaborative VR puzzles. Perceived intelligence/co-presence effects were attributed to appearance; voice affected likability, believability and eeriness. This motivates a carefully bounded transfer hypothesis, not an established voice-performance benefit in spatial search. [Institutional abstract](https://experts.illinois.edu/en/publications/collaborating-with-my-doppelg%C3%A4nger-the-effects-of-self-similar-ap/), [paper DOI](https://doi.org/10.1145/3651288).

Stein et al. used a swivel chair, eye/head recording and calibration checks for extended-field search. Their results distinguish reaching a target set from searching within it. This supports evaluating separate orientation/local-search behavior, but does not validate this project's posture or thresholds. [Primary study](https://www.nature.com/articles/s41598-024-59657-5).

OpenXR distinguishes tracked from untracked gaze pose; a transform alone is insufficient evidence of a valid current sensor sample. [Khronos specification](https://registry.khronos.org/OpenXR/specs/1.1-khr/pdf/xrspec.pdf).

A nonsignificant result is not evidence of equivalence. The eventual interpretation needs uncertainty estimates and, if claiming no meaningful benefit, a justified equivalence margin and sufficient precision. Counterbalancing addresses order confounding but does not remove awareness of the voice manipulation.

## Ordered backlog

The table is a recommended default work sequence. Dependencies in each issue control implementation readiness; parallel starts and finalization gates are listed below.

| Order | Issue | Deliverable |
|---|---|---|
| 01 | [#7](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/7) | Resolve meeting-to-protocol conflicts and freeze the spatial-search prototype contract |
| 02 | [#8](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/8) | Correct search-onset timing and add versioned spatial-trial telemetry |
| 03 | [#9](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/9) | Implement a participant-centered 360-degree search layout |
| 04 | [#10](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/10) | Generate reproducible trials balanced across 45, 90, 135 and 180 degrees |
| 05 | [#11](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/11) | Replace proximity encouragement with two-stage directional voice coaching |
| 06 | [#12](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/12) | Prepare matched voice libraries and prevent generic fallback in self-similar blocks |
| 07 | [#13](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/13) | Run two counterbalanced voice blocks with practice, breaks and stopping rules |
| 08 | [#14](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/14) | Validate Focus Vision gaze quality and separate eye from head movement |
| 09 | [#15](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/15) | Migrate analysis to voice-by-theta trials and add pilot quality reports |
| 10 | [#16](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/16) | Ground the voice hypothesis, choose measures and update the power plan |
| 11 | [#17](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/17) | Pilot posture, trial duration and fatigue, then deliver the lab demo |
| 12 | [#18](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/18) | Reconcile IRB materials and implement the selected voice-data lifecycle |

## Execution gates

1. **Resolve the prototype contract (#7).** Start the provider/protocol decision in #18 and literature/measure work in #16 alongside it. Do not wait until the end to resolve participant voice handling.
2. **Build the foundation:** correct timing/schema (#8), layout (#9), and audio isolation/readiness (#12). Once layout exists, implement theta (#10). Add coaching (#11), session blocks (#13), and sensor validation (#14) using the shared contract/schema.
3. **Prepare pilot evidence:** finish the descriptive/schema-validation portion of #15. Complete a short instrumented end-to-end development run with all angles and both voices. Confirm the applicable procedure before any human research pilot; institutional/provider review in #18 is a prerequisite to participant use.
4. **Run the pilot and deliver the demo (#17).** Measure posture, error/timeout rates, trial duration, coaching quality and fatigue. Do not infer a date from “Friday”; confirm the actual appointment.
5. **Freeze study readiness:** feed pilot evidence into #16's final power/trial-count decisions; finish #15's confirmatory model and #18's reconciled packet/voice deletion verification. All are required before the corresponding confirmatory study work.

This staged order is intentional: #15's descriptive pilot reporting precedes the pilot, while its confirmatory model follows #16. #16's literature work precedes the pilot, while its final power calculation follows it. #18's provider/authorization decision begins early, while final packet reconciliation follows the measured design.

Perspective/inner-speech effects and sonification remain deferred under the meeting proposal. They are not additional experimental factors in this backlog.

## Scope and verification

This session performed static code/document inspection, checked remote branch and issue/PR metadata, and verified selected primary research sources. It created planning issues; it did not implement their checklists or run the headset, collect participant data, validate eye-movement measures, modify Huron or establish an approval state. No runtime changes were made and no Unity/device build was triggered for this documentation-only audit.
