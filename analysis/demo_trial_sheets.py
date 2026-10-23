"""Generate a small, deterministic SYNTHETIC trial export for schema review.

No participant data, device measurements, audio or validated survey responses are
generated. Three example rows are not a replacement for the 40-trial schedule.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path

from trial_csv import export
from trial_column_dictionary import definition

PARTICIPANT = "SYNTHETIC_DEMO"
SESSION = PARTICIPANT + "_run001"


def write_csv(path: Path, rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def vec(x=0, y=0, z=0):
    return dict(x=x, y=y, z=z)


def generate(output: Path) -> dict:
    """Create sources and pass them through the same exporter used for real runs."""
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    run = output / "sources" / "synthetic_demo_run001"
    run.mkdir(parents=True)
    replay = output / "sources" / "replays"
    replay.mkdir()
    records = [{"kind": "header", "time": 0, "recordingId": "synthetic-demo-v1",
                "participantCode": PARTICIPANT, "sessionMode": "synthetic_demo",
                "repeatEventVersion": "guidance-repeat-v1", "sampling": "synthetic_20hz_not_hardware"}]
    objectives = []
    cases = ["clean_completion", "wrong_selection_then_completion", "paused_interrupted_incomplete"]
    for index, case in enumerate(cases):
        tid = f"{SESSION}_r{index:02d}"
        start = 1 + index * 10
        end = [2, 3, 2.5][index]
        completed = index != 2
        theta = [45, 90, -45][index]
        target = vec(3 * math.sin(math.radians(theta)), [2, 1.2, 1.8][index],
                     3 * math.cos(math.radians(theta)))
        context = dict(participantCode=PARTICIPANT, runNumber=1,
                       sourceRunFolder=run.name, sourceTrialId=tid,
                       trialId=f"trial-{index}", practice=False, voice="neutral",
                       sessionMode="synthetic_demo")

        def add(kind, seconds, **fields):
            records.append(dict(context, kind=kind, time=round(start + seconds, 6), **fields))

        add("trial", -0.5, layout="rotational_scatter_v4", targetId="target", startPlane=0, targetPlane=[1, 2, 7][index],
            signedTheta=theta, absoluteTheta=abs(theta), selectionMethod="controller_ray_trigger",
            objects=[dict(id="target", target=True, shape="Cube", color="Red", position=target),
                     dict(id="distractor", target=False, shape="Sphere", color="Blue", position=vec(0, 1.5, 3))])
        add("readiness_completed", -.25, readinessPosition=vec(0, 1.6, 3), readinessWall=0)
        add("search_start", 0, searchSeconds=0)
        add("audio_playback_start", 0.1, searchSeconds=0.1,
            detail="guidance;text=Look " + ("left" if theta < 0 else "right"))
        add("audio_cancelled" if index == 2 else "audio_playback_end", 0.3,
            searchSeconds=0.3, detail="guidance;")
        if index == 1:
            add("selection", 0.8, searchSeconds=0.8, correct=False, objectId="distractor", selectingHand="right")
            add("audio_playback_start", 1.1, searchSeconds=1.1, detail="guidance;text=A little higher")
            add("audio_playback_end", 1.3, searchSeconds=1.3, detail="guidance;")
        if index == 2:
            add("search_paused", 0.5, searchSeconds=0.5)
            add("search_resumed", 1.5, searchSeconds=0.5)
        for frame in range(int(end * 20) + 1):
            seconds = frame / 20
            paused = index == 2 and 0.5 <= seconds < 1.5
            elapsed = seconds if index != 2 else seconds - min(1, max(0, seconds - 0.5))
            elapsed = round(elapsed, 6)
            head = vec(seconds * 0.01, 1.6, 0)
            direction = [target[k] - head[k] for k in "xyz"]
            norm = math.sqrt(sum(v * v for v in direction))
            rotation = math.radians(theta * min(1, elapsed / end)) / 2
            invalid = index == 2 and frame == 35
            gaze_hit = elapsed >= 0.6 and not paused and not invalid
            controller_hit = elapsed >= 0.9 and not paused
            pose = dict(searchSeconds=round(elapsed, 6), searchActive=not paused,
                        headAvailable=True, headTracked=1, headPosition=head,
                        headRotation=dict(x=0, y=math.sin(rotation), z=0, w=math.cos(rotation)),
                        gazeAvailable=not invalid, gazeTracked=0 if invalid else 1,
                        gazeOrigin=head, gazeDirection=vec(*(v / norm for v in direction)) if gaze_hit else vec(0, 0, 1),
                        hoveredId="target" if gaze_hit else "",
                        rightController=dict(available=True, rayAvailable=True, tracked=1,
                                             position=vec(0.2, 1.2, 0), rotation=dict(x=0, y=0, z=0, w=1),
                                             triggerValue=1 if completed and seconds == end else 0,
                                             hoveredId="target" if controller_hit else ""))
            add("sample", seconds, **pose)
        if completed:
            add("selection", end, correct=True, objectId="target", selectingHand="right", **pose)
        objectives.append(dict(index=index, trial_id=tid, block=0, voice_condition="neutral",
                               demo_scenario=case, outcome="completed" if completed else "interrupted",
                               completed=completed, search_started_at=1000 + start,
                               capture_at=1000 + start + end if completed else -1,
                               search_exposure_seconds=end if completed else 1.5,
                               wrong_captures=1 if index == 1 else 0))
    records.append(dict(kind="end", time=30, complete=False, reason="synthetic_interrupted_session"))
    records.sort(key=lambda record: record["time"])
    with (replay / "recording.jsonl").open("w", encoding="utf-8") as stream:
        for sequence, record in enumerate(records):
            record.update(schema=2, sequence=sequence, frame=sequence)
            stream.write(json.dumps(record) + "\n")
    summary = dict(participant_id=PARTICIPANT, run_number=1, session_id=SESSION,
                   session_mode="synthetic_demo", rounds_per_block=40, voice_order="demo_neutral_only",
                   neutral_voice_id="synthetic-no-audio", theta_reference="demo_initial_forward_wall",
                   objectives=objectives)
    (run / "trial_summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    survey_base = dict(participant_id=PARTICIPANT, run_number=1, block=0, voice_condition="neutral")
    write_csv(run / "nasa_tlx.csv", [dict(survey_base, response_id="SYNTHETIC_TLX_BLOCK0",
              instrument_version="synthetic_demo_raw_dimensions_0_100", scale_min=0, scale_max=100,
              performance_direction="high_is_poor", mental=40, physical=20,
              temporal=30, performance=25, effort=45, frustration=15)])
    survey = output / "sources" / "reliance.csv"
    write_csv(survey, [dict(survey_base, response_id="SYNTHETIC_RELIANCE_BLOCK0",
                           instrument_version="draft_demo_not_validated", used_guidance_percent=80,
                           willing_to_receive_instruction_1_7=6)])
    report = export(run, output / "export", replay, [survey], include_motion=True)
    with (output / "export/trials.csv").open(newline="") as stream:
        fields = csv.DictReader(stream).fieldnames
    write_csv(output / "trial_column_dictionary.csv",
              [dict(column=name, meaning=definition(name)) for name in fields])
    (output / "README.md").write_text(
        "# Synthetic trial sheets\n\nAll data are invented for software/schema review. "
        "No participant was measured. Exclude all rows from study analysis.\n\n"
        "Three examples: clean completion; wrong selection then completion; paused/interrupted "
        "incomplete trial. They are a schema excerpt, not a balanced 40-trial block. "
        "The intentionally incomplete recording flags all associated trials.\n\n"
        "Block-level survey answers repeat across three rows with the same response ID. "
        "These are not three independent responses. The Raw NASA-TLX mean uses explicit "
        "0-100 metadata with high performance numbers meaning poor performance.\n\n"
        "20 Hz is an artificial fixture cadence, not measured headset or native gaze frequency. "
        "Retain source JSONL for object state and events; motion CSV alone cannot recreate the scene.\n")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = generate(args.output)
    print(f"Generated {report['trial_count']} synthetic trials and {report['motion']['row_count']} pose records")
