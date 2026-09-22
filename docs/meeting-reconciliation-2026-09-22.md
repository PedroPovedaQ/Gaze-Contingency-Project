# September 22 backlog reconciliation

**Protocol proposal / progress audit.** Received September 22, 2026; the meeting date and the prompt's “Friday” deadline are unconfirmed. This records the requested design and evidence on `v2`, not a completed pilot or a frozen participant protocol.

Board: [Gaze Study — Participant Readiness](https://github.com/users/PedroPovedaQ/projects/2). Delivery owner: [epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19).

## Sources and precedence

- The supplied annotated run sheet is **byte-for-byte identical** to `docs/protocol/experimenter-run-sheet.md` at audited `v2` commit [`6457c1f`](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/commit/6457c1f5a89621943010f506c784d9c06e611c80). Its annotations are retained; no second run sheet was created. SHA-256: `fbafe753bd531552e364e3749cbb4d13321ca813f1b95ae70885a29c41843a82`.
- The [supplied meeting requirements](meetings/2026-09-22-pilot-requirements.txt) are preserved verbatim. The paste ends at “REQUIRED FIRST STEP: REPOSITORY + TASK”; no missing instructions were inferred.
- The newer meeting prompt controls conflicts: **participant-preferred versus self-similar voice**, within-participant counterbalanced blocks; **controller ray + trigger selects**, while gaze remains independently measured and drives warmer/colder guidance.
- Retain the existing provisional **30 trials per voice plus two practice trials**. Neither this count nor the new interaction is implemented by this documentation update. Practice exposure/order, allocation and measurement rules remain researcher decisions.
- Inspected all **29 existing issues (#6–#34)** and all 29 project cards, current runtime entry points, analysis/schema, protocol and survey sources, the older local `tasks/` lists, and the separate replay branch. Every requirement already has an owner. **No new issues, duplicate closures or issue splits are needed.** The September 20 reconciliation already created #33/#34 and clarified producer/consumer boundaries.

## Progress actually on v2

“Implemented” below means present in the audited source. It does not imply new headset verification in this session.

| Area | Implemented evidence | Remaining work / owner |
|---|---|---|
| Surrounding search | `RotationalSearchLayout.cs`, `FindObjectGameManager.cs`: eight planes, 56 objects, seated origin; through-wall gaze filtering is present | Controller targeting across all planes and post-break alignment: #9/#34. Fixed-front gate and balanced allocation: #10/#13 |
| Schedule and blocks | `ChallengeSet.cs`: **7 + 7 measured trials**, two separate practice trials. `SessionConfig.cs`: persisted neutral/self-similar order | Preferred labels/actual voice IDs, provisional 30+30 manifest, short complete-session mode: #10/#13/#17 |
| Preferred voice | `VoiceModeSelector.cs` selects a male/female neutral profile; `SessionConfig.NeutralVoiceId` maps two fixed voices | No curated 5–6-candidate ratings/ranking workflow. Extend this selector: #33, then #12 |
| Cloning/readiness | Existing recording/countdown/tone/early-finish, retry phases and participant-bound clone; eight starter clips with background preparation and isolated voice caches | Earlier sample collection, versioned passage, structured failure metadata, selected preferred voice support and hardware QA: #25/#12 |
| Matched wording | `VoicePromptText.cs` and session lock provide shared Collaborative/External script modes on `v2` (commit `39863df`) | Choose one shared style; audit all pre-task speech and required preview exposure: #24 |
| Selection | `FindObjectGameManager.OnObjectCaptured` still subscribes to gaze-dwell capture; wrong captures are retained | Controller ray/trigger, wrong-choice continuation, repeat-target action and target-display decision: #34 |
| Timing and data | `StudyTrialClock.cs`, `TrialDataLogger.cs`: search-onset/pause timing, outcomes, wrong captures, voice/block/trial IDs, object manifests and audio events. `GazeDataLogger.cs`: per-frame gaze/per-eye fields, stable hit IDs and practice flag | Independent first gaze/controller latency, cumulative motion, counters, quality flags and full canonical trial rows: #8/#20/#21/#22. Legacy “fixation/saccade” names do not establish validated eye events |
| Surveys and operator materials | Three Qualtrics draft imports/builders, review PDFs, annotated run sheet and provisional flow artwork are saved | Preferred-voice measures, final wording/scales, one TLX route, join/export test and synchronized operator editions: #26/#27/#30 |
| Storage and completion | Local event/gaze writers and trial summaries exist | Parse/identity/count/stream validation, interrupted-write recovery and no false success: #28 |

**Implemented locally, not on v2:** `codex/trial-demo-replay` contains recording/replay work at `407566a` (branch tip `e47f530` when inspected). The existing #22/#28 evidence reports Unity/fixture tests and an Android build blocked by no connected device. These are prior reports, not checks rerun here. Neither commit is an ancestor of audited `v2`. Preserve that work and assess its data coverage/format mapping before integration; do not count its viewer or synthetic video as a verified participant dataset. Replay UI polish remains deferred.

**Implemented historically / verification limit:** `progress.md` records a successful beta device build on September 12 and later script-mode compilation/regression checks on September 16, when Build And Run found no device. No current complete-session headset acceptance evidence was added by this audit. No issue is marked Verified or closed for planning work.

## All meeting requirements have existing owners

The issue acceptance criteria remain canonical; this table is a coverage index, not a parallel backlog.

| Prompt section | Requirement | Existing issue owners |
|---|---|---|
| 1 | Curated preferred voices; every score/rank and exact IDs | #33, #12 |
| 2 | Early sample, cloning status/retry and passage | #25, #12, #18 |
| 3 | Pre-task voice exposure inventory | #24 |
| 4 | Fitting, built-in calibration, validation and quality metadata | #14 |
| 5 | Simple seated task/selection/repeat instructions | #27, #34; optional diagram #30 |
| 6 | Practice controls, tracking and analysis exclusion | #13, #17 |
| 7 | Controller selection independent of gaze | #34 |
| 8 | Wrong selections retained; same trial continues | #34, #20 |
| 9 | Repeat-target action and raw request timestamps | #34, #21, #20 |
| 10 | Delivered hint count and event identities | #21, #23 |
| 11 | Explicit condition and counterbalanced order | #13, #8 |
| 12 | Surveys, break up to five minutes, revalidation | #13, #27 |
| 13 | One logical-trial outcome row, useful existing fields and units | #20, #8 |
| 14 | Cumulative translation/rotation; wrap-safe axes and quaternions | #20, #22 |
| 15 | Noise/validity-aware first-target gaze independent of controller | #20, #14 |
| 16 | Controller pose/ray/hit/buttons/trigger | #22, #34 |
| 17 | Raw eyes, validity and actual source/sample rates | #22, #14 |
| 18 | Raw state for later reconstruction; defer sophisticated UI | #22, #28 |
| 19 | Stable object manifests and appearance/disappearance events | #22, #9 |
| 20 | Participant/run/block/trial joins, no timestamp-only identity | #8, #28 |
| 21 | Local-first save, validation, optional verified sync | #28, #18 |
| 22 | External Qualtrics handoffs and stable IDs | #26, #13 |
| 23 | Liking, voice similarity and accent similarity separately | #26, #33 |
| 24 | Speech separated from researcher actions | #27 |
| 25 | Completion validator detects missing/corrupt data | #28 |
| 26 | Short complete-session mode and researcher diagnostics | #17 |
| 27 | Defer advanced analytics/plots/replay | #15, #28; future factors #29 |

## Additional run-sheet annotations reconciled

- **Protocol proposal — #26/#33:** preserve own perceived accent, preferred accent, perceived voice gender and the matching strategy. Score every candidate and both experimental voices; keep separate proposed seven-point voice-similarity and accent-similarity items. Scale endpoints, anchors, ranking/ties and timing still need a decision.
- **Protocol proposal — #8/#20/#26:** provide a derived trial-level analysis table that can repeat block-level TLX/voice scores by an explicit participant/run/block/condition join. Retain original survey responses and provenance. Repeated values are block observations, not independent trial measurements; unmatched/missing responses remain missing. Do not copy unfinished survey questions into runtime or replace raw trial data with survey-enriched rows.
- **Open decision — #20:** the run sheet's “time to final gaze” and “final time to correct zone” need separate operational definitions, including which signal defines zone entry, persistence/re-entry, onset, pause handling, censoring and units. They must not silently become synonyms for first gaze, controller contact or completion. Implement the core first-gaze/controller/selection measures first.
- **Protocol proposal — #22:** trigger-held state (0/1), controller and eye rays, head quaternion/position and object state support future replay. “60 Hz” is a requested sampling target to evaluate, not an established rate; preserve measured intervals, source timing, validity and gaps.
- **Protocol proposal — #27/#30:** use explicit **Say/Ask** labels for speech and italic, visually distinct **Action** instructions in regenerated documents; color can reinforce the distinction but must not be its only cue. Include a simple seated 360-degree diagram without internal zone IDs. Existing PDF/Word/figure files remain earlier review editions.
- **Open decision — #18/#25/#27:** disclose synthetic/self-similar voice use in consent; resolve the exact sample timing and participant wording. The annotation about awareness does not authorize concealing voice cloning. Neutral “first/second block” wording avoids repeated condition announcements.
- **Open research verification — #14/#16:** the run sheet requests Drewes, Feder & Einhäuser (2021), DOI `10.3389/fnins.2021.656913`, and Niehorster et al., DOI `10.3758/s13428-026-03039-4`. These are user-supplied references awaiting bibliographic/full-text verification in those tasks. No method, threshold, sampling claim or arousal interpretation was adopted from them in this update.

## What to do next

1. **Resolve the small set of method choices in #7** and version the schema/clock/identity contract in #8. Draft operator and survey changes in #27/#26; reconcile participant/cloud-data wording in #18. This is the immediate decision queue, not permission to change methodology silently.
2. **Complete setup:** #33 preferred-voice evaluations → #25 early sample and #12 preparation/recovery; #14 fitting/calibration; #24 shared wording/exposure audit. Reuse the shipped selector, cloning and audio services.
3. **Complete trials and blocks:** #34 controller selection/repeat → #9 all-plane targeting; #10 reproducible 30+30/short manifests and fixed-front alignment; #13 practice/external surveys/break/revalidation; #23 stale-audio cancellation. Build the #8/#20/#21/#22 data producers alongside these changes.
4. **Establish a trustworthy finish:** #28 local-save/completion validator and compact joinable fixture, with lightweight quality output under #15. Test missing/corrupt files, partial sessions and lost tracking before claiming success.
5. **Rehearse the full experience in #17:** both voice orders, short and provisional full schedules, wrong/correct choices, repeat, failed voice preparation, survey/break and verified export. Use #27's operator script and record timing, comfort, data defects and go/no-go evidence.

Defer advanced plots, replay UI/MP4 polish, path/statistical modeling and final publication assets. Preserve #16 power/outcome work, #30 figure, #31 thesis package and #32 video; these are not deleted because they are secondary to the pilot. #11/#29 stay deferred and #6 stays historical.

## Open decision register

| Decision | Owner | Needed before |
|---|---|---|
| Candidate grouping/gender/accent matching; 0–100 or 1–100; ranking/ties; presentation/exposure | #7/#33/#26 | Freeze selection workflow |
| Trigger press or hold, debounce/hold duration, repeat button, persistent target text/feedback | #7/#34 | Freeze interaction and instructions |
| First-gaze persistence/validity/gap rule, calibration offset validation, pause and timeout clocks | #7/#14/#20 | Freeze measurement algorithms |
| Two-practice-trial voice/exposure policy, fixed-front tolerance, 30-per-voice angle/zone allocation and retries | #7/#10/#13 | Freeze full schedule |
| Final shared perspective and pre-task utterances | #7/#24 | Freeze audio library and operator flow |
| Survey item set/scales/scoring, one TLX route, external handoff and disclosure/sample timing | #26/#18/#27 | Freeze participant materials |
| Primary outcome, sample/power, pilot duration/comfort and actual pilot/committee dates | #16/#17/#7 | Confirm study and calendar commitments |

## Local legacy tasks

The current working checkout also contains untracked September 6 `tasks/00–06` lists. Their useful voice, IRB, manuscript, analysis and execution items map to #12/#25/#18/#31/#15/#17, and scene/angle work to #9/#10/#16. Some text still proposes a 2×2 factor, offline/cloud choice or tuning trial counts to obtain an effect. These are historical inputs; the current meeting requirements and #7/#16 govern design and justification. The local files and unrelated unsaved build/document work were preserved, not published as a second active backlog.

## Verification of this update

Source audit and documentation/project reconciliation only. No runtime, live survey, generated PDF/Word, institutional submission or headset build changes. GitHub status reflects partial implementation and remaining acceptance criteria; source review cannot establish calibration quality or pilot readiness.
