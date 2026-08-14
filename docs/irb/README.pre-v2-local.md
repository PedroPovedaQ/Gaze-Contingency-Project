# Gaze Contingency IRB Working Folder

This folder separates project-specific drafts from reference materials supplied by
another researcher in the lab.

## Status

Everything under `project/` is a **first-pass working draft**. Nothing here should
be described as IRB-approved, used for recruitment, or administered to
participants until the PI/faculty advisor has reviewed it and the approved UCF
templates have been completed.

## Folder map

- `project/` contains Gaze Contingency Project materials.
- `references/lab-example/` contains the original files from the other lab study.
  They are retained only as structural examples and questionnaire references.
- `source-inventory.md` records what each reference file contains and whether it
  fits this study.
- `scripts/irb/build_irb_packet.py` regenerates the project-specific Word drafts.

## Project packet

- `irb-documentation-draft.tex` and `irb-documentation-draft.pdf` are the original
  consolidated protocol draft.
- `gaze-contingency-hrp-503-content-draft.docx` provides section-by-section draft
  content for transfer into the current official UCF protocol form.
- `gaze-contingency-participant-materials-draft.docx` contains recruitment,
  screening, consent-information, session-script, stopping, withdrawal, and
  debrief language.
- `gaze-contingency-questionnaires-draft.docx` contains the proposed screening,
  demographics, XR-experience, simulator-sickness, NASA-TLX, usability,
  manipulation-check, privacy/trust, and interview items.
- Matching PDFs are review copies generated from the Word drafts.

## What was adapted

The lab example showed the expected submission structure: protocol content,
participant information/consent language, recruitment text, health screening,
validated questionnaires, study-specific questionnaires, and interview prompts.
The project packet follows that structure but replaces the other study's motor
control, ping-pong, controller, standing, and timeflow content with this project's
mixed-reality visual-search, eye-tracking, gaze-dwell, and spoken-guidance
procedures.

The following instruments were retained or adapted because they map to this
study's risks or research questions:

- Simulator Sickness Questionnaire items for pre/post safety monitoring.
- Raw NASA-TLX for perceived workload.
- System Usability Scale for overall usability.
- Minimal demographics and prior XR/eye-tracking experience.
- Study-specific guidance responsiveness, helpfulness, trust, reliance,
  distraction, and gaze-privacy items.
- A short strategy and experience interview.

The GEQ, full UEQ, UEQ-S, Presence Questionnaire, VEQ, ping-pong experience, and
six motor-performance questionnaires were not placed in the proposed
administration schedule. They would add participant burden without directly
measuring the current hypotheses. They remain available in `references/`.

## Decisions still required

- PI, faculty advisor, department, phone, and email.
- Final study title and whether the IRB covers only the implemented two-condition
  study or also the future self-similar voice factor.
- Sample size and supporting power or precision analysis.
- Compensation and partial-payment policy.
- UCF study site and approved storage location.
- Final block/round schedule and counterbalancing.
- Whether NASA-TLX is collected after each condition block; the current Unity
  build collects it once after the alternating 14-round run.
- Final eye-tracking quality, recalibration, exclusion, and missing-data rules.
- Data retention, deletion, sharing, and withdrawal cutoff.
- Whether voice recording/synthesis is in scope. If included, it requires separate
  consent and provider/retention language.

## Privacy note

The lab reference protocol contains another investigator's name, institutional
contact information, study locations, and study-specific decisions. Do not publish
or submit those reference files as part of this project. Use the project drafts
and the current official UCF templates instead.
