# Study hub

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

Updated **October 20, 2026**. Current: [final-survey decision and handoff reconciliation](protocol/2026-10-20-final-survey-decision.md). **Qualtrics on hold.** Start with the [current implementation audit and outstanding checks](protocol/2026-10-20-alignment-audit.md). For historical context, see the [meeting decisions and task map](meeting-reconciliation-2026-09-30.md) and [September 28 data/guidance requirements](meeting-reconciliation-2026-09-28.md). Older implementation snapshots below are dated evidence, not current runtime verification.

**Current status:** working beta; protocol and survey forms are being finalized. IRB not yet submitted, per the researcher. This page separates current planning from implemented behavior and older documents.

| I want to… | Open |
|---|---|
| Prepare a rehearsal | [Current editable run sheet](protocol/overleaf/experimenter-run-sheet.tex) · [Headset QA](voice-study-qa.md) — locally reconciled and compiled; operator rehearsal and final instrument/consent review remain |
| Edit the formatted meeting run sheet | [Overleaf project](https://www.overleaf.com/project/6a84bc097073254fbca6985b) · [Standalone LaTeX and PDF](protocol/overleaf/README.md) — all 19 live Overleaf files verified against local sources at `f9f49e4` |
| Understand the current experiment | [Protocol at a glance](#current-protocol-at-a-glance) · [Confirmed decisions and implementation gaps](thesis-study-alignment.md) |
| See every survey | [Survey shelf](#survey-shelf) |
| See everything we measure | [Measures register](measures-register.md) — timing, scoring, roles, data sources and missing implementation |
| Find papers and citations | [Reference shelf](#reference-shelf) |
| Know what to do next | [GitHub delivery board](https://github.com/users/PedroPovedaQ/projects/2) · [Epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19) |
| Check what the app actually does | [Eight-plane beta](eight-plane-beta.md) · [Round flow](guide/02-gameplay-round-flow.md) · [Telemetry](guide/03-gaze-agent-and-telemetry.md) |

[Current procedure figure (SVG)](protocol/overleaf/study-procedure-current.svg) · [PDF](protocol/overleaf/study-procedure-current.pdf) · [Runtime/data-flow diagram](diagram.md). These replace the old dwell/table and 60-trial graphics for planning; they are not protocol approval.

## Current protocol at a glance

**Protocol proposal — current researcher direction:** two counterbalanced preferred/self-similar voice blocks, **40 measured trials each** plus one practice trial. The agent knows the target; the participant discovers it through horizontal guidance to the correct wall, then within-wall up/down guidance, and selects with the **right controller ray + trigger**. Target identity stays out of participant speech/UI.

Both voices will use shared participant-directed commands (“turn left,” “look slightly up”), short conversational variants and less silent contextual assistance. Setup will offer **6–8 candidates per chosen gender** and show `Loading...` during normal processing. Correct selection will receive a chime and varied reinforcement from the active voice. Extra cloned-voice pitch/personality remains an **Open decision** to pilot, with voice-identity/style confounds documented.

**Protocol proposal — session flow:** consent/background/SSQ; preferred-voice auditions and natural-voice recording; headset calibration/validation; one practice trial using the first assigned voice; first 40-trial voice block; rest/comfort/tracking check; second 40-trial block; headset removal and final SSQ; final questionnaires separately referring to each voice block; comparative preference and open-first debrief. No questionnaire between blocks.

**Data contract:** one performance/subjective CSV per trial plus separate motion/reconstruction data, joined by stable IDs/timestamps. Preserve raw events, objects and audio/video references. Block ratings repeated in trial rows retain their original scope.

**Implemented locally, hardware acceptance pending:** the app now has 40+40 measured trials, hidden-target shared commands, horizontal/fine/vertical guidance, six [voice choices](voice-choices.md) per gender, right-controller controls and a shared validity-aware gaze stream. [Trial CSV export](trial-performance-csv.md) describes the post-session exporter. See the [current system overview](guide/01-system-overview.md) and [morning evidence/checklist](overnight/2026-10-01-morning-verification.md). Meeting decisions are not proof that all runtime behavior is implemented. Earlier September 22 v2/dwell/14-round and 30+30 planning descriptions are superseded for the current study design.

**Open decisions:** headset audition/acceptance of the implemented six-per-gender catalog, cadence and spatial thresholds, optional prosody/treatment comparability, primary outcomes and power/sensitivity, final-survey burden and retrospective recall, calibration acceptance and hardware verification. Existing tasks and ownership are in the [September 30 task map](meeting-reconciliation-2026-09-30.md#existing-task-ownership).

**Power planning progress, October 1:** a [reproducible sensitivity report](voice-power-sensitivity.md) compares participant counts and 10 versus 40 trials per voice using explicit synthetic variance/correlation/missingness assumptions. It provides analytical benchmarks, simulation uncertainty and bias checks, and recruitment-retention scenarios. Final outcome, meaningful effect, model, sample size and missingness policy remain open; this is not evidence that 40+40 is adequately powered.

The [long procedure](experiment-procedure.md) now separates the current implementation from historical decisions; the [local run sheet](protocol/overleaf/experimenter-run-sheet.tex) is reconciled and compiles. The October 20 Overleaf/local reconciliation covers the current protocol and survey documents. Legacy rendered [IRB sources](irb/source/protocol.md) and submission packets still require a separately reviewed rebuild; these notes do not claim an approved IRB packet.

## Survey shelf

“Included” means selected for study planning, not a completed participant-ready form. Source PDFs retain original wording; archived study PDFs may contain older administration instructions.

| Survey / instrument | Status for our study | Read / download |
|---|---|---|
| **NASA-TLX** — workload | Final survey, separately referencing each block; documented 0–10 adaptation and complete unweighted mean. | [Questionnaire source](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/02_NASA-TLX.pdf) |
| **IMI** — motivation and activity experience | Selected 24-item four-subscale form; final retrospective sets for each block. | [Official complete instrument PDF](../resources/surveys/imi-complete.pdf) · [Our selection/scoring plan](measures-register.md#imi-specification-and-source-transfer) · [Kao reference paper](../resources/surveys/kao-2021-self-similar-avatar-voice.pdf) |
| **Guo et al. Table A1** — agent perception | Selected 21 items: Q1–6, Q13–18, Q20–22, Q37–42; final retrospective sets. Q20–22 analyzed individually. | [All questions and original references](../resources/surveys/guo-2024-survey.md) · [CSV item bank](../resources/surveys/guo-2024-table-a1-items.csv) · [Paper PDF, Table A1 on p. 23](../resources/surveys/guo-2024-collaborating-with-my-doppelganger.pdf) |
| **Voice similarity and individual voice ratings** | Selected 14 custom numeric ratings per referenced block, collected at the end; accent questions removed. | [Source draft](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/03_Assistance_and_Voice_Ratings.pdf) |
| **Final preference / interview** | Included; update legacy comparison questions to two voices. | [Source draft](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/05_Post-Session_Comparison.pdf) |
| **Background / prior experience** | Retained as protocol proposal, once before the task. | [Source draft](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/01_Demographics_and_Prior_Experience.pdf) |
| **SSQ** — simulator sickness | Retained as safety/comfort proposal; baseline and post-exposure. | [Questionnaire source](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/06_Simulator_Sickness_Questionnaire.pdf) |
| **Legacy eeriness differential** | Candidate; not interchangeable with Guo Q20–Q22. Avoid duplication; original anchors need verification. | [Source draft](irb/source/questionnaires.md) · [Existing PDF draft](irb/submission/04_Perceived_Eeriness.pdf) |
| **IPQ presence** | On hold; not reconfirmed for passthrough MR. | [Legacy PDF](irb/submission/07_Presence.pdf) · [Official item list](https://igroup.org/pq/ipq/download.php) |

**Survey burden:** 65 numeric ratings per referenced block, **130 across both final-survey sets**, plus technical checks, optional text and final SSQ/comparison. [Current complete questions](protocol/overleaf/survey-questions-by-block.tex) and [scoring](protocol/overleaf/survey-instruments.tex). Pilot burden and retrospective recall. Legacy PDF forms below are historical, not current collection materials.

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
