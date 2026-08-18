# IRB Protocol Decision Register

Study: **Follow My Voice: Gaze-Contingent XR Search with a Self-Similar Agent**  
Decision set: **Protocol design v2, 2026-08-14**

These are **current protocol decisions** selected for drafting, implementation, and
formative validation. They are not UCF IRB approvals. Administrative facts and
institutional determinations that the research team cannot invent are listed
separately at the end.

## Study leadership and scope

- Dr. Roshan Venkatakrishnan is the proposed UCF Principal Investigator of record;
  Pedro Poveda is the student investigator. UCF must confirm eligibility and the
  final Huron roles.
- One in-person UCF laboratory visit lasts no more than **75 minutes**.
- The standard schedule contains four condition blocks with up to 16 experimental
  trials per block (**up to 64 experimental trials total**). Sixteen trials per
  block is the planning target, not a promise that every participant will complete
  all 64. The researcher may end a block or session early for the 75-minute limit,
  safety, withdrawal, or a documented technical failure. Trial count must never be
  changed in response to a participant's performance or emerging condition results.
- The study enrolls adults age 18 or older who can consent and follow the
  standardized English voice/task script.
- The study does not automatically exclude pregnancy or age above 65. Exclusions
  are limited to scientifically necessary consent, display-safety, seated-rotation,
  hearing/audio, color-discrimination, voice, and eye-calibration criteria.

## Enrollment, allocation, and compensation

- Enroll **up to 60 people** to obtain **48 complete datasets**, with 12 complete
  datasets in each Williams sequence. A complete dataset contains at least 12
  technically valid trials in every factorial cell.
- Use the four Williams sequences A-B-D-C, B-C-A-D, C-D-B-A, and D-A-C-B. A
  pre-generated schedule contains 15 assignments per sequence; enrollment stops
  when 48 complete datasets with 12 per sequence are obtained or 60 people have
  enrolled.
- Compensation is a **$15 UCF-approved gift card**. Anyone who signs consent and
  begins the in-person screening receives the full $15 even if they withdraw, are
  found ineligible, fail calibration/practice, or the researcher stops the session.
  No-show/cancellation before consent receives no payment. Payment never depends on
  performance, voice quality, eye-tracking quality, or analyzability.

## Consent and recruitment

- Use written paper consent with signatures and a separate participant-initial line
  authorizing the study-limited synthetic voice. Do not request a waiver of signed
  documentation and do not use electronic consent in the initial submission.
- Recruitment channels are UCF email, approved physical/digital flyers, approved
  social-media posts, and word-of-mouth referral using the approved script. Do not
  use SONA/course credit in the initial protocol.
- English-only participation is justified by the standardized English speech
  recording, fixed English audio library, and need to hold speech content constant.
- The study uses neutral hypothesis withholding, not deception. HRP-509 is not
  proposed unless UCF classifies the neutral description as incomplete disclosure.

## Voice workflow

- Use **OpenVoice V2**, pinned to the institutionally reviewed release/commit, on an
  offline UCF-managed encrypted workstation. No participant recording, speaker
  embedding, prompt text, or generated clip is sent to a third-party service.
- Generate both voice conditions from the same fixed prompt manifest and base TTS
  settings. The generic condition uses the fixed neutral base voice; the
  self-similar condition applies the participant-derived tone-color conversion.
  Normalize paired clips for wording, loudness, intelligibility, and timing.
- Allow at most two recording takes and one regeneration of the fixed prompt
  library. If an intelligible complete self-similar library cannot be produced,
  stop before randomized blocks and pay the participant $15. Do not switch to a
  cloud service or generic-only study without a UCF-approved modification.
- Voice-similarity ratings are manipulation checks, not eligibility or exclusion
  criteria.

## Voice and research-data retention

- Delete failed voice takes immediately. Delete the usable source recording,
  participant embedding/model artifact, generated self-similar clips, generic/self-
  similar session cache, and headset copies within **24 hours after the session**,
  after the session record and coded research files have been verified. Record the
  deletion result.
- Store coded research data in a restricted UCF OneDrive/SharePoint research folder
  controlled by the PI. Store consent, payment/contact records, and the encrypted
  identity key separately from telemetry/questionnaires.
- Delete the identity key and scheduling/contact information **30 days after the
  session**, after payment and any timely withdrawal request are resolved. A separate
  summary opt-in list contains email only, is not linked to study ID, and is deleted
  after the summary is sent.
- Retain coded task/telemetry/questionnaire data, consent/payment records, protocol
  records, audit logs, and analysis code for at least **five years after study
  closure**, or longer if UCF requires it.
- Public sharing is limited to aggregate results, analysis code, and a disclosure-
  reviewed derived dataset. Raw voice, speaker embeddings, generated self-similar
  audio, the identity key, contact/payment records, and precise raw gaze streams are
  never public.

## Calibration, practice, and technical quality

- Run manufacturer calibration followed by a nine-direction validation (forward
  plus the eight plane centers). The formative acceptance rule is at least 8 of 9
  targets detected, median angular error at most 3 degrees, and 90th-percentile
  angular error at most 6 degrees. Validate these limits with nonparticipant testing
  on the Focus Vision before freezing the confirmatory protocol.
