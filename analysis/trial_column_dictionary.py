"""Column definitions for the review demo; no measurement or scoring rules added."""

DEFINITIONS = {
    "export_schema": "Version of the summary export contract.",
    "session_mode": "Source session mode; not_recorded for legacy files without it.",
    "data_purpose": "synthetic, development, study_declared or unknown; not a quality certification.",
    "exclude_from_study_analysis": "1 means excluded; 0 means declared study mode only; blank requires review.",
    "analysis_exclusion_reason": "Recorded reason for exclusion or unknown mode requiring review.",
    "participant_id": "Source participant code; SYNTHETIC_DEMO identifies invented example data.",
    "run_number": "Measured run number; absent for practice.",
    "session_id": "Stable session identity from summary or participant/run fallback.",
    "trial_id": "Stable logical trial key; practice keys include recording ID.",
    "is_practice": "1 for practice, 0 for measured-schedule rows; synthetic rows still remain synthetic.",
    "block_index": "Zero-based block index; absent for practice.",
    "trial_index": "Zero-based index in the source schedule.",
    "voice_condition": "Source condition label; historical neutral labels remain unchanged.",
    "voice_order": "Recorded order of voice blocks.",
    "actual_voice_id": "Provider voice identity when recorded; absent when unknown.",
    "outcome": "Source/replay trial outcome, including completed, not_started or interrupted.",
    "completed": "1 only when completion is supported by summary or correct replay selection.",
    "missed_target": "1 for deadline expiry without correct selection, including after wrong attempts; 0 for correct selection; blank for unresolved/technical/unstarted outcomes.",
    "missed_target_status": "Evidence for the missed-target classification; blanks are not zero misses.",
    "active_search_limit_seconds": "Recorded search deadline, excluding explicit pauses. Missing for legacy recordings without a documented limit; never inferred as 15 seconds.",
    "search_started_at_unity_seconds": "Unity clock at recorded search onset; not replay time.",
    "capture_at_unity_seconds": "Unity clock at final capture; not replay time.",
    "search_exposure_seconds": "Active-search duration observed, excluding pauses; may describe an incomplete trial.",
    "search_wall_seconds": "Completed-trial Unity capture minus onset, including pauses.",
    "pause_seconds": "Completed-trial wall duration minus active exposure when both clocks are available.",
    "time_to_final_selection_seconds": "Active-search time to final correct trigger selection; blank if unfinished.",
    "completion_source": "Whether summary or replay selection supplies completion timing.",
    "incorrect_selection_count": "Observed wrong selections; source-summary fallback when supported.",
    "instruction_count": "Actual guidance playback starts, including delivered repeats; excludes requests/prefetch/success speech.",
    "hint_count": "Guidance playback starts excluding delivered repeats.",
    "interrupted_instruction_count": "Started guidance cancelled or failed during active search.",
    "repeat_request_count": "Explicit guidance-repeat requests; blank without the versioned logging contract.",
    "delivered_repeat_count": "Actual guidance_repeat playback starts; blank without the versioned contract.",
    "gaze_total_samples": "Rendered-frame gaze observations considered during active search; not native eye samples.",
    "gaze_valid_samples": "Frame observations with gaze explicitly available and tracked.",
    "gaze_unknown_tracking_samples": "Frame observations with gaze tracking state unknown.",
    "gaze_validity_rate": "Valid frame gaze count divided by considered frame gaze count, range 0 to 1.",
    "time_to_first_target_gaze_seconds": "First explicitly tracked target gaze-ray intersection on the active clock; not a fixation.",
    "time_to_final_target_gaze_seconds": "Last tracked target intersection during observed active search, including unfinished trials; not a fixation episode.",
    "time_to_first_right_controller_target_seconds": "First tracked right-controller ray/target intersection on the active clock.",
    "completion_timing_definition": "Identifies recorded controller selection, legacy dwell completion or an undocumented method; never substitutes gaze-zone arrival.",
    "zones_visited_count": "Alias of unique_zones_visited_count: distinct confirmed horizontal walls during active search.",
    "unique_zones_visited_count": "Distinct walls confirmed by stable valid gaze. Does not count repeated visits twice.",
    "zone_transition_count": "Confirmed wall changes within uninterrupted valid-gaze segments; no inferred crossings across gaps.",
    "zone_revisit_count": "Confirmed transitions into a previously visited wall; no inferred returns across tracking gaps.",
    "zone_observation_gap_count": "Breaks in an observed gaze segment due to invalidity, pauses, geometry, time or source gaps.",
    "zone_stability_seconds": "Continuous distinct valid observations required to confirm a wall; exporter policy, not fixation validation.",
    "zone_max_gap_seconds": "Largest adjacent observation gap allowed for continuous zone evidence.",
    "gross_head_translation_m": "Sum of adjacent valid tracked head translation distances; gaps and pauses are not bridged.",
    "gross_head_rotation_deg": "Sum of shortest adjacent quaternion angular displacements; gaps and pauses are not bridged.",
    "head_motion_observation_count": "Frame/selection head observations considered during active search, including invalid head observations.",
    "head_motion_interval_count": "Valid adjacent head intervals included in motion totals.",
    "head_motion_covered_seconds": "Sum of durations of included head-motion intervals.",
    "head_motion_gap_count": "Observed sample gaps exceeding configured max-motion-gap-seconds.",
    "target_object_id": "Stable target ID within the recorded layout.",
    "target_shape": "Recorded target shape.", "target_color": "Recorded target color.",
    "target_plane": "Recorded target wall index.", "start_plane": "Recorded starting wall index.",
    "signed_theta_deg": "Planned signed horizontal angle; consult theta_reference and schedule_version (v7 constrains eye-to-cross-to-target geometry).",
    "absolute_theta_deg": "Magnitude of planned signed turn; not actual accumulated head rotation.",
    "theta_reference": "Recorded reference convention for the planned horizontal angle.",
    "coordinate_frame": "Coordinate system for target/head geometry; do not mix replay and world positions.",
    "target_distance_m": "Euclidean target-to-head distance at first tracked post-onset pose.",
    "target_minus_head_height_m": "Signed target Y minus head Y at first tracked post-onset pose.",
    "actual_target_bearing_deg": "Target azimuth relative to recording forward at the geometry reference.",
    "initial_head_yaw_deg": "Head forward azimuth at the first tracked post-onset pose.",
    "actual_signed_head_to_target_yaw_deg": "Shortest signed head-forward-to-target azimuth at geometry reference.",
    "geometry_reference": "Frame or selection snapshot supplying initial measured geometry.",
    "geometry_reference_search_seconds": "Active-clock delay from onset to the geometry reference pose.",
    "scheduled_height_delta_m": "Trial-layout target Y minus recorded readiness-cross Y. Positive is above the plane midpoint.",
    "scheduled_height_reference": "Recorded cross/plane-midpoint reference, independent of head height; not proof of quota balancing.",
    "scheduled_height_reference_y_m": "Recorded cross height in the fixed replay origin coordinate system (m).",
    "schedule_version": "Recorded challenge-set/schedule version.",
    "target_identity_policy": "Recorded policy governing target identity disclosure and scheduling.",
    "height_assignment_policy": "Recorded policy for target height assignment; not proof of vertical balance.",
    "schedule_seed": "Recorded seed for schedule reproduction.", "layout_seed": "Recorded seed for object layout.",
    "angle_pair": "Recorded allocation ID; v7 does not promise mirrored trials across blocks.",
    "selection_method": "Recorded interaction method, such as controller_ray_trigger.",
    "selecting_hand": "Hand recorded at final selection; historical left-hand data remain left-hand data.",
    "replay_file": "Matched source recording path.", "replay_recording_id": "Recording identity; scopes replay time.",
    "replay_schema": "Version of the raw replay record contract.",
    "build_guid": "Unity build identity from replay header.", "app_version": "App version from replay header.",
    "replay_sampling": "Recorded replay sampling description; not verified native eye frequency.",
    "nasa_tlx_score": "Unweighted mean of all six 0-100 dimensions after explicit performance-direction alignment. Missing metadata/data leave it blank.",
    "nasa_tlx_performance_direction": "Declared high_is_poor (keep) or high_is_good (use 100 minus performance for scoring only).",
    "nasa_tlx_scoring_method": "Versioned unweighted six-dimension mean rule; a method label alone does not mean a score was computed.",
    "nasa_tlx_scoring_status": "Whether all six dimensions were scored, or the specific missing/invalid scale metadata or dimension preventing a total.",
    "quality_flags": "Semicolon-separated source/coverage warnings; inspect alongside status columns.",
    "source_summary": "Source trial_summary.json path, fingerprinted in manifest.",
    "source_trial_json": "Unmodified objective content serialized as JSON; legacy event labels are not validation.",
}


