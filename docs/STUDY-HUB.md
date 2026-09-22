# Study hub

Updated **September 22, 2026**. Start here for the current study plan, questionnaires and research sources. [Latest meeting reconciliation: progress, owners and next steps](meeting-reconciliation-2026-09-22.md).

**Current status:** working beta; protocol and survey forms are being finalized. IRB not yet submitted, per the researcher. This page separates current planning from implemented behavior and older documents.

| I want to… | Open |
|---|---|
| Prepare a rehearsal | [Annotated run sheet](protocol/experimenter-run-sheet.md) · [Earlier Word edition](protocol/Gaze_Search_Experimenter_Run_Sheet.docx) — #27 must reconcile generic/dwell wording before using a revised script |
| Edit the formatted meeting run sheet | [Overleaf project](https://www.overleaf.com/project/6a84bc097073254fbca6985b) · [Standalone LaTeX and PDF](protocol/overleaf/README.md) — annotated review edition, September 22 |
| Understand the current experiment | [Protocol at a glance](#current-protocol-at-a-glance) · [Confirmed decisions and implementation gaps](thesis-study-alignment.md) |
| See every survey | [Survey shelf](#survey-shelf) |
| See everything we measure | [Measures register](measures-register.md) — timing, scoring, roles, data sources and missing implementation |
| Find papers and citations | [Reference shelf](#reference-shelf) |
| Know what to do next | [GitHub delivery board](https://github.com/users/PedroPovedaQ/projects/2) · [Epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19) |
| Check what the app actually does | [Eight-plane beta](eight-plane-beta.md) · [Round flow](guide/02-gameplay-round-flow.md) · [Telemetry](guide/03-gaze-agent-and-telemetry.md) |

## Current protocol at a glance

**Protocol proposal — current researcher direction:** seat the participant at the center of the search field and have them rotate to find objects. Compare the participant's explicitly **preferred voice** with their **self-similar voice**, in two counterbalanced blocks. Both voices provide gaze-contingent warmer/colder guidance. Gaze is measured independently; **controller ray + trigger selects**. Voice preparation uses cloud services. This supersedes arbitrary generic-voice and gaze-dwell requirements, but does not change the beta by itself.

**Protocol proposal — session flow:**

1. Consent, background questions and baseline symptom check.
2. Early voice sample/cloning and curated preferred-voice previews, every candidate's rating/rank and saved exact voice identity; #24 defines necessary exposure.
3. Headset fitting, built-in eye calibration, readability/custom validation and fixed-front reference; researcher delivers task instructions.
4. Short practice for search, controller selection, target repetition and tracking. Two practice trials are provisional; voice exposure and readiness criterion remain open.
5. First voice block. Each trial: **return to fixed front → hear target → reveal objects → rotate/search with warmer/colder hints → controller ray/trigger selects target**. Provisional schedule: 30 trials per voice, 60 measured plus two practice.
6. External block-specific questionnaires and break up to five minutes. Proposed battery: NASA-TLX, IMI, selected Guo agent-perception measures, separate voice/accent similarity and liking ratings, and technical checks. Final items/order and one TLX collection route remain open.
7. Re-don headset, revalidate tracking and alignment to the same front, then complete the second voice block and its questionnaires.
8. Post-exposure symptoms, voice preference, final interview and debrief. Verify files and survey joins.

**Implemented on audited v2 (`6457c1f`):** the beta has eight planes at 45-degree intervals, seven objects per plane (56 total), two practice trials and 14 measured trials in two neutral/self-similar voice blocks. Selection remains gaze dwell; the comparator is a male/female neutral profile. Eight starter clips load before remaining audio prepares in the background. The shared script modes are merged.

**Implementation gaps:** preferred-voice evaluations (#33), controller selection/repeat (#34), fixed-front reset and 30+30 schedule (#10/#13), external survey gates, calibration metadata, canonical trial rows and completion validation. The final shared [voice script mode](voice-script-pilot.md) still needs selection. Replay work is on a separate local branch, not on audited `v2`. See the [evidence table](meeting-reconciliation-2026-09-22.md#progress-actually-on-v2).

**Open decisions:** candidate matching/scales/ranking/exposure; trigger press versus hold and repeat mapping; first-gaze rule and calibration validation; practice policy; 30-per-voice angle allocation; shared wording; survey selection; primary outcome and sample justification; timeout and actual pilot date. See the [decision register](meeting-reconciliation-2026-09-22.md#open-decision-register).

[Current decision detail](thesis-study-alignment.md) is the best companion to this summary. The [long procedure](experiment-procedure.md) and [IRB protocol source](irb/source/protocol.md) still contain historical sections and require reconciliation. They are not frozen operator scripts.

## Survey shelf

“Included” means selected for study planning, not a completed participant-ready form. Source PDFs retain original wording; archived study PDFs may contain older administration instructions.

| Survey / instrument | Status for our study | Read / download |
|---|---|---|
| **NASA-TLX** — workload | Included, after each voice block. Finalize documented response range and scoring. | [Questionnaire source](irb/source/questionnaires.md#raw-nasa-task-load-index-after-each-block) · [Existing PDF draft](irb/submission/02_NASA-TLX.pdf) |
| **IMI** — motivation and activity experience | Included. Proposed four subscales: interest/enjoyment, effort/importance, pressure/tension, value/usefulness. Final form open. | [Official complete instrument PDF](../resources/surveys/imi-complete.pdf) · [Our selection/scoring plan](measures-register.md#imi-specification-and-source-transfer) · [Kao reference paper](../resources/surveys/kao-2021-self-similar-avatar-voice.pdf) |
| **Guo et al. Table A1** — agent perception | Added to planning. Full bank preserved: 42 ratings + one open response. Selection and voice-agent adaptations open. | [All questions and original references](../resources/surveys/guo-2024-survey.md) · [CSV item bank](../resources/surveys/guo-2024-table-a1-items.csv) · [Paper PDF, Table A1 on p. 23](../resources/surveys/guo-2024-collaborating-with-my-doppelganger.pdf) |
| **Voice similarity and individual voice ratings** | Included; final items and overlap with Guo survey need review. | [Source draft](irb/source/questionnaires.md#post-block-assistance-and-voice-check) · [Existing PDF draft](irb/submission/03_Assistance_and_Voice_Ratings.pdf) |
| **Final preference / interview** | Included; update legacy comparison questions to two voices. | [Source draft](irb/source/questionnaires.md#post-session-comparison-legacy-draft--revise-for-voice-only-design) · [Existing PDF draft](irb/submission/05_Post-Session_Comparison.pdf) |
| **Background / prior experience** | Retained as protocol proposal, once before the task. | [Source draft](irb/source/questionnaires.md#background-and-prior-experience-once-per-participant) · [Existing PDF draft](irb/submission/01_Demographics_and_Prior_Experience.pdf) |
| **SSQ** — simulator sickness | Retained as safety/comfort proposal; baseline and post-exposure. | [Questionnaire source](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/06_Simulator_Sickness_Questionnaire.pdf) |
| **Legacy eeriness differential** | Candidate; not interchangeable with Guo Q20–Q22. Avoid duplication; original anchors need verification. | [Source draft](irb/source/questionnaires.md#perceived-eeriness-legacy-candidate--exact-form-open) · [Existing PDF draft](irb/submission/04_Perceived_Eeriness.pdf) |
| **IPQ presence** | On hold; not reconfirmed for passthrough MR. | [Legacy PDF](irb/submission/07_Presence.pdf) · [Official item list](https://igroup.org/pq/ipq/download.php) |

**Survey burden:** the proposed full IMI selection (24) + full Guo rating bank (42) + TLX (6) totals **72 ratings per block**, before other checks. These are resources to select from deliberately; the final battery has not been frozen.

[Combined questionnaire source](irb/source/questionnaires.md) · [Survey resource index and provenance](../resources/surveys/README.md) · [Survey implementation issue #26](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/26).

## Reference shelf

| Reference collection | What it contains |
|---|---|
| [TimeFlow submission-video reference](../resources/videos/README.md) | Original supplied video (Git LFS); [final submission video task #32](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/32). |
| [Motor-control submission and figure reference](../resources/papers/README.md) | User-supplied *How Altered Time Scales Affect Motor Skill Acquisition in Virtual Reality*; visual precedent for our [setup/flow figure task #30](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/30). |
| [Kao et al. (2021), PDF](../resources/surveys/kao-2021-self-similar-avatar-voice.pdf) · [DOI](https://doi.org/10.1145/3474665) | Self-similar avatar voice in educational games; precedent for the proposed IMI dimensions. |
| [Guo et al. (2024), PDF](../resources/surveys/guo-2024-collaborating-with-my-doppelganger.pdf) · [DOI](https://doi.org/10.1145/3651288) | Self-similar appearance and voice in a collaborative VR task; full Table A1 survey. |
| [Guo survey's original references](../resources/surveys/guo-2024-survey.md#original-references-retained-from-guo-et-al) | Biocca et al. (2001), Moussawi & Koufaris (2019), Zibrek et al. (2018), Reysen (2005), Lam et al. (2023). |
| [IMI official source](https://selfdeterminationtheory.org/intrinsic-motivation-inventory/) | Instrument background, construct definitions and original sources; saved complete packet above. |
| [Research literature inventory](literature/xr-search-and-self-similar-voice-inventory.md) | Annotated literature for XR search and voice research. |
| [Local paper archive](literature/papers/README.md) · [Paper manifest](literature/papers/manifest.csv) | Downloaded papers, source URLs/DOIs, relevance and availability. |
| [Master bibliography](manuscript/references.bib) | Citation records for the manuscript, including survey-source attributions. |

## Keep this hub current

- Change a measure: update the [measures register](measures-register.md), then reconcile the participant form and analysis.
- Change a procedure: update the [decision checkpoint](thesis-study-alignment.md) and protocol sources; reflect the change in this summary.
- Add a survey or paper: add its file/source and provenance to the appropriate resource index, then add a link here.
- Use the [GitHub board](https://github.com/users/PedroPovedaQ/projects/2) for work status and dependencies; use these documents for study content.

The older [IRB packet](irb/README.md), generated forms and manuscript outputs are drafts. They do not automatically refresh when these source documents change.
