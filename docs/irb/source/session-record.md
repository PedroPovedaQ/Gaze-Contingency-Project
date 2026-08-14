# Researcher Session, Deviation, and Event Record

**Protocol proposal:** This checklist records the intended approved workflow. It does not establish that the workflow is implemented or authorized for participant use.

Study: **Follow My Voice: Gaze-Contingent XR Search with a Self-Similar Agent**

This internal record uses study ID only. Do not record diagnoses or unnecessary identifying details. Store signed consent, contact/payment records, and the identity key separately.

Study ID: ____________________  Session ID: ____________________  Date: ____________________ \
Researcher: ____________________  Location: ____________________

## Pre-Session Controls

- ☐ Current UCF approval confirmed; study not expired/suspended.
- ☐ Researcher listed on study and CITI/training current.
- ☐ Correct approved consent/material versions available.
- ☐ Private room, marked seated origin, swivel chair, clear rotational area, and emergency contacts ready.
- ☐ Headset/contact surfaces sanitized; equipment safety check passed.
- ☐ Approved release build/configuration loaded.
- ☐ Build commit/hash: ____________________  App version: ____________________
- ☐ Deterministic stimulus manifest/version: ____________________
- ☐ Counterbalancing schedule version: ____________________  Assigned Williams sequence: ☐ A-B-D-C ☐ B-C-A-D ☐ C-D-B-A ☐ D-A-C-B
- ☐ Allocation was revealed from the pre-generated randomized schedule only after study ID assignment; assigned sequence was not selected by staff.
- ☐ Approved voice provider/account; model-improvement sharing disabled; private/no-library-sharing controls confirmed.
- ☐ Institution-managed encrypted voice workstation ready; automatic cloud backup/sync disabled for temporary study folders.
- ☐ Provider access uses designated staff, least privilege, MFA when supported, and revocable credentials.
- ☐ Provider credential is restricted to the staff workflow and is not embedded in the headset build/logs.
- ☐ Marked origin and eight-plane 360-degree search geometry validated at 45-degree intervals; all 56 object locations are visible from the seated rotation area.

## Consent and Eligibility

- Consent version/date: ____________________
- Consent obtained by: ____________________  Date/time: ____________________
- ☐ Participant received/offered a copy.
- ☐ Voice-cloning authorization initialed/signed.
- ☐ Comprehension confirmed.
- Screening result: ☐ Eligible ☐ Ineligible ☐ Postponed ☐ PI review
- Approved screening reason code if not eligible: ____________________
- Pre-session symptom stop threshold triggered? ☐ No ☐ Yes—do not proceed

## Voice Procedure

- Recording script/version: ____________________  Take: ____________________  Duration: ____________________
- ☐ No name or direct identifier in recording/file/provider alias.
- Provider/product/model/version/settings: ____________________
- Voice alias: ____________________  Voice ID stored only in restricted deletion log: ☐ Yes
- Upload/create result: ☐ Success ☐ Failed ☐ Not attempted
- Fixed study prompts generated on staff workstation and spot-checked: ☐ Yes ☐ No
- Prompt-library manifest/generation log: ____________________  Copied to headset: ☐ Yes ☐ No
- Headset provider/API network call disabled/absent: ☐ Yes ☐ No
- Generic voice version/model: ____________________
- Voice-clone or synthesis issue/fallback: ____________________

## Headset, Calibration, and Practice

- Headset asset ID: ____________________  Eye-tracking provider/version: ____________________
- Eight-plane geometry/origin check: ☐ Pass ☐ Fail  Result/retry: ____________________
- Fit/vision correction acceptable: ☐ Yes ☐ No
- Eye calibration result: ☐ Pass ☐ Fail  Retries: ______  Quality/validity output: ____________________
- Multi-direction gaze validation result: ☐ Pass ☐ Fail  Directions/targets checked: ____________________
- Audio check: ☐ Pass ☐ Fail  Volume/configuration: ____________________
- Practice trials attempted: ______ (maximum 8)  Consecutive correct: ______ (criterion 3)  Criterion met: ☐ Yes ☐ No
- Dwell selection understood: ☐ Yes ☐ No
- Participant chose to continue after practice: ☐ Yes ☐ No

## Experimental Blocks

