# Two-voice study QA — current local build

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

**Implemented locally; headset verification pending (October 1, 2026).** Compare participant-preferred and self-similar voices with the same gaze-contingent imperative guidance. There are two separate practice trials, then **40 measured trials per voice / 80 total**. The agent knows the hidden target; the participant follows directions and selects using the right controller. This checklist supersedes older 14-trial, first-person, button-shortcut and countdown instructions.

## Run the complete flow

1. Connect the Focus Vision, unlock the computer and open the intended Unity project. Run `scripts/refocus-unity-and-build-device.sh`; verify actual installation and launch, not merely successful menu dispatch. Keep the left controller off. Provider configuration is local/ignored and must not enter Git or reports.
2. Dismiss headset notices, release the trigger and use the right ray to select **Start setup**. A held or untargeted trigger must not advance. Choose the participant's preferred agent gender presentation, then hover over each voice card to audition. Trigger-select a card and choose **Confirm** after its sample finishes. Check all six cards per gender and the selected border; see [catalog](voice-choices.md).
3. Use a consented/authorized pilot recording to check enrollment. The recording introduction and a short tone precede microphone capture; there must be **no 3–2–1 countdown**. Read naturally. **Finish recording** becomes available after one second and submits only captured audio; automatic timeout remains. Processing screens show **Loading...**. Do not upload a participant recording just to run this checklist without the required authorization.
4. Accept both starter audio samples with the right-ray **Accept audio** button after playback. Test **Replay**, retry and re-record paths. Check identity, intelligibility, comfortable loudness and pacing; silence or wrong identity fails QA. Remaining phrases load progressively and may require on-demand preparation. Starter readiness does not establish a fully offline library.
5. Fit the headset and run vendor calibration, then independent validation/drift checks in [gaze validation](gaze-measurement-validation.md). Complete two practice trials, one per voice. Confirm right-ray highlighting, one entry haptic pulse, trigger-release/re-press selection, wrong-choice continuation and correct-choice chime/varied reinforcement. Looking alone must never select.
6. Complete 40 measured trials, submit the block survey, rest and continue explicitly. Verify the voice changes for the next 40 with unchanged wording/policy. The previous-wall readiness cross and non-identifying announcement must finish before object reveal/search timing. Neither speech nor participant UI may name the target's color/shape.
7. Repeat assignment checks with the other voice order and saved participant preference. Saved order/voice choices control reuse of an ID; parity alone does not establish the observed order. A restart is a new run, never a replacement block combined with another run. Short debug sessions do not establish full 80-trial completion.

## Failure and timing checks

| Scenario | Expected behavior / evidence |
|---|---|
| Missing key, verification-required clone or failed enrollment | Explicit retry/cancel or technical failure; no wrong-voice fallback. |
| Corrupt prepared audio | Decode/preparation failure is visible; retry does not silently use another voice. |
| Network disconnect | Already prepared clips can play; unavailable background/on-demand clips can still fail. Record readiness and failure rather than promising offline operation from starter acceptance. |
| Hold trigger through a checkpoint | A held button cannot skip it; release and target the button before confirming. |
| Readiness and introduction | Objects remain hidden until valid readiness gaze and non-identifying speech finish; search time starts at reveal. |
| Open Settings during readiness/search | Search/readiness pauses, objects hide and speech stops. Closing settings leaves explicit Continue; it must not auto-resume. |
| Pause / application interruption | Active-search time excludes the pause; recording retains pause/resume markers. Verify on-device interruption and durability. |
| Invalid/stale gaze, blink or tracking loss | New direction decisions suspend; stale tips cancel; recovery must not invent a wall exit/arrival. |
| Look up/down within the same wall | No repeated area-entry cue merely because elevation changes. Fine horizontal alignment precedes vertical guidance. |
| Overshoot or wall-boundary jitter | Relevant return/updated cue without old-direction speech continuing. Verify actual sound and recorded decisions. |
| Wrong / correct selection | Wrong selection retains the search without revealing target identity; success feedback finishes before transition. |
| Dedicated QA timeout / withdrawal | Retain incomplete outcomes and completed trials; no silent replacements. Timeout remains disabled by default pending protocol choice. |
| Repeat guidance | Separate requested and delivered-repeat events; repeats do not disclose the hidden target. |

`StudyCheckpoint.Confirm()` and manager pause/resume methods support programmatic tests. The participant UI is not an access-control boundary. Physical ray/haptic/voice timing still needs the headset.

## Inspect the exported run

Pull the entire coded run directory plus its linked `EyeMeasurements` and `GazeReplays` folders. Preserve originals, then run:

```sh
python3 scripts/verify-study-session.py /absolute/path/to/run_folder
# A deliberately interrupted session requires explicit incomplete mode:
python3 scripts/verify-study-session.py /absolute/path/to/run_folder --allow-incomplete
python3 analysis/trial_csv.py /absolute/path/to/run_folder --replays /absolute/path/to/GazeReplays --output /absolute/path/to/new-trial-export
python3 analysis/eye_quality.py /absolute/path/to/gaze_observations.jsonl --out /absolute/path/to/new-eye-report
python3 analysis/guidance_audit.py --eye /absolute/path/to/gaze_observations.jsonl --replay /absolute/path/to/recording.jsonl --out /absolute/path/to/new-guidance-audit
python3 analysis/recording_links.py /absolute/path/to/run_folder --eye /absolute/path/to/gaze_observations.jsonl --replay /absolute/path/to/recording.jsonl --out /absolute/path/to/new-link-audit
```

Inspect `trial_summary.json`, `trial_schedule.csv`, `trial_events.csv`, `object_manifest.csv`, compatibility `gaze_log.csv`, the voice-library manifest, surveys and linked raw/replay files. The current layout contains 168 uniquely identified objects per trial. A complete 80-row planned schedule is not proof that 80 trials were actually performed. Verify outcomes, timing, source completeness and exact participant/run/block/trial joins. Guidance software timestamps do not establish acoustic onset.

Supply every raw recording explicitly with repeated `--eye` arguments to `recording_links.py` if logging reopened. Its exact observation joins, object/selection checks and coordinate checks complement session and guidance validation. Exit 2 means incomplete evidence (including older recordings without sample recording IDs), not a pass. The report preserves source hashes and distinguishes invalid/unavailable gaze from measured poses. Inspect `assets.csv`: missing saved playback clips are incomplete, while malformed PCM16 files or unsafe asset paths are issues. Reused clips are checked once with all referring event sequences retained. File format checks do not establish voice identity, naturalness, audibility, video availability, questionnaire completeness, native sensor rate or hardware accuracy.

The per-trial exporter creates one performance/subjective CSV per trial plus a combined table and provenance manifest; reconstruction streams remain separate. Repeated block survey responses retain block scope. Analysis pairs only complete runs using their recorded schedule; historical 7+7 records retain their original rule. These are descriptive QA outputs, not confirmatory power or outcome tests.

## Remaining empirical decisions

**Protocol proposal / open validation.** Both voices use ElevenLabs `eleven_turbo_v2_5` and shared participant-directed wording. The clone-only audio treatment remains and can affect prosody; amplitude matching does not establish perceived loudness, similarity or a pure voice-identity manipulation. Confirm every prepared prompt and success variant by listening. Final instruments, sample size/power, 40+40 burden, timeout, signed-height reference/quotas and consent/cloud lifecycle need research decisions. Native 120 Hz, task-wide accuracy, independent eye-event validation and pupil/workload interpretation remain open. Local checks do not establish institutional approval or participant readiness.
