# Qualtrics draft surveys

## Current final survey (2026-10-28): side-by-side voice ratings

| Survey | Qualtrics | Source |
|---|---|---|
| **Final survey — side-by-side, with draft ARQ (current, v2026-10-28c)** | `SV_3retnpdBMwBx5bw` — [open builder](https://ucf.yul1.qualtrics.com/survey-builder/SV_3retnpdBMwBx5bw/edit) | [`04-final-side-by-side.qsf`](04-final-side-by-side.qsf), item map [`04-final-side-by-side.item-map.csv`](04-final-side-by-side.item-map.csv) |

Every condition-specific item is one Qualtrics *Side by side* question with two columns, **Selected Voice** and **Self-Similar Voice** (NASA-TLX 0–10, 24 IMI, 21 Guo, 5 draft ARQ, 12 custom items, plus per-voice liking, reliance and gender-presentation questions; technical checks; voice preference and debrief). Item wording is generated from the thesis appendix `docs/thesis/appendices/a-instruments.tex`, so the survey and the thesis cannot drift:

```bash
python3 scripts/qualtrics/build_final_survey_qsf.py   # uses final-survey-sbs-template.qsf (exported from UCF Qualtrics)
```

Import the regenerated QSF as a **new** survey (Tools → Import/Export → Import survey); never overwrite a survey with responses. The baseline questionnaire is now the in-headset **audio intake** (see the thesis Methods and the runtime docs), so it is no longer a Qualtrics survey.

Embedded data (set from the survey URL by the study runtime's `final-survey-handoff-<id>.json`): `participant_id`, `session_id`, `run_id`, `condition_order`, `selected_voice_block`, `self_similar_voice_block`, `selected_voice_name`, `handoff_id`. Once the survey is published, the link format is `https://ucf.yul1.qualtrics.com/jfe/form/SV_3retnpdBMwBx5bw?participant_id=P001&session_id=...&run_id=...&condition_order=A&selected_voice_block=2&self_similar_voice_block=1&selected_voice_name=...&handoff_id=...`. The survey is still an unpublished draft.

Superseded/working copies left in the account (no responses; safe for the owner to delete): `SV_0NhnsoYbpb72eYC` (side-by-side template stub), `SV_8HUANgpYbdf8jPM` (first import), `SV_cvbB1qoHtFoenDE` (v2026-10-28b, no embedded data or demographics). The three 2026-09-19 drafts below predate the final-survey decision (they still contain the SSQ and per-block administration) and are kept only for history.


These files are the reproducible **Advanced TXT** imports for the current questionnaire battery. They are drafts for protocol and pilot review; they are not an IRB approval or a published participant link.

| Draft | Qualtrics survey | Administration | Source file |
|---|---|---|---|
| Baseline & pre-task | `SV_5u2FpCk1228bfPE` — [open builder](https://ucf.yul1.qualtrics.com/survey-builder/SV_5u2FpCk1228bfPE/edit?Section=SV_5u2FpCk1228bfPE) | Once before the headset/task: consent-facing instructions, study code, demographics/experience and baseline SSQ | [`01-baseline-pre-task.txt`](01-baseline-pre-task.txt) |
| Post voice block | `SV_cwsLpEVffEIo9SK` — [open builder](https://ucf.yul1.qualtrics.com/survey-builder/SV_cwsLpEVffEIo9SK/edit?Section=SV_cwsLpEVffEIo9SK) | After each generic or self-similar voice block: block identifiers, NASA-TLX, full 24-item IMI proposal, technical checks and Guo Table A1 source bank | [`02-post-voice-block.txt`](02-post-voice-block.txt) |
| Post-session & safety | `SV_emqdHkeDg3gjYpw` — [open builder](https://ucf.yul1.qualtrics.com/survey-builder/SV_emqdHkeDg3gjYpw/edit?Section=SV_emqdHkeDg3gjYpw) | After removing the headset: post-exposure SSQ, symptom stop/ready checks, voice preference and qualitative interview | [`03-post-session.txt`](03-post-session.txt) |

Printable copies are generated under [`output/pdf/`](../../../output/pdf/):

- [Baseline and pre-task PDF](../../../output/pdf/gaze-study-baseline-pre-task.pdf)
- [Post-voice-block PDF](../../../output/pdf/gaze-study-post-voice-block.pdf)
- [Post-session and safety PDF](../../../output/pdf/gaze-study-post-session-safety.pdf)

Annotated provenance copies add a yellow source note after every item group and a references page:

- [Annotated baseline and pre-task PDF](../../../output/pdf/gaze-study-baseline-pre-task-annotated-sources.pdf)
- [Annotated post-voice-block PDF](../../../output/pdf/gaze-study-post-voice-block-annotated-sources.pdf)
- [Annotated post-session and safety PDF](../../../output/pdf/gaze-study-post-session-safety-annotated-sources.pdf)

The annotations distinguish published instruments, adaptations and study-created items. A study-created note means no external validated questionnaire is being claimed for that item.

The drafts were imported on 2026-09-19 and the blank starter block was removed from each survey. Qualtrics appended the import date to block names; that is expected. Question export tags preserve the repository item IDs so exports can be joined to [`docs/measures-register.md`](../../../docs/measures-register.md).

## Rebuild or revise

Edit the questionnaire definitions in [`scripts/qualtrics/build_surveys.py`](../../../scripts/qualtrics/build_surveys.py), then run:

```bash
python3 scripts/qualtrics/build_surveys.py
```

Import the regenerated file into a new Qualtrics draft or a versioned copy. Do not overwrite a survey that has pilot responses. Before collection, freeze the item map, response anchors, order, requiredness, scoring and missingness rules, and reconcile the final form with the IRB packet. The current study still has open decisions around the final burden, IPQ inclusion, the appearance-dependent Guo items and the primary outcome.

IPQ is intentionally not included: the measures register keeps it on hold for the passthrough MR design. SUS and UEQ are also outside the current battery. The Guo source bank is preserved in full for review; it is not yet a validated composite for this study.
