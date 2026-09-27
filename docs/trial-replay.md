# Trial recording and playback

**Implemented:** selectively ported from `origin/v2-app-demo` onto the current study build. The current controller selection, 168 scattered objects, first-person self-similar speech, two ten-trial voice blocks and balanced theta schedule remain the source of live behavior. Video export, desktop rehearsal and the old branch's derived-analysis scripts are not part of this port.

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
- **Frame sample:** head pose, gaze origin/direction and independent XRI gaze-hover object; XR-origin pose; left/right controller poses, controller tracking status, ray origin/direction/endpoint, controller-hit object ID, trigger-button state and analog trigger value. Separate readiness wall/pose/progress, valid-inside, pause and announcement-ready state; search activity, elapsed active-search time, render frame and monotonic recording time accompany samples. Changed object transforms are included when needed.
- **Selection:** the authoritative accepted object, correctness, selecting hand and both controller snapshots captured before reset, object destruction or round advancement. Choices use `controller_ray_trigger_press_v1`.
- **Events/audio:** search start, transitions, pauses, block checkpoints, stop/completion and software voice playback start/end/cancellation, with saved normalized PCM clips. Practice and measured trials have distinct replay IDs even when their object IDs repeat. Measured trials link back to participant/run/trial IDs in the existing CSVs.

`hoveredId` means **gaze hover**. `leftController.hoveredId` and `rightController.hoveredId` mean **controller ray intersection**, using the same hit eligibility as selection. There is no `DwellTarget` or `DwellProgress` dependency and no dwell-selection behavior. The legacy `dwell` field is zero in new files; the reader still accepts original schema 1 recordings without inventing controller telemetry for them.

Pose availability means that the rig transform was accessible, not that tracking was valid. Tracking and trigger-button fields use `1`/`0` for observed true/false and `-1` for unavailable/unknown; analog trigger is `0..1` or `-1`. A missing controller sample is unknown. A controller ray is available only with an active tracked physical controller. Hit IDs are populated only during active search. The saved endpoint describes the live cast; the viewer joins its endpoints with a straight illustrative line.

Sampling is **once per rendered frame**, after controller processing, while a trial snapshot exists (including explicit pauses). Transitions between trials have event records but no pose stream. This does **not** establish native 120 Hz eye acquisition or a guaranteed 60 Hz controller stream. Analyze timestamps and frame gaps to measure actual cadence. Gaze hover is an interaction proxy, not a validated fixation, saccade or pupil-based workload measure.

All positions are metres in Unity axes (+Y up, +Z forward), relative to one fixed recording origin. The header saves its world position/yaw. XR-origin changes are recorded separately so recentering does not redefine previous samples.

## Watch a trial in Unity

1. Open **Tools → Codex → Trial Replay → Open Viewer**.
2. Open a package's `recording.jsonl`.
3. Select a trial, play/pause, scrub, change speed or jump to the next choice/audio event.
4. Choose **Orbit**, **Recorded head** or **Overhead**. Right-drag to orbit and scroll to zoom.
5. Toggle **Gaze**, **Controllers** and **Target** overlays. Controller rays thicken while the trigger is held; the status text shows each trigger value and unknown state.

Playback uses an isolated preview scene; it does not run study logic, synthesize speech or write participant responses. Backward seeking reconstructs recorded state. The recorded-head view uses a fixed preview field of view. Overlays/materials are illustrative, not the headset's original rendering. Audio plays only at 1×, follows recorded start/stop events and is omitted when its asset is missing. The 40 ms live interruption fade envelope, capture sound effects and acoustic onset are not reconstructed exactly.

## Reliability and verification

File writes use a bounded background queue. A replay failure logs `[TrialReplay] Recording incomplete` and stops this recorder without stopping the task or the existing CSV logger. Completion/stop attempts to drain queued records for up to two seconds; failure handling during a trial does not wait for a stalled writer. A missing or incomplete footer marks the package incomplete; an abrupt device kill may lose buffered tail records. The reader retains a valid prefix after a truncated final JSON fragment, but rejects malformed interior records, unknown object hits, invalid poses/trigger values, unsupported/mixed schemas and asset traversal. Files are never overwritten. Viewer limits are 1 GiB / two million records.

Run **Tools → Codex → Trial Replay → Run Verification** for serialization, controller state, trial linkage, real selection-hook ordering, replay failure isolation, rewind, interruption recovery, corruption, legacy compatibility and audio checks. The closed-sink fixture deliberately emits one `Recording incomplete` error; the final PASS confirms live selection continued. **Verify Rendering** renders a 168-object synthetic fixture and reports its preview path. **Create Synthetic Demo** opens a clearly labeled synthetic recording in the viewer; it is not participant evidence. The same checks are callable with `-executeMethod TrialReplayChecks.Run` / `TrialReplayChecks.RenderSmoke` in a separate Unity batch session.

**Open hardware validation:** complete a short headset session with both controllers, wrong/correct choices, eye/controller tracking loss, a pause, a voice interruption and a block boundary. Pull the package and compare IDs, trigger events, poses, audio and timing with the live session. Measure frame-time/storage overhead and the recorded cadence on the Vive, including first-use mesh/audio extraction. Desktop verification and a successful Android build do not establish acceptable headset overhead or sensor accuracy.

Tracking context: [replay #28](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/28), [motion telemetry #22](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/22). The readiness/schedule integration appends start-wall metadata to the existing trial CSV; separate block-level NASA-TLX recording remains in place.

**Implemented readiness gate:** the viewer reconstructs the fixed cross and left-to-right charge separately from object selection. Readiness events are retained before search; dwell-based readiness never populates legacy object `dwell`. The current schedule version is `previous-wall-angle-v3-10-trials`; the per-block theta quotas refer to the previous target wall.
