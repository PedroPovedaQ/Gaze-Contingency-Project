"""Export one performance/subjective CSV per trial from copied, stopped recordings.

Raw recordings remain authoritative. This is a post-session reduction, not a new
sensor, fixation detector, survey scale, or replacement for reconstruction data.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from mega_csv import csv_rows, replay_rows

SCHEMA = "trial-performance-subjective-v1"
CONTACT_METHOD = "first_explicitly_tracked_ray_intersection_snapshot_v1"
BASE_FIELDS = [
    "export_schema",
    "participant_id",
    "run_number",
    "session_id",
    "trial_id",
    "is_practice",
    "block_index",
    "trial_index",
    "voice_condition",
    "voice_order",
    "actual_voice_id",
    "outcome",
    "completed",
    "search_started_at_unity_seconds",
    "capture_at_unity_seconds",
    "search_exposure_seconds",
    "search_wall_seconds",
    "pause_seconds",
    "time_to_final_selection_seconds",
    "completion_source",
    "incorrect_selection_count",
    "instruction_count",
    "interrupted_instruction_count",
    "instruction_count_status",
    "instruction_count_source",
    "repeat_request_count",
    "delivered_repeat_count",
    "repeat_count_status",
    "gaze_total_samples",
    "gaze_valid_samples",
    "gaze_unknown_tracking_samples",
    "gaze_validity_rate",
    "time_to_first_target_gaze_seconds",
    "time_to_final_target_gaze_seconds",
    "time_to_first_right_controller_target_seconds",
    "gaze_contact_status",
    "controller_contact_status",
    "contact_method",
    "final_gaze_method",
    "time_to_final_correct_zone_seconds",
    "zones_visited_count",
    "zone_measurement_status",
    "gross_head_translation_m",
    "gross_head_translation_x_m",
    "gross_head_translation_y_m",
    "gross_head_translation_z_m",
    "gross_head_rotation_deg",
    "gross_head_rotation_x_deg",
    "gross_head_rotation_y_deg",
    "gross_head_rotation_z_deg",
    "head_motion_interval_count",
    "head_motion_covered_seconds",
    "head_motion_gap_count",
    "head_motion_status",
    "head_rotation_method",
    "target_object_id",
    "target_shape",
    "target_color",
    "target_plane",
    "start_plane",
    "signed_theta_deg",
    "absolute_theta_deg",
    "theta_reference",
    "target_x",
    "target_y",
    "target_z",
    "coordinate_frame",
    "target_distance_m",
    "target_minus_head_height_m",
    "actual_target_bearing_deg",
    "initial_head_yaw_deg",
    "actual_signed_head_to_target_yaw_deg",
    "geometry_reference",
    "geometry_reference_search_seconds",
    "scheduled_height_delta_m",
    "scheduled_height_reference",
    "schedule_version",
    "schedule_seed",
    "layout_seed",
    "angle_pair",
    "selection_method",
    "selecting_hand",
    "replay_file",
    "replay_recording_id",
    "replay_schema",
    "build_guid",
    "app_version",
    "replay_sampling",
    "survey_status",
    "subjective_scoring_status",
    "nasa_tlx_mental",
    "nasa_tlx_physical",
    "nasa_tlx_temporal",
    "nasa_tlx_performance",
    "nasa_tlx_effort",
    "nasa_tlx_frustration",
    "nasa_tlx_score",
    "quality_flags",
    "source_summary",
    "source_trial_json",
]


def number(value: Any) -> float | None:
    if value is None or value == "" or isinstance(value, bool):
        return None
    try:
        value = float(value)
    except (ValueError, TypeError):
        return None
    return value if math.isfinite(value) else None


def nonnegative(value: Any) -> float | None:
    result = number(value)
    return result if result is not None and result >= 0 else None


def vector(value: Any, keys: str = "xyz") -> tuple | None:
    if not isinstance(value, dict):
        return None
    values = tuple(number(value.get(k)) for k in keys)
    return values if all(v is not None for v in values) else None


def magnitude(v: tuple) -> float:
    return math.sqrt(sum(x * x for x in v))


def quaternion(value: Any) -> tuple | None:
    q = vector(value, "xyzw")
    length = magnitude(q) if q else 0
    return tuple(x / length for x in q) if length > 1e-12 else None


def multiply(a: tuple, b: tuple) -> tuple:
    x, y, z, w = a
    X, Y, Z, W = b
    return (
        w * X + x * W + y * Z - z * Y,
        w * Y - x * Z + y * W + z * X,
        w * Z + x * Y - y * X + z * W,
        w * W - x * X - y * Y - z * Z,
    )


def rotation_delta(a: tuple, b: tuple) -> tuple[float, tuple]:
    # New * inverse(old): shortest incremental rotation in the fixed recording
    # axes. Axis-angle vector components are NOT Euler yaw/pitch/roll differences.
    delta = multiply(b, (-a[0], -a[1], -a[2], a[3]))
    if delta[3] < 0:
        delta = tuple(-v for v in delta)
    angle = math.degrees(2 * math.atan2(magnitude(delta[:3]), max(0, delta[3])))
    norm = magnitude(delta[:3])
    axes = (
        tuple(abs(v) * angle / norm for v in delta[:3]) if norm > 1e-12 else (0, 0, 0)
    )
    return angle, axes


def heading(q: tuple) -> float | None:
    x, y, z, w = q
    forward_x = 2 * (x * z + w * y)
    forward_z = 1 - 2 * (x * x + y * y)
    if math.hypot(forward_x, forward_z) < 1e-9:
        return None
    return math.degrees(math.atan2(forward_x, forward_z))


def encode(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return str(int(value))
    if isinstance(value, float):
        return format(value, ".10g")
    return str(value)


@dataclass
class Trial:
    row: dict
    flags: set[str] = field(default_factory=set)
    started: bool = False
    ended: bool = False
    paused: bool = False
    target: dict | None = None
    previous_head: tuple | None = None
    previous_sample: float | None = None
    last_active_seconds: float | None = None
    samples: int = 0
    valid_gaze: int = 0
    unknown_gaze: int = 0
    valid_controller: int = 0
    instructions: int = 0
    interrupted: int = 0
    active_instruction: str | None = None
    wrong: int = 0
    intervals: int = 0
    gaps: int = 0
    covered: float = 0
    movement: float = 0
    rotation: float = 0
    move_axes: list = field(default_factory=lambda: [0.0, 0.0, 0.0])
    rot_axes: list = field(default_factory=lambda: [0.0, 0.0, 0.0])

    def consume(self, r: dict, max_gap: float) -> None:
        kind = r["kind"]
        elapsed = nonnegative(r.get("searchSeconds"))
        if kind == "trial":
            targets = [o for o in r.get("objects", []) if o.get("target") is True]
            if len(targets) > 1:
                raise ValueError(f"Multiple replay targets: {self.row['trial_id']}")
            self.target = targets[0] if targets else None
            target_ids = {
                value
                for value in (
                    r.get("targetId"),
                    (self.target or {}).get("id"),
                    self.row.get("target_object_id"),
                )
                if value
            }
            if len(target_ids) > 1:
                raise ValueError(
                    f"Conflicting target identities: {self.row['trial_id']}"
                )
            self.row.update(
                target_object_id=r.get("targetId") or (self.target or {}).get("id"),
                selection_method=r.get("selectionMethod"),
                replay_schema=r["schema"],
            )
            for source, dest in [
                ("scheduleVersion", "schedule_version"),
                ("scheduleSeed", "schedule_seed"),
                ("layoutSeed", "layout_seed"),
                ("startPlane", "start_plane"),
                ("targetPlane", "target_plane"),
                ("signedTheta", "signed_theta_deg"),
                ("absoluteTheta", "absolute_theta_deg"),
                ("anglePair", "angle_pair"),
            ]:
                if r.get(source) is not None and not self.row["is_practice"]:
                    self.row[dest] = r[source]
            if self.target:
                self.row.update(
                    target_shape=self.target.get("shape"),
                    target_color=self.target.get("color"),
                    coordinate_frame="replay_fixed_origin",
                )
                pos = vector(self.target.get("position"))
                if pos:
                    self.row.update(zip(("target_x", "target_y", "target_z"), pos))
            return
        # Object movement must affect the next geometry sample; never regenerate
        # positions from a seed or mix world CSV coordinates with replay poses.
        for change in r.get("changes") or []:
            if self.target and change.get("id") == self.target.get("id"):
                self.target.update(change)
                self.flags.add("target_moved_during_recording")
        if kind == "search_start":
            if self.started:
                raise ValueError(f"Duplicate search onset: {self.row['trial_id']}")
            self.started = True
            if self.row.get("outcome") in (None, "not_started"):
                self.row["outcome"] = "incomplete"
            return
        if kind == "search_paused":
            self.paused = True
            self.previous_head = None
            self.previous_sample = None
        elif kind == "search_resumed":
            self.paused = False
            self.previous_head = None
            self.previous_sample = None
        if not self.started or self.ended or self.paused:
            return
        if elapsed is None:
            self.flags.add("missing_active_search_clock")
            return
        if (
            self.last_active_seconds is not None
            and elapsed < self.last_active_seconds - 1e-6
        ):
            raise ValueError(f"Active search clock regressed: {self.row['trial_id']}")
        self.last_active_seconds = elapsed
        if kind == "selection":
            if r.get("searchActive") is True:
                self.contacts(r, elapsed)
            if r.get("correct") is True:
                existing = nonnegative(self.row.get("time_to_final_selection_seconds"))
                if existing is not None and abs(existing - elapsed) > 0.02:
                    self.flags.add("summary_replay_completion_mismatch")
                self.row.update(
                    time_to_final_selection_seconds=elapsed,
                    completion_source="replay_selection",
                    search_exposure_seconds=elapsed,
                    completed=True,
                    outcome="completed",
                    selecting_hand=r.get("selectingHand"),
                )
                self.ended = True
            else:
                self.wrong += 1
        context = (r.get("detail") or "").split(";", 1)[0]
        if kind == "audio_playback_start":
            # The current recorder calls guidance 'tip'. New policy names must be
            # explicitly added/versioned, never guessed from spoken text.
            self.active_instruction = (
                context if context in ("tip", "guidance", "guidance_repeat") else None
            )
            if self.active_instruction:
                self.instructions += 1
        elif (
            kind in ("audio_cancelled", "audio_failure", "audio_playback_end")
            and context == self.active_instruction
        ):
            if self.active_instruction and kind != "audio_playback_end":
                self.interrupted += 1
            self.active_instruction = None
        if kind != "sample":
            return
        if r.get("searchActive") is not True:
            self.previous_head = None
            self.previous_sample = None
            return
        self.samples += 1
        self.row["search_exposure_seconds"] = max(
            elapsed, nonnegative(self.row.get("search_exposure_seconds")) or 0
        )
        gaze_valid, controller_valid = self.contacts(r, elapsed)
        self.valid_gaze += int(gaze_valid)
        self.valid_controller += int(controller_valid)
        self.unknown_gaze += int(r.get("gazeTracked", -1) == -1)
        pos, q = vector(r.get("headPosition")), quaternion(r.get("headRotation"))
        valid_head = (
            r.get("headAvailable") is True and r.get("headTracked") == 1 and pos and q
        )
        if (
            self.previous_sample is not None
            and r["time"] - self.previous_sample > max_gap + 1e-9
        ):
            self.gaps += 1
        self.previous_sample = r["time"]
        if valid_head:
            self.geometry(pos, q, elapsed)
            if self.previous_head is not None:
                t, old_pos, old_q = self.previous_head
                delta_time = r["time"] - t
                if 0 < delta_time <= max_gap + 1e-9:
                    delta = tuple(b - a for a, b in zip(old_pos, pos))
                    self.movement += magnitude(delta)
                    angle, axes = rotation_delta(old_q, q)
                    self.rotation += angle
                    for i in range(3):
                        self.move_axes[i] += abs(delta[i])
                        self.rot_axes[i] += axes[i]
                    self.covered += delta_time
                    self.intervals += 1
            self.previous_head = (r["time"], pos, q)
        else:
            self.previous_head = None
            self.flags.add("invalid_or_unknown_head_samples")

    def contacts(self, r: dict, elapsed: float) -> tuple[bool, bool]:
        """Selection snapshots can capture a hit missed between rendered samples."""
        target_id = self.row.get("target_object_id")
        gaze_valid = r.get("gazeAvailable") is True and r.get("gazeTracked") == 1
        if gaze_valid and target_id and r.get("hoveredId") == target_id:
            self.row.setdefault("time_to_first_target_gaze_seconds", elapsed)
            self.row.setdefault("first_gaze_contact_source", r["kind"])
            self.row["time_to_final_target_gaze_seconds"] = elapsed
        c = r.get("rightController") or {}
        controller_valid = (
            c.get("available") is True
            and c.get("rayAvailable") is True
            and c.get("tracked") == 1
        )
        if controller_valid and target_id and c.get("hoveredId") == target_id:
            self.row.setdefault(
                "time_to_first_right_controller_target_seconds", elapsed
            )
            self.row.setdefault("first_controller_contact_source", r["kind"])
        return gaze_valid, controller_valid

    def geometry(self, head: tuple, q: tuple, elapsed: float) -> None:
        if self.row.get("geometry_reference") or not self.target:
            return
        target = vector(self.target.get("position"))
        if target is None:
            return
        d = tuple(b - a for a, b in zip(head, target))
        bearing = (
            math.degrees(math.atan2(d[0], d[2]))
            if math.hypot(d[0], d[2]) > 1e-9
            else None
        )
        head_yaw = heading(q)
        self.row.update(zip(("target_x", "target_y", "target_z"), target))
        self.row.update(
            target_distance_m=magnitude(d),
            target_minus_head_height_m=d[1],
            actual_target_bearing_deg=bearing,
            initial_head_yaw_deg=head_yaw,
            actual_signed_head_to_target_yaw_deg=(
                (bearing - head_yaw + 180) % 360 - 180
            )
            if bearing is not None and head_yaw is not None
            else None,
            geometry_reference="first_tracked_head_sample_after_search_start",
            geometry_reference_search_seconds=elapsed,
        )

    def finish(self) -> dict:
        row = self.row
        if not row.get("replay_file"):
            self.flags.add("no_matching_replay")
        if self.started:
            if row.get("instruction_count") is None:
                row.update(
                    instruction_count=self.instructions,
                    interrupted_instruction_count=self.interrupted,
                    instruction_count_status="observed_playback_starts",
                    instruction_count_source="replay",
                )
            elif row["instruction_count"] != self.instructions:
                self.flags.add("instruction_count_cross_source_mismatch")
            row.update(
                gaze_total_samples=self.samples,
                gaze_valid_samples=self.valid_gaze,
                gaze_unknown_tracking_samples=self.unknown_gaze,
                gaze_validity_rate=self.valid_gaze / self.samples
                if self.samples
                else None,
                head_motion_interval_count=self.intervals,
                head_motion_covered_seconds=self.covered,
                head_motion_gap_count=self.gaps,
            )
            if row.get("incorrect_selection_count") is None:
                row["incorrect_selection_count"] = self.wrong
            elif row["incorrect_selection_count"] != self.wrong:
                self.flags.add("summary_replay_wrong_selection_mismatch")
        if self.intervals:
            row.update(
                gross_head_translation_m=self.movement,
                gross_head_rotation_deg=self.rotation,
                head_motion_status="observed_valid_intervals_only",
            )
            for axis, move, rot in zip("xyz", self.move_axes, self.rot_axes):
                row["gross_head_translation_" + axis + "_m"] = move
                row["gross_head_rotation_" + axis + "_deg"] = rot
        else:
            row["head_motion_status"] = "no_valid_adjacent_intervals"
        if self.gaps:
            self.flags.add("sample_gaps_not_bridged")
        for metric, status, valid in [
            (
                "time_to_first_target_gaze_seconds",
                "gaze_contact_status",
                self.valid_gaze,
            ),
            (
                "time_to_first_right_controller_target_seconds",
                "controller_contact_status",
                self.valid_controller,
            ),
        ]:
            row[status] = (
                "observed"
                if row.get(metric) is not None
                else "not_observed"
                if valid
                else "no_valid_samples"
            )
        row.update(
            contact_method=CONTACT_METHOD,
            final_gaze_method="last_observed_tracked_target_intersection_not_fixation",
            head_rotation_method="shortest_quaternion_increment_fixed_frame_rotation_vector_axes_v1",
            zone_measurement_status="not_exported_pending_zone_definition",
            repeat_count_status="not_recorded_as_distinct_request_delivery_events",
            subjective_scoring_status="raw_responses_only",
        )
        row["quality_flags"] = ";".join(sorted(self.flags))
        return row


def read_summary(run: Path) -> tuple[dict, dict[str, Trial]]:
    summary = json.loads((run / "trial_summary.json").read_text(encoding="utf-8-sig"))
    participant, run_number = str(summary["participant_id"]), int(summary["run_number"])
    if run_number < 1 or not participant:
        raise ValueError("Invalid participant/run identity")
    session = summary.get("session_id") or f"{participant}_run{run_number:03d}"
    trials = {}
    for source in summary["objectives"]:
        index = int(source["index"])
        tid = source.get("trial_id") or f"{session}_r{index:02d}"
        if tid in trials or index < 0:
            raise ValueError(f"Duplicate/invalid trial: {tid}")
        block = source.get("block")
        if block is None and summary.get("rounds_per_block"):
            block = index // int(summary["rounds_per_block"])
        row = {
            "export_schema": SCHEMA,
            "participant_id": participant,
            "run_number": run_number,
            "session_id": session,
            "trial_id": tid,
            "is_practice": False,
            "block_index": block,
            "trial_index": index,
            "voice_condition": source.get("voice_condition"),
            "voice_order": summary.get("voice_order"),
            "outcome": source.get("outcome", "unknown"),
            "completed": source.get("completed") is True,
            "source_summary": str(run / "trial_summary.json"),
            "source_trial_json": json.dumps(source, ensure_ascii=False, sort_keys=True),
            "schedule_version": summary.get("challenge_set"),
            "schedule_seed": summary.get("schedule_seed"),
            "theta_reference": summary.get("theta_reference"),
            "selection_method": summary.get("selection_method"),
        }
        for key in (
            "start_plane",
            "target_plane",
            "signed_theta_deg",
            "absolute_theta_deg",
            "angle_pair",
            "layout_seed",
        ):
            row[key] = source.get(key)
        for source_key, dest in [
            ("search_started_at", "search_started_at_unity_seconds"),
            ("capture_at", "capture_at_unity_seconds"),
            ("search_exposure_seconds", "search_exposure_seconds"),
        ]:
            row[dest] = nonnegative(source.get(source_key))
        if row["search_started_at_unity_seconds"] is not None:
            row["incorrect_selection_count"] = nonnegative(source.get("wrong_captures"))
        if row["completed"]:
            elapsed = nonnegative(source.get("search_exposure_seconds"))
            if elapsed is None:
                elapsed = nonnegative(source.get("time_to_find_seconds"))
            row.update(
                time_to_final_selection_seconds=elapsed,
                completion_source="trial_summary",
            )
            start, end = (
                row["search_started_at_unity_seconds"],
                row["capture_at_unity_seconds"],
            )
            if start is not None and end is not None and end >= start:
                row["search_wall_seconds"] = end - start
                if elapsed is not None and end - start >= elapsed - 0.02:
                    row["pause_seconds"] = max(0, end - start - elapsed)
        # The historical neutral label is retained; do not relabel it 'preferred'.
        if row["voice_condition"] in ("neutral", "preferred"):
            row["actual_voice_id"] = summary.get("neutral_voice_id")
        trials[tid] = Trial(row)
    return summary, trials


def add_source_records(
    run: Path, summary: dict, trials: dict, inputs: set[Path]
) -> None:
    """Use recorded CSV events for instruction counts even without optional replay.

    Never add counts from CSV and replay together: they represent the same audio.
    Event clocks are Unity time; no conversion to replay time is made here.
    """
    manifest = run / "object_manifest.csv"
    targets = set()
    if manifest.exists():
        inputs.add(manifest)
    for _, r in csv_rows(manifest):
        tid = r.get("trial_id")
        if tid not in trials:
            raise ValueError(f"Object references unknown trial: {tid}")
        if r.get("is_target", "").lower() not in ("1", "true"):
            continue
        if tid in targets:
            raise ValueError(f"Multiple manifest targets: {tid}")
        targets.add(tid)
        trials[tid].row.update(
            target_object_id=r.get("object_id"),
            target_shape=r.get("shape"),
            target_color=r.get("color"),
            coordinate_frame="unity_world",
            target_x=number(r.get("x")),
            target_y=number(r.get("y")),
            target_z=number(r.get("z")),
        )
    voice_path = run / "voice-library-manifest.json"
    clips = {}
    if voice_path.exists():
        inputs.add(voice_path)
        for clip in json.loads(voice_path.read_text(encoding="utf-8-sig")).get(
            "clips", []
        ):
            if clip.get("clip_id"):
                clips[clip["clip_id"]] = clip.get("voice_id")
    path = run / "trial_events.csv"
    if not path.exists():
        return
    inputs.add(path)
    state = {}
    for line, r in csv_rows(path):
        if r.get("participant_id") != str(summary["participant_id"]) or r.get(
            "run_number"
        ) != str(summary["run_number"]):
            raise ValueError(f"Event identity mismatch: {line}")
        tid = r.get("trial_id")
        if not tid:
            continue
        if tid not in trials:
            raise ValueError(f"Event references unknown trial: {tid}")
        t = trials[tid]
        if r.get("voice_condition") and r["voice_condition"] != t.row.get(
            "voice_condition"
        ):
            raise ValueError(f"Event voice mismatch: {tid}")
        st = state.setdefault(
            tid,
            {
                "started": False,
                "paused": False,
                "ended": False,
                "count": 0,
                "interrupted": 0,
                "active": None,
                "voices": set(),
            },
        )
        kind = r.get("event_type")
        if kind == "search_start":
            if st["started"]:
                raise ValueError(f"Duplicate CSV search onset: {tid}")
            st["started"] = True
        elif kind == "search_paused":
            st["paused"] = True
        elif kind == "search_resumed":
            st["paused"] = False
        elif kind in ("capture_correct", "session_stopped"):
            st["ended"] = True
        # Parse only the metadata before free text, which may contain semicolons.
        detail = dict(
            piece.split("=", 1)
            for piece in r.get("detail", "").split(";text=", 1)[0].split(";")
            if "=" in piece
        )
        context = detail.get("context")
        if kind == "audio_playback_start" and clips.get(detail.get("clip_id")):
            st["voices"].add(clips[detail["clip_id"]])
        if not st["started"] or st["paused"] or st["ended"]:
            continue
        if kind == "audio_playback_start":
            st["active"] = (
                context if context in ("tip", "guidance", "guidance_repeat") else None
            )
            if st["active"]:
                st["count"] += 1
        elif (
            kind in ("audio_playback_end", "audio_cancelled", "audio_failure")
            and context == st["active"]
        ):
            if st["active"] and kind != "audio_playback_end":
                st["interrupted"] += 1
            st["active"] = None
    for tid, st in state.items():
        t = trials[tid]
        if st["started"]:
            t.row.update(
                instruction_count=st["count"],
                interrupted_instruction_count=st["interrupted"],
                instruction_count_status="observed_playback_starts",
                instruction_count_source="trial_events_csv",
            )
        if len(st["voices"]) == 1:
            t.row["actual_voice_id"] = next(iter(st["voices"]))
        elif len(st["voices"]) > 1:
            t.row["actual_voice_id"] = None
            t.flags.add("multiple_recorded_voice_ids")


def add_replays(
    run: Path,
    replays: Path | None,
    summary: dict,
    trials: dict,
    inputs: set[Path],
    warnings: list[str],
    max_gap: float,
) -> None:
    if replays is None:
        return
    if not replays.exists():
        raise FileNotFoundError(replays)
    paths = [replays] if replays.is_file() else sorted(replays.rglob("recording.jsonl"))
    expected = (str(summary["participant_id"]), str(summary["run_number"]), run.name)
    for path in paths:
        path = path.resolve()
        scan_warnings = []
        identities = set()
        header = None
        for _, r in replay_rows(path, scan_warnings):
            if r["kind"] == "header":
                header = r
            if not r.get("practice") and r.get("sourceRunFolder"):
                identities.add(
                    (
                        r.get("participantCode"),
                        str(r.get("runNumber")),
                        r["sourceRunFolder"],
                    )
                )
        if expected not in identities:
            continue
        if len(identities) != 1:
            raise ValueError(f"Replay links multiple measured runs: {path}")
        inputs.add(path)
        warnings.extend(scan_warnings)
        recording_id = header.get("recordingId")
        if not recording_id:
            raise ValueError(f"Missing recording identity: {path}")
        touched = set()
        for _, r in replay_rows(path, []):
            if r["kind"] in ("header", "end"):
                continue
            practice = r.get("practice") is True
            if practice:
                if r.get("participantCode") != expected[0]:
                    raise ValueError("Practice participant mismatch")
                if not r.get("trialId"):
                    continue
                tid = f"{recording_id}:{r['trialId']}"
                if tid not in trials:
                    trials[tid] = Trial(
                        {
                            "export_schema": SCHEMA,
                            "participant_id": expected[0],
                            "run_number": None,
                            "session_id": None,
                            "trial_id": tid,
                            "is_practice": True,
                            "voice_condition": r.get("voice"),
                            "outcome": "incomplete",
                            "completed": False,
                        }
                    )
            else:
                if (
                    r.get("participantCode"),
                    str(r.get("runNumber")),
                    r.get("sourceRunFolder"),
                ) != expected:
                    continue
                tid = r.get("sourceTrialId")
                if not tid:
                    continue
                if tid not in trials:
                    raise ValueError(f"Replay trial absent from summary: {tid}")
            t = trials[tid]
            if t.row.get("replay_file") not in (None, str(path)):
                raise ValueError(f"Multiple replay sources for trial: {tid}")
            t.row.update(
                replay_file=str(path),
                replay_recording_id=recording_id,
                build_guid=header.get("buildGuid"),
                app_version=header.get("appVersion"),
                replay_sampling=r.get("sampling") or header.get("sampling"),
            )
            if (
                t.row.get("voice_condition")
                and r.get("voice")
                and t.row["voice_condition"] != r["voice"]
            ):
                raise ValueError(f"Replay voice mismatch: {tid}")
            touched.add(tid)
            t.consume(r, max_gap)
        if scan_warnings:
            for tid in touched:
                trials[tid].flags.add("replay_incomplete")


def join_surveys(
    run: Path,
    summary: dict,
    trials: dict,
    paths: list[Path],
    inputs: set[Path],
    warnings: list[str],
) -> None:
    native = run / "nasa_tlx.csv"
    paths = ([native] if native.exists() else []) + paths
    names = set()
    seen = set()
    for path in paths:
        path = path.resolve()
        name = path.stem
        if name in names or not re.fullmatch(r"[A-Za-z0-9_-]+", name):
            raise ValueError(
                "Survey filenames need distinct alphanumeric/underscore/hyphen stems"
            )
        if not path.is_file():
            raise FileNotFoundError(path)
        names.add(name)
        inputs.add(path)
        for line, r in csv_rows(path):
            if not r.get("participant_id") or not r.get("run_number"):
                raise ValueError(
                    f"Survey requires participant_id and run_number: {path}:{line}"
                )
            if r["participant_id"] != str(summary["participant_id"]) or r[
                "run_number"
            ] != str(summary["run_number"]):
                continue
            tid, block = r.get("trial_id", ""), r.get("block", "")
            scope = "trial" if tid else "block" if block != "" else "session"
            key = (name, scope, tid if tid else block)
            if key in seen:
                raise ValueError(f"Duplicate survey response scope: {key}")
            seen.add(key)
            measured = [t for t in trials.values() if not t.row["is_practice"]]
            targets = (
                (
                    [trials[tid]]
                    if tid in trials and not trials[tid].row["is_practice"]
                    else []
                )
                if tid
                else [
                    t
                    for t in measured
                    if block == "" or str(t.row.get("block_index")) == block
                ]
            )
            if not targets:
                raise ValueError(f"Survey has no matching trial/block: {path}:{line}")
            for t in targets:
                if block and str(t.row.get("block_index")) != block:
                    raise ValueError("Survey trial/block mismatch")
                voice = r.get("voice_condition", "")
                if (
                    not voice
                    and path == native
                    and r.get("condition", "").startswith("gaze_aware_voice-")
                ):
                    voice = r["condition"].removeprefix("gaze_aware_voice-")
                if voice and voice != t.row.get("voice_condition"):
                    raise ValueError(f"Survey voice mismatch: {path}:{line}")
                prefix = f"survey__{name}__{scope}__"
                t.row.update({prefix + k: v for k, v in r.items()})
                t.row.update(
                    {
                        prefix + "scope": scope,
                        prefix + "source_file": str(path),
                        prefix + "source_row": line,
                        prefix + "response_id": r.get("response_id")
                        or f"source_row:{name}:{line}",
                        prefix + "instrument_version": r.get("instrument_version")
                        or "not_recorded",
                    }
                )
                t.row["survey_status"] = "available"
                if not r.get("response_id") or not r.get("instrument_version"):
                    t.flags.add("survey_metadata_incomplete")
                if not voice:
                    t.flags.add("survey_voice_not_recorded")
                if path == native:
                    for item in (
                        "mental",
                        "physical",
                        "temporal",
                        "performance",
                        "effort",
                        "frustration",
                    ):
                        t.row["nasa_tlx_" + item] = r.get(item)
    for t in trials.values():
        t.row.setdefault(
            "survey_status",
            "not_applicable_practice"
            if t.row["is_practice"]
            else "pending_or_not_collected",
        )


def fingerprint(path: Path) -> dict:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return {"path": str(path), "bytes": path.stat().st_size, "sha256": h.hexdigest()}


def export(
    run: Path,
    output: Path,
    replays: Path | None = None,
    survey_paths: list[Path] | None = None,
    max_gap_seconds: float = 0.1,
) -> dict:
    run, output = run.resolve(), output.resolve()
    if output.exists():
        raise FileExistsError(f"Choose a new export directory: {output}")
    if not math.isfinite(max_gap_seconds) or max_gap_seconds <= 0:
        raise ValueError("max_gap_seconds must be finite and positive")
    summary, trials = read_summary(run)
    inputs = {run / "trial_summary.json"}
    warnings = []
    # Preserve planned geometry when a trial never reaches a replay snapshot.
    for _, r in csv_rows(run / "trial_schedule.csv"):
        inputs.add(run / "trial_schedule.csv")
        if r.get("participant_id") != str(summary["participant_id"]):
            raise ValueError("Schedule participant mismatch")
        tid = f"{summary.get('session_id') or str(summary['participant_id']) + '_run' + str(summary['run_number']).zfill(3)}_r{int(r['trial_index']):02d}"
        if tid not in trials:
            raise ValueError(f"Schedule trial absent from summary: {tid}")
        trials[tid].row.update({"source_schedule__" + k: v for k, v in r.items()})
    add_source_records(run, summary, trials, inputs)
    add_replays(run, replays, summary, trials, inputs, warnings, max_gap_seconds)
    join_surveys(run, summary, trials, survey_paths or [], inputs, warnings)
    rows = [t.finish() for t in trials.values()]
    fields = (
        BASE_FIELDS + sorted(set().union(*(r.keys() for r in rows)) - set(BASE_FIELDS))
        if rows
        else BASE_FIELDS
    )
    manifest = {
        "schema": SCHEMA,
        "participant_id": str(summary["participant_id"]),
        "run_number": summary["run_number"],
        "trial_count": len(rows),
        "max_motion_gap_seconds": max_gap_seconds,
        "warnings": sorted(set(warnings)),
        "inputs": [fingerprint(p) for p in sorted(inputs)],
        "trials": [],
        "contact_policy": CONTACT_METHOD
        + "; interaction proxy, no persistence threshold, not validated fixation",
        "motion_policy": "observed adjacent tracked samples only; no bridging pauses, invalid tracking or gaps; no interpolation",
        "geometry_policy": "first tracked post-onset head pose to recorded target; vertical quota reference remains unspecified",
        "survey_policy": "raw responses repeated with scope/provenance; do not count block answers as independent trial observations",
        "missing_policy": "blank means unavailable/not observed; see status columns; zero requires observed evidence",
        "reconstruction_policy": "retain source JSONL/audio/scene/gaze files; these summaries do not replace them",
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix=".trial-csv-", dir=output.parent
    ) as temporary:
        staged = Path(temporary) / "package"
        staged.mkdir()

        def write(path: Path, values: list[dict]) -> None:
            with path.open("w", encoding="utf-8", newline="") as f:
                w = csv.DictWriter(f, fieldnames=fields)
                w.writeheader()
                for row in values:
                    w.writerow({k: encode(v) for k, v in row.items()})
                f.flush()
                os.fsync(f.fileno())

        for row in rows:
            tid = row["trial_id"]
            safe = re.sub(r"[^A-Za-z0-9_-]", "_", tid)[:100]
            suffix = hashlib.sha256(tid.encode()).hexdigest()[:10]
            name = f"trial_{safe}_{suffix}_performance_subjective.csv"
            write(staged / name, [row])
            manifest["trials"].append(
                {"trial_id": tid, "file": name, "is_practice": row["is_practice"]}
            )
        write(staged / "trials.csv", rows)
        (staged / "manifest.json").write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n"
        )
        if output.exists():
            raise FileExistsError(output)
        os.rename(staged, output)
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--replays", type=Path)
    parser.add_argument("--survey", type=Path, action="append", default=[])
    parser.add_argument("--max-motion-gap-seconds", type=float, default=0.1)
    args = parser.parse_args()
    report = export(
        args.run, args.output, args.replays, args.survey, args.max_motion_gap_seconds
    )
    print(f"Exported {report['trial_count']} trials to {args.output}")
    for warning in report["warnings"]:
        print("NOTE:", warning)


if __name__ == "__main__":
    main()
