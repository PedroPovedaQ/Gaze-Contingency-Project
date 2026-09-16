# Study hub

Updated **September 16, 2026**. Start here for the current study plan, questionnaires and research sources.

**Current status:** working beta; protocol and survey forms are being finalized. IRB not yet submitted, per the researcher. This page separates current planning from implemented behavior and older documents.

| I want to… | Open |
|---|---|
| Run or rehearse a session | [Experimenter run sheet — Word](protocol/Gaze_Search_Experimenter_Run_Sheet.docx) · [Readable source](protocol/experimenter-run-sheet.md) — scripts, actions and current implementation gaps |
| Understand the current experiment | [Protocol at a glance](#current-protocol-at-a-glance) · [Confirmed decisions and implementation gaps](thesis-study-alignment.md) |
| See every survey | [Survey shelf](#survey-shelf) |
| See everything we measure | [Measures register](measures-register.md) — timing, scoring, roles, data sources and missing implementation |
| Find papers and citations | [Reference shelf](#reference-shelf) |
| Know what to do next | [GitHub delivery board](https://github.com/users/PedroPovedaQ/projects/2) · [Epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19) |
| Check what the app actually does | [Eight-plane beta](eight-plane-beta.md) · [Round flow](guide/02-gameplay-round-flow.md) · [Telemetry](guide/03-gaze-agent-and-telemetry.md) |

## Current protocol at a glance

**Protocol proposal — confirmed direction:** seat the participant at the center of the search field and have them rotate to find objects. Compare generic and self-similar voices within each participant, in two counterbalanced blocks. Both voices provide gaze-contingent warmer/colder guidance. Voice preparation uses cloud services.

**Protocol proposal — session flow:**

1. Consent, background questions and baseline symptom check.
2. Seated setup, headset fitting, eye-tracking calibration/validation and fixed-front reference.
3. Task explanation, separate voice recording/preparation, and playback checks.
4. Practice until ready; the final practice criterion remains open.
5. First voice block. Each trial: **return to fixed front → hear target → reveal objects → rotate/search with warmer/colder hints → gaze-select target**.
6. Block-specific questionnaires and headset break. Proposed battery: NASA-TLX, IMI, selected Guo agent-perception measures, voice similarity/ratings and technical checks. Final item selection/order remains open.
7. Re-don headset, revalidate tracking and alignment to the same front, then complete the second voice block and its questionnaires.
8. Post-exposure symptoms, voice preference, final interview and debrief. Verify files and survey joins.

**Implemented:** the beta has eight planes at 45-degree intervals, seven objects per plane (56 total), two practice trials and 14 measured trials in two voice blocks. It loads eight starter clips before preparing remaining audio in the background.

**Implementation gaps:** the beta does not yet enforce the planned fixed-front reset; the local script pilot now applies one selected wording mode to both voices, but the final study script still needs pilot selection. See [voice script modes](voice-script-pilot.md). Gaze/contact, movement and delivered-audio measures need the validation tracked in the project issues. The beta's trial count is not the finalized research schedule.

**Open decisions:** primary outcome; final trial angles/repetitions and count; sample justification (15–20 participants was discussed, not fixed); shared voice wording; questionnaire selection/adaptations; practice and calibration criteria; timeout, breaks and total session duration.

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
