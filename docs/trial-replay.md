# Trial recording and playback

**Implemented locally — October 1, 2026; headset verification pending:** selectively ported from `origin/v2-app-demo` onto the current study build. The live task uses right-controller selection, 168 scattered objects, shared imperative guidance without spoken target identity, and two 40-trial voice blocks with balanced horizontal theta magnitudes. The separate browser timeline can attach run video; see below. The old branch's experiment flow is not the current study protocol.

## Record on the Vive

The recorder attaches automatically to the game manager. Recording begins at the first trial transition and covers practice and measured trials in one uniquely named package:

```text
/sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeReplays/<UTC>_<ID>/
  recording.jsonl
  audio/clip_0000.wav
```

Keep the whole package together. To copy recordings using Unity's bundled adb:

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb pull \
  /sdcard/Android/data/com.DefaultCompany.MixedRealityTemplate/files/GazeReplays \
  /tmp/gaze-replays
```

These files are separate from `GazeData`; clearing old trial data in future must include both directories. Voice caches and enrollment settings remain separate. Packages contain coded participant linkage and participant-specific synthetic voice clips; handle them with the session's access and retention rules. No microphone enrollment recording, credentials or room video is copied.

## What schema 2 records

- **Trial snapshot:** all object IDs, target identity, shape/color, mesh geometry, position, rotation and scale; wall/slot identity, layout seed, measured schedule version/seed, zero-based block, angle pair and start wall and signed/absolute theta relative to that start. Practice has block `-1` and no measured-run linkage.
- **Frame sample:** head pose, shared-stream gaze origin/direction and gaze-ray intersection; XR-origin pose; left/right controller poses, controller tracking status, ray origin/direction/endpoint, controller-hit object ID, trigger-button state and analog trigger value. Separate readiness wall/pose/progress, valid-inside, pause and announcement-ready state; search activity, elapsed active-search time, render frame and monotonic recording time accompany samples. Changed object transforms are included when needed. Retaining both controller fields preserves format compatibility; current study input is right-only.
- **Selection:** the authoritative accepted object, correctness, selecting hand and both controller snapshots captured before reset, object destruction or round advancement. Choices use `controller_ray_trigger_press_v1`.
- **Events/audio:** search start, transitions, pauses, block checkpoints, stop/completion and software voice playback start/end/cancellation, with saved normalized PCM clips. Practice and measured trials have distinct replay IDs even when their object IDs repeat. Measured trials link back to participant/run/trial IDs in the existing CSVs.

`hoveredId` means **shared valid gaze-ray intersection** in new recordings; historical files used XRI hover. `leftController.hoveredId` and `rightController.hoveredId` mean **controller ray intersection**, using the same hit eligibility as selection. There is no `DwellTarget` or `DwellProgress` dependency and no dwell-selection behavior. The legacy `dwell` field is zero in new files; the reader still accepts original schema 1 recordings without inventing controller telemetry for them.

New headers advertise `gazeLinkVersion = gaze-observation-link-v1`. Every gaze-bearing sample/selection saves the exact `gazeRecordingId` and `gazeObservationSequence`, receipt/input-event times and source. The header's initial `gazeRecordingPath` is descriptive only: gaze logging can reopen into another recording during a replay. Invalid observations retain their identity with `gazeTracked = 0`; zero pose placeholders are not measurements. A formerly valid observation can also become stale before replay capture. A sequence of `-1` means no observation was available. These additional fields preserve schema-2 reader compatibility; older files cannot retroactively establish the new exact link.

Pose availability means that the rig transform was accessible, not that tracking was valid. Tracking and trigger-button fields use `1`/`0` for observed true/false and `-1` for unavailable/unknown; analog trigger is `0..1` or `-1`. A missing controller sample is unknown. A controller ray is available only with an active tracked physical controller. Hit IDs are populated only during active search. The saved endpoint describes the live cast; the viewer joins its endpoints with a straight illustrative line.

Sampling is **once per rendered frame**, after controller processing, while a trial snapshot exists (including explicit pauses). Transitions between trials have event records but no pose stream. This does **not** establish native 120 Hz eye acquisition or a guaranteed 60 Hz controller stream. Analyze timestamps and frame gaps to measure actual cadence. Gaze hover is an interaction proxy, not a validated fixation, saccade or pupil-based workload measure.

All positions are metres in Unity axes (+Y up, +Z forward), relative to one fixed recording origin. The header saves its world position/yaw. XR-origin changes are recorded separately so recentering does not redefine previous samples.

## Watch a trial in Unity

1. Open **Tools → Codex → Trial Replay → Open Viewer**.
2. Open a package's `recording.jsonl`.
3. Select a trial, play/pause, scrub, change speed or jump to the next choice/audio event.
4. Choose **Orbit**, **Recorded head** or **Overhead**. Right-drag to orbit and scroll to zoom.
5. Toggle **Gaze**, **Controllers** and **Target** overlays. Controller rays thicken while the trigger is held; the status text shows each trigger value and unknown state.
6. Use **Previous sample** / **Next sample** to inspect individual observations, including records with identical timestamps. Expand **Source samples and cursor state** for the original JSON, source line/sequence, raw-eye recording/observation link, tracking flags and delivery age.
7. **Save cursor snapshot** writes a new JSON containing source path/header, exact sample/current record and current object transforms. It never overwrites the recording or an existing export. Keep the snapshot with the source package; it is an inspection artifact, not a new participant trial.

Playback uses an isolated preview scene; it does not run study logic, synthesize speech or write participant responses. Backward seeking reconstructs recorded state. The recorded-head view uses a fixed preview field of view. Overlays/materials are illustrative, not the headset's original rendering. Audio plays only at 1× and follows saved start/stop/cancellation records. Recorded one-shot success effects can overlap speech; reused WAVs receive each playback event's own volume. Clips are cut at the earlier of saved stop or natural WAV duration, including after a seek. Missing/corrupt assets are reported without synthesizing substitutes. The viewer owns its audio sources in a disposable preview scene and does not stop other Editor previews. Interruption envelopes and acoustic onset are not reconstructed exactly; the saved software events are not microphone evidence.

The viewer holds a pose for at most the configurable **Max pose age** (default 0.1 seconds) and clears it across trial/search/pause/stop boundaries until a new observation arrives. Unknown tracking (`-1`) never draws a measured gaze/controller ray or drives the recorded-head camera. If head tracking is unavailable, the labeled orbit overview is shown. Historical values remain inspectable through gaps. This is a display rule, not a validated sensor freshness threshold, acquisition rate or fixation definition. No pose interpolation is performed.

## Reliability and verification

File writes use a bounded background queue. A replay failure logs `[TrialReplay] Recording incomplete` and stops this recorder without stopping the task or the existing CSV logger. Completion/stop attempts to drain queued records for up to two seconds; failure handling during a trial does not wait for a stalled writer. A missing or incomplete footer marks the package incomplete; an abrupt device kill may lose buffered tail records. The reader retains a valid prefix after a truncated final JSON fragment, but rejects malformed interior records, unknown object hits, invalid poses/trigger values, unsupported/mixed schemas and asset traversal. Files are never overwritten. Viewer limits are 1 GiB / two million records. Native WAV loading validates RIFF/data lengths, PCM16 rate/alignment fields, mono/stereo channel counts and nonempty bounded payloads; recorded playback volumes must be finite and within 0..1.

`analysis/recording_links.py` also inventories referenced audio in `assets.csv`, validates the recorder's PCM16 WAV format/length/rates and hashes available files. Missing files and playback records lacking a saved clip remain incomplete evidence even when the replay footer says complete. Invalid formats, traversal and symlink references are reported as issues. These checks are local and do not synthesize replacements, change recordings or verify that the participant heard the correct voice.

Run **Tools → Codex → Trial Replay → Run Verification** for serialization, controller state, trial linkage, real selection-hook ordering, replay failure isolation, rewind, interruption recovery, corruption, legacy compatibility and audio checks. The closed-sink fixture deliberately emits one `Recording incomplete` error; the final PASS confirms live selection continued. **Verify Rendering** renders a 168-object synthetic fixture and reports its preview path. **Create Synthetic Demo** opens a clearly labeled synthetic recording in the viewer; it is not participant evidence. The same checks are callable with `-executeMethod TrialReplayChecks.Run` / `TrialReplayChecks.RenderSmoke` in a separate Unity batch session.

**Open hardware validation:** complete a headset session using the right controller with wrong/correct choices, eye/controller tracking loss, a pause, a voice interruption and a block boundary. Verify that the left controller cannot activate study controls. Pull the package and compare IDs, trigger events, poses, audio and timing with the live session. Run the [exact recording-links audit](../analysis/README.md#exact-replayraw-eye-link-audit) on all raw-eye files for the run. Measure frame-time/storage overhead and the recorded cadence on the Vive, including first-use mesh/audio extraction. Desktop verification and a successful Android build do not establish acceptable headset overhead or sensor accuracy; a short run does not establish full 80-trial completion.

Tracking context: [replay #28](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/28), [motion telemetry #22](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/22). The readiness/schedule integration appends start-wall metadata to the existing trial CSV; separate block-level NASA-TLX recording remains in place.

**Implemented readiness gate:** the viewer reconstructs the fixed cross and left-to-right charge separately from object selection. Readiness events are retained before search; dwell-based readiness never populates legacy object `dwell`. The current schedule version is `previous-wall-angle-v5-stratified-40-trials`; the per-block theta quotas refer to the previous target wall. Signed-height balance remains a separate open decision.

## Browser timeline with run video

**Implemented locally:** [Run video in the timeline](timeline-video.md) describes direct raw replay import, an exact sample/event inspector, linked final trial summaries, and attaching existing headset footage with shared playback controls. The overhead browser page keeps video local and honors recorded object movement, visibility and pauses. Video capture, automatic clock synchronization and video rendered from reconstructed Unity playback are separate capabilities. P020 actual-footage alignment remains unverified because no matching video was available.

### Audio reconstruction checks

`TrialReplayAudioChecks.Run` is included in the integration suite and can also run through **Tools → Codex → Trial Replay → Verify Audio Reconstruction**. It tests overlapping speech/effects, repeated asset references with different volumes, source-sequence boundaries, stop/seek/pause/speed/disposal, missing assets and malformed WAV headers using synthetic samples. For the separate silent real-engine check, use `-batchmode -nographics -executeMethod TrialReplayAudioChecks.RunEngine` without `-quit`; it exits with a result after Editor updates verify both sources advance, one can stop independently and disposal leaves the study scene unchanged. This does not establish audible fidelity; compare speech/chime overlap and interruption timing during the morning replay check.

### Native full-session state-seek check — October 1

The Unity viewer loaded a clearly synthetic 80-trial, 432,000-sample, 168-object-per-trial recording (652 MB source). Across 240 descending seeks to late/middle/early positions in every trial, the full-prefix reference had p95 **111.6 ms** and max **476.5 ms**; the indexed saved-trial implementation had p95 **1.35 ms** and max **7.36 ms** on an M4/32 GiB Mac, Unity 6000.3.10f1. Both verified every selected trial, sample context and static object position. Load took 10.23 s / 9.54 s respectively; the indexed run's sampled process working set after load was about 2.15 GB. These are one-run synchronous state timings, not GUI paint, cold-load, peak-memory or headset guarantees. All saved samples remain available.

The index starts reconstruction at the latest eligible complete trial snapshot and preserves absolute JSONL source lines. Regression checks compare every state field and transform against full-prefix replay across 80 trials, equal timestamps and random rewinds. Trial changes clear prior readiness; selection records now capture and apply object movements since the last frame, before round advancement.

Reproduce with new paths:

```bash
python3 analysis/timeline/benchmark_fixture.py /path/to/synthetic.jsonl --seconds 60 --unity-geometry
# In a separate Unity batch process, with the project closed:
# Set GAZE_REPLAY_BENCHMARK_SOURCE=/path/to/synthetic.jsonl
# Set GAZE_REPLAY_BENCHMARK_OUTPUT=/path/to/new-report.json
# Optional reference mode: GAZE_REPLAY_BENCHMARK_MODE=full-prefix
# Then invoke -batchmode -nographics -quit -executeMethod TrialReplayBenchmark.Run
```

[Saved measurements and source/fixture hashes](../analysis/timeline/benchmarks/2026-10-01/native-capacity.json) contain synthetic evidence only. The large fixture is not committed. Actual headset reconstruction and full-window usability still require verification.
