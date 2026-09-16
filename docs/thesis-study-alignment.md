# Seated rotating-search thesis: alignment checkpoint

**Measurement planning (2026-09-16):** see the [living measures register](measures-register.md) for all surveys, behavioral metrics, scoring, implementation gaps and open decisions. IMI is now included; final subscales/form are proposed.

Updated 2026-09-16. Planning companion to [the procedure](experiment-procedure.md) and [epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19). This is not a frozen participant protocol or an institutional approval record.

## Settled direction

**Protocol proposal — user-directed study design:** seat the participant at the center and have them rotate to find objects around them. Retain the existing swivel-chair procedure as the working apparatus: chair, torso and head may rotate; walking/standing is not part of the search task.

**Protocol proposal — meeting decision:** compare self-similar and generic voice within participants, counterbalance the two block orders, and keep gaze-contingent guidance in both. Pilot collaborative and external wording, then choose one perspective shared by both voices; perspective is not an additional thesis factor.

**Implemented:** the beta promoted to `v2` at `4198cfa` provides eight planes every 45 degrees, seven objects per plane, a seated origin, gaze dwell selection, two practice trials and 14 measured trials. Eight starter clips load before voice checks; remaining audio loads in the background. Objects stay hidden until the round instruction finishes. The current transition follows the participant's view rather than enforcing forward alignment. That checkpoint used different wording across voices. The local issue #24 implementation now offers collaborative/external script pilot modes, with identical selected wording across both voices and a session lock; see [pilot scripts and assessment](voice-script-pilot.md). The final study perspective is not yet selected.

## Decisions confirmed on 2026-09-16

**Protocol proposal — confirmed user decision:** return to the same fixed front direction before every trial. Keep the room/object coordinate system fixed; do not rotate the scene or redefine front to match the participant. The scheduler balances front-to-target angular displacement, not previous-target-to-current-target displacement. Record actual onset head bearing and alignment quality. The first-contact clock begins at object reveal after the front-alignment step and target instruction; the return-to-front interval is separate from search time. A visible front cue and alignment/readiness gate are implementation gaps; tolerance and dwell/readiness parameters require device testing.

**Protocol proposal — confirmed user decision:** keep only warmer/colder guidance, with the same policy in both voices. Coarse directional guidance and spatialized target-plane cues are outside the current thesis scope. Stale-feedback interruption remains required.

**Open decision:** use 24, 16 or the current seven measured trials per voice for the initial development pilot? A provisional count is a feasibility test, not a power calculation or the final recruitment protocol. Balanced angles and repetitions must be verified rather than inferred from the total count.

**Protocol proposal — confirmed user decision:** use cloud voice processing for the participant study. The current implementation uses ElevenLabs for generic speech and Mistral/Voxtral for cloning/self-similar speech. The researcher reports that the IRB application has **not been submitted**. Replace offline/no-third-party-transfer claims in the draft sources with the actual reviewed data flow before submission. Exact provider retention/deletion controls, account setup and institutional requirements remain to be verified; do not carry over promises of offline processing or universal 24-hour remote deletion without evidence.

## Reconciliation work

| Document area | Mismatch or outstanding change |
|---|---|
| `docs/experiment-procedure.md` | Lead with seated rotating search and two voice blocks. Separate current beta from historical shelf/four-cell text. Apply fixed-front trial alignment and warm/cold-only guidance; reconcile trial counts, practice criterion, timeout and headset-off breaks after the remaining decisions. |
| `docs/guide/02-gameplay-round-flow.md`, `03-gaze-agent-and-telemetry.md`, `docs/eight-plane-beta.md` | Keep executable behavior distinct from planned scheduler, calibration and telemetry extensions. The beta is now on v2; do not describe it as local-only. |
| `docs/irb/decision-register.md` and `source/protocol.md` | Replace older four-cell/Williams allocation, 48 complete/60 enrolled, 64-trial assumptions and offline voice claims. Record the application as unsubmitted, per the researcher. Preserve historical decisions and actual institutional status. |
| IRB consent, recruitment, screening, session record and voice script | Match the selected voice-data flow, retention, session duration, eligibility and standardized procedure. Do not invent changes to compensation or retention from the thesis simplification. |
| IRB questionnaires and post-participation materials | Two block-specific workload records, individual voice ratings/similarity checks, exploratory internal-dialogue question and final interview; specify survey timing and identifiers. |
| Planned-study manuscript and analysis documentation | One voice factor; controlled wording; repeated trial geometry; first-contact/selection and delivered-hint measures; raw motion/playback joins. Update sample justification after pilot evidence. Physiological eye-movement interpretations remain exploratory and require validation. |
| Generated IRB/manuscript outputs | Regenerate only after source reconciliation. Existing PDFs do not automatically reflect these decisions. |

## Defaults that need piloting, not arbitrary answers now

**Protocol proposal:** retain eight planes, 56 objects, approximately 1.5 m radius and gaze dwell as the starting apparatus. Evaluate front/side/rear readability, gaze acquisition and comfortable chair rotation before adjusting dimensions.

**Open validation:** establish calibration/validation criteria, practical timeout, enough practice, total duration, fatigue, final repetitions and sample/sensitivity justification from development rehearsal and the permitted pilot. Existing numerical thresholds are not validated merely because they appear in a document.

**Protocol proposal:** retain immediate workload measurement after each block, a headset break, and end-of-session voice ratings/interview as the procedural outline. Qualtrics is preferred; determine access and the survey handoff without delaying instrumentation work. Revalidate gaze and origin alignment after re-donning the headset.

**Open decision:** choose the primary outcome before confirmatory collection. Record total search time, first target contact, contact-to-selection, error/outcome and guidance measures now; do not silently treat all measures as co-primary. Keep the 15–20 participant discussion provisional.
