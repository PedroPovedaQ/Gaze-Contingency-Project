# Trial recording, replay and derived measurements

**Implemented:** new trials write an independent replay package alongside the existing study CSV workflow. The desktop Unity viewer reconstructs saved object geometry, transforms, head pose, gaze interaction state and authoritative choices without entering Play mode. Original study CSV formats are unchanged.

**Protocol proposal:** the repeat-layout tool collects mouse-click responses on the desktop. These are explicitly labeled `desktop_rehearsal`; they are not headset gaze measurements or additional participant-study trials. A headset repeat condition and its treatment of learning effects remain an **Open decision**.

## Record and retrieve

Recording starts automatically with the first trial transition. It includes practice and measured trials under distinct replay trial IDs. Packages live at:

```text
Application.persistentDataPath/GazeReplays/<UTC timestamp>_<recording ID>/
  recording.jsonl
  audio/clip_0000.wav
```

On Android, pull `GazeReplays` from the app's files directory, using the installed application's actual package ID. Preserve the package directory with its `audio` subdirectory. Do not copy only the JSONL file if audio matters.

Each package is local and may contain coded participant linkage, gaze/head movement and a participant-specific synthetic voice. Keep these under the same access and retention rules as the source session. The recorder does not copy enrollment microphone recordings, API credentials or passthrough video.

The JSONL header identifies source type, schema, recording ID, build GUID, app/Unity version, UTC start and coordinate convention. `trial` snapshots include every object's ID, target flag, shape/color, mesh geometry and position/quaternion/scale. `sample` records contain frame/time, head and gaze pose availability, tracking state, XR-origin pose when available, hover ID, dwell fraction, search exposure and changed object transforms. `selection` records preserve the accepted object ID and correctness before the game advances. Trial/voice/checkpoint records preserve the software event timeline.

`participantCode`, `runNumber`, `sourceRunFolder` and `sourceTrialId` link measured records to the existing CSV run. Practice has no measured-run linkage. `trialId` is replay-local and remains unique even when practice and measured rounds reuse object identifiers.

All spatial values are metres in Unity coordinates relative to the fixed recording origin: +Y up and +Z forward. The header preserves the origin world pose. The XR-origin sample is separate: a recenter never silently changes the saved coordinate convention. Tracking validity is `1` for explicitly tracked, `0` for explicitly untracked, and `-1` for unavailable/unknown. A transform existing does not establish valid eye tracking.

Disk writes use a bounded background queue. An overflow or I/O failure reports `[TrialReplay] Recording incomplete` and stops replay recording without changing the existing study logger. A missing completion footer means the package is incomplete. The reader accepts a truncated final JSON fragment but rejects malformed interior records, unsupported schemas, invalid transforms/IDs and asset traversal. Already complete malformed records are not repaired by guessing.

## Watch a trial

1. Open **Tools → Codex → Trial Replay → Open Viewer** in Unity.
2. Choose the package's `recording.jsonl`.
3. Select a trial, play/pause, drag the time slider, change speed or jump to the next choice/hint.
4. Choose **Recorded head**, **Orbit** or **Overhead**. Right-drag or scroll the viewport to orbit or zoom. Toggle gaze and target overlays as needed.

Playback runs in an isolated preview scene, with no study managers, interaction callbacks, TTS requests or participant-log writes. Recorded outcomes are applied directly. Seeking backward clears future events before reconstructing state. Sampled poses are displayed as recorded; there is no invented eye-tracking interpolation.

Hints use saved normalized PCM audio clips and software playback offsets. Audio preview is muted at speeds other than 1×. Missing audio is reported while visual playback remains available. Head view is a pose reconstruction with a fixed preview field of view, not the original optical projection or room video. Object geometry and color are preserved; preview materials and highlight overlays are illustrative, not a pixel-identical reproduction of the headset shader. The original capture boop and passthrough are not recorded by this voice-clip stream.

Older `object_manifest.csv` and `gaze_log.csv` remain usable in the existing analysis workflow. They cannot recover missing rotations, meshes, head poses, exact event identities or audio; this viewer requires the new package rather than silently treating a partial historical reconstruction as exact.

## Generate a demonstration without a headset

Use **Tools → Codex → Trial Replay → Create Synthetic Demo**. This creates a local fixture with 56 objects, a wrong choice, a pause, tracking loss, an object movement, a correct choice and a synthetic tone. Its header and MP4 label identify it as synthetic, not participant evidence.

