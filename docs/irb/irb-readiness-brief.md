# IRB-readiness brief: Gaze-Contingent AI Assistance and Self-Similar Voice

Version: first-pass draft, 2026-08-11

## Bottom line

The planned work likely constitutes non-exempt human-subjects research rather than a course-only exercise or internal pilot. It is designed to develop generalizable knowledge, enroll living adults in a laboratory, manipulate assistance and voice conditions, and obtain information through interaction, eye-gaze telemetry, questionnaires, and identifiable voice recordings. Calling a session a “pilot” does not by itself remove it from the federal definition. UCF’s IRB, not the research team or this brief, must make the official determination and decide whether the submission is exempt, expedited, or requires convened-board review.

The conservative project-specific assumption is: prepare a Huron IRB study using HRP-503 and HRP-502, disclose the eye-tracking and voice-cloning data paths, and do not conduct pilot data collection with people until UCF confirms the determination and approval status. This is research-readiness guidance, not legal advice or an institutional ruling.

Authoritative grounding: [45 CFR 46.102](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.102), [45 CFR 46.111](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.111), [45 CFR 46.116](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.116), [45 CFR 46.117](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.117), [UCF IRB](https://www.research.ucf.edu/compliance/irb/), [UCF investigator responsibilities](https://www.research.ucf.edu/for-researchers/compliance/irb/investigators/irb-investigators-responsibilities/), and [UCF Study Application Instructions](https://www.research.ucf.edu/wp-content/uploads/sites/56/2026/02/IRB-Guidance-07-Study-Application-Instructions.pdf).

## Readiness verdict

**Documents:** A credible first-pass packet is now present, but every visible **OPEN DECISION** must be resolved and the UCF PI must review it before upload.

**Runtime:** Not ready for approved participant collection. The current build implements 14 deterministic gaze-aware/unaware rounds with a fixed generic ElevenLabs voice. The intended 2 × 2 voice experiment is not implemented.

**Voice cloning:** Feasible only after UCF and institutional security/privacy review approve the vendor path. The proposed workflow uses ElevenLabs Instant Voice Cloning (IVC), with a clear 1–2 minute participant recording and explicit authorization. A protected staff workstation—not the headset—will upload the recording, generate the fixed prompt library, and perform cleanup; the headset release build must contain no provider credential and make no provider API call. ElevenLabs states that IVC requires confirmation of the right and consent to clone the voice. Professional Voice Cloning is not proposed because ElevenLabs limits it to cloning one’s own voice on the voice owner’s account. See [IVC documentation](https://elevenlabs.io/docs/eleven-creative/voices/voice-cloning/instant-voice-cloning), [voice-cloning concepts](https://elevenlabs.io/docs/eleven-api/concepts/voice-cloning), and the [privacy policy](https://elevenlabs.io/privacy-policy).

## Reconciliation of protocol and implementation

| Area | Current implementation | What the application must say / readiness gate |
|---|---|---|
| Experimental design | 14 fixed rounds; odd rounds gaze-unaware and even rounds gaze-aware; participant number does not alter order. | **Protocol proposal:** four balanced conditions: gaze-contingent/unaware × generic/self-similar voice. Use sequential study IDs mapped to a frozen balanced schedule; use distinct deterministic layouts within a session; disclose the final sequence and block/trial counts. |
| Voice | Fixed “Rachel” voice ID and generic “Ava” introduction. The headset currently calls ElevenLabs directly and loads a packaged API key. No microphone capture, cloning, participant voice ID, or deletion workflow. | Disclose that cloning is proposed, not implemented. Use an approved encrypted staff workstation for recording, IVC creation, fixed-prompt generation, and deletion. The headset receives prepared clips only and contains no provider credential/API path. Validate the end-to-end workflow before submission or amend before use. |
| Hint exposure | Gaze-aware hints begin earlier and repeat more often than gaze-unaware hints. | Match opportunities, timing, count, duration range, and content class. Define opportunity versus completed exposure, log both, and make live gaze the policy input that differs. Do not describe the current conditions as matched. |
| Telemetry | Per-render-frame gaze pose/hover CSV plus event/summary logs. No sample-level tracking-validity or calibration-status field; no native device timestamp/rate. | Add calibration result, validity/availability, dropped-sample or provider status, display/search onset, hint requested/played/failed, voice condition/voice ID alias, and block/trial schedule events. Disclose actual sampling basis and missingness. |
| Eye metrics | XRI hover episodes are logged as “fixations”; angular transitions are logged as “saccades.” Blink classification uses custom thresholds. | Use “gaze-hover/dwell proxy” and “angular transition proxy” unless a validated algorithm is added. Pre-specify thresholds and do not claim clinical or diagnostic eye measures. |
| Timing | Later `time_to_find` values begin before transition/announcement completes. | Add explicit search-display onset and calculate search time from that event. Preserve raw timestamps and document the derived metric. |
| Questionnaires | The app prompts NASA-TLX at the end of a run; some docs say after each block. | Choose one schedule and make the app, protocol, instruments, and analysis plan agree. This draft proposes raw NASA-TLX after each block. |
| Data-quality exclusions | No final calibration/missingness thresholds or auditable disposition log. | Pre-specify exclusions before analysis; retain reason codes; report enrolled, completed, excluded, and analyzed counts. Never condition compensation on analyzable gaze data. |

## Submission checklist

### Protocol narrative

- [ ] Confirm exact study title, PI of record, department, phone, email, faculty advisor, lab/room, funding, conflicts-of-interest answer, and anticipated dates.
- [ ] Complete a power or precision analysis and set enrolled/analyzable sample sizes.
- [ ] Freeze the primary hypothesis, primary outcome, confirmatory contrasts, alpha/multiplicity plan, and missing-data plan.
- [ ] Decide whether the gaze main effect and gaze × voice interaction are both primary; if the available sample cannot support confirmatory inference, label condition effects exploratory and frame the study as feasibility-focused.
- [ ] Finalize 2 × 2 block/trial counts, counterbalancing, rest breaks, practice trials, and maximum session duration.
- [ ] Reconcile every **Protocol proposal** with a tested release build; archive the application version, commit, build identifier, and configuration.
- [ ] Describe the full session in order: consent, screening, voice recording, staff-workstation prompt generation, Room Setup/plane verification, headset fit, calibration, practice, blocks, questionnaires, breaks, debrief, compensation if offered, and cleanup.

### Consent and voice authorization

- [ ] Use the current UCF HRP-502 template and plain language.
- [ ] State that participation is voluntary, refusal has no penalty, and withdrawal is allowed without loss of earned compensation.
- [ ] Explicitly disclose voice recording, creation of a synthetic voice that can say words the participant never recorded, third-party upload, foreseeable impersonation/privacy risk, access controls, deletion limits, and whom to contact.
- [ ] Confirm whether voice cloning is required for participation. This draft treats it as required because it is a study factor.
- [ ] Confirm whether UCF permits a separate voice-authorization initial/signature line and whether documentation of consent may be electronic.
- [ ] Ensure consent, protocol, Huron smart-form responses, recruitment, and vendor facts are identical.

### Recruitment and eligibility

- [ ] Use only IRB-approved email/flyer/SONA/social copy and channels.
- [ ] Do not imply guaranteed benefit or say the system diagnoses eye/health conditions.
- [ ] State the lab visit, mixed-reality headset, eye tracking, voice recording/cloning, duration, compensation, and basic eligibility in recruitment.
- [ ] Use a neutral screening process; store contact/screening information separately from research data.
- [ ] Confirm inclusion/exclusion criteria are scientifically necessary and do not unfairly exclude protected groups.

### Risks and mitigations

- [ ] MR discomfort: seated setup where feasible, clear play area, researcher spotter, breaks on request, immediate stop/removal, symptom check, and post-session observation as needed.
- [ ] Visual/neurological risk: exclude or obtain medical guidance for relevant seizure/photosensitivity history; warn about eyestrain, headache, dizziness, nausea, disorientation, and fatigue.
- [ ] Physical risk: clean/sanitize contact surfaces, manage cables/obstacles, adjust headset fit, and avoid participant movement outside the marked area.
- [ ] Voice/privacy risk: private recording room, no names in the script/file, participant-scoped study ID, encrypted transfer/storage, least-privilege access, no Voice Library sharing, no reuse, and deletion verification.
- [ ] Provide UCF an explicit minimal-risk comparison covering consumer-headset symptoms, identifiable voice upload, synthetic impersonation, and residual vendor retention; UCF determines the risk level.
- [ ] Psychological risk: warn that hearing a synthetic self-similar voice may feel uncanny or uncomfortable; allow immediate pause/withdrawal.
- [ ] AI error: describe that hints and synthetic speech may be delayed, wrong, or unnatural and are not professional advice.

### Privacy, eye tracking, webcam/video, and audio

- [ ] Confirm that headset passthrough cameras are used for live rendering but no camera frames, room video, webcam video, or face video are saved or transmitted. If implementation changes, submit a modification first.
- [ ] State that eye-gaze rays, eye openness, provider values, object hover/dwell, timestamps, trial events, and performance are recorded; raw eye camera images are not.
- [ ] Treat source voice audio, voice data/model, and self-similar synthesized audio as identifiable even when labeled with a participant code.
- [ ] Keep the identity/contact key separate from telemetry and voice assets; define exactly who can access each location.
- [ ] Do not put participant names, emails, or free-text identifiers in filenames, ElevenLabs voice names, CSVs, or Unity logs.
- [ ] Remove all API keys/provider calls from the headset release build; keep credentials only in the approved staff-controlled prompt-generation workflow.

### Vendor and voice-clone controls

- [ ] Obtain UCF approval for ElevenLabs or select an institution-approved alternative; document contracting/DPA/security review as applicable.
- [ ] Use IVC, not PVC, unless the provider and UCF approve a different participant-owned-account workflow.
- [ ] Disable “Improve the models for everyone” before any participant upload and record evidence of that setting.
- [ ] Create a unique, non-identifying voice name; never publish or share it to a voice library.
- [ ] Test deletion of source samples, voice object, generated clips/history, local recording, device TTS cache, and any backups; document what deletion cannot guarantee.
- [ ] Treat a failed identifiable-voice deletion as an immediate privacy incident: disable/revoke access, escalate to the PI/provider, assess UCF reporting, and pause voice collection while exposure remains unresolved.
- [ ] Record the provider, product, model/version, account tier, region if known, date/time created, voice alias, deletion request/result, and operator—without storing the provider API key in study logs.
- [ ] Compare an institution-hosted/on-device voice path with cloud IVC and document why the selected path is necessary and proportionate to the scientific voice-interaction aim.
- [ ] Freeze and record provider model/settings, normalize paired clips for wording/loudness/duration/intelligibility, and pause on provider/version or quality drift.
- [ ] Obtain UCF confirmation of the retention statement. ElevenLabs states it may use some submitted data to improve models unless opted out and may retain generated voice data for up to three years after the last interaction, subject to deletion requests and policy exceptions.

### Compensation

- [ ] Decide amount/form, funding source, distribution method, timing, prorating, no-show policy, withdrawal payment, and whether identifying payment records are required.
- [ ] State that payment does not depend on performance, correct trials, usable eye tracking, or completion if the participant stops for safety.
- [ ] Offer an equivalent alternative assignment if course credit is used.

### De-identification, retention, and analysis

- [ ] Replace names with study IDs in research files; document that coded data are not anonymous and that voice assets remain potentially identifying.
- [ ] Finalize retention periods for consent/payment records, linkage key, raw voice recording, provider clone, synthesized audio/cache, gaze telemetry, questionnaires, and analysis outputs.
- [ ] Store study data only in UCF-approved encrypted storage; do not rely on the headset as the archival location.
- [ ] Verify transfer, checksum, backup, access logging, and secure deletion procedures.
- [ ] Pre-register or timestamp the analysis/exclusion plan before looking at condition effects.
- [ ] Distinguish pre-assignment calibration ineligibility from post-assignment tracking failure; retain assignment-policy observations where possible and pre-specify missing-data/per-protocol sensitivity analyses.
- [ ] Retain coded raw task/telemetry/questionnaire data and immutable processing code so derived dwell/search measures are reproducible; do not let this general retention rule override the approved early-deletion schedule for identifiable voice assets.

### Adverse events, deviations, modifications, and records

- [ ] Train staff on stop criteria, first aid/emergency contacts, privacy incidents, and UCF reportable-event requirements.
- [ ] Record symptoms, device incidents, protocol deviations, unanticipated problems, confidentiality breaches, and corrective actions without diagnosis.
- [ ] Notify the PI promptly and report to UCF according to the current event-reporting policy and Huron workflow.
- [ ] Do not implement changes—including voice provider/model, recording duration, new telemetry, camera recording, eligibility, compensation, recruitment, or trial schedule—before UCF approves a modification, except when necessary to eliminate an immediate hazard.
- [ ] Maintain current protocol/consent/materials, approvals, modifications, delegation/training records, consent records, screening/enrollment logs, session/build/configuration logs, compensation records, data-transfer/deletion logs, deviations/events, analysis code, and closure records for the UCF-required period.

## What to do next

1. The UCF PI and faculty advisor should review this brief and the draft documents, resolve the open decisions, and confirm PI eligibility in Huron.
2. Complete CITI Human Subjects Research training. UCF’s FAQ directs human-research personnel to the applicable Group 1 Biomedical or Group 2 Social/Behavioral course; the PI/IRB should confirm which group fits this protocol. [UCF CITI instructions](https://rcr.research.ucf.edu/citi/)
3. Ask UCF IRB whether the study should be submitted as expedited/non-exempt and whether the proposed third-party IVC workflow and early deletion of identifiable voice assets are acceptable. Contact [UCF IRB](https://www.research.ucf.edu/compliance/irb/contact/) at irb@ucf.edu or 407-823-2901.
4. Implement and test the protocol gates listed above using synthetic/developer data only; do not record pilot participants before approval.
5. Remove all draft/open-decision labels, comments, instruction text, and tracked changes; run HRP-259; upload the final Word documents and every participant-facing instrument to the Huron study.
