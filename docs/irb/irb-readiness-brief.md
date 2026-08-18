# IRB-readiness brief: Follow My Voice—Gaze-Contingent XR Search with a Self-Similar Agent

Version: decision-frozen draft, 2026-08-14

## Bottom line

The planned work likely constitutes non-exempt human-subjects research rather than a course-only exercise or internal pilot. It is designed to develop generalizable knowledge, enroll living adults in a laboratory, manipulate assistance and voice conditions, and obtain information through interaction, eye-gaze telemetry, questionnaires, and identifiable voice recordings. Calling a session a “pilot” does not by itself remove it from the federal definition. UCF’s IRB, not the research team or this brief, must make the official determination and decide whether the submission is exempt, expedited, or requires convened-board review.

The conservative project-specific assumption is: prepare a Huron IRB study using HRP-503 and HRP-502, disclose the eye-tracking and voice-cloning data paths, and do not conduct pilot data collection with people until UCF confirms the determination and approval status. This is research-readiness guidance, not legal advice or an institutional ruling.

Authoritative grounding: [45 CFR 46.102](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.102), [45 CFR 46.111](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.111), [45 CFR 46.116](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.116), [45 CFR 46.117](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-A/part-46/subpart-A/section-46.117), [UCF IRB](https://www.research.ucf.edu/compliance/irb/), [UCF investigator responsibilities](https://www.research.ucf.edu/compliance/irb/investigators/irb-investigators-responsibilities/), and [UCF Study Application Instructions](https://www.research.ucf.edu/wp-content/uploads/sites/56/2026/02/IRB-Guidance-07-Study-Application-Instructions.pdf).

## Readiness verdict

**Documents:** The scientific and procedural decisions are frozen in [`decision-register.md`](decision-register.md). The packet still requires official contacts, lab/room, dates, gift-card funding/distribution details, institutional confirmations, PI review, and a protocol-concordant tested build before upload.

**Runtime:** Not ready for approved participant collection. The paper-aligned protocol proposes 48 complete datasets, four condition blocks with a standard schedule of 16 trials each (up to 64 experimental trials), eight vertical search planes around a seated participant, a four-sequence Williams design, participant-derived voice playback, matched hint opportunities, and two-step coarse/fine gaze guidance. The current build instead implements 14 deterministic front-facing bookshelf rounds with alternating gaze-aware/unaware hinting and a fixed generic ElevenLabs voice.

**Voice cloning:** The selected lower-risk path is OpenVoice V2 running offline on a UCF-managed encrypted workstation, pinned to an institutionally reviewed release/commit. No participant audio, speaker embedding, prompt text, or generated clip is sent to an outside service. Both voice conditions use the same prompt manifest/base TTS; the self-similar condition applies local tone-color conversion. Source audio, embeddings, generated clips, and device/session caches are deleted within 24 hours. UCF/institutional review and nonparticipant feasibility/deletion testing remain mandatory. See the [official OpenVoice repository and MIT license](https://github.com/myshell-ai/OpenVoice) and [OpenVoice paper](https://doi.org/10.48550/arXiv.2312.01479).

## Reconciliation of protocol and implementation

| Area | Current implementation | What the application must say / readiness gate |
|---|---|---|
| Experimental design | 14 fixed front-facing bookshelf rounds; odd rounds gaze-unaware and even rounds gaze-aware; participant number does not alter order. | Four condition blocks, normally 16 trials each and no more than 64 total; enroll up to 60 for 48 complete datasets; use Williams sequences A-B-D-C, B-C-A-D, C-D-B-A, and D-A-C-B with 12 complete datasets per sequence. Prespecify and log early stops. |
| Search environment | Front-facing virtual bookshelf. | Seat the participant at a marked origin in a swivel chair. Render 56 objects across eight vertical planes at 45-degree intervals, seven objects per plane. In each full 16-trial condition manifest, schedule the target exactly twice on every plane and validate the full geometry before the session. |
| Voice | Fixed “Rachel” voice ID and generic “Ava” introduction. The headset currently calls ElevenLabs directly and loads a packaged API key. No microphone capture, local conversion, participant voice ID, or deletion workflow. | Remove the cloud/provider path from the release build. Run pinned OpenVoice V2 offline on the approved workstation, generate the fixed paired library, copy clips only, and verify 24-hour deletion. |
| Hint exposure | Gaze-aware hints begin earlier and repeat more often than gaze-unaware hints. | Use matched planned opportunities at 4, 10, 16, 22, 28, 34, and 40 seconds after `search_onset`, with a 45-second timeout. In gaze-contingent blocks, use coarse spatialized target-plane guidance followed by discrete proximity feedback; in noncontingent blocks, play duration-matched general prompts without gaze, hover, coverage, or target-proximity input. Log opportunities, abstentions, and completed exposures separately. |
| Telemetry | Per-render-frame gaze pose/hover CSV plus event/summary logs. No sample-level tracking-validity or calibration-status field; no native device timestamp/rate. | Add calibration result, validity/availability, dropped-sample or provider status, display/search onset, hint requested/played/failed, voice condition/voice ID alias, and block/trial schedule events. Disclose actual sampling basis and missingness. |
| Eye metrics | XRI hover episodes are logged as “fixations”; angular transitions are logged as “saccades.” Blink classification uses custom thresholds. | Use “gaze-hover/dwell proxy” and “angular transition proxy” unless a validated algorithm is added. Pre-specify thresholds and do not claim clinical or diagnostic eye measures. |
| Timing | Later `time_to_find` values begin before transition/announcement completes. | Add explicit search-display onset and calculate search time from that event. Preserve raw timestamps and document the derived metric. |
| Questionnaires | The app prompts NASA-TLX at the end of a run; some docs say after each block. | Administer Raw NASA-TLX after each block, nine fixed study-created ratings after every block, two mechanism items after gaze blocks, and the complete 16-item SSQ pre/post. Do not use SUS/UEQ. |
| Data-quality exclusions | No final calibration/missingness thresholds or auditable disposition log. | Pre-specify exclusions before analysis; retain reason codes; report enrolled, completed, excluded, and analyzed counts. Never condition compensation on analyzable gaze data. |

## Submission checklist

### Protocol narrative

- [ ] Confirm exact study title, PI of record, department, phone, email, faculty advisor, lab/room, funding, conflicts-of-interest answer, and anticipated dates.
- [ ] Complete the preregistration simulation for the frozen target of 48 complete datasets and ceiling of 60; document the paired sensitivity benchmark of approximately dz=.46 at two-sided alpha .025 and 80% power.
- [ ] Freeze the primary hypothesis, primary outcome, confirmatory contrasts, alpha/multiplicity plan, and missing-data plan.
- [ ] Confirm the paper's hierarchy: guidance main effect on search time as the primary test and guidance × voice interaction as the secondary focal test, with Holm correction across both.
- [ ] Freeze the paper-aligned design: four condition blocks with a standard 16-trial manifest and up to 64 trials total, four Williams sequences, target scheduled exactly twice on each of eight planes per full condition block, prespecified early-stop reasons, up to eight practice trials with a three-consecutive-correct criterion, breaks between blocks, and a maximum session duration.
- [ ] Reconcile every **Protocol proposal** with a tested release build; archive the application version, commit, build identifier, and configuration.
- [ ] Describe the full session in order: consent, screening, voice recording, staff-workstation prompt generation, marked-origin/eight-plane validation, headset fit, multi-direction calibration/validation, practice, blocks, questionnaires, breaks, debrief, compensation if offered, and cleanup.

### Consent and voice authorization

- [ ] Use the current UCF HRP-502 template and plain language.
- [ ] State that participation is voluntary, refusal has no penalty, and withdrawal is allowed without loss of earned compensation.
- [ ] Explicitly disclose voice recording, offline local synthesis that can say words the participant never recorded, foreseeable impersonation/privacy risk, access controls, 24-hour deletion, and whom to contact.
- [ ] Confirm whether voice cloning is required for participation. This draft treats it as required because it is a study factor.
- [ ] Confirm UCF acceptance of signed paper consent plus the separate voice-authorization initial line; electronic consent and waiver of documentation are not proposed.
- [ ] Ensure consent, protocol, Huron smart-form responses, recruitment, and local voice-processing facts are identical.

### Recruitment and eligibility

- [ ] Use only IRB-approved UCF email, flyer, social-media, and word-of-mouth copy; SONA/course credit is not proposed.
- [ ] Do not imply guaranteed benefit or say the system diagnoses eye/health conditions.
- [ ] State the lab visit, mixed-reality headset, eye tracking, voice recording/cloning, duration, compensation, and basic eligibility in recruitment.
- [ ] Use a neutral screening process; store contact/screening information separately from research data.
- [ ] Confirm inclusion/exclusion criteria are scientifically necessary and do not unfairly exclude protected groups.

### Risks and mitigations

- [ ] MR discomfort: seated swivel-chair setup, marked origin, clear rotational area, researcher spotter, breaks between blocks and on request, immediate stop/removal, symptom check, and post-session observation as needed.
- [ ] Visual/neurological risk: exclude or obtain medical guidance for relevant seizure/photosensitivity history; warn about eyestrain, headache, dizziness, nausea, disorientation, and fatigue.
- [ ] Physical risk: clean/sanitize contact surfaces, manage cables/obstacles, adjust headset fit, screen for concerns about repeated seated rotation, and instruct participants to remain seated at the marked origin.
- [ ] Voice/privacy risk: private recording room, no names in the script/file, participant-scoped study ID, encrypted transfer/storage, least-privilege access, no Voice Library sharing, no reuse, and deletion verification.
- [ ] Provide UCF an explicit minimal-risk comparison covering consumer-headset symptoms, local identifiable voice processing, synthetic impersonation, workstation exposure, and deletion failure; UCF determines the risk level.
- [ ] Psychological risk: warn that hearing a synthetic self-similar voice may feel uncanny or uncomfortable; allow immediate pause/withdrawal.
- [ ] AI error: describe that hints and synthetic speech may be delayed, wrong, or unnatural and are not professional advice.

### Privacy, eye tracking, webcam/video, and audio

- [ ] Confirm that headset passthrough cameras are used for live rendering but no camera frames, room video, webcam video, or face video are saved or transmitted. If implementation changes, submit a modification first.
- [ ] State that eye-gaze rays, eye openness, provider values, object hover/dwell, timestamps, trial events, and performance are recorded; raw eye camera images are not.
- [ ] Treat source voice audio, voice data/model, and self-similar synthesized audio as identifiable even when labeled with a participant code.
- [ ] Keep the identity/contact key separate from telemetry and voice assets; define exactly who can access each location.
- [ ] Do not put participant names, emails, or free-text identifiers in filenames, local embedding aliases, CSVs, or Unity logs.
- [ ] Remove all cloud voice API keys/provider calls from the headset release build and verify the local workstation is offline during voice processing.

### Local voice-conversion controls

- [ ] Obtain UCF/institutional approval for the pinned OpenVoice V2 package, weights, workstation, and offline data path.
- [ ] Verify by network observation that participant processing sends no audio, embeddings, prompt text, or clips externally.
- [ ] Generate only the fixed prompt manifest; use non-identifying local aliases and never retain unrestricted/free-form voice capability for participant reuse.
- [ ] Test deletion of failed takes, source recording, speaker embedding/model artifact, generated clips, workstation cache/backups, and headset copies within 24 hours.
- [ ] Treat failed deletion or unexpected network transfer as an immediate privacy incident: restrict access, notify the PI, assess UCF reporting, and pause voice collection.
- [ ] Record OpenVoice commit/version, model-weight identifier, base TTS, settings, generation date/time, local alias, deletion result, and operator.
- [ ] Normalize paired clips for wording, loudness, duration range, and intelligibility; pause on model/version or quality drift.
- [ ] Do not substitute a cloud service if local generation fails; stop the session and amend the protocol if the local path is infeasible.

### Compensation

- [ ] Confirm funding/distribution details for the selected $15 gift card. Full payment begins once signed consent and screening start; no-show/cancellation before consent receives no payment.
- [ ] State that payment does not depend on performance, correct trials, usable eye tracking, or completion if the participant stops for safety.
- [ ] Offer an equivalent alternative assignment if course credit is used.

### De-identification, retention, and analysis

- [ ] Replace names with study IDs in research files; document that coded data are not anonymous and that voice assets remain potentially identifying.
- [ ] Verify the selected retention schedule: voice artifacts within 24 hours; linkage/contact at 30 days; coded research/regulatory records at least five years after closure.
- [ ] Store study data only in UCF-approved encrypted storage; do not rely on the headset as the archival location.
- [ ] Verify transfer, checksum, backup, access logging, and secure deletion procedures.
- [ ] Pre-register or timestamp the analysis/exclusion plan before looking at condition effects.
- [ ] Distinguish pre-assignment calibration ineligibility from post-assignment tracking failure; retain assignment-policy observations where possible and pre-specify missing-data/per-protocol sensitivity analyses.
- [ ] Retain coded raw task/telemetry/questionnaire data and immutable processing code so derived dwell/search measures are reproducible; do not let this general retention rule override the approved early-deletion schedule for identifiable voice assets.

### Adverse events, deviations, modifications, and records

- [ ] Train staff on stop criteria, first aid/emergency contacts, privacy incidents, and UCF reportable-event requirements.
- [ ] Record symptoms, device incidents, protocol deviations, unanticipated problems, confidentiality breaches, and corrective actions without diagnosis.
- [ ] Notify the PI promptly and report to UCF according to the current event-reporting policy and Huron workflow.
- [ ] Do not implement changes—including the voice model/release or addition of an external provider, recording duration, new telemetry, camera recording, eligibility, compensation, recruitment, or trial schedule—before UCF approves a modification, except when necessary to eliminate an immediate hazard.
- [ ] Maintain current protocol/consent/materials, approvals, modifications, delegation/training records, consent records, screening/enrollment logs, session/build/configuration logs, compensation records, data-transfer/deletion logs, deviations/events, analysis code, and closure records for the UCF-required period.

## What to do next

1. Dr. Roshan Venkatakrishnan should review the decision register and draft documents, confirm PI eligibility in Huron, and complete the administrative contact/location/date/funding fields.
2. Complete CITI Human Subjects Research training. UCF’s FAQ directs human-research personnel to the applicable Group 1 Biomedical or Group 2 Social/Behavioral course; the PI/IRB should confirm which group fits this protocol. [UCF CITI instructions](https://rcr.research.ucf.edu/citi/)
3. Ask UCF IRB whether the study should be submitted as expedited/non-exempt and whether the offline OpenVoice workflow, signed consent/voice authorization, and early deletion schedule are acceptable. Contact [UCF IRB](https://www.research.ucf.edu/compliance/irb/contact/) at irb@ucf.edu or 407-823-2901.
4. Implement and test the protocol gates listed above using synthetic/developer data only; do not record pilot participants before approval.
5. Remove all draft/open-decision labels, comments, instruction text, and tracked changes; run HRP-259; upload the final Word documents and every participant-facing instrument to the Huron study.
