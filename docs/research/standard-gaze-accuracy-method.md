# Standard gaze preparation and accuracy calculation

**Implemented — 2026-10-27.** This describes the simplified standard experiment setup, not the historical six-target shape-confirmation workflow. It is a developer-defined, within-session spatial verification procedure. All thresholds below are pilot settings; they are not validated Focus Vision specifications. No new wearer result is established by this implementation change.

## Participant flow and separation of fitting from verification

After participant/voice setup, select Start once, follow three calibration dots, then follow five verification dots after a three-second transition. A result screen offers Continue on success, Retry, and Skip (no offset). Continue leads to look-and-right-trigger activation practice, then centering and the experiment. Skip is also retained on calibration and verification instructions; it clears the correction and records a skip, never a pass. B stops a running verification, after which preparation offers Retry/Skip.

Three fit dots estimate one head-relative yaw/pitch correction. At each dot, a three-second countdown precedes collection of at least 1.5 seconds and 30 distinct software input updates. Invalid tracking, large head movement or delivery gaps restart the collection window; excessive variability rejects the fit. Per-dot mean yaw/pitch offsets are averaged equally across the three dots. The magnitude of this fitted yaw/pitch pair must not exceed 8°. This magnitude is the Euclidean norm of the two fitted components, distinct from the 3D angular error defined below. Fit positions are world-fixed at 2 m radial distance, using the tracked camera position and horizontal head heading captured at fit start. The three nominal (yaw, elevation) pairs are (0°, 0°), (+12°, +6°), and (−12°, −6°); Unity Euler pitch has the opposite sign to elevation. Only the central dot is at the captured camera height; the diagonals are above/below it. This is camera height, not a separately measured eye midpoint.

The resulting correction is provisional (`awaiting_accuracy`) during verification. It is not accepted for study trials. Five newly collected target windows test it without changing its fitted parameters. These are held-out observations, not necessarily entirely novel viewing directions: the central direction is revisited at a different forward distance. The old independent shape step and manual shape OK are absent from standard setup. Standalone Trial Viewer validation still retains its historical optional shape workflow; do not describe it as the new standard flow.

## Verification targets and observation selection

Five world-fixed dots appear in this order: center, left, right, down, up. Their nominal offsets relative to the horizontal heading captured at verification start are (yaw, pitch) = (0,0), (−15,0), (15,0), (0,−10), (0,10) degrees. The target plane is 2.4 m forward; off-center dots are farther than 2.4 m radially. Their positions are constructed as `(2.4 tan(yaw), 2.4 tan(pitch), 2.4)` in the anchored frame. The anchored frame starts at the tracked camera position, uses yaw-only camera heading, and retains world vertical regardless of head pitch. Targets remain world-fixed during the sequence. The target is a small sphere scaled to 0.018 m diameter (nominally about 0.43° at the center, slightly smaller off-center; actual visual angle depends on the eye position). Each target lasts 2.5 seconds; the initial 0.5 seconds are excluded uniformly to allow gaze to reach it. This fixed settling exclusion does not detect or prove fixation.

Each observation must have valid gaze, an applied candidate correction, a known target, and finite nonzero pre-correction and corrected rays. The pipeline already requires a valid head pose to apply a head-relative correction. Duplicate source records are removed before the settled time window is selected: HTC rows use the available native query identity (epoch/tracker/base space/session/before/after timestamps), falling back to host query receipt time; EXT rows use input event time. Timing of the scoring window uses the corresponding host receipt clock and the recorded target-onset marker. These are software/query observations, not proof of independent native sensor frames or sensor sample rate. Dropped or invalid samples do not become zero error.

Both fitting and scoring use one selected gaze ray: the valid OpenXR EXT gaze pose, or the guarded HTC binocular fallback when EXT is untracked and both native eyes are valid. The fallback origin is the midpoint of the two eye origins; its direction is the normalized sum of their normalized directions. The result is not an average of separately scored left/right eye errors. Original per-eye and pre-correction data are retained.

## Angular error and aggregation

