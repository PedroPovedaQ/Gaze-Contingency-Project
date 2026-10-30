# Run video in the trial timeline

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

**Implemented locally — October 1, 2026.** The browser timeline imports a saved replay JSONL, displays its recorded scene overview and exact source samples, and can display a locally selected headset video alongside them. One video and alignment belong to one recording; switching its trials seeks within the same recording. This extends [#37](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/37). It does not start video capture, render Unity video from telemetry, or add video to the performance CSV.

## Open a run

Create a page directly from a copied/closed replay package:

```bash
python3 analysis/replay_timeline.py /path/to/recording.jsonl \
  --output /path/to/new-timeline-data.json --html /path/to/new-timeline.html
```

Optionally add `--trial-export /path/to/trial-csv-export` to show the performance/subjective table from `analysis/trial_csv.py`. That folder must contain `manifest.json` and `trials.csv`, and the manifest must reference the exact replay SHA-256. Rows join by recording identity and exact trial identity, including separate practice IDs. Mismatched participant/run/voice/block values are rejected when present. Final results and block/session responses are explicitly labeled as context, not values measured at the moving cursor. Without that explicit export, the page shows summary data as unavailable.

The importer retains all saved frame samples and pose-bearing selections, source line/sequence, raw gaze recording ID and observation sequence, and original clock fields. It does not resample or interpolate. Next/previous sample controls step those saved observations; event selection jumps to pause/readiness/audio/selection records. Click an overhead object to inspect its recorded metadata and transformations; backward scrubbing rebuilds the prior layout. Save cursor snapshot downloads JSON with original sample, latest event, source fingerprint, current object positions and separately labeled final summary. The raw-record panel retains historical samples through gaps but labels them as historical.

The default `--max-sample-age 0.1` hides poses after 100 ms without another saved sample. This is a configurable display freshness rule, **not** a fixation threshold or evidence of sensor acquisition rate. Untracked/unavailable head and gaze poses are hidden; recorded object visibility and search pauses are honored. Search lanes exclude pauses. The display cadence is render-frame cadence, not native eye-sensor Hz. Source JSONL/WAV, independent eye files, object meshes and trial CSVs remain authoritative and are not replaced by the page. Run the recording/link validators separately; successful display is not package acceptance.

Prepared historical P020 pages are still supported:

```bash
python3 analysis/timeline_page.py /path/to/timeline-data.json \
  --output /path/to/timeline-video.html
```

Open the HTML file in a browser, or serve its directory on localhost. The page has no external dependencies or upload/network requests. Existing output files are never overwritten. Source recordings remain unchanged. Full-rate long recordings produce large HTML files; new raw imports embed separate trial payloads and parse only the selected trial. This avoids the single-payload failure observed in a 570 MB page and releases the prior trial's parsed graph on switching. Publishing the page is atomic and refuses overwrites even if another file appears while packaging. The overhead view collapses height and does not render the original Unity scene, camera background or replay WAVs; use the native replay viewer for 3D/audio.

The page labels the recorded source as headset, editor simulation/rehearsal, synthetic or unknown. It does not infer research eligibility from a clean recording footer or relabel an editor rehearsal as participant evidence. Exported snapshots include the original header and coordinate-origin metadata.

1. Choose the run's headset recording under **Run video**. Browser-playable MP4 (H.264) or WebM is recommended.
2. Pause the timeline at an identifiable event, such as a trigger selection. With the video controls, find and pause the same event in the footage.
3. Click **Align this video frame with timeline cursor**. Alternatively enter the recording second corresponding to video time zero and apply it.
4. Use the timeline's play/pause, speed, scrubber, trial selector and event jump controls. Video audio is off initially; enable **Video audio** to listen.
5. **Save alignment** downloads a small JSON sidecar. Reopening the page requires reattaching the same video and restoring that sidecar. It records the run identity, filename/size/last-modified metadata, manual offset and timestamp. This is file matching metadata, not a cryptographic identity check.

The sidecar is specific to participant, recording UTC and recording ID for new imports (legacy prepared pages use participant/UTC). Wrong-run or mismatched-file restoration is rejected. Browser file permissions are temporary: saving alignment does not save or upload the video bytes. Keep the footage and sidecar with the run's raw package under the study's existing storage rules.

## Timing and coverage

`video_seconds = recording_seconds - recording_seconds_at_video_start`

The recording clock includes readiness, transitions and pauses. Do **not** align to the pause-excluding active-search clock or restart video time at each trial. Manual alignment is approximate; no synchronization accuracy is claimed. Check another recognizable event later in the recording for drift. Edited clips, missing middle segments or rate drift require separate alignment treatment; the current constant offset assumes a continuous video with the same clock rate.

Before video start or after video end, the player hides the frame and labels missing video coverage while telemetry remains usable. Seeking hides stale video frames. During covered playback, the timeline waits for media seeking/buffering and corrects drift beyond 0.18 seconds; this is a display tolerance, not research-grade synchronization. At pause/scrub it seeks to the cursor. Unsupported or unfinished videos show an error and can be removed. Source telemetry gaps retain their existing labels even if video is available.

**Pending evidence:** P020's data folder contains no headset video, so actual P020 video alignment is unverified. Synthetic footage is used only in automated tests, never attached as participant evidence. Continuous capture for each future run, acquisition timestamps, multiple clip segments and drift correction remain separate work. Raw footage remains distinct from reconstructed Unity playback (#36).

## Verification

```bash
python3 -m unittest discover -s analysis -p 'test_timeline_page.py' -v
python3 -m unittest discover -s analysis -p 'test_replay_timeline.py' -v
# Requires Node, Playwright with Chromium, Python and ffmpeg on the machine:
node analysis/timeline/test_video.cjs
node analysis/timeline/test_replay.cjs
```

The video browser test generates clearly synthetic footage and checks local attachment, alignment, scrubbing, shared trial-clock mapping, speed/pause, coverage gaps, settings export/restore, wrong-run rejection, corrupt media recovery and mobile layout. The replay browser test imports raw synthetic JSONL and checks fixed cursor samples/source IDs, stepping, object movement/inspection, pause gaps, exported snapshots, event jumps and backward reconstruction. Python tests include 80 synthetic trial identities and the 39→40 voice boundary, missing tracking, interrupted tails, corruption and exact summary linkage. The short 80-trial fixture verifies context, not sustained hardware capture or full-session browser performance. Optional `TIMELINE_TEST_SCREENSHOTS=/path/to/output` writes captures. No participant media is required.

## Full-session synthetic capacity check — October 1

An Apple M4 with 32 GiB memory, macOS 15.6 and headless Chromium 151.0.7922.34 loaded the separate-trial pages below. Each fixture has 80 trials and 168 objects per trial. The synthetic 90 Hz cadence is generated test data, not a headset measurement.

| Fixture | Saved samples | HTML size | Observed load | Trial-switch p95 | Scrub p95 |
|---|---:|---:|---:|---:|---:|
| 40 seconds/trial, old single payload | 288,000 | 570 MB | Failed JSON parsing; no inspector | — | — |
| 40 seconds/trial, separate payloads | 288,000 | 508 MB | 2.02 s | 21 ms | 3.0 ms |
| 60 seconds/trial, separate payloads | 432,000 | 758 MB | 2.68 s | 34 ms | 2.4 ms |

The final 60-second run checked the source trial identity after switching/scrubbing all 80 trials. An earlier run of that same page loaded in 3.19 s. Its 3-second playback probe had an animation-interval p95 of about 18.5 ms. The final sampled JavaScript heap was 18.8 MB; this excludes DOM storage and is **not** total or peak browser memory. Offline generation of the 60-second fixture's page took about 121 seconds in that run. No cold-cache or exclusive-host control was used, so these are capacity checks, not latency guarantees. Actual recorded-run/Unity/video comparison, other machines and very long individual trials remain verification items.

Reproduce with new output paths (the generated files are deliberately large):

```bash
python3 analysis/timeline/benchmark_fixture.py /path/to/synthetic-recording.jsonl --seconds 60
python3 analysis/replay_timeline.py /path/to/synthetic-recording.jsonl \
  --output /path/to/synthetic-data.json --html /path/to/synthetic-timeline.html
node analysis/timeline/benchmark.cjs /path/to/synthetic-timeline.html /path/to/new-report.json
```

The [saved measurements, fixture hashes and limitations](../analysis/timeline/benchmarks/2026-10-01/capacity.json) contain no participant data. These checks retain all observations; no sample decimation was used to make the test pass.

## Phase boundaries in source inspection

**Implemented locally:** a pause, resume, search start, trial transition or recording stop invalidates the displayed prior pose immediately, even when it is younger than the configured maximum age. A new source observation is required before rendering resumes. The inspector retains the prior record as historical evidence. The Unity viewer uses the same phase-boundary rule; this does not establish headset tracking validity or clock synchronization.

### Exact event cursors

**Implemented locally:** selecting a recorded event or using **Next event** stops at that event's source sequence, including when several events/samples share a timestamp. Sample stepping also preserves sequence. Ordinary time scrubbing shows the latest eligible record at the cursor. Cursor snapshots retain the selected sequence; use it with the recording identity when comparing runtimes, because decimal timestamp parsers can differ at the last floating-point bit. This is not a measured clock offset.

**Historical check — October 1:** the incomplete P020 package loaded in both Unity and the direct-import browser viewer. Forty-four cursor comparisons agreed on source sample fields/lines, freshness and 168 object positions within serialization tolerance. The valid prefix was retained and the incomplete ending remained explicit. This supports legacy reconstruction, not the current study's hardware, native eye rate, acoustic fidelity or video synchronization. Participant artifacts remain local and ignored by Git.
