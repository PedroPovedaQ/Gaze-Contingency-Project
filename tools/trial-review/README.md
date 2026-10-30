# Trial Review desktop app

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](../../docs/balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

**Implemented compatibility:** Trial Review and CSV exports use saved trial/block metadata and preserve schedule versions. Regression fixtures cover v8 indices 31/32 and legacy v7 indices 39/40.

**Implemented October 27 — participant IPD:** before **Begin interview**, enter binocular distance IPD in millimetres and choose its source (pupillometer, ruler, or participant report). Optionally enter the headset lens spacing separately. Check values before creating the interview; they are saved with that session and sent with the participant assignment. Leave unavailable measurements blank/Not recorded. These are manual entries, not hardware readings. The flat trial CSV and joined **Final data** panel include participant IPD, source and headset lens spacing. Earlier recordings and headset-only quick tests have no inferred IPD. Details: `work_log/2026-10-27-participant-ipd.md` at the repository root.

**Implemented October 26 — neutral quick-test audio cache:** in **Wi-Fi settings**, select **Prepare neutral test audio** before starting a headset session. The Mac reads the installed app’s exact Eric phrase catalog, reuses existing headset MP3s, synthesizes only missing lines, decodes them with ffmpeg, and transfers only changed cache files. Wait for **Neutral audio ready on headset**. This does not start a trial. The headset retains the cache across app restarts and works without the viewer once prepared. Changed wording/model/voice keys remain separate; changed source bytes or damaged decode files fall back to the MP3. Final level matching, manifests and replay waveforms remain headset-owned. Transcript validation remains bypassed as configured.

The Mac needs ffmpeg and, only for missing phrases, `~/.config/follow-my-voice/api_keys.json` with `elevenlabs_key`. Cache: `~/.local/share/follow-my-voice-neutral-audio`; status: `neutral-audio-status.json` in the live archive. This action currently targets **Eric / neutral development quick test**, not all neutral participant voices or clones. Finish/cancel an existing headset session first. Runtime catalog: `PreparationLogs/neutral-audio-catalog.json`. The worker is also callable as `python3 tools/trial-review/neutral_audio.py --endpoint HEADSET_IP:5555 --output /path/status.json`.

**Implemented October 25 — live headset controls:** While following **Live**, the Wi-Fi panel has a separate **Headset controls → Walls** button. It sets all eight headset wall outlines and degree labels together using the same API as the wrist setting; target objects, search geometry, schedule and trial clocks are unchanged. **Viewer walls** still affects only the desktop reconstruction. This control requires the updated headset build and a fresh, awake connection. The label reflects headset-reported state, including wrist changes, and requests show pending/confirmed/rejected status. Saved and detached review cannot send controls. Commands are session-bound, expire after ten seconds, and become invalid across sleep/focus changes. Accepted requests are retained under `PreparationLogs/controls_<launch-session>/commands.jsonl`; changes during an active replay also appear as `headset_setting_changed` events. Pre-trial changes use the standalone audit without starting a replay.

**Implemented October 25 — theta v7:** **At cursor** displays recorded pre-cue assigned/realized angles, error, signed target height, eye/cross/target coordinates, gaze sequence and both realtime clocks. Coordinates are explicitly Unity world, separate from replay coordinates. Earlier cursors/legacy recordings remain unavailable. Repeated pre-search readiness attempts and backwards seeking retain the correct snapshot and finalized object positions. Selected-trial export adds exact `pre-cue-theta.jsonl`; **Final data** includes every flattened snapshot field. Condensed CSV adds five fields: snapshot status, realized signed/absolute angle, assignment error, and target-minus-cross height (22 columns total). These are recorded geometric measurements, not total physical rotation.

**Implemented October 23 — compact walls:** new experiment recordings include `hasSearchPlaneBounds`, `searchPlaneBottom` (recording-local Y) and `searchPlaneHeight`. The viewer uses these exact vertical bounds for outlines and gaze display projection. Older files retain object-derived bounds and all recorded object positions; the viewer never applies a global 0.7 scale to historical data. Default new study walls are 2.2036 m tall with unchanged radius/count/size/separation.

## Condensed whiteboard CSV

**Implemented locally, October 16, 2026.** Automatic run export also produces
`trials_condensed.csv`, an Excel-compatible UTF-8 CSV with the whiteboard's
performance/subjective subset. Open it using **Open condensed CSV** under
**Participant & run files**. It is attached separately under **Dataset → Current
dataset → Files**. Existing recordings generate it when reopened or when
**Regenerate CSVs** is selected; earlier exports remain intact. Live mode waits
for run end and verified transfer, as with the full export.

The original 17 columns are source participant ID, trial ID, block index, voice condition,
absolute wall theta, signed wall theta, actual signed head-to-target yaw,
target-minus-head height, Euclidean target distance, delivered instruction count,
active search time, first and final gaze contact time, first right-controller
contact time, final selection time, NASA-TLX score and subjective performance.
Angles use degrees, height/distance metres, and times active-search seconds.

This is a projection of `trials.csv`, with the same rows and measurements, not a
new calculation or motion export. Stable trial/block IDs keep rows joinable.
Missing values stay blank; recorded zeros stay zero. Consult the full CSV for
practice, unfinished/not-started outcomes, development exclusions and quality
flags. Catalog reassignment stays in the full CSV; condensed PID is the source ID.
Height is relative to the first tracked head pose after onset, not the scheduled
cross-relative height quota. Gaze contacts are not validated fixations. Repeated
block-level workload values are not independent trial-level responses.

The whiteboard's separate **Performance** heading has no confirmed instrument
mapping: `subjective_performance_rating` stays blank rather than substituting
the TLX performance subscale. `manifest.json` includes the condensed schema,
column definitions and limitations; the export receipt fingerprints the CSV.

## Jev decisions and hover help

**Implemented locally, October 12, 2026.** Click a marker on the **Jev decisions** track or open the **Jev** inspector tab. It displays the recorded request state, eligible options and metadata, returned probability bars when supplied, request latency, application outcome and explicitly linked software playback onset. The inspector is recorded history and may show outcomes after the current cursor; selecting a request seeks to its original scene. `--jev` opens this inspector at launch.

Green markers indicate speak/playback, gray indicates wait/pending or legacy telemetry, orange indicates fade approval, red indicates timeout/error, and outlined markers indicate stale responses. Hover controls, timeline events and candidate text for explanations. New recordings require the updated headset build for complete traces. Historical runs show legacy events without inventing missing candidates, probabilities or playback associations. A provider response is not proof of playback, and playback is not proof the participant heard it.


## Live Wi-Fi mode

**Implemented locally, October 10, 2026.** The desktop viewer can receive the
headset's existing recordings over wireless ADB, reconstruct the current run,
and archive new runs automatically. Receiving recordings needs no Unity server. Remote headset controls require
the updated study build. The headset remains the source of truth and continues recording if
the computer disconnects. This is buffered live telemetry reconstruction, not
passthrough screen video or a new sensor acquisition path.

1. Put the Vive and Mac on the same trusted Wi-Fi network. The network must allow
   devices to reach each other (guest/client-isolated networks may not).
2. Launch the desktop app, choose **Wi-Fi settings**, then **Connect Wi-Fi**. Enter the
   headset IPv4 address if wireless ADB is already enabled. Otherwise leave it
   blank and connect one authorized headset by USB once; the receiver enables
   ADB TCP/IP and then uses only the Wi-Fi endpoint. The installed Android SDK
   `adb` and `/usr/bin/python3` are required on the Mac.
3. Wait for **WI-FI CONNECTED** before unplugging USB. Start trials normally in
   the headset. **Follow live** follows the newest run with a 200 ms viewer buffer;
   actual delay also includes headset flushes, transfer and parsing time. The receiver
   waits 100 ms between transfer passes. Full-file hash verification waits until
   replay growth stops so it does not block live transfer. Initial join and explicit **Resume live** join the buffered edge. Transient
   signal recovery retains the cursor. Catch-up can skip silence, but cannot skip
   a received audio interval: scene, subtitles and WAV playback share the same clock.
   **Waiting for headset signal** replaces the scene while no fresh tracked samples
   are arriving, including paused/closed runs, tracking loss and disconnection.
   A connected ADB transport alone does not count as a live headset. After two seconds
   without observed source-file growth the view waits. When a pause/end arrives with
   buffered data, the viewer finishes that received prefix as **Buffered replay**
   before waiting; it does not present it as fresh headset activity. Normal fresh chunk parsing does not interrupt an already following view.
   An initially offline headset is retried automatically at the configured address.
   Repeated **Follow live** clicks do not rewind an already-following view.
4. Select first person, third person or overhead as usual. Scrubbing, selecting a
   trial, pausing or changing playback speed leaves follow mode while archiving
   continues. **Follow live** returns to the latest run. Saved WAV playback at
   1× follows the recorded events, including interruptions. Late/missing assets
   are reported; missing speech is never synthesized. Live playback waits up to
   five seconds at a missing audio start, then reports one unavailable segment.
   An asset arriving during that wait starts at offset zero with its recorded scene.
5. **Open saved files** opens `~/Documents/Trial Review Live/` by default.
   `data/GazeReplays/<run>/recording.jsonl` and `audio/` form the replay package;
   `data/EyeMeasurements/`, `data/GazeData/` and `data/PreparationLogs/` retain
   raw eye observations, trial/CSV metadata and preparation logs. These are the
   app-generated files; external surveys, microphone recordings and headset
   video must be collected separately.
6. Let pending bytes reach zero and file hashes finish verifying before closing
   the receiver. **Stop receiver** or quitting the viewer stops wireless copying;
   it does not stop the study or delete either copy. Reconnect to recover missed
   bytes and new runs while the source files still exist on the headset.

On first connection the receiver takes the newest replay and current/latest
supporting folders, plus any folders explicitly linked by that replay. It then
watches all new folders in the four recording roots, including runs created
while disconnected or between receiver sessions. It does not import all older
history on first connection. The archive is tied to the physical headset serial
and application ID; another headset needs a different output directory.

The receiver polls replay-file sizes separately from the full archive. JSONL/CSV
transfers of at least 1 MiB use gzip when the headset supports it; saved files
remain byte-identical and uncompressed. Smaller chunks and WAVs transfer directly. Recurring
archive scans and stable-file hash verification run in background workers; replay
transfers retain priority. The full archive is refreshed about every three seconds
plus scan time, with manifests checkpointed once per second and each transferred
chunk still fsynced. Unchanged local files are rechecked every 30 seconds. Historical link discovery
reads bounded 128 KiB chunks and checkpoints complete-line offsets, so reconnecting
does not synchronously reparse the whole archive. Joining
an existing recording must still catch up before it can be shown live. These are
receiver scheduling choices, not a guarantee of headset-to-screen latency.

**Backup status is evidence, not a completeness claim:** `live-status.json` reports
connection, copied bytes and verified-file counts. `data/wireless-manifest.json`
contains source size/mtime and SHA-256 for verified files. Hash equality means the
saved file matches that source snapshot; a still-growing or prematurely stopped
recording is not thereby a completed experiment. Partial JSONL tails wait for more
bytes. The viewer freezes on lost connectivity and validates sequence, geometry
and tracking using the same checks as offline replay. There is no inferred motion
across missing observations. A 1 GiB/two-million-record live-view limit stops the
preview with an error while the receiver continues archiving.

Launch directly into live mode:

```sh
open '/path/Trial Review.app' --args --live --endpoint 192.168.1.208:5555
```

Launch arguments apply when starting the app. If it is already open, use its
**Open run…** or **Follow live** controls to change that window. To review alongside
live, use **Wi-Fi settings → Review window**. This creates an independent window,
initially muted, with a frozen copy of the currently parsed recording and available
played WAVs. **Open run…** in that window opens other saved runs. Its seeking,
playback, mute and closing do not stop the main live receiver. The copy retains
original recording identity and incomplete-recording status; final CSV exports and
raw-eye files remain in the original run. Snapshots live under `review-snapshots/`
in the archive directory. `--detach-review` opens the same window on launch;
`--review-window --recording /path/recording.jsonl` opens a review-only instance
without a receiver. The main live window must stay open to keep receiving.
The IP above is an example and can change. `--live-output '/path/to/archive'`
selects another destination. To run just the receiver without the viewer:

```sh
python3 tools/trial-review/live_bridge.py --endpoint HEADSET_IP:5555 \
  --output "$HOME/Documents/Trial Review Live"
```

ADB TCP/IP may need enabling again after a headset reboot. No public/cloud server
is used. Keep the Mac awake and receiver running during collection. The headset
copy protects against Wi-Fi loss; do not delete it until the archive is verified.

Tests cover interrupted/resumed transfers, short reads, file rewrites/truncation,
path containment, new-run discovery across restart, linked raw-eye/trial/audio
files, partial UTF-8 JSONL tails, exact record reconstruction, new meshes/trials,
rewind, late WAV arrival and continuous playback through audio-plan updates.

**Implemented locally, October 8, 2026.** A separate macOS Unity app reconstructs
saved trial packages in 3D. It defaults to recorded-head first person and offers
orbit/overhead cameras, saved speech/effect playback, trial/event navigation,
sample stepping, a run timeline, and a cursor-data inspector. A separate Final
data tab imports performance/subjective CSVs from the exact recording.

Offline mode opens copied/stopped `recording.jsonl` packages with their `audio/`
folder; live mode adds wireless copying from the Vive. Neither mode launches the
study, synthesizes voices or starts trials. The optional Astra evaluation sends
allowlisted decision evidence through Codex after processing; reconstruction itself
runs locally. The build contains only synthetic example data, and participant
recordings stay outside the application bundle.

## Build

Unity 6000.3.10f1 with Mac standalone support and the study checkout’s installed VIVE OpenXR package are required (the latter supplies controller meshes only). Use a **new** output path:

```bash
python3 tools/trial-review/build.py --output '/absolute/path/Trial Review.app'
```

Optional `--project /outside/checkout/generated-project` reuses a marked generated
project. `--verify-recording /copy/recording.jsonl --verify-export /copy/export`
also exercises real saved head poses and the exact CSV join before building.
`--unity` chooses the installed Unity executable. Build logs and shared-source
SHA-256s are saved in the generated project. The script clears inherited Git
variables for the child process. No study build settings, scene, credentials,
XR packages or Editor instance are changed. Never point it at the study project.

This is a local development app, not a notarized distribution release. Neither
Android deployment nor a headset is required to run it.

## Subtitles

Subtitles are enabled by default below the scene, with an independent **Subtitles**
view toggle. They follow recorded speech start/end/cancellation and pause events,
including when scrubbing backward, and do not require the WAV file to have arrived.
Success chimes do not replace speech captions. Waiting for live signal hides captions.
Text is the recorded authored phrase, not a transcription of headset sound; it cannot
prove that a generated clip pronounced every word correctly.

## Open a saved recording

The interface defaults to UCF-inspired black and gold (`#FFC904`). Use **Light mode**
in the top bar to switch to warm white panels with dark text; **Dark mode** switches
back. The choice persists between launches. `--theme light` or `--theme dark` can
override it for a single launch. Themes affect viewer controls and timeline styling,
not the recorded object colors, gaze measurements or reconstructed scene lighting.
The scene-overlay controls keep gray backgrounds in both themes: enabled
options use a filled radio-style circle and bold label; disabled options use an
outlined circle and normal label. They toggle independently, so multiple can be on.
Circular indicators use 26-point type, twice the label size.

**Detection ring** shows the target's reconstructed gaze region, cyan outside and
green when the target wins the closest-angular-center comparison. All objects use
that same vicinity rule for gaze shine: 1.6x base luminance and a gold rim; right
controller hover retains its blue rim. The desktop rim uses a lightweight shader,
not the headset's URP material. Invalid/stale gaze clears gaze feedback, and hiding
the ring does not disable shine. Equal angular matches within 0.01 degrees produce
no winner. The overlay follows the displayed corrected/recorded ray.

**Reconstruction limitation:** snapshots do not save collider dimensions, so bounds
are estimated using the current study collider recipe, saved shapes and transforms.
Padding uses the latest preceding measured guidance audit in the current trial,
or the 6-degree default when absent; the footer identifies which. Changes between
audit samples cannot be recovered exactly. The live angular policy and collider
recipe are copied from study source during preparation. These researcher overlays
do not overwrite recorded hits, timings or evidence of what a participant saw.

### Participants, datasets and automatic round/run CSVs

**Implemented locally:** choose **Dataset** beside the session selector (also available
under **Final data → Edit participant & dataset…**). **Participants** creates or edits
coded participant IDs and optional display names. **Current dataset** assigns the open
recording to a participant and gives the run an optional name. Changing an assignment
requires a reason and records a timestamped history. These are local viewer assignments;
they do not change headset enrollment or rewrite original recording IDs.

**Saved datasets** reopens cataloged runs. **Attach file…** links one selected file;
recording-package, linked study-data and generated CSV files are also listed individually.
Each file has a **Finder** button, and **Open dataset folder** opens the run folder.
Attachments are local file links, not uploads or copies; moved files are shown as missing.
The catalog is saved in the app's persistent data folder as `participant-datasets.json`.

Opening a saved recording automatically generates performance/subjective CSVs using
the bundled study analysis exporter. Live mode waits for the current recording's
end record and a connected, fully copied and hash-verified archive. A stopped or
incomplete recording without an end marker must be opened as a saved run. No per-frame
export runs while the participant is searching.

Under **Final data**, **Open round + run CSVs** opens the generated folder:

- `trials.csv`: one row per scheduled/recorded round across the full study run.
- `trial_*_performance_subjective.csv`: one round per file, with practice marked.
- `manifest.json`: original source hashes, methods, availability and warnings.

The destination is `~/Documents/Trial Review Exports/Runs/<name-or-ID>_<version>/`.
Every row includes `participant_name`, `assigned_participant_id`, `dataset_id` and
`dataset_name` alongside the unchanged original `participant_id`.
Renaming, reassigning or changed source files create a new export; unchanged inputs reuse the
verified export. Previous exports remain intact. Files that have been edited after
generation are not silently overwritten.

The exporter needs the matching study data folder containing `trial_summary.json`.
It finds standard wireless archives and adjacent copied run folders automatically;
otherwise use **Choose study data folder…**, then select the linked run folder.
**Regenerate CSVs** retries after missing data becomes available. Missing measurements
or surveys stay unavailable; generating files does not mean a run was completed.
These summary CSVs do not replace the separate motion/eye data or replay/audio package.
This feature requires the Mac's `/usr/bin/python3`; calculations run locally.

New recordings with `gazeRayVersion=recorded_used_and_unaligned_ray_v1` preserve the actual used eye ray and the selected source before quick alignment, both in the same recording coordinate frame. **Recorded gaze** uses the saved eye origin/direction without moving it to the avatar's head or applying the alignment again. **Before alignment** shows the separately saved pre-correction ray. The cursor inspector reports source, alignment ID/status, applied flag and yaw/pitch correction; stepping backward restores those recorded values, and invalid/stale gaze stays hidden. The standalone build verification exercises the live quick-alignment engine, recorder mapping, JSON round trip and first/third-person renderer with synthetic data.

These are additive schema-2 fields: older recordings remain readable. P020's exact-recording legacy X-origin recovery is unchanged. Old recordings without alignment evidence cannot acquire a retrospective quick calibration. A passed quick check is not task-wide accuracy validation.

1. Launch the app; the bundled **synthetic** demo is the default.
2. **Open run…** (or the session selector) opens the file browser. Choose a replay
   package and select `recording.jsonl`. Loading a large recording is synchronous and may take
   time; recordings are bounded to the shared reader's 1 GiB/two-million-record limit.
3. Search trials by number, voice, ID, block or angle; filter All / Study / Practice.
   Select a trial from the left. **Whole session** is the default: drag the playhead or
   anywhere on the speech, gaze or event tracks to seek continuously across trials. The
   reconstruction and inspector follow the cursor, with both study and trial clocks.
   **Spacebar** toggles play/pause, including after scrubbing; holding it does not repeat.
   Text fields and modal dialogs retain normal typing. Use sample/event buttons for exact
   stepping, or toggle **Selected trial / Whole session** to narrow the range.
   Track tick labels are relative to that range;
   original session timestamps remain in raw data. Playback audio is supported at 1×; seeking,
   stopping, cancellation and overlapping saved effects use the shared audio plan.
4. **First person** follows recorded tracked head poses. Its primary gaze overlay
   is an orange dot at the nearest intersection with the finite inferred study-wall
   panels. Invalid/stale gaze, unknown layouts and rays missing the panels show no
   dot. It never snaps to an object or target. Third-person views retain the gaze
   ray; the optional raw/pre-alignment overlay retains a thin diagnostic line.
   This display projection does not change recorded origins, directions or data.
   **Third person** supports
   right-drag and wheel zoom; **Reset view** restores the default orbit. Camera controls
   stay visible even before a recording loads. **Overhead** is a second researcher view. Toggle gaze,
   controller and target overlays independently. **Walls** controls the inferred wall outlines;
   **Degrees** separately controls the angle labels, including when walls are hidden.
   The seated human and chair are explicitly illustrative; the body/chair estimate is not recorded torso tracking.
5. **At cursor** shows sample age, tracking, trigger state, current saved speech and
   gaze provenance. **Show raw observation** expands the detailed sample including hits.
   Unknown tracking is `-1`; absent legacy fields may appear as schema defaults in
   the decoded object. The source file remains authoritative.
6. **Final data** automatically loads a successfully generated saved-run export.
   **Choose CSV export…** also loads an existing directory with `manifest.json` and
   `trials.csv`, generated by `analysis/trial_csv.py`. The app verifies source hash,
   recording ID, logical trial identity (including practice), participant, voice,
   run and block. Missing/unmatched rows stay unavailable. Final results and repeated
   block survey responses are explicitly distinguished from cursor measurements.

### Timeline and trial export (desktop redesign)

**Implemented locally:** speech segments follow the saved audio plan, including recorded
cancellations and missing-asset warnings. The gaze lane displays usable replay observations
with the existing 100 ms display freshness limit; invalid samples and longer gaps break the
lane. These are not fixations or acquisition-rate measurements. Event markers link to exact
record sequences. Playback speed cycles through 0.25×, 0.5×, 1× and 2×; volume and mute apply
only to this standalone player. Saved audio plays at 1× only.

**Export trial data…** works on a stopped, saved recording. It creates a new folder in
`~/Documents/Trial Review Exports/` containing samples.csv, events.csv, objects.csv,
observations.jsonl, a source-hash manifest and a README. Initial object positions are in
objects.csv; subsequent transforms and supported replay fields are included in decoded JSON; keep the original recording for byte-exact source data. This is a
recorded-observation subset, not a replacement for the full replay/audio package or the
separate eye-acquisition stream. A loaded, hash-matched performance/subjective row is copied
to final-summary.csv; absent summaries and questionnaires are never fabricated. Export is
disabled for a growing live recording: open its stopped saved copy first.

**Follow live** remains a separate top-level action. **Wi-Fi settings** shows endpoint,
archive progress and receiver controls. Scrubbing leaves live follow while archiving continues.
The waiting viewport displays only the large centered “Waiting for headset signal” heading.

Launch a specific package and summary from a terminal:

```bash
open '/path/Trial Review.app' --args \
  --recording '/path/package/recording.jsonl' --summary '/path/export'
```

## Reconstruction limits

This is interactive telemetry reconstruction, not recorded passthrough video. The
65° camera FOV and unlit materials are illustrative, not exact headset optics or
lighting. Saved meshes, object transforms and head poses are preserved. The
100 ms display freshness rule is inherited from the existing viewer; untracked
or stale head poses show a labeled overview, never an invented first-person pose.
Pauses and recorded visibility are retained. No pose interpolation is performed.

Only saved WAVs are played; missing assets are reported, never synthesized. Software
playback times do not prove acoustic onset or reproduce interruption envelopes
exactly. Truncated tails are visibly labeled and only the valid prefix is replayed.
MP4 export, real-room passthrough reconstruction and browser embedding are not part of this version.

## Shared source and isolation

`prepare.py` copies the existing replay data/state/reader/audio and their pure data
or policy dependencies. It adapts the Editor renderer's camera/texture ownership
and removes the Editor-only audio output while retaining the existing state and
playback logic. Exact adapter boundaries fail closed if shared source changes;
all copied source hashes are recorded. These are generated copies, not another
committed version of the replay algorithms.

App code lives in `Runtime/`, build/verification in `Editor/`. Only the generated
project has a replay scene and minimal built-in Unity modules. No changes to the
study's `Assets/` or package configuration are needed.

## Verification

The build verifies recorded camera pose, stale/unknown tracking, pauses, backward
seek, trial changes, exact source-sequence cursor, audio cancellation/seek/speed,
quoted multiline CSV parsing, successful summary joins and wrong-hash rejection.
Optional real-package verification checks sampled recorded-head poses and summaries.
Graphical inspection of the built app remains necessary for rendering/layout;
these checks alone do not validate headset acquisition or research measures.

## Human, chair and Vive controller models

**Implemented locally:** Kenney’s [Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists)
provides the CC0 human mesh/skin. The minimal unmodified source subset, license and
download provenance are in `ThirdParty/KenneyProtagonists`. The seated pose, simple
headset and swivel chair are local illustrative additions. The avatar is hidden in
first person; **Human model** toggles it in third-person/overhead views.

The official Focus 3 L/R meshes are copied at prepare time from the existing HTC
VIVE OpenXR SDK cache. HTC source meshes are not vendored into this tool’s source;
its license notice remains attached and is included in the built app, alongside
Kenney’s notice. See the [HTC SDK terms](https://developer.vive.com/resources/downloads/licenses-and-agreements/english/).
Controllers are displayed at their independently recorded `position`/`rotation`
when `available` and `tracked == 1`, even if the recorded ray is unavailable.
Their mesh origin alignment is illustrative and still warrants hardware visual
comparison; the recorded transforms/rays are never changed to fit the human hands.
**Vive controllers** toggles both meshes and the right-controller ray. The left-controller ray is omitted; historical left-hand data and its model remain available.

The **chair seat follows torso yaw**, with a stationary pedestal/base orientation;
the seated model retains recorded head rotation, while its chair remains fixed at the layout center. First-person view retains recorded head position too. No torso sensor
was recorded. A display-only estimate allows ±35° neck yaw before turning the
torso/seat, at up to 90°/s using recorded observation intervals. These values are
visualization settings, not physiological thresholds. The estimator resets at
trial/pause boundaries, invalid head observations and >100 ms gaps. It is
precomputed in source order so rewinding does not change the pose. The body and
controllers are hidden when their required recorded tracking is absent/stale.
Hands now follow controller grip anchors through deterministic two-bone arm IK.
Elbows and grip alignment are illustrative, not recorded body kinematics. The
stylized arm lengths can extend up to 1.5× to accommodate ordinary reach; extreme
poses are clamped and reported as **arm reach limited**, without moving controllers.
Missing controller tracking returns that arm to its neutral illustration. There is
no claimed finger tracking or full-body reconstruction.

**Corrected gaze** is enabled for exact recordings listed in
`Resources/GazeCorrectionProfiles.json`. P020 uses `p020-local-x-unmirror-v1`:
undo the old position-only X reflection in the recorded XR-origin frame, then
return to recording coordinates. Direction is unchanged. This preserves the
actual eye–head offset; it does **not** move the ray to the avatar head. Matching
requires recording ID, build GUID and SHA-256. Missing/invalid tracking-origin
reference hides the corrected ray; there is no cosmetic fallback for that file.

**Raw gaze** remains the original saved Unity gaze pose. For unreviewed recordings,
**Eye ray (display)** retains the explicit cosmetic head-origin illustration and
reports raw-origin displacement. Neither path edits the recording. Rays appear
through active cross charging (including zero charge) and search, with the existing
tracking/freshness/pause checks. Recorded hit IDs/highlights and trial summaries
remain historical; they have not been recomputed from corrected geometry.

To export auditable derived coordinates without replacing source files:

```sh
python3 tools/trial-review/correct_gaze.py /path/to/recording.jsonl /new/output/directory
```

This requires an exact reviewed profile. It writes `derived-gaze.csv` and
`manifest.json` with source hash, version, original and corrected origins,
unchanged direction, unavailable rows, head residuals and cross/target angles.
It refuses an existing output directory. P020 has 9,703 sample rows, 8,134 corrected
tracked samples, and a disclosed incomplete trailing row. Median eye–head distance
falls from 1.655 m to 0.0241 m. For the 1,564 recorded readiness-inside samples,
median angle to the cross changes from 20.66° to 8.40°. Gate membership is not
independent fixation ground truth; this establishes coordinate recovery, **not
calibrated gaze accuracy**. Target-angle columns describe geometry, not intended
fixation. Further spatial validation is still required before scientific use.

Optional launch arguments `--view third-person` or `--view overhead` and
`--time 30` select an initial perspective and recording time. Without them,
recorded-head first person remains the default.


## Wall and room toggles

**Implemented locally:** **Walls** toggles wire outlines and **Degrees** independently toggles signed wall labels: 0° forward, +45°/+90°/+135° to the right, −45°/−90°/−135° to the left, and ±180° at the rear. Either overlay can be shown without the other. Labels use the saved layout orientation (0° is forward), stay readable in all camera views, and apply to both live and saved replay. They are viewer overlays, not evidence that the participant saw the guides. The outlines represent eight-wall
rotational layouts. Radius is inferred from snapshot object-plane positions; height
uses recorded search-plane bounds when supplied, otherwise spans the snapshot objects.
**Room** independently toggles the surrounding environment. New recordings tagged
`roomEnvironment=neutral-room-v1` use the shared runtime room: 10 × 10 m floor,
6 m walls at 5 m from center, wall sRGB (0.38, 0.38, 0.38), floor (0.32, 0.32, 0.32),
unlit/no shadows and no colliders. Historical recordings without the tag retain the
illustrative room, including its one-metre floor grid; no historical room is asserted
to have been seen by a participant. Both viewer toggles still start enabled; viewer
outlines are diagnostic overlays even though headset outlines now default off.
Room walls are single-sided for an open cutaway from the external orbit camera.
These are virtual reconstructions, not a captured physical room or evidence that the wall
visuals were enabled during acquisition. Unknown/non-octagonal layouts receive
only the generic backdrop, not invented study-wall assignments. Context follows
the snapshot origin/rotation and is rebuilt on trial changes and backward seeks.
The recorded object transforms, rays, hits and measurements remain unchanged.

### Live waiting screen

While following live without a fresh headset signal, the viewport shows a shaded
UCF-gold pyramid rotating once every 30 seconds with a subtle floating motion,
plus the actual waiting reason: Wi-Fi unavailable, archive catch-up, headset app paused, head tracking unavailable, or no fresh headset samples. The animation uses wall-clock time independently
of trial playback and disappears when fresh live data arrives. An official UCF
stacked logo sits in the bottom-right corner of the waiting viewport; provenance
is in `Resources/UcfLogo-source.md`. The prop is active only during its isolated
camera render and never appears in the reconstructed study scene.

### Live mode switch

The top-right **Live mode** switch is gold/right when following incoming data and
gray/left while reviewing a saved cursor. Turning it on connects or resumes live
following. Turning it off pauses playback at the current cursor while the receiver
continues archiving. Use **Stop receiver** in Wi-Fi settings to stop the transfer.
Seeking in the timeline also switches live following off.

### Voice clips prepared ahead of playback

**Implemented:** after voice treatment, verification and final level matching,
the headset exports the prepared library into that session's replay `audio/`
folder during Loading, one clip per frame. Later background/on-demand clips are
exported when admitted for playback. Raw provider downloads are not transferred.
The replay file stays empty until the first real trial transition: preparation
does not change the recording origin, study clock or trial measurements.

The Wi-Fi receiver copies complete WAVs before bulk telemetry and yields between
transfer batches to revisit playback events. Start events reference the same
immutable clip paths; the viewer follows recorded session timestamps, gains,
fades and cancellations. Cached files alone never start speech. Headset guidance
does not wait for Wi-Fi or desktop acknowledgement. Late clips join at the current
cursor while still active; already-ended speech is not replayed out of sequence.
This is buffered reconstruction of software playback, not acoustic onset
measurement or a guaranteed zero-latency audio stream. New headset recordings
require the updated app; older recordings remain compatible.

### Guidance state at the cursor

New recordings contain a captured guidance state on each sample and `guidance_state` transition events. The **At cursor** inspector shows the stage, preceding stage, transition reason, permitted action, revision, direct gaze hit, vicinity winner, and right-controller hit. **Events** and the timeline include state transitions; clicking an event seeks its exact sequence. State markers label reconstructed objects in orange (direct gaze), gold (captured vicinity winner), and blue (right controller). The State markers toggle hides these labels independently of ray visibility.

Captured vicinity winners take precedence over geometry reconstructed by the viewer. Legacy recordings retain the previous reconstructed shine, explicitly labeled **not recorded**, and have no invented guidance state or state markers. “None” means a valid observation without a hit; invalid tracking, missing state, stale samples and a disconnected live headset are shown as unavailable. Seeking backward restores the snapshot at that cursor rather than preserving later state. Ring shape remains an estimated reconstruction even when its winner was recorded.
# October 20: state inspection and evaluation

**Implemented:** the At cursor inspector shows recorded central guidance state and separate gaze-hit, vicinity-winner and controller-hit markers. Legacy snapshots remain unavailable, not inferred. Lifecycle states can persist after the last pose sample; markers cannot.

The **Evaluation** tab shows automatic checks, selected/proposed versus actually played phrases, and separately labeled Astra judgments. Run CSV processing creates the local evaluation, human-rating worksheet and request-only benchmark. Automatic Astra evaluation is enabled by default for this requested workflow and can be disabled in the tab. It uses saved Codex ChatGPT authentication, account limits and an explicit Astra request; the Mac needs internet access. It does not run in the headset guidance loop.

If an older run lacks a complete schedule/summary pair, the full CSV export still reports that error. Guidance evaluation can process the recording independently; it does not fabricate missing performance rows or weaken CSV validation.

Results are stored separately under `~/Documents/Trial Review Evaluations`, verified against the exact exported inputs, and linked to the dataset. Pending, failed and unavailable results never become passing scores. See [evaluation documentation](../../docs/research/guidance-evaluation.md) for provenance, privacy, caching, human review and training limitations. Launch with `--evaluation` to open that tab directly.


### Live preparation and paired evaluations

**Implemented:** the headset publishes an atomic `PreparationLogs/live-headset-state.json`
heartbeat, separate from participant recordings. Live mode shows **Preparing experiment**
and its reported 0–100% progress before a trial/header exists. The receiver reads this
small optional file independently of archive copying. Two distinct heartbeats establish
freshness after connection; five seconds without a change makes its status unavailable.
Missing telemetry in older builds never becomes invented loading progress. Preparation
freezes the previous replay and does not advance study clocks.

**Implemented:** the Evaluation tab can import `guidance-paired-pilots.json` beside its
local evaluation or from `~/Documents/Trial Review Evaluations/`. It labels this as a
separate offline dataset, displays variant metrics and representative timelines, and
keeps unknown evidence visible. Policy replay timings are simulated, not headset or
acoustic measurements. Paired choices do not predict what a participant would do.

Sequence evaluation separates first **observed qualified central-state action** to
software playback from qualified **request** to playback. The former requires fresh
recorded observations; gaps leave it unknown. Recent speech, state/action transitions,
and the last fresh state at software start accompany advisory Astra judgments. The
request-only `guidance-benchmark.jsonl` remains unchanged in purpose; the separate
`guidance-review-cases.jsonl` also covers local confirmations. `--case-id` on the judge
limits a review to explicit cases while retaining full input hashes and reporting
selected/available coverage. Model ratings remain advisory, never human ratings.


### Fixed seated participant in third-person view

The illustrative chair stays at the trial layout center in X/Z. Its height is fixed
from the first usable recorded head sample in that trial, resolved in recording
order so scrubbing does not reposition it. Torso/seat yaw and head rotation still
animate; head translation does not drag the entire human and chair around. The
third-person/overhead orbit center is fixed too. Controller poses, gaze rays,
object positions, first-person head pose and exported measurements remain recorded
data; the seated model is an illustration, not full-body tracking.
# Live viewer audio controls (October 20, 2026)

The Wi-Fi toolbar includes **Mute voice** and a **0–100% volume slider**. The transport bar also shows the current percentage. These affect only audio in Trial Review (including recorded effects), not the headset, saved WAVs or recorded levels. Both settings persist between launches and remain usable before the first trial. Changing volume or mute does not restart speech.

While following live data, a brief scene-buffer underrun no longer stops an already playing WAV. Tracking/transport loss, explicit pause and stale-source handling still stop playback. Delayed audio assets or cursor catch-up can still omit a line that has already ended before it arrives; saved replay remains the complete evidence path. This is reconstructed software playback, not a live headset audio stream or acoustic timing measurement.

## Live speech coverage validation (October 20, 2026)

**Implemented:** speech-aware catch-up, a five-second late-WAV wait, explicit
unavailable-segment counts, and independent detached review windows. Output logs
include the source playback sequence and starting frame offset. Intentional
joining/resuming at the live edge can omit earlier history; this is not a replay
of everything spoken before connection.

Run `build.py --verify-audio-recording /path/recording.jsonl` with the usual build
arguments to compare the previous catch-up clock with the speech-aware clock.
On recording `20261020T220003_e3346253c3754cbe8f94240b6ac1b989`, a deterministic
1.5-second delivery-batch simulation started 24/30 available audio segments with
the earlier clock versus 30/30 with the new clock. Zero versus 30 started at frame
zero. These are simulated transport/software-output observations, not headset or
acoustic measurements, and do not identify which phrases the wearer previously
reported missing. Separate checks cover late WAV arrival, bounded missing assets,
cancellation, partial live tails and snapshot isolation. A new wearer run remains
the end-to-end check for actual Wi-Fi delivery and listening.

### Search deadline

New headset recordings emit `trial_timeout` after 15 active-search seconds. Trial Review labels these trials **Time up**, hides the expired scene at that event, and retains the recorded encouragement/subtitles. A timeout does not imply a trigger selection or session failure. Historical recordings retain their original durations; the viewer does not impose the new deadline on them.

### Eye validation controls

Live mode → **Eye validation** starts isolated spatial, saccade, control, arithmetic,
search or drift exercises. Enter the participant code and IPD measurements; choose
ABBA/BAAB arithmetic order. Live status shows the exercise and remaining time.
Begin, answer/rating and stop commands are bound to the current headset session
and validation revision. Raw recordings sync in `EyeMeasurements`; see
[the protocol and first-device test](../../docs/testing/eye-validation-exercises.md).

## Development screenshots

Run the installed desktop bundle with an absolute destination directory:

```sh
open -n "$HOME/Applications/Trial Review Redesign 20261028.app" --args \
  --capture /tmp/trial-review-shots \
  -screen-width 1440 -screen-height 900 -screen-fullscreen 0 \
  -logFile /tmp/trial-review-capture.log
```

Capture forces the bundled synthetic demo and in-memory development intake/catalog fixtures. It bypasses live initialization, ADB/receiver launch, polling, microphone access, audio playback/workers, intake jobs, exports and automatic evaluation. Other launch modes are ignored; real UI input is ignored during capture. The output is numbered PNGs plus Unity's log, and the app exits after `TRIAL_REVIEW_CAPTURE_PASS`. `TRIAL_REVIEW_CAPTURE_INPUT_PASS` confirms the existing keyboard and timeline handlers passed synthetic GUI-event checks. Use 1280 × 800 for the small-window pass. These captures verify presentation, not real consent, recording, cloning or headset operation.
