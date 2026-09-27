"""Export one wide CSV from a run and matching replay packages (stdlib only)."""
from __future__ import annotations

import argparse
import csv
import json
import os
from contextlib import closing
import sqlite3
import tempfile
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterator

SCHEMA = "mega-csv-v1"
Row = dict[str, Any]
BASE = ["export_schema", "row_type", "participant_id", "run_number", "trial_id", "block",
        "is_practice", "frame", "join_status", "source_file", "source_row", "replay_file", "replay_sequence"]


def flat(value: dict, prefix: str) -> Row:
    """Keep source namespaces distinct; variable-length lists remain JSON cells."""
    result: Row = {}
    for key, item in value.items():
        name = prefix + key
        if isinstance(item, dict):
            result.update(flat(item, name + "__"))
        else:
            result[name] = json.dumps(item, ensure_ascii=False) if isinstance(item, list) else item
    return result


def csv_rows(path: Path) -> Iterator[tuple[int, Row]]:
    if not path.exists():
        return
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if not reader.fieldnames or len(set(reader.fieldnames)) != len(reader.fieldnames):
            raise ValueError(f"Missing/duplicate CSV header: {path}")
        for number, row in enumerate(reader, 2):
            if None in row or any(v is None for v in row.values()):
                raise ValueError(f"Malformed CSV record {number}: {path}")
            yield number, row


def replay_rows(path: Path, warnings: list[str]) -> Iterator[tuple[int, Row]]:
    """Only tolerate a truncated final fragment; do not silently skip interior damage."""
    previous_sequence = -1
    previous_time = -1.0
    schema = None
    ended = False
    with path.open(encoding="utf-8") as stream:
        for number, line in enumerate(stream, 1):
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                if not line.rstrip().endswith("}") and not stream.read(1):
                    warnings.append(f"Truncated final replay fragment: {path}:{number}")
                    break
                raise ValueError(f"Corrupt replay record: {path}:{number}") from None
            if not isinstance(record, dict) or record.get("schema") not in (1, 2):
                raise ValueError(f"Unsupported replay schema: {path}:{number}")
            if number == 1:
                schema = record["schema"]
                if record.get("kind") != "header":
                    raise ValueError(f"Replay has no header: {path}")
            sequence, time = record.get("sequence", -1), record.get("time", -1)
            if (record["schema"] != schema or sequence <= previous_sequence or
                    not isinstance(time, (int, float)) or not 0 <= time < float("inf") or
                    time < previous_time or ended):
                raise ValueError(f"Invalid replay ordering: {path}:{number}")
            previous_sequence, previous_time = sequence, time
            ended = record.get("kind") == "end"
            if ended and not record.get("complete"):
                warnings.append(f"Replay marked incomplete: {path}")
            yield number, record
    if not ended:
        warnings.append(f"Replay has no completion footer: {path}")


