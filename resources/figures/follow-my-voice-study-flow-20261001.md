# Follow My Voice: setup and session figure

**Draft — October 1, 2026.** Panel A summarizes the locally implemented setup; panel B combines implemented task counts/controls with the proposed operator flow. Hardware acceptance, final survey administration, participant sample size and study approval are not established by this figure. It supersedes the old 60-trial/target-naming/dwell artwork for current planning; earlier files and historical Git versions are preserved.

- [Editable vector SVG](follow-my-voice-study-flow-20261001.svg)
- [Vector PDF](../../output/pdf/follow-my-voice-study-flow-20261001.pdf)
- [PNG preview](follow-my-voice-study-flow-20261001.png)
- [Reproducible vector builder](../../scripts/build-study-flow-figure.py)

## Caption

**Seated setup and planned flow for the preferred versus self-similar voice study.** (A) A participant wearing the HTC Vive Focus Vision remains seated on a swivel chair within eight virtual search surfaces. Representative dots indicate the scattered objects; the schematic does not depict all 168 objects or their height distribution. Eye gaze informs guidance and measurement. The right-controller ray highlights an object and the trigger confirms selection. The scene's room-front reference stays fixed; later trial readiness uses the previous target wall rather than a repeated return to front. (B) Two separate practice trials precede two counterbalanced voice blocks of 40 measured trials each. Both voices use shared participant-directed commands. The target is known to the agent but is not named or displayed to the participant. The agent guides horizontal turning and then finer horizontal/vertical adjustments. A correct selection ends active-search timing before chime/reinforcement and transition. Block questionnaires, breaks and refit/drift checks occur outside active-search time. Setup activities are grouped; final operator ordering, questionnaire route/battery and acceptance thresholds remain under review.

## Alt text

Two-panel diagram. On the left, a top view shows a seated headset wearer at the center of an octagon of dashed virtual surfaces. Gray dots stand for objects. A green eye-gaze arrow and blue right-controller ray point outward; an arrow above the octagon marks fixed room front. On the right, eight connected boxes show consent/baseline, setup and voice preparation, calibration/independent validation, two practice trials, first 40-trial voice block, questionnaires/break/drift check, second 40-trial voice block, and final questions/interview/debrief. A lower row shows readiness, object reveal/search timing, horizontal-to-vertical guidance, controller selection, and correct-capture feedback. An incorrect-selection arrow returns to guidance for the same target.

## Provenance and verification

Original vector primitives generated from current project requirements and source code; no prior paper artwork or participant photograph is embedded. Content sources: [current run sheet](../../docs/protocol/overleaf/experimenter-run-sheet.tex), [round lifecycle](../../docs/guide/02-gameplay-round-flow.md), [schedule](../../docs/balanced-angle-schedule.md), [geometry](../../docs/eight-plane-beta.md), and [gaze/telemetry](../../docs/guide/03-gaze-agent-and-telemetry.md). The screenshot/reference paper mentioned in task #30 is historical context, not empirical support for the current counts or methods.

The SVG retains text and vector shapes for editing. The PDF is one vector page, 526 × 546 points (approximately 185.6 × 192.6 mm). Intended placement is full two-column manuscript width, approximately 180 mm; main labels are 7.2–10 point before minor reduction, with a 14-point title. Do not reduce the combined figure into a single narrow column; split/reflow panels if that is required. The PNG is a high-resolution preview, not the vector source. Final venue sizing and researcher content approval remain open.

Rebuild from the repository root with a Python environment containing ReportLab:

```bash
python3 scripts/build-study-flow-figure.py
pdftoppm -scale-to 1800 -singlefile -png \
  output/pdf/follow-my-voice-study-flow-20261001.pdf \
  resources/figures/follow-my-voice-study-flow-20261001
```

PDF timestamps are generated at build time; byte-identical output is not claimed. Re-render and visually check after edits. Retain explicit **Implemented**, **Protocol proposal** and **Open decision** distinctions when adapting it for manuscripts or participant instructions.