The verification fixture is generated by `TrialReplayChecks.CreateDemo`; source recordings are not checked into Git.

## Recalculate descriptive measurements

```bash
python3 scripts/analyze-trial-replay.py /path/to/package/recording.jsonl /path/to/new-derived-folder
```

The output includes `trial_metrics.csv` and `provenance.json`. It reports search exposure, correct/wrong choices, raw tracking availability counts, sample gaps and sampled target/distractor hover occupancy. Practice remains a separate flag. The provenance includes a SHA-256 of the source, analysis version and any incomplete-prefix warning. Existing output directories and paths inside the source package are rejected.

These are **derived measurements of existing behavior**, not newly collected human responses. Hover occupancy is an XRI interaction proxy, not validated fixation duration. Intervals crossing explicit pauses or gaps above `--gap-limit` (default 0.1 s) do not contribute hover occupancy. Unknown tracking remains unknown; available hover poses with unknown validity can contribute to the explicitly named interaction-proxy measure. Changing this threshold is an analysis choice recorded in provenance, not a validated headset threshold.

## Collect fresh desktop choices using a saved layout

At the desired trial, select **Repeat layout (mouse rehearsal)**. The viewer resets to that trial's saved initial geometry and target. Old selections and gaze are not injected. Click objects to make fresh choices; wrong choices continue and the correct choice ends the attempt. **End rehearsal** retains an incomplete attempt.

Responses save to a unique `Application.persistentDataPath/GazeReplayDerived/desktop_<ID>/recording.jsonl`, with a copied scene snapshot, source recording/trial, fresh timing and `input=mouse_click`. No gaze samples or fixation claims are fabricated. Original recordings remain untouched. The new file can be opened in the same viewer or processed by the analysis script.

## Export video

In the viewer, **Export frames + audio** creates a new subfolder containing fixed-rate PNG frames, `audio.wav` and `export.json`. The export covers the recording and uses either recorded-head or default orbit view. It is independent of the current playback time/speed. For a selected time interval, use the batch entry below.

```bash
python3 scripts/encode-trial-replay.py /path/to/export-folder
```

The encoder requires `ffmpeg`, refuses overwrites, and produces a labeled H.264/AAC `demo.mp4`. Missing audio or an incomplete source is reflected in the manifest and video label. A cancelled frame export remains incomplete and cannot be encoded by this helper. Voice audio is cropped at recorded stop/cancel/pause times and aligned to the export start; no external synthesis occurs.

Automation entry points:

- `TrialReplayChecks.Run`: non-graphics serialization, state, selection integration, corruption/recovery, rehearsal and audio checks.
- `TrialReplayChecks.RenderSmoke`: set `REPLAY_SMOKE_OUTPUT` to a new directory; generate synthetic input, verify nonempty rendering and export a short demo.
- `TrialReplayExport.Batch`: set `REPLAY_INPUT`, `REPLAY_OUTPUT`, optionally `REPLAY_START`, `REPLAY_END` and `REPLAY_HEAD=1`. Use Unity's `-batchmode -quit -projectPath ... -executeMethod ... -logFile ...` arguments. Rendering requires a graphics-capable session; do not use `-nographics` for export.

Export ranges are bounded to ten minutes per export to limit memory. Export longer sessions as separate trials/segments. The initial viewer keeps a validated recording in memory and reconstructs backward seeks from its saved prefix; it rejects files larger than 1 GiB and more than two million records. This is intended for study trials, not unlimited gameplay archives.

## Verification and hardware gate

**Implemented software verification:** compile and Editor checks, Python analysis/encoder tests, a synthetic rendered export and checks against existing layout/voice behavior. Use `python3 -m unittest discover -s scripts/tests -p test_trial_replay.py` and **Tools → Codex → Trial Replay → Run Verification**.

**Open hardware validation:** before participant collection, record a headset trial with a wrong selection, correct selection, explicit pause and loss of eye tracking. Compare saved object IDs, transforms, audio events and search exposure with the live trial. Measure recorder CPU/frame-time and storage overhead, including first-use mesh and audio sample extraction. Test retrieval and interrupted shutdown on Android. Desktop checks do not establish sensor validity, acoustic onset or acceptable headset overhead.

Implementation grounding: Unity's [time API](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/time/realtimesincestartupasdouble), [JSON serialization API](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/jsonutility), and [preview renderer source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/PreviewRenderUtility.cs). Installed Unity 6000.3.10f1 compilation and rendering are the compatibility checks for this project.
