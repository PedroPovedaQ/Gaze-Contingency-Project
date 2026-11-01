# Participant readiness and full-flow rehearsal

**Implemented / audited November 1, 2026.** The current task has one uncounted practice followed by 96 measured trials (48 per voice), 168 objects per trial, hidden targets, a 25-second active-search deadline, and right-controller ray/trigger selection. Gaze guides assistance; looking alone never selects. This replaces the October 1 checklist's two-practice, 40+40, per-block questionnaire and timeout-disabled instructions.

**Protocol decision:** start with 20 participants, 10 per counterbalanced order. The researcher maintains the assignment roster: A = Self-Similar Voice first; B = Selected Voice first. Intake accepts that assignment; it does not randomize order or enforce a quota. Keep the self-similar-only inner-thought treatment and existing left/right guidance for rearward targets; no spoken “behind you” cue is added. Study intakes lock both blocks to Local MiniLM v9; Jev comparison is development-only.

## Readiness verdict

**Open validation:** software checks support a supervised development rehearsal, not a claim that participant collection is ready. The [November 1 schedule verification](../work_log/2026-11-01-schedule-v9.md) records the tested revision, installed desktop comparison, service health and limits; the earlier readiness-audit receipts remain historical. Close these specific items before collection:

- Confirm the approved consent/protocol and cloud voice-data handling process with the study team. A software consent attestation is not an approval receipt.
- Confirm the released final comparison survey and align its URL/version/publication metadata with the runtime. The current handoff still declares `SV_3retnpdBMwBx5bw`, `v2026-10-28c DRAFT`, `surveyPublished=false`. A copied URL is not proof of survey availability or completed responses.
- Complete and archive a consented development rehearsal through intake, cloning, full transfer, gaze preparation, one practice, both 48-trial blocks, rest, final survey linkage and export validation. Exercise both assigned orders. No complete wearer receipt is established by this software audit.

The retained Skip button records missing gaze preparation without an offset; it does not establish calibration accuracy. Saccades and IPA remain exploratory until their separate headset measurement validation is complete. The v9 model's three failed along-cue long-way observation challenge cases remain a documented limitation, not a demonstrated participant outcome.

## Run the complete flow

1. Use `~/Applications/Follow My Voice Study Intake.app`, connect the Focus Vision and start Live reception. Keep the Mac awake and connected throughout Local MiniLM use. Confirm the model is available before beginning. If installing a new build, verify installation and launch, not just menu dispatch. Keep private provider configuration outside Git and reports.
2. Follow the [consent-first audio intake run sheet](guide/10-audio-intake.md). Reserve an unused P### with its assigned A/B order and choose **Development rehearsal** for testing. Record the prospective study/cloud-voice consent receipt before any answer recording. Record participant IPD with its measurement source when available; retain missing status when it is not measured, and distinguish headset lens spacing from participant IPD.
3. Let the participant audition and confirm their Selected Voice. River, outside the study catalog, speaks the nine fixed questions. After each prompt the researcher starts answer capture; the tone precedes capture. Record only participant answers. Exercise Stop, a skipped baseline item, and recovery from interrupted transcription using authorized test speech. Do not use a real participant recording merely to run software QA.
4. Complete researcher eligibility review before cloning. All four eligibility items must be affirmative and unskipped. If speech is insufficient, use the explicit reading fallback before review. Confirm clone input contains only the frozen participant-answer snapshot. An unclear/negative eligibility review must prevent clone/cache/handoff.
5. Wait for both complete phrase libraries, transfer and verify them, then send the participant-bound session to the headset. Confirm P### and order, followed by listening acceptance of both voices. Check identity, intelligibility, pacing and comfortable loudness; silence or wrong identity fails the rehearsal. Selected Voice stays dry; only Self-Similar Voice receives the retained inner-thought effect. Complete cache/transfer does not itself establish listening acceptance.
6. Fit the headset and complete vendor calibration as appropriate. Standard app preparation is three fit dots, a three-second transition, five independent verification dots, result/Retry/explicit Skip, then three-object look-and-right-trigger activation and centering. See [accuracy method](research/standard-gaze-accuracy-method.md). A provisional correction cannot authorize trials before its verification passes; a recorded skip is a separate outcome. Verify release/re-press selection and that gaze alone never selects.
7. Complete one uncounted practice in the assigned first voice, then 48 measured trials. The readiness cross and non-identifying announcement finish before object reveal and search timing. Neither hints nor participant UI may disclose the target's color/shape. Correct capture finishes its sound/confetti/feedback; wrong selection leaves the search active; the deadline produces a recorded timeout.
8. At the block boundary, rest and check comfort/tracking, then continue explicitly into the other voice for 48 measured trials. There is no per-block questionnaire in this flow. Wording, layout rules, guidance policy and deadline remain the same across voices.
9. After trial 96, remove the headset with assistance, check symptoms/comfort and use Trial Viewer's matching completed-run survey handoff. Verify P###, desktop session, run, order, selected voice name and the two condition labels. Administer the released external final survey; the viewer does not submit or certify responses.
10. Repeat assignment checks for the other order. Resume saved intake state rather than overwriting a participant. A restarted experiment is a new run; do not combine replacement blocks across runs. Short quick tests do not prove full-flow completion.

