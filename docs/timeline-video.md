# Run video in the trial timeline

**Implemented locally — September 28, 2026.** The browser timeline can display a locally selected headset video alongside the recorded scene overview and data cursor. One video and alignment belong to one run; switching its trials seeks within the same recording. This extends the browser prototype for [#37](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/37). It does not start video capture, render Unity video from telemetry, or add video to the performance CSV.

## Open a run

Package an existing prepared `timeline-data.json` (the current P020 preparation format):

```bash
python3 analysis/timeline_page.py /path/to/timeline-data.json \
  --output /path/to/timeline-video.html
```

Open the HTML file in a browser, or serve its directory on localhost. The page has no external dependencies or upload/network requests. Existing output files are never overwritten. Source recordings remain unchanged. The existing P020 data preparation is still a prototype; this command packages its prepared run data, not arbitrary raw replay JSONL.

1. Choose the run's headset recording under **Run video**. Browser-playable MP4 (H.264) or WebM is recommended.
2. Pause the timeline at an identifiable event, such as a trigger selection. With the video controls, find and pause the same event in the footage.
3. Click **Align this video frame with timeline cursor**. Alternatively enter the recording second corresponding to video time zero and apply it.
4. Use the timeline's play/pause, speed, scrubber, trial selector and event jump controls. Video audio is off initially; enable **Video audio** to listen.
5. **Save alignment** downloads a small JSON sidecar. Reopening the page requires reattaching the same video and restoring that sidecar. It records the run identity, filename/size/last-modified metadata, manual offset and timestamp. This is file matching metadata, not a cryptographic identity check.

The sidecar is specific to the participant and recording UTC identity. Wrong-run or mismatched-file restoration is rejected. Browser file permissions are temporary: saving alignment does not save or upload the video bytes. Keep the footage and sidecar with the run's raw package under the study's existing storage rules.

## Timing and coverage

`video_seconds = recording_seconds - recording_seconds_at_video_start`

The recording clock includes readiness, transitions and pauses. Do **not** align to the pause-excluding active-search clock or restart video time at each trial. Manual alignment is approximate; no synchronization accuracy is claimed. Check another recognizable event later in the recording for drift. Edited clips, missing middle segments or rate drift require separate alignment treatment; the current constant offset assumes a continuous video with the same clock rate.

Before video start or after video end, the player hides the frame and labels missing video coverage while telemetry remains usable. Seeking hides stale video frames. During covered playback, the timeline waits for media seeking/buffering and corrects drift beyond 0.18 seconds; this is a display tolerance, not research-grade synchronization. At pause/scrub it seeks to the cursor. Unsupported or unfinished videos show an error and can be removed. Source telemetry gaps retain their existing labels even if video is available.

**Pending evidence:** P020's data folder contains no headset video, so actual P020 video alignment is unverified. Synthetic footage is used only in automated tests, never attached as participant evidence. Continuous capture for each future run, acquisition timestamps, multiple clip segments and drift correction remain separate work. Raw footage remains distinct from reconstructed Unity playback (#36).

## Verification

```bash
python3 -m unittest discover -s analysis -p 'test_timeline_page.py' -v
# Requires Node, Playwright with Chromium, Python and ffmpeg on the machine:
node analysis/timeline/test_video.cjs
```

The browser test generates clearly synthetic footage and checks local attachment, alignment, scrubbing, shared trial-clock mapping, speed/pause, coverage gaps, settings export/restore, wrong-run rejection, corrupt media recovery and mobile layout. Optional `TIMELINE_TEST_SCREENSHOTS=/path/to/output` writes desktop/mobile captures. No participant media is required.
