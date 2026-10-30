"""Recorded pre-cue geometry contract. Never infer missing measurements."""
import math

REFERENCE = "pre_cue_eye_origin_to_readiness_cross"
V7 = {"readiness-target-angle-v7-one-practice", "readiness-target-angle-v7-quick-origin0"}
V8 = {"readiness-target-angle-v8-signed-steps-32-one-practice",
      "readiness-target-angle-v8-signed-steps-32-quick-origin0"}
PRE_CUE_SCHEDULES = V7 | V8
QUICK_SCHEDULES = {v for v in PRE_CUE_SCHEDULES if v.endswith("quick-origin0")}
SCALARS = {
    "round": "round", "gazeSequence": "gaze_sequence",
    "time": "realtime_seconds", "gazeReceiptTime": "gaze_receipt_realtime_seconds",
    "assignedSignedDegrees": "assigned_signed_deg", "realizedSignedDegrees": "realized_signed_deg",
    "absoluteDegrees": "absolute_deg", "assignmentErrorDegrees": "assignment_error_deg",
    "targetHeightRelativeToCross": "target_minus_cross_height_m",
}
VECTORS = {"eyeOrigin": "eye_origin", "readinessPosition": "cross", "targetPosition": "target"}
FIELDS = ["pre_cue_theta_status", "pre_cue_theta_reference", "pre_cue_theta_coordinate_frame"] + [
    "pre_cue_theta_" + name for name in SCALARS.values()
] + ["pre_cue_theta_" + name + "_" + axis + "_m" for name in VECTORS.values() for axis in "xyz"]


def circular_delta(a, b):
    return (a - b + 180) % 360 - 180


def flatten(snapshot, *, trial_index=None, assigned=None):
    """Reject malformed/contradictory snapshots rather than exporting false zeros."""
    if snapshot is None:
        return {"pre_cue_theta_status": "not_recorded"}
    if not isinstance(snapshot, dict) or snapshot.get("reference") != REFERENCE:
        raise ValueError("Pre-cue theta: invalid reference or snapshot")
    row = {"pre_cue_theta_status": "recorded", "pre_cue_theta_reference": REFERENCE,
           "pre_cue_theta_coordinate_frame": "unity_world"}
    for source, dest in SCALARS.items():
        value = snapshot.get(source)
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
            raise ValueError(f"Pre-cue theta: missing/non-finite {source}")
        if source in ("round", "gazeSequence") and (type(value) is not int or value < 0):
            raise ValueError(f"Pre-cue theta: invalid {source}")
        row["pre_cue_theta_" + dest] = value
    for source, dest in VECTORS.items():
        vector = snapshot.get(source)
        if not isinstance(vector, dict):
            raise ValueError(f"Pre-cue theta: missing {source}")
        for axis in "xyz":
            value = vector.get(axis)
            if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
                raise ValueError(f"Pre-cue theta: invalid {source}.{axis}")
            row[f"pre_cue_theta_{dest}_{axis}_m"] = value
    if trial_index is not None and snapshot["round"] != trial_index:
        raise ValueError("Pre-cue theta: round mismatch")
    if assigned is not None and abs(circular_delta(snapshot["assignedSignedDegrees"], assigned)) > .001:
        raise ValueError("Pre-cue theta: assigned angle disagrees with schedule")
    if (snapshot["assignedSignedDegrees"] not in (0, 45, -45, 90, -90, 135, -135, 180)
            or abs(snapshot["realizedSignedDegrees"]) > 180
            or not 0 <= snapshot["absoluteDegrees"] <= 180
            or abs(snapshot["assignmentErrorDegrees"]) > 180):
        raise ValueError("Pre-cue theta: signed angle outside shortest-turn range")
    if not 0 <= snapshot["gazeReceiptTime"] <= snapshot["time"]:
        raise ValueError("Pre-cue theta: invalid receipt/snapshot times")
    eye, cross, target = (snapshot[key] for key in VECTORS)
    cx, cz = cross["x"] - eye["x"], cross["z"] - eye["z"]
    tx, tz = target["x"] - eye["x"], target["z"] - eye["z"]
    if math.hypot(cx, cz) < 1e-6 or math.hypot(tx, tz) < 1e-6:
        raise ValueError("Pre-cue theta: degenerate horizontal geometry")
    realized = math.degrees(math.atan2(cz * tx - cx * tz, cx * tx + cz * tz))
    error = circular_delta(snapshot["realizedSignedDegrees"], snapshot["assignedSignedDegrees"])
    if (abs(circular_delta(realized, snapshot["realizedSignedDegrees"])) > .02
            or abs(abs(snapshot["realizedSignedDegrees"]) - snapshot["absoluteDegrees"]) > .001
            or abs(circular_delta(error, snapshot["assignmentErrorDegrees"])) > .001
            or abs(error) > .0101
            or abs(target["y"] - cross["y"] - snapshot["targetHeightRelativeToCross"]) > .001):
        raise ValueError("Pre-cue theta: angle/height geometry or assignment error mismatch")
    return row