For observation i at target j, let o_ij be the used eye-ray origin, t_j the recorded target world position, and g_ij the corrected gaze direction. Define unit vectors:

- u_ij = (t_j − o_ij) / ||t_j − o_ij||
- v_ij = g_ij / ||g_ij||

The angular error is:

`e_ij = arccos(clamp(v_ij · u_ij, −1, 1)) × 180/π`.

The implementation uses Unity `Vector3.Angle`. This is the unsigned three-dimensional angular separation between the gaze ray and the direction from that same eye origin to the target center. It accounts for horizontal and vertical error together; it is not pixel distance, controller error, signed pitch bias, or a hit-rate percentage.

For each target, save the number of retained observations, mean, median and 90th percentile (P90) of e_ij. Percentiles use linear interpolation at index `(n−1)p` in sorted errors. The displayed overall error is the arithmetic average of the five target means:

`E = (1/5) Σ_j [(1/n_j) Σ_i e_ij]`.

Thus every target contributes equally even if its observation count differs. This is mean angular error, not RMS error, precision, or the mean signed offset. A lower value indicates better agreement with the instructed target positions. A value of 1.26° means average directional separation of 1.26°, not 98.74% accuracy.

## Acceptance and retained correction

All five target windows must finish, each with at least 30 retained observations, median error ≤2°, and P90 error ≤3°. The overall displayed mean alone does not determine pass/fail. If the fitted correction magnitude is ≥0.25°, compare corrected and pre-correction rays on the same retained observations: each target must not worsen by more than 0.1° in median or 0.25° in P90, and the equally weighted mean of per-target median improvements must be ≥0.25°. Improvement means pre-correction median minus corrected median, so positive is better. Below 0.25° fitted magnitude, the benefit/no-worsening gates are skipped, while completion, observation-count and absolute-error gates still apply. These preserve the benefit checks previously performed by the shape stage, now evaluated on the five verification dots.

Only a completed, recorded pass can promote the provisional correction to accepted. Accepted reports link the five-dot validation ID and do not claim a manual shape confirmation. Failure, Skip, interruption or source/participant change clears the candidate. A failed recording write prevents proceeding as a pass. After acceptance the correction remains bound to that participant and gaze source; focus loss or invalidation requires preparation again before starting trials.

## Scope and evidence limits

Target fixation is instructed and assumed, not independently observed. The score combines hardware tracking, application ray processing/correction, headset fit, and the participant's ability to follow the target instructions. It is a within-session application-level spatial check over these five directions at this forward distance. It does not certify whole-field/device accuracy, validate saccade detection or IPA, establish sensor cadence, or quantify precision. Correlated observations are not independent participant replications. Skipped/failed checks must remain visible in analysis and follow the study's inclusion policy.

At setup this is an accuracy verification; the same dot schedule can be used later as a drift check to assess a change since setup. This change does not add an automatic mid-experiment drift-check schedule.

## Reproducibility and thesis coordination

Sources: `Assets/GazeQuickAlignment.cs`, `Assets/EntryGazeAlignment.cs`, `Assets/StudyGazePreparation.cs`, `Assets/StudyGazeAccuracy.cs`, `Assets/SharedGazeStream.cs`, `Assets/EyeValidationSession.cs`, `Assets/EyeValidationSession.View.cs`, and `Assets/EyeValidationProtocol.cs`.

Receipts: `EyeMeasurements/entry_alignment_<id>.json`, `study_preparation_<id>.json`, and immutable `validation_<timestamp>_<id>/manifest.json`, `samples.jsonl`, `events.jsonl`, plus referenced raw recording segments. The new alignment method is `head_local_three_dot_mean_five_dot_verified_v3`; runtime accuracy summary method is `five_dot_equal_target_mean_500ms_query_deduplicated_v2`. Historical reports keep their original methods/results. The five-target display schedule itself remains `eye-validation-v1`.

**Open decision:** protocol approval and acceptance thresholds require study-team review; Skip remains enabled at Pedro's request. This note specifies implemented software, not externally validated research standards. The thesis agent should describe this version separately from historical runs and cite a frozen code revision when available. No manuscript edits are made by the experiment agent.
