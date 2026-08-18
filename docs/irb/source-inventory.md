# Lab IRB Source Inventory

The source files are examples from a different UCF VR motor-control study. Their
approval status, version status, and applicability to this project have not been
assumed.

| Source file | Contents | Use for this project |
|---|---|---|
| `HRP-502_Motor_Control_and_Timeflow_Feedback.docx` | Participant information/consent language | Structural reference only; replace all study-specific content |
| `HRP-503_Motor_Control_and_Timeflow_Feedback.docx` | Full protocol/application content | Structural reference only |
| `Study 9491 HRP-503_Motor_Control_and_Timeflow_Feedback IRB edits.docx` | Edited protocol version | Reference for review expectations; do not treat edits as approval for this study |
| `VR_Study_Recruitment_Email_Simple.pdf` | Recruitment email | Adapted to MR search, eye tracking, seated swivel-chair rotation, and a $15 gift card |
| `demographics.pdf` | Age, gender, education, handedness, vision | Reduced to variables needed for eligibility, description, or analysis |
| `experience.pdf` | VR, gaming, sports, and ping-pong experience | Retain XR/gaming; remove sport and ping-pong items |
| `experience_3.pdf` | Pre-task confidence and controller comfort | Replace with headset, gaze-dwell, and spoken-guidance familiarity |
| `experince_2.pdf` | Hand-eye coordination and ping-pong skill | Not applicable |
| `health_safety.pdf` | VR safety screening | Adapted for MR headset use, vision, hearing, color discrimination, and eye tracking |
| `Simulator Sickness Questionnaire - Google Forms.pdf` | 16 SSQ symptoms, 0-3 | Retain as pre/post safety measure |
| `Copy of Simulator Sickness Questionnaire - Google Forms.pdf` | Duplicate 16-item SSQ export | Duplicate retained only for provenance |
| `nasa_tlx.pdf` | Six workload subscales | Retain; align with current 0-100 Unity implementation |
| `System Usability Scale.pdf` | Ten SUS items | Not selected; overall usability is not a focal construct |
| `User Experience Questionnaire.pdf` | Full 26-item UEQ | Not selected; too broad for the confirmatory questions |
| `User Experience Questionnaire - Short.pdf` | Eight-item UEQ-S | Not selected; avoids redundant nonfocal usability burden |
| `Game Experience Questionnaire - Google Forms.pdf` | 42 GEQ items | Not selected; story and game-experience domains do not fit the task |
| `Presence Questionnaire - Google Forms.pdf` | 32 presence items | Not selected; presence is not a current primary construct |
| `VEQ-Questionnaire.pdf` | Body ownership, agency, and body-change items | Not applicable; the study does not manipulate an avatar body |
| `performance_1.pdf` | Perceived pre/training/post improvement | Not applicable to the current round design |
| `performance_2.pdf` | Control, physics, presence, demand, frustration | Mostly motor-task specific; workload is covered by NASA-TLX |
| `performance_3.pdf` | Training-speed manipulation check | Not applicable |
| `performance_4.pdf` | Perceived phase duration | Not applicable |
| `performance_5.pdf` | Effects of noticed timing changes | Not applicable |
| `performance_6.pdf` | Short discomfort checklist | Redundant with SSQ; SSQ selected |
| `interview.pdf` | Strategy, ease, difficulty, unusual behavior | Adapted to search strategy, hint use, trust, distraction, and privacy |

## Proposed administration schedule

| Time | Materials | Purpose |
|---|---|---|
| Before consent | Minimal eligibility prescreen | Avoid collecting unnecessary research data from ineligible volunteers |
| After consent | Demographics, XR/eye-tracking experience, baseline SSQ | Describe sample, record covariates, establish symptom baseline |
| After each of four condition blocks (normally 16 trials; may end earlier under prespecified rules) | Raw NASA-TLX and guidance/voice mechanism checks | Compare workload, perceived gaze responsiveness, directional/proximity utility, voice similarity, and experience by condition |
| End of session | Post-SSQ, comparative guidance/privacy items, interview | Safety change, interpretation, and qualitative context |

The paper-aligned protocol uses four condition blocks ordered by one of four
Williams sequences (A-B-D-C, B-C-A-D, C-D-B-A, or D-A-C-B). The current
round-by-round alternating runtime is not protocol-concordant; condition-specific
NASA-TLX and manipulation checks should not be used for research collection until
the block schedule and condition logging are implemented and verified.