- Permit two calibration retries after refitting. If validation still fails, stop
  the session and pay $15. Recalibrate between trials after a material headset shift
  or less than 70% valid gaze availability over the preceding 60 seconds; permit one
  mid-session refit/recalibration before a technical stop.
- Practice requires three consecutive correct independent selections within eight
  trials. Failure ends the session with full compensation.
- A complete dataset requires at least 12 technically valid trials in each of the
  four cells. For gaze-derived secondary measures, a trial requires at least 70%
  valid gaze samples; a complete participant requires this threshold in at least 12
  trials per cell. Observable assignment-policy outcomes remain in the primary
  analysis where possible. Do not remove statistical outliers solely because they
  are slow or inaccurate.
- Trial manifests schedule 16 trials per condition and place the target twice on
  each of the eight planes. If a block ends early, retain the completed trials and
  document the reason; do not add unscheduled replacement trials. Freeze the
  standard schedule, stopping rules, and any pre-enrollment pilot adjustment before
  confirmatory enrollment. A later systematic change requires PI review and an IRB
  modification or determination when applicable.

## Trial and hint policy

- Each trial times out at **45 seconds**. Hint opportunities occur at 4, 10, 16, 22,
  28, 34, and 40 seconds, for a maximum of seven.
- The target plane is attended after valid gaze remains within +/-22.5 degrees of
  its center for at least 300 ms. Fine-stage proximity uses a 45-degree operating
  range. "Very close" means target angular half-size plus 2 degrees. A change of at
  least 5 degrees since the preceding valid opportunity produces "warmer" or
  "colder"; smaller changes produce "about the same."
- Fine-stage evidence requires at least 80% valid gaze samples in the preceding
  500 ms. Otherwise the policy logs an abstention and plays a duration-matched
  neutral fallback so audio exposure remains matched. Fine stage returns to coarse
  only after the target plane is outside the attended sector at two consecutive
  opportunities.
- The guidance-response window begins at prompt offset and ends after 3 seconds, at
  selection, or at the next hint opportunity, whichever occurs first.
- Provide a minimum two-minute seated break after blocks 1-3 and longer on request.

## Measures and analysis

- Administer the six **Raw NASA-TLX** subscales after every block on 0-100 scales,
  without pairwise weighting.
- Administer nine fixed study-created post-block ratings: gaze responsiveness,
  helpfulness, distraction, voice similarity, familiarity, trust, comfort,
  uncanniness, and reliance. After gaze-contingent blocks only, add separate coarse-
  direction and proximity-helpfulness items. Analyze items separately; do not claim
  a validated composite.
- Do not include SUS or UEQ. General usability is not a focal construct and the
  added burden is not justified.
- Administer the complete 16-item Simulator Sickness Questionnaire before headset
  use and after headset removal, using the standard 0-3 response anchors and
  canonical scoring. Use only a brief safety check between blocks.
- Use age bands rather than exact age: 18-24, 25-34, 35-44, 45-54, 55-64, 65+, or
  prefer not to answer.
- The primary confirmatory outcome is right-censored trial search time from
  `search_onset` to correct capture, analyzed with a participant-clustered mixed-
  effects time-to-event model. The two focal tests are the guidance main effect and
  guidance-by-voice interaction; apply Holm correction across them. At 48 complete
  participants, a simple paired sensitivity calculation at two-sided alpha .025 has
  approximately 80% power for a standardized within-participant contrast of about
  0.46; a simulation using pilot variance and censoring remains a preregistration
  gate.
- Wrong captures, timeouts/first-attempt success, head rotation, plane revisits,
  hint response, workload, experience items, symptoms, and validated gaze-derived
  measures are secondary or exploratory as specified in the analysis plan.

## Safety and stopping

- Postpone if the baseline SSQ shows moderate/severe nausea, dizziness, vertigo,
  disorientation, or blurred vision, or if the person reports eye pain, a current
  migraine, or another condition that makes participation unsafe.
- Stop immediately on participant request; any severe symptom; moderate nausea,
  dizziness, vertigo, disorientation, or balance difficulty; eye pain; a concerning
  neurological symptom; inability to remain safely seated; or researcher concern.
- Keep the participant seated without the headset until symptoms are mild and they
  report being ready to leave. Follow UCF emergency procedures for severe,
  worsening, or persistent symptoms; researchers do not diagnose.
- The PI reviews events and deviations monthly and within 24 hours of any serious
  event, confidentiality incident, or failed voice deletion. Pause enrollment for
  any serious/unanticipated related event, voice-data exposure/deletion failure, or
  two severe headset-related symptom events in a rolling ten sessions pending UCF
  consultation.

## Administrative inputs still required

These are facts or institutional determinations, not discretionary protocol choices:

- official PI/student-investigator names, departments, phone numbers, and UCF email;
- UCF building, laboratory, and room;
- gift-card funding source and distribution/accounting mechanism;
- submission date, anticipated calendar dates, and Huron study number;
- final UCF confirmation of PI eligibility, risk/review category, consent wording,
  injury language, required ancillary reviews, and whether HRP-509 is needed;
- final institutional approval of the workstation, OpenVoice package/weights, UCF
  OneDrive/SharePoint locations, and access roles.
