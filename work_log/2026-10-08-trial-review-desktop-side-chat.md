# Trial Review desktop app — side chat

**Implemented locally, October 8, 2026.** User explicitly requested building the
standalone desktop replay viewer after the CSV export work. All new app source is
under `tools/trial-review/`; no study Assets, scenes, build settings, device app or
credentials were changed. No commit/push.

## Delivered

- Standalone macOS Unity app, recorded-head first person by default, orbit and
  overhead, trial/event navigation, timeline scrubbing and exact sample stepping.
- Shared saved-audio reconstruction, including cancellation/seek, 1× only.
- Validity/freshness-aware camera and overlays; historical decoded data is labeled.
- Final performance/subjective panel with source hash and exact trial identity
  joins; block survey context is separated from cursor values.
- Read-only package browser plus `--recording` / `--summary` launch arguments.
- Bundled clearly synthetic 3D fixture; no participant data included in app bundle.
- Isolated generated project/build script that copies hashed existing replay
  sources and adapts only Editor rendering/audio ownership; excludes live study
  bootstraps/XR/settings and clears inherited Git variables before Unity.

## Output

`/Users/pedro.poveda/.codex/visualizations/2026/10/09/01a11e0e-0efa-77c2-9214-3a4ff9c75e91/outputs/trial-review/`

Use **Trial Review 1.0.app** (final), or **Open P20.command** to open P20 with its
matching export. Earlier **Trial Review.app** is an obsolete test build. Final app
was launched with P20. `build-verification.log` and `shared-source-sha256.json`
preserve evidence. Source instructions: `tools/trial-review/README.md`.

Copied the historical P020 package ending `72945966761b49a0941c46c758a073df` and its
matching GazeData run_001 from the connected headset without altering device data
or running/stopping the study. Raw files remain in output/recordings. The replay
has 9 trial snapshots (2 practice, 7 measured) and a truncated final record; the
app prominently labels this and preserves the valid prefix. The CSV export has
22 scheduled/practice rows, not 22 fully recorded trials. Missing surveys remain
unavailable.

## Verification

- Mac universal build succeeded using Unity 6000.3.10f1.
- Three Python tests pass for isolation, refusing unrelated destinations and
  fail-closed adapter boundaries.
- Unity checks pass for exact recorded camera, unknown/stale tracking, pauses,
  rewind/trial switching/source sequence, saved audio cancellation/seek/speed,
  quoted CSV, exact summary join and wrong-hash rejection.
- P020 verification checked 98 recorded-head poses across the 9 saved trials
  and joined the actual matching v2 CSV export.
- Built app visibly rendered P020 objects/head view, and the final build visibly
  displays recorded-head readiness and prominent truncated-recording warning.
  Native automation clicks did not reliably reach Unity controls; full hands-on
  button/audio usability validation remains appropriate. No runtime exceptions
  were reported in the inspected player log.

## Scope/remaining

Interactive reconstructed 3D, not MP4 export or passthrough video. Fixed 65°
preview FOV, simplified unlit materials, no inferred body/avatar or pose
interpolation. Not a notarized distribution release. Source and artifacts remain
local/uncommitted. No Android Build And Run, because this isolated desktop tool
must not interrupt the main thread's headset work.


## Human + Vive controllers + swivel chair follow-up

User requested a human model, real Vive controller models and a swivel chair whose
turning follows the torso. **Implemented locally:** CC0 Kenney Protagonists human
with seated static limbs and an illustrative chair/headset; official Focus 3 L/R
meshes from the installed SDK, copied only into the isolated generated build
project. Both vendors' license notices are bundled. Source provenance is recorded.

Chair seat and torso share estimated yaw; head pose remains independent. A
±35° display neck allowance and 90°/s bounded torso turn estimate is precomputed
in source order, making seek/rewind deterministic. These are visual settings, not
research thresholds. Chair base stays fixed in yaw. No actual torso or chair motion
was recorded; the app labels this explicitly. Arms/hands stay posed and do not
pretend to match independently tracked controllers. Head/model visibility obeys
freshness, controller tracking is separate from ray validity, and first person
hides the body. Shared recording values/export measurements are untouched.

**Latest build: Trial Review 1.3.app**, in the output directory above. Replaced
Open P20.command to use it and added Open P20 Third Person.command (time 30s).
Launched final build with P020 in third person and visually verified seated human,
swivel chair and controller geometry. Native build tests pass for head anchor and
rotation, controller pose, missing/invalid/gapped tracking, first-person visibility,
model toggles, small independent head turns, torso-rate bound and deterministic
rewind. Existing 98 real-camera-pose/CSV tests and the three Python isolation tests
pass. Model build logs/source hashes saved beside app.

No study Assets/settings, Android deployment, commit or push.


## Hands and eye-origin overlay — Trial Review 1.4

