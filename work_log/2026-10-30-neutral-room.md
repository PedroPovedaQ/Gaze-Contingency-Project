# Neutral participant room — October 30, 2026

## Implemented

Before this change, `m_ShowDegreeGuides` defaulted true: participants saw eight thin search-plane outlines and degree labels unless the wrist/live operator Walls control hid them. The earlier thesis room fixture explicitly hid labels but retained frames. On a headset the existing mixed-reality switch controls native passthrough; the old pale template backdrop seen in editor figures is not a photograph of the physical room.

The default is now false. The eight mathematical search planes, 1.5 m radius, 168 objects, placement, readiness, sectors and guidance are unchanged. Operator diagnostic controls can still explicitly reveal the outlines and labels.

`StudyRoomEnvironment` creates a **10 × 10 m floor and four 6 m tall walls**, each **5 m from center**, aligned to seated yaw and the existing captured floor height. The floor is y=0 in the figures; runtime uses the tracked value. The large room clears the object ring by more than 3 m. Walls are inward-facing, so external cameras see a cutaway. There is no ceiling.

Materials are opaque, texture-free, unlit neutral gray: walls **sRGB (0.38, 0.38, 0.38, 1)** (approximately #616161), floor **(0.32, 0.32, 0.32, 1)** (approximately #525252). They have no specular response, cast/received shadows or probe contribution. Existing object materials and scene lighting are retained. No voice-dependent branch or material exists; the room is created once for the search session and stays through both voice blocks.

The old template environment mesh initially occluded parts of the new room in GPU captures. The runtime now temporarily suppresses its renderers with `forceRenderingOff`, retaining the existing fade state and restoring the previous visibility on room destruction. The room is removed with the playfield after search. Calibration and voice-confirmation preparation occur before its creation; their existing thesis images and the separate desktop voice-selection image have unchanged backgrounds, so none needs re-rendering.

New room panels are meshes constructed without primitive colliders, on Ignore Raycast. They never have a collider, rigidbody or interactable. They cannot intercept gaze/controller physics queries or select events. The readiness cross remains on the unchanged inner plane, closer than every distant wall. Right-controller ray plus trigger remains the sole selection method.

New trial records carry the optional schema-2 field `roomEnvironment=neutral-room-v1`. Trial Review copies the same room source and renders it at recorded origin/yaw/floor. Its existing Viewer walls and Room toggles stay independent (viewer overlays still default enabled). Old schema-1/2 recordings without the field retain their previous illustrative room and inferred/recorded search bounds. No saved recording was edited.

## Verification

- Full `StudyVerificationChecks.Run` in Unity 6000.3.10f1 passed with GPU and `-nographics`: 27 PASS markers each. Added checks cover hidden defaults, five visual surfaces, nonzero floor/yaw, no colliders/shadows, unfiltered rays to all eight top/bottom locations, unchanged readiness-plane projection, independent debug toggle and restoration of both previously hidden/visible backdrops. Existing controller, gaze filtering, readiness, guidance, 64-trial schedule and legacy replay checks pass.
- Backdrop restoration initially failed in edit-mode fixtures; `[ExecuteAlways]` now permits the visual component's cleanup in editor captures as well as runtime. The failure was observed before the fix; both final integration runs pass.
- 128 desktop Python tests and 355 analysis tests passed. First analysis invocation used system Python without PyWavelets; rerunning in the repository `.venv` passed all 355. Synthetic test data only.
- Trial Review checks and standalone build passed (25 PASS markers), including new-room/legacy switching, rewind, parsing and independent toggles. Built `~/Applications/Follow My Voice Neutral Room 20261030.app`; the existing Study Intake app was not replaced.
- APK build succeeded in 84.8 s, reported 1168.1 MB. URP Unlit is explicitly present in the shader compilation log. Both VIVE package exceptions (`VIVEFocus3Feature.GetOpenXrExtensionInfo` and `ModifyAndroidManifest.AddOpenXRFeatures`) also occur in the prior schedule build log; they did not stop this build. No vendor or project-setting change retained. Build-hook preloaded-asset cleanup was restored to the clean starting settings.
- Required `scripts/refocus-unity-and-build-device.sh` was attempted. The wireless helper found no online Focus Vision, so no installation or launch was attempted. **The APK still needs installing.** This run does not claim wearer verification.
- `git diff --check` passed. Final local review covers correctness, tests, resource cleanup, additive replay contract and project standards; see `receipts/2026-10-30-neutral-room/review.md`.

## Renders and contrast

Output: coordination `thesis-unity-shots/room/` under the `thesis-main-sync` checkout's `.context/coordination/`. `Assets/Editor/ThesisRoomCapture.cs` produced final `room-first-person.png` and `room-wide.png`, plus four `contrast-{red,blue,yellow,green}.png` diagnostic images. All are 2560 × 1440, 4× MSAA, Apple M4 GPU, and every final PNG was opened and inspected. The previous requested pair is preserved byte-for-byte as `room-first-person-outlined.png` / `room-wide-outlined.png`, with its original receipt/index/manifest under `outlined-*`.

The two requested cameras keep their prior position/FOV: seated (0,1.2,0), +Z, 80°; wide (4,5,-5), look-at (0,1.05,0), 34°. 168 real factory objects; layout seed 42000; synthetic `THESIS_ROOM` identities from the current schedule. No burned-in labels, reticle or target highlight. First-person viewport-edge clipping and object overlap in the wide projection are perspective effects, not room occlusion.

The diagnostic images put every sphere/cube/pyramid/cylinder/star/capsule in each of the four actual colors at both y=0.1 and y=2.1036 m. All 48 examples have separate visible silhouettes, chromatic contrast, and positive clearance above the floor; the top row is backed by walls and the bottom by the floor. Pixel analysis independently found 48 separate colored components; the smallest median RGB distance to adjacent gray was 61.48 in 8-bit RGB units (`contrast.json`). This is a digital-image diagnostic, not a calibrated perceptual threshold. Saturated colors remain distinguishable where luminance alone is similar.

**Open validation:** physical luminance, wearer color discrimination, comfort and pupil/IPA response under the new environment are not established. Constant neutral materials across both voices avoid introducing a voice-specific background, but do not establish luminance-controlled physiological measurements. These editor renders are development illustrations, not headset lens photographs or participant evidence.

## Scope and coordination

No schedule, trial counts, guidance, voice/model routing, microphone, ElevenLabs, cloning or participant-data work. No `docs/thesis` changes, push or Overleaf publication. Existing `progress.md` content is preserved and excluded from the commit. Shared-board CLAIM/DONE posts identify ownership, final local commit and these dimensions/materials. Vikunja snapshot failed because localhost:3456 refused the connection.
