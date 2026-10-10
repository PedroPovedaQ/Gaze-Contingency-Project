# Trial Review desktop app

## Live Wi-Fi mode

**Implemented locally, October 10, 2026.** The desktop viewer can receive the
headset's existing recordings over wireless ADB, reconstruct the current run,
and archive new runs automatically. No different study build or Unity server is
required. The headset remains the source of truth and continues recording if
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
   replay growth stops so it does not block live transfer. Initial catch-up/reconnect
   joins the buffered edge instead of playing through the backlog.
   **Waiting for headset signal** replaces the scene while no fresh tracked samples
   are arriving, including paused/closed runs, tracking loss and disconnection.
   A connected ADB transport alone does not count as a live headset. After two seconds
   without observed source-file growth the view waits; an explicit pause marker waits
   immediately. Normal fresh chunk parsing does not interrupt an already following view.
   An initially offline headset is retried automatically at the configured address.
   Repeated **Follow live** clicks do not rewind an already-following view.
4. Select first person, third person or overhead as usual. Scrubbing, selecting a
   trial, pausing or changing playback speed leaves follow mode while archiving
   continues. **Follow live** returns to the latest run. Saved WAV playback at
   1× follows the recorded events, including interruptions. Late/missing assets
   are reported; missing speech is never synthesized.
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
**Open run…** or **Follow live** controls instead of starting a second instance.
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
study, invokes voice providers, starts trials or uploads to external services. The build contains only synthetic example
data; participant recordings stay outside the application bundle.

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

## Open a saved recording

New recordings with `gazeRayVersion=recorded_used_and_unaligned_ray_v1` preserve the actual used eye ray and the selected source before quick alignment, both in the same recording coordinate frame. **Recorded gaze** uses the saved eye origin/direction without moving it to the avatar's head or applying the alignment again. **Before alignment** shows the separately saved pre-correction ray. The cursor inspector reports source, alignment ID/status, applied flag and yaw/pitch correction; stepping backward restores those recorded values, and invalid/stale gaze stays hidden. The standalone build verification exercises the live quick-alignment engine, recorder mapping, JSON round trip and first/third-person renderer with synthetic data.

These are additive schema-2 fields: older recordings remain readable. P020's exact-recording legacy X-origin recovery is unchanged. Old recordings without alignment evidence cannot acquire a retrospective quick calibration. A passed quick check is not task-wide accuracy validation.

1. Launch the app; the bundled **synthetic** demo is the default.
2. **Open run…** (or the session selector) opens the file browser. Choose a replay
   package and select `recording.jsonl`. Loading a large recording is synchronous and may take
   time; recordings are bounded to the shared reader's 1 GiB/two-million-record limit.
3. Search trials by number, voice, ID, block or angle; filter All / Study / Practice.
   Select a trial from the left. **Play**, drag the timeline ruler, click a speech/event
   segment, or use the sample/event stepping buttons. Toggle **Selected trial / Whole session**
   to change the displayed time range. Track times are relative to that range;
   original session timestamps remain in raw data. Playback audio is supported at 1×; seeking,
   stopping, cancellation and overlapping saved effects use the shared audio plan.
4. **First person** follows recorded tracked head poses. **Third person** supports
   right-drag and wheel zoom; **Reset view** restores the default orbit. Camera controls
   stay visible even before a recording loads. **Overhead** is a second researcher view. Toggle gaze,
   controller and target overlays independently. **Walls** controls the inferred wall outlines;
   **Degrees** separately controls the angle labels, including when walls are hidden.
   The seated human and chair are explicitly illustrative; the body/chair estimate is not recorded torso tracking.
5. **At cursor** shows sample age, tracking, trigger state, current saved speech and
   gaze provenance. **Show raw observation** expands the detailed sample including hits.
   Unknown tracking is `-1`; absent legacy fields may appear as schema defaults in
   the decoded object. The source file remains authoritative.
6. **Final data → Choose CSV export…** loads a directory with `manifest.json` and
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
the head retains its separate recorded position and rotation. No torso sensor
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

**Implemented locally:** **Walls** toggles wire outlines and **Degrees** independently toggles the 0°–315° labels. Either overlay can be shown without the other. Labels use the saved layout orientation (0° is forward), stay readable in all camera views, and apply to both live and saved replay. They are viewer overlays, not evidence that the participant saw the guides. The outlines represent eight-wall
rotational layouts. Radius is inferred from snapshot object-plane positions; height
spans the snapshot objects. **Room background** independently toggles a neutral
floor, one-metre floor grid and four outer room surfaces. Both start enabled.
Room walls are single-sided for an open cutaway from the external orbit camera.
These are visual aids, not a captured physical room or evidence that the wall
visuals were enabled during acquisition. Unknown/non-octagonal layouts receive
only the generic backdrop, not invented study-wall assignments. Context follows
the snapshot origin/rotation and is rebuilt on trial changes and backward seeks.
The recorded object transforms, rays, hits and measurements remain unchanged.
