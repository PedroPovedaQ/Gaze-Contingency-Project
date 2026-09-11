# Two-voice study QA

**Implemented, awaiting headset QA.** The experimental factor is neutral versus self-similar voice; guidance is always gaze-contingent. The session has two practice trials (one in each assigned voice), followed by 14 experimental trials in two blocks of seven. The seeded experimental stimuli are unchanged.

## Run the complete flow

1. Connect the Focus Vision and use `scripts/refocus-unity-and-build-device.sh`. Supply the existing development provider configuration on the device. No credentials are included in this checkout.
2. Dismiss headset notices, release the controller buttons, then point at and click **Start setup**. A global trigger press must not skip this screen or select a voice. Choose the participant's neutral profile: Trigger / 1 for male (Eric), A/X / 2 for female (Rachel). Self-similar recording follows. After reading the passage, click **Finish recording** below it or press Trigger / 1 to submit early; only captured audio is submitted. The button activates after one second of audio, and recording still stops automatically at the configured limit. Both providers must be available for initial preparation.
3. Wait for the complete phrase library to prepare in both voices. Accept each audio sample only after it finishes; A/X / 2 replays it; B/Y / 3 restarts voice setup if the sample is unacceptable. Check identity, intelligibility, comfortable loudness and approximate pacing. Silence or incorrect identity is a failed QA result.
4. Complete both practice trials and confirm the practice checkpoint. Practice does not count toward the 14 experimental trials.
5. Complete seven trials, submit the block-1 NASA-TLX, take a break, and confirm the next block. Verify a voice change with the same gaze-contingent policy. Complete the next seven and submit block-2 NASA-TLX.
6. Repeat with the other neutral profile and an opposite-parity coded participant ID. Odd IDs start neutral; even IDs start self-similar. The saved `voice-order-v1.txt` and `neutral-profile-v1.txt` control subsequent runs for that ID. Track incomplete enrollments separately: ID parity balances assigned order, not necessarily completed sessions. A restart requires new enrollment and a new run, not a replacement block.

## Failure and timing checks

| Scenario | Expected behavior |
|---|---|
| Missing provider key or failed enrollment | Task cannot start; retry/re-record or cancel is explicit. |
| Corrupt cached MP3 during preparation | Readiness fails; corrupt decode file is removed for regeneration on retry. No wrong-voice fallback. |
| Disconnect network after both libraries are ready | Prepared speech continues offline. No synthesis request is needed during trials. |
| Hold the selection/confirmation trigger | A held button cannot skip a checkpoint. |
| Look at target during its announcement | Objects are hidden; search capture/timing begin only after announcement completion and object setup. |
| Press P in Editor or invoke `PauseSession()` | Objects hide, speech/dwell stop, and search time stops accumulating. Confirm checkpoint / `ResumeSession()` to resume. |
| Set a short timeout in the Inspector for a dedicated QA session | Session stops as timeout without replacement trials. Timeout is disabled (0) by default until the protocol sets a ceiling. |
| Escape in Editor / `WithdrawSession()` | Incomplete summary records withdrawal; completed trials remain intact. |
| Missing prepared phrase / playback failure | Technical stop, logged failure, no substitution. |
| Headset/application interruption | Search pauses, or transition stops technically; no silent continuation. |

**Implemented controls:** researcher methods are available on `FindObjectGameManager`; `StudyCheckpoint.Confirm()` is the programmatic equivalent of the confirmation button. The UI is not an access-control boundary.

## Inspect the exported run

Pull the entire run directory from `GazeData/Pnnn/`, then run:

```sh
python3 scripts/verify-study-session.py /absolute/path/to/run_folder
# For a deliberately interrupted session:
python3 scripts/verify-study-session.py /absolute/path/to/run_folder --allow-incomplete
scripts/run-analysis-pipeline.sh --data-dir /absolute/path/to/GazeData --no-pull
```

Expected artifacts: `trial_summary.json` (schema 2), `trial_events.csv`, `object_manifest.csv`, `gaze_log.csv`, a run-specific copy of `voice-library-manifest.json`, and `nasa_tlx.csv` with block/run identifiers. Search events distinguish transition, objects-ready, search onset, pauses, capture and technical stop. Audio events carry clip IDs and playback start/end calls; these are software timestamps, not measured acoustic onset at the ear. Repeated distractors have distinct object IDs.

The validator checks assigned voices against playback manifests, complete phrase sets, 56 distinct objects/one target per trial, and search duration against events. Analysis exports `voice_block_metrics.csv` and `voice_pair_differences.csv`; only complete 14-trial runs contribute paired differences. Excluded/incomplete runs are listed in `voice_pair_exclusions.csv`. These are descriptive QA outputs, not a confirmatory test.

## Remaining empirical decisions

**Protocol proposal / open validation:** both providers use the same text, one common achieved RMS level across both libraries (up to -20 dBFS, lowered for the worst-case peak headroom) and fixed playback rate; ElevenLabs and Voxtral remain different synthesis models. Prosody, perceived loudness, neutrality and self-similarity are not established by normalization and require the listening checks above. Final sample size, trial timeout, spatial/theta mechanics, cloud retention/deletion and the full protocol packet remain separate research work. No institutional approval is implied by implementation.