## Failure and timing checks

| Scenario | Expected behavior / evidence |
|---|---|
| Missing consent, negative/unclear eligibility or insufficient usable speech | No cloning or session handoff; existing answers and provenance retained. |
| Missing/corrupt audio, wrong participant or template/hash conflict | Explicit failure or technical stop; no wrong-voice substitution or overwriting conflicting caches. |
| Network or ADB interruption | Record the interruption. Existing clips do not make MiniLM independent of its Mac service. Verify reconnection and decisions before continuing. |
| Hold trigger through a checkpoint | Held input cannot skip it; release, target and confirm. |
| Readiness and introduction | Objects remain hidden; active search starts at reveal. |
| Settings, pause or application interruption | Search/readiness pauses, objects hide and speech stops; return requires explicit Continue. Verify pause/resume records and durability. |
| Invalid/stale gaze, blink or tracking loss | New direction decisions suspend; stale speech cancels; recovery must not invent arrival/exit. |
| Vertical movement, wall boundary jitter or overshoot | No false repeated arrival from elevation alone; updated guidance must not leave an old direction playing. |
| Right/wrong selection and deadline | Record correct, wrong and timeout outcomes separately. A “forgot to select” reminder belongs to an eligible deadline outcome, not ordinary target looking. |
| Rest or withdrawal | Retain incomplete outcomes and finished trials; never fabricate replacements. |
| Repeat guidance | Distinguish requested and delivered repeats; no hidden-target disclosure. |

Programmatic checks do not establish physical controller behavior, audibility, microphone isolation or wearer comfort. Guidance software timestamps do not establish acoustic onset.

## Inspect the exported run

Pull the entire coded run directory and its linked `EyeMeasurements` and `GazeReplays` folders. Preserve originals and write each report to a new destination:

```sh
python3 scripts/verify-study-session.py /absolute/path/to/run_folder
python3 scripts/verify-study-session.py /absolute/path/to/interrupted_run --allow-incomplete
python3 analysis/trial_csv.py /absolute/path/to/run_folder --replays /absolute/path/to/GazeReplays --output /absolute/path/to/new-trial-export
python3 analysis/eye_quality.py /absolute/path/to/gaze_observations.jsonl --out /absolute/path/to/new-eye-report
python3 analysis/guidance_audit.py --eye /absolute/path/to/gaze_observations.jsonl --replay /absolute/path/to/recording.jsonl --out /absolute/path/to/new-guidance-audit
python3 analysis/recording_links.py /absolute/path/to/run_folder --eye /absolute/path/to/gaze_observations.jsonl --replay /absolute/path/to/recording.jsonl --out /absolute/path/to/new-link-audit
```

Inspect summary, schedule, events, object manifest, raw gaze, replay, audio manifests, intake assignment, preparation receipts and final survey handoff. A 96-row plan is not evidence that 96 trials occurred. Verify actual outcomes and participant/run/block/trial joins. Supply all raw files with repeated `--eye` arguments if logging reopened. Missing/unavailable data are not zero; incomplete evidence is not a pass. The exporter retains historical schedule rules instead of relabelling older runs as the current design.

**Open validation:** institutional/protocol approval, hearing/voice quality, task-wide eye accuracy, native sampling behavior, independent saccade validation and pupil/workload interpretation cannot be established by these checks. Clone-only treatment can affect perception; amplitude matching does not establish equal perceived loudness or isolate voice identity as the sole manipulation.