def definition(field: str) -> str:
    if field in DEFINITIONS:
        return DEFINITIONS[field]
    if field.startswith("survey__"):
        _, instrument, scope, name = field.split("__", 3)
        return f"Raw {instrument} {scope} response/provenance field: {name}. Repeated block responses retain one response ID."
    if field.startswith("nasa_tlx_"):
        return f"Raw NASA-TLX {field.removeprefix('nasa_tlx_')} response; scale/orientation follow the source instrument."
    if field.startswith("gross_head_translation_"):
        return "Sum of absolute head translation increments on this fixed recording axis (m); only valid adjacent intervals."
    if field.startswith("gross_head_rotation_"):
        return "Sum of absolute rotation-vector increments on this fixed axis (deg); NOT Euler yaw/pitch/roll differences."
    if field in ("target_x", "target_y", "target_z"):
        return "Recorded target position component in metres; see coordinate_frame."
    if field.endswith("_status"):
        return "Availability/quality state for " + field.removesuffix("_status").replace("_", " ") + "; blank measurements are not zero."
    if field.endswith("_source"):
        return "Source evidence for " + field.removesuffix("_source").replace("_", " ") + "."
    if field.endswith("_method"):
        return "Versioned method describing " + field.removesuffix("_method").replace("_", " ") + "; see docs/trial-performance-csv.md."
    if field.endswith("_version") or field.endswith("_policy"):
        return "Recorded contract identifier: " + field.replace("_", " ") + ". Missing source metadata remain blank."
    if field.startswith("source_schedule__"):
        return "Original planned schedule field: " + field.removeprefix("source_schedule__") + "."
    raise ValueError(f"No definition for exported column: {field}")

# Additive fields retain snapshot clocks/frame separately from replay geometry.
from theta_snapshot import SCALARS as THETA_SCALARS, VECTORS as THETA_VECTORS
DEFINITIONS.update({
    "pre_cue_theta_status": "recorded only for a complete validated snapshot; not_recorded means absent, never zero.",
    "pre_cue_theta_reference": "Recorded pre-cue eye-origin to readiness-cross horizontal angle reference.",
    "pre_cue_theta_coordinate_frame": "unity_world; these coordinates must not be combined with replay_fixed_origin positions.",
    "direction_policy": "Recorded signed-angle allocation policy; independent v7 blocks are not mirrored pairs.",
})
for _source, _name in THETA_SCALARS.items():
    DEFINITIONS["pre_cue_theta_" + _name] = (
        "Exact recorded pre-cue snapshot field " + _source +
        ". Angles are degrees, height is metres, clocks are app realtime seconds (not replay time); round is zero-based and gaze sequence identifies the source observation."
    )
for _source, _name in THETA_VECTORS.items():
    for _axis in "xyz":
        DEFINITIONS[f"pre_cue_theta_{_name}_{_axis}_m"] = f"Exact recorded {_source}.{_axis} in Unity world metres at pre-cue readiness completion."
