# Eight-plane rotational search beta

**Implemented:** this beta replaces the table placement step with seated centering. After voice setup and both audio checks, release the trigger, face the intended forward direction, then press Trigger / Enter at the 360° SEARCH BETA checkpoint. That headset position and horizontal heading are fixed for the run. No room-setup table is required. Remain seated and rotate to search.

**Implemented:** eight vertical tangent planes have centers at 0°, 45°, 90°, 135°, 180°, 225°, 270°, and 315°, clockwise from the captured forward direction. Each contains seven objects (a center and six surrounding slots), giving 56 objects per round. Plane centers start 1.5 m away at the captured eye height. Slots are 0.25 m apart within each plane. Thin noninteractive frames and degree labels identify the planes. The objects stay fixed in the room when the headset turns; the goal display follows below the view. The transition cross follows the current view, so this beta does not enforce a return to 0° before each trial.

**Implemented:** existing target/distractor identities, two practice trials, 14 experimental rounds, voice blocks, gaze dwell, wrong-selection feedback, processing/recording announcements and questionnaires are preserved. Gaze-aware warm/cold hints compare objects on the same plane by distance; the beta does not interpret plane slot numbers as shelf adjacency. Coverage uses eight planes and stable object IDs.

**Implemented:** eye-gaze selection ignores room geometry and UI graphics, raycasting only against the searchable object layer. Controller menu interaction is retained. Verify an object behind a room wall still highlights and completes its dwell; the closest searchable object remains the gaze target when search objects overlap.

**Implemented:** experimental object_manifest.csv adds layout, plane_id, plane_slot, plane_azimuth, origin_x/y/z and forward_yaw. Existing columns remain. The layout tag is rotational_beta_v1. Gaze object IDs can be joined to this manifest. Practice remains outside experimental trial logging. Do not combine beta geometry with shelf data without filtering the layout tag.

**Protocol proposal / open decisions:** radius, slot spacing, density, comfort and gaze reliability require headset QA. The existing target schedule is retained and is not exactly balanced across eight planes. Coarse directional/spatial audio, theta calibration, forced forward alignment, revised trial balancing and the larger manuscript schedule are not implemented by this geometry beta. The current experiment remains always gaze-contingent with two voice blocks.

**Implemented:** voice setup prepares four starter phrases per voice (eight clips total). After the eight starter clips, the remaining audio loads silently in the background during audio checks, practice and gameplay. Upcoming round instructions are queued before the remaining hints and announcements, with both voices prepared per phrase. Playback preempts background work, which resumes afterward. If a needed clip is not ready yet, it generates on demand; all clips remain cached by voice. A waiting message appears before search begins; the timer and gaze selection remain gated until the round instruction finishes. First-use hints may arrive later while synthesis completes. Added clips must match the accepted audio level and update the active-run voice manifest. The manifest records `starter_then_background` so this preparation mode can be identified during analysis.

## QA

1. Complete recording and both audio checks; confirm preparation reports four phrases per voice and no table tap is requested. Check a later uncached round shows its audio waiting message with objects hidden and no running search timer. Check a repeated hint uses cached audio and each block retains its assigned voice.
2. Sit at the intended center, face forward, release the trigger and press it once to begin. Holding it from a prior prompt must not start the run.
3. Confirm eight framed planes, seven objects per plane, and 0° forward / 90° right / 180° behind.
4. Turn around: objects must remain anchored while the goal stays readable. Check that the target can be selected at front, side and rear angles.
5. Complete both labeled practice trials and their checkpoint. Confirm the same center/heading remain through practice and the measured rounds.
6. Verify wrong selection, correct selection, pause/resume, questionnaire transition and session reset. Reset reloads setup and allows a fresh center.
7. Check the experimental manifest's layout and plane columns after a measured round. Inspect headset comfort and object readability before adjusting the 1–3 m radius in the manager Inspector.

The stable shelf checkpoint remains on v2 at a67fe92. The beta work is on codex/eight-plane-beta.