def export(run: Path, output: Path, replays: Path | None = None,
           survey_paths: list[Path] | None = None) -> dict:
    run, output = run.resolve(), output.resolve()
    report_path = output.with_suffix(".manifest.json")
    if output.exists() or report_path.exists():
        raise FileExistsError(f"Export exists; choose a new output path: {output}")
    summary_path = run / "trial_summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
    participant, run_number = str(summary["participant_id"]), str(summary["run_number"])
    warnings: list[str] = []
    inputs: set[Path] = {summary_path}
    trials: dict[str, Row] = {}
    schedules: dict[str, Row] = {}
    block_by_trial: dict[str, str] = {}
    rounds_per_block = summary.get("rounds_per_block")
    for trial in summary.get("objectives", []):
        index = int(trial["index"])
        tid = trial.get("trial_id") or f"{participant}_run{int(run_number):03d}_r{index:02d}"
        if tid in trials:
            raise ValueError(f"Duplicate trial identity: {tid}")
        trials[tid] = trial
        if "block" in trial:
            block_by_trial[tid] = str(trial["block"])
        elif rounds_per_block:
            block_by_trial[tid] = str(index // int(rounds_per_block))
    for _, schedule in csv_rows(run / "trial_schedule.csv"):
        inputs.add(run / "trial_schedule.csv")
        if schedule.get("participant_id") != participant:
            raise ValueError("Schedule participant does not match this run")
        tid = f"{participant}_run{int(run_number):03d}_r{int(schedule['trial_index']):02d}"
        if tid in schedules:
            raise ValueError(f"Duplicate planned trial: {tid}")
        schedules[tid] = schedule
        block_by_trial[tid] = schedule["block_index"]
    session = flat({k: v for k, v in summary.items() if k != "objectives"}, "session__")
    targets: dict[str, list[Row]] = defaultdict(list)
    for _, item in csv_rows(run / "object_manifest.csv"):
        if item.get("is_target", "").lower() in ("1", "true"):
            targets[item.get("trial_id", "")].append(item)
    surveys: list[tuple[Path, int, Row, str, str]] = []
    survey_lookup: dict[tuple[str, str], list[Row]] = defaultdict(list)
    names: set[str] = set()
    paths = ([run / "nasa_tlx.csv"] if (run / "nasa_tlx.csv").exists() else []) + (survey_paths or [])
    for path in paths:
        path = path.resolve()
        if path.stem in names:
            raise ValueError("Survey filenames must have distinct stems")
        names.add(path.stem)
        inputs.add(path)
        for number, survey in csv_rows(path):
            # External survey files require exact participant AND run linkage.
            if survey.get("participant_id") != participant:
                continue
            if path.parent != run and str(survey.get("run_number", "")) != run_number:
                continue
            if survey.get("run_number", run_number) != run_number:
                continue
            block = survey.get("block", "")
            payload = flat(survey, f"survey__{path.stem}__")
            payload[f"survey__{path.stem}__source_row"] = number
            tid = survey.get("trial_id", "")
            scope = "trial:" + tid if tid else "block:" + block if block != "" else "run"
            surveys.append((path, number, payload, block, tid))
            survey_lookup[(path.stem, scope)].append(payload)
    for (name, block), values in survey_lookup.items():
        if len(values) > 1:
            warnings.append(f"Multiple {name} responses for block {block!r}; preserved as rows, not broadcast")

    def context(tid: str = "", practice: bool = False) -> Row:
        row: Row = {"export_schema": SCHEMA, "participant_id": participant,
                    "run_number": "" if practice else run_number, "trial_id": tid,
                    "is_practice": int(practice)}
        # Measured-run aggregates/surveys must never be attached to practice.
        if not practice:
            row.update(session)
            block = block_by_trial.get(tid, "")
            row["block"] = block
            row.update(flat(trials.get(tid, {}), "trial__"))
            row.update(flat(schedules.get(tid, {}), "schedule__"))
            if len(targets.get(tid, [])) == 1:
                row.update(flat(targets[tid][0], "target__"))
            if tid:
                for name in names:
                    for scope in (["run", "trial:" + tid] + (["block:" + block] if block != "" else [])):
                        values = survey_lookup.get((name, scope), [])
                        if len(values) == 1:
                            # A block-scoped and run-scoped response in the same
                            # instrument cannot silently overwrite each other.
                            suffix = scope.split(":", 1)[0] + "__"
                            row.update({k.replace(f"survey__{name}__", f"survey__{name}__{suffix}", 1): v
                                        for k, v in values[0].items()})
        return row

    output.parent.mkdir(parents=True, exist_ok=True)
    counts: Counter = Counter()
    columns = set(BASE)
    with tempfile.TemporaryDirectory(prefix="mega-csv-") as temporary, closing(
            sqlite3.connect(str(Path(temporary) / "gaze.sqlite"))) as db:
        db.execute("CREATE TABLE gaze (trial TEXT, frame TEXT, line INTEGER, data TEXT, used INTEGER DEFAULT 0)")
        gaze_path = run / "gaze_log.csv"
        if gaze_path.exists():
            inputs.add(gaze_path)
        for line, gaze in csv_rows(gaze_path):
            if gaze.get("participant_id", participant) != participant or gaze.get("run_number", run_number) != run_number:
                raise ValueError(f"Gaze identity mismatch at row {line}")
            db.execute("INSERT INTO gaze(trial,frame,line,data) VALUES (?,?,?,?)",
                       (gaze.get("trial_id", ""), gaze.get("frame", ""), line, json.dumps(gaze)))
        db.execute("CREATE INDEX gaze_frame ON gaze(trial,frame)")
        db.commit()
        spool = Path(temporary) / "rows.jsonl"
        with spool.open("w", encoding="utf-8") as stream:
            def emit(row: Row) -> None:
                columns.update(row)
                counts[row["row_type"]] += 1
                stream.write(json.dumps(row, ensure_ascii=False) + "\n")

            emit({**context(), "row_type": "session", "source_file": str(summary_path)})
            for tid in sorted(trials.keys() | schedules.keys()):
                emit({**context(tid), "row_type": "trial", "source_file": str(summary_path)})
            voice_path = run / "voice-library-manifest.json"
            if voice_path.exists():
                inputs.add(voice_path)
                voice = json.loads(voice_path.read_text(encoding="utf-8-sig"))
                emit({**context(), "row_type": "voice_manifest", "source_file": str(voice_path),
                      **flat({k: v for k, v in voice.items() if k != "clips"}, "voice_manifest__")})
                for index, clip in enumerate(voice.get("clips", [])):
                    emit({**context(), "row_type": "voice_clip", "source_file": str(voice_path),
                          "source_row": index, **flat(clip, "voice_clip__")})
            for path, number, payload, block, tid in surveys:
                emit({**context(), "row_type": "survey", "block": block, "trial_id": tid,
                      "source_file": str(path), "source_row": number, **payload})
            for filename, kind, prefix in [("trial_events.csv", "event", "event__"),
                                            ("object_manifest.csv", "object", "object__")]:
                path = run / filename
                if path.exists(): inputs.add(path)
                for number, data in csv_rows(path):
                    tid = data.get("trial_id", "")
                    emit({**context(tid), "row_type": kind, "source_file": str(path),
                          "source_row": number, **flat(data, prefix)})
            packages = 0
            for path in sorted(replays.rglob("recording.jsonl")) if replays and replays.exists() else []:
                scan_warnings: list[str] = []
                # Match exact coded participant, run number AND run folder. Never use proximity in time.
                def belongs(record: Row) -> bool:
                    return (record.get("participantCode") == participant and
                            str(record.get("runNumber")) == run_number and
                            record.get("sourceRunFolder") == run.name)
                matched = False
                identities = set()
                for _, record in replay_rows(path, scan_warnings):
                    matched = belongs(record) or matched
                    if not record.get("practice") and record.get("sourceRunFolder"):
                        identities.add((record.get("participantCode"), str(record.get("runNumber")), record["sourceRunFolder"]))
                if not matched: continue
                if len(identities) > 1:
                    raise ValueError(f"Replay links multiple measured runs; cannot assign its practice/header safely: {path}")
                packages += 1; inputs.add(path)
                warnings.extend(scan_warnings)
                recording_id = ""
                for number, record in replay_rows(path, []):
                    kind = record.get("kind", "unknown")
                    if kind == "header": recording_id = record.get("recordingId", "")
                    practice = bool(record.get("practice"))
                    if not belongs(record) and not practice and kind not in ("header", "end"):
                        continue
                    tid = record.get("sourceTrialId", "") if not practice else f"{recording_id}:{record.get('trialId', '')}"
                    arrays = {key: record.get(key) or [] for key in ("objects", "meshes", "changes")}
                    row = {**context(tid, practice), "row_type": "replay_" + kind,
                           "replay_file": str(path), "replay_sequence": record.get("sequence"),
                           "source_file": str(path), "source_row": number, "frame": record.get("frame"),
                           **flat({k: v for k, v in record.items() if k not in arrays}, "replay__")}
                    if kind == "sample" and not practice and tid:
                        matches = db.execute("SELECT rowid,line,data,used FROM gaze WHERE trial=? AND frame=?",
                                             (tid, str(record.get("frame", "")))).fetchall()
                        if len(matches) == 1 and matches[0][3] == 0:
                            ident, line, payload, _ = matches[0]
                            gaze = json.loads(payload)
                            if gaze.get("is_practice", "0") in ("0", ""):
                                row.update(flat(gaze, "gaze__"))
                                row.update(row_type="sample", join_status="exact_trial_frame",
                                           source_file=str(gaze_path), source_row=line)
                                db.execute("UPDATE gaze SET used=1 WHERE rowid=?", (ident,))
                        if row["row_type"] != "sample": row["join_status"] = "no_unique_unused_gaze_frame"
                    emit(row)
                    for key, prefix in (("objects", "replay_object"), ("changes", "replay_object_change")):
                        for item in arrays[key]:
                            emit({**context(tid, practice), "row_type": prefix, "replay_file": str(path),
                                  "replay_sequence": record.get("sequence"), **flat(item, prefix + "__")})
                    for mesh in arrays["meshes"]:
                        base = {**context(tid, practice), "replay_file": str(path),
                                "replay_sequence": record.get("sequence"), "mesh__id": mesh["id"]}
                        for field in ("vertices", "normals", "triangles"):
                            for index, value in enumerate(mesh.get(field) or []):
                                emit({**base, "row_type": "mesh_" + field, "mesh__index": index,
                                      **(flat(value, "mesh__") if isinstance(value, dict) else {"mesh__value": value})})
            if not packages:
                warnings.append("No matching replay: motion/replay columns cannot be populated")
            for line, payload in db.execute("SELECT line,data FROM gaze WHERE used=0 ORDER BY line"):
                gaze = json.loads(payload)
                practice = gaze.get("is_practice") == "1"
                emit({**context(gaze.get("trial_id", ""), practice), "row_type": "gaze_sample",
                      "frame": gaze.get("frame", ""), "join_status": "gaze_only",
                      "source_file": str(gaze_path), "source_row": line, **flat(gaze, "gaze__")})
        fields = BASE + sorted(columns - set(BASE))
        # Build completely before publishing; never leave a partial CSV or replace an existing one.
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", newline="", dir=output.parent,
                                         prefix=".mega-", suffix=".csv", delete=False) as result:
            staged = Path(result.name)
            try:
                writer = csv.DictWriter(result, fieldnames=fields)
                writer.writeheader()
                with spool.open(encoding="utf-8") as stream:
                    for line in stream: writer.writerow(json.loads(line))
                result.flush()
                os.fsync(result.fileno())
            except Exception:
                staged.unlink(missing_ok=True)
                raise
        try:
            os.link(staged, output)  # same-filesystem atomic, exclusive publication
        finally:
            staged.unlink(missing_ok=True)
    manifest = {"schema": SCHEMA, "participant_id": participant, "run_number": run_number,
                "rows": dict(counts), "columns": len(fields), "warnings": sorted(set(warnings)),
                "inputs": [{"path": str(p), "bytes": p.stat().st_size} for p in sorted(inputs)],
                "clock_policy": "gaze/event Unity time and replay relative monotonic time retained separately; frame joins only",
                "coordinate_policy": "gaze/object CSV world coordinates and replay fixed-origin coordinates remain separately named",
                "survey_policy": "unambiguous block/run responses repeated; count original survey rows for independent responses",
                "row_order": "grouped by source; not a cross-clock chronological sort"}
    with report_path.open("x", encoding="utf-8") as stream:
        json.dump(manifest, stream, indent=2)
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path, help="GazeData/P###/run_... directory")
    parser.add_argument("--replays", type=Path, help="Copied GazeReplays directory")
    parser.add_argument("--survey", action="append", type=Path, default=[], help="Extra CSV with participant_id, run_number and optional block")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = export(args.run, args.output, args.replays, args.survey)
    print(f"Wrote {sum(report['rows'].values())} rows, {report['columns']} columns: {args.output}")
    for warning in report["warnings"]: print("NOTE:", warning)


if __name__ == "__main__":
    main()