User requested hands follow the floating controllers and gaze originate at the eyes.
Implemented `TrialReviewArm` with deterministic two-bone IK, neutral reset on invalid
tracking, optional length adaptation capped at 1.5x and disclosed reach limits.
Palm/controller anchors are approximate visual grips, not recorded hand tracking.
Head/controller transforms are preserved. Eye ray starts at avatar head/eye anchor,
preserving recorded world direction; Raw gaze remains separately toggleable.
Overlay reports the raw-origin displacement (P020 ~1.52 m at 30 s). This needs a
main-thread coordinate-space investigation; visual reanchoring is not a data fix.
New checks passed along with existing suite and real recording checks. Visually
verified both raised arms meeting controllers and orange ray leaving the headset.
No exceptions in player log. Latest app: Trial Review 1.4.app in the existing output
folder; both P20 launchers updated. No main study Assets edits or Android build.


## Room and study walls — Trial Review 1.5

Added Runtime/TrialReviewEnvironment.cs: independent Study walls / Room background
toggles below viewport, both enabled initially. Inferred connected octagonal
outlines from rotational snapshot object-plane distances and vertical extent.
Neutral room includes floor/grid and four inward-facing surfaces for an orbit
cutaway. No physical room reconstruction claimed; labels make this explicit.
Rebuilds on trial identity changes, respects snapshot transform, never edits data.
Unknown layouts receive only generic room, not fabricated study walls.
Unity environment checks and all prior build checks passed; 3 preparation tests
passed. Visually checked P020 with both outlines and floor/backdrop rendering.
Trial Review 1.5.app and updated P20 launchers are in the existing output directory.


## Pre-charge eye ray — Trial Review 1.6

The orange avatar-origin ray was incorrectly gated solely by State.Visible, hiding
it during readiness when the cross is charging and study objects are hidden.
Added fresh, matching-trial, active/unpaused readiness as a visibility condition,
using the same phase-boundary rules as the cross. Zero charge is included.
Tracking validity/freshness and the Eye ray toggle still apply. Added synthetic
readiness checks and real P020 check at 0.5474 s / 8.34% charge. No source recording
changes or main study Assets edits.

Built Trial Review 1.6; P020 visually verified at 0.55 seconds: orange eye ray and partially charged cross visible while objects are hidden. Updated both P20 launchers to 1.6.


## Right-controller ray only — Trial Review 1.7

User requested removal of the irrelevant left-controller ray. The isolated renderer
adapter now passes null to its left-ray drawing call. Right-controller and eye rays
are retained, as are historical left-hand data and the controller/hand mesh.
Added both-tracked ray suppression/data preservation verification. No main study
renderer or acquisition changes.

Built 1.7 and visually verified P020 at 0.55 s: cyan left-controller line absent, purple right-controller and orange eye rays present. All Unity verification and three Python tests passed. P20 launchers updated.


## Chair clearance — Trial Review 1.8

Shifted chair construction origin 0.14 m backward from hips, moving seat/backrest,
armrests and base together. Avatar/tracked transforms unchanged. Existing Unity
suite and real P020 checks passed; visually verified at P020 30 seconds. App open
and launchers updated to 1.8. Kenney Protagonists archive contains the current
red-shirt skater plus cyborg, black-shirt skater and white-suit appearances on the
same rig; preview copied to output kenney-avatar-options.png. No replacement chosen.


## Larger display selectors — Trial Review 1.9

User requested bigger selectors under the 3D view. Replaced default tiny GUILayout
toggles with full-card GUI.Toggle controls (42px height, 16pt text, 22px checkbox).
Responsive 2–4-column grid reserves its height from the viewport, retaining all
seven options and the inferred-room note. No replay or study-data logic changes.

Built 1.9; existing Unity suite and P020 verification pass. Visually checked larger selectors and complete labels on P020; launchers updated to 1.9.


## Proper historical gaze correction — Trial Review 1.10

P020 exact SHA256 cb68e2b79c69ab421b7f6c4562c669dfcb91f0f268e1d09b7cd74ef4b0ede362
is reviewed in Resources/GazeCorrectionProfiles.json. Build and recording ID must
also match. Runtime TrialReviewGazeCorrection reflects the saved gaze origin about
the saved XR-local x=0 plane, preserving direction and the ~2.4 cm eye–head offset.
Actors uses that derived origin in place of cosmetic eye anchoring for this file.
Raw gaze and historical hit IDs remain unchanged; unknown files get no automatic
correction. Missing/invalid XR pose means no corrected ray, not a head fallback.

correct_gaze.py exports raw/derived coordinates, availability, historical IDs,
head residuals and readiness/target geometric angles with a provenance manifest.
Output: p20-gaze-correction-v1/ under the existing visualization directory. Includes
9,703 sample rows, 8,134 corrected rows, incomplete-tail disclosure. Readiness-inside
angle median 8.40 degrees after correction remains a limitation; no calibration
or fixation validation claim, and no historical outcome metrics recomputed.

Built/opened 1.10; visually verified corrected-origin line and 2.3 cm instantaneous eye–head offset at P020 30 s. All existing/new Unity checks passed including 8,134 tracked-sample regressions and source preservation. Six Python tests pass; derived CSV count/blank-gap/hash checks pass. Launchers updated.