| Block | Assistance | Voice | Planned trials | Valid trials | Start/end | Break offered/taken | Notes/reason codes |
|---|---|---|---:|---:|---|---|---|
| 1 | | | 16 | | | | |
| 2 | | | 16 | | | | |
| 3 | | | 16 | | | | |
| 4 | | | 16 | | | | |

- ☐ Raw NASA-TLX completed after each completed block.
- ☐ Manipulation/comfort checks completed after each completed block.
- ☐ End-of-session instruments completed or skips recorded without pressure.
- ☐ Each condition manifest places the target exactly twice on each of the eight planes.
- ☐ Search-onset, scheduled hint opportunity (first at 4 seconds; then every 6 seconds), policy evidence/state, playback outcome, selection, and trial-end events logged.
- ☐ Each fine-stage opportunity logged raw gaze-target angle, target angular size, normalized proximity, preceding valid comparison value, and resulting warmer/colder/about-the-same/very-close state or abstention.
- ☐ Gaze-contingent blocks used coarse directional then fine proximity guidance; noncontingent blocks did not receive gaze, hover, coverage, or target-proximity state.

## Data Transfer and Cleanup

- Coded data destination: ____________________  Transfer time/operator: ____________________
- File manifest/checksum validation: ☐ Pass ☐ Fail  Record: ____________________
- Headset research logs removed after verified transfer: ☐ Yes ☐ No ☐ N/A
- Local source recording deleted: ☐ Yes ☐ No  Date/time/result: ____________________
- Provider source sample deleted: ☐ Yes ☐ No ☐ Not supported  Date/time/result: ____________________
- Provider private voice deleted: ☐ Yes ☐ No ☐ Not supported  Date/time/result: ____________________
- Provider generated history/clips deleted: ☐ Yes ☐ No ☐ Not supported  Date/time/result: ____________________
- Device TTS cache/self-similar clips deleted: ☐ Yes ☐ No  Date/time/result: ____________________
- Identity/linkage record updated under approved plan: ☐ Yes ☐ No
- Any deletion failure: access disabled/revoked ☐ N/A ☐ Yes  PI/provider notified ☐ N/A ☐ Yes
- UCF reportability assessed and further voice collection paused while unresolved: ☐ N/A ☐ Yes

## Session Disposition and Data-Quality Reason Codes

Disposition: ☐ Completed ☐ Participant withdrew ☐ Researcher stopped ☐ Technical stop ☐ Ineligible after start

Compensation amount/form issued or owed: ____________________  Payment record location: ____________________

Pre-specified reason codes (select all that apply; do not decide based on condition outcome):

- ☐ None known at session end
- ☐ Consent withdrawn
- ☐ Calibration failure after approved retries
- ☐ Eight-plane geometry/origin validation failure
- ☐ Tracking availability below approved threshold
- ☐ Missing/corrupt search-onset or response event
- ☐ Corrupted/incomplete file
- ☐ Wrong build/configuration/schedule
- ☐ Voice condition generation/playback failure
- ☐ Major interruption/protocol deviation
- ☐ Participant did not complete
- ☐ Other approved code: ____________________

Final analysis exclusion is determined under the approved analysis plan, not by the session researcher.

## Symptoms or Adverse Event

Did the participant report or display a symptom/incident? ☐ No ☐ Yes

If yes:

- Date/time and block/trial: ____________________
- Observable facts and participant’s words (no diagnosis): ____________________
- Task stopped and headset removed: ☐ Yes ☐ No
- Seated/rested and monitored: ☐ Yes ☐ No
- Emergency/medical assistance offered or activated: ____________________
- Outcome before departure/transfer of care: ____________________
- PI notified date/time: ____________________
- UCF reportability assessment and Huron report/reference: ____________________
- Corrective/protective action: ____________________

## Protocol Deviation or Confidentiality Incident

Deviation/incident occurred? ☐ No ☐ Yes

- Date/time: ____________________
- Approved requirement versus what occurred: ____________________
- Immediate safety/privacy effect: ____________________
- Data affected: ____________________
- Immediate containment/correction: ____________________
- PI notified: ____________________
- UCF report/modification reference if required: ____________________
- Prevention/follow-up: ____________________

Researcher signature/initials: ____________________  Date/time completed: ____________________
