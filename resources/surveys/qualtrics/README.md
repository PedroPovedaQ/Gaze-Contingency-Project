# Qualtrics draft surveys

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
