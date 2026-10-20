"""Desktop adapter for the study's authoritative performance/subjective exporter."""
from pathlib import Path
import argparse
import csv
import hashlib
import json
import os
import re
import sys
import tempfile

HERE = Path(__file__).resolve().parent
ANALYSIS = HERE / "Analysis" if (HERE / "Analysis").is_dir() else HERE.parents[1] / "analysis"
sys.path.insert(0, str(ANALYSIS))
from mega_csv import replay_rows
from trial_csv import export, fingerprint
from trial_column_dictionary import definition


# A projection of the authoritative trial export, not another measurement engine.
# Stable source IDs allow every compact row to join back to its full quality flags.
CONDENSED_FILE = "trials_condensed.csv"
CONDENSED_FIELDS = (
    "participant_id", "trial_id", "block_index", "voice_condition",
    "absolute_theta_deg", "signed_theta_deg",
    "actual_signed_head_to_target_yaw_deg", "target_minus_head_height_m",
    "target_distance_m", "instruction_count", "search_exposure_seconds",
    "time_to_first_target_gaze_seconds", "time_to_final_target_gaze_seconds",
    "time_to_first_right_controller_target_seconds", "time_to_final_selection_seconds",
    "nasa_tlx_score", "subjective_performance_rating",
)


def write_condensed(package):
    """Keep whiteboard fields verbatim; missing measures never become zero."""
    with (package / "trials.csv").open(newline="", encoding="utf-8") as stream:
        rows = list(csv.DictReader(stream))
    # UTF-8 BOM lets Excel recognize the CSV's encoding when opened directly.
    with (package / CONDENSED_FILE).open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=CONDENSED_FIELDS)
        writer.writeheader()
        for row in rows:
            compact = {field: row.get(field, "") for field in CONDENSED_FIELDS}
            # The board's separate Performance heading has no agreed survey mapping.
            # Do not substitute TLX's performance subscale or objective completion time.
            compact["subjective_performance_rating"] = ""
            writer.writerow(compact)
    return {
        "schema": "whiteboard-trials-v1", "file": CONDENSED_FILE,
        "row_count": len(rows), "source_file": "trials.csv",
        "columns": {field: (
            "Reserved for the whiteboard's separate subjective performance rating. "
            "Blank until the instrument and source mapping are confirmed."
            if field == "subjective_performance_rating" else definition(field)
        ) for field in CONDENSED_FIELDS},
        "limitations": [
            "Same scheduled/recorded/practice rows as trials.csv; consult its outcome, practice and quality fields before analysis.",
            "participant_id is the recorded source ID; catalog reassignment remains in trials.csv and desktop-export.json.",
            "Height delta is target minus head height at the first tracked post-onset pose, not the scheduled cross-relative height quota.",
            "Blank is unavailable, not zero. Gaze contacts are ray intersections, not validated fixations.",
            "Block survey scores may repeat across trials; they are not independent trial-level responses.",
        ],
    }


def component(value):
    if not value or value in (".", "..") or "/" in value or "\\" in value:
        raise ValueError("Recording has an invalid participant/run folder identity")
    return value


def resolve_run(recording, chosen=None):
    identities = set()
    for _, record in replay_rows(recording, []):
        if record.get("kind") == "trial" and record.get("sourceRunFolder"):
            identities.add((component(record.get("participantCode")),
                            component(record["sourceRunFolder"]), record.get("runNumber")))
    if len(identities) != 1:
        raise ValueError("A single linked study run is required; this recording has no unambiguous run identity")
    participant, folder, run_number = identities.pop()
    candidates = {Path(chosen).resolve()} if chosen else set()
    if not chosen:
        # Bounded layout lookup only; never scan the user's home directory recursively.
        for parent in list(recording.parents)[:3]:
            candidates.update((parent / folder, parent / "GazeData" / participant / folder,
                               parent / "data" / "GazeData" / participant / folder))
    matches = []
    for candidate in candidates:
        summary = candidate / "trial_summary.json"
        if not summary.is_file() or candidate.name != folder:
            continue
        data = json.loads(summary.read_text(encoding="utf-8-sig"))
        if str(data.get("participant_id")) == participant and data.get("run_number") == run_number:
            matches.append(candidate.resolve())
    matches = sorted(set(matches))
    if len(matches) != 1:
        raise ValueError("Choose the matching study data folder containing trial_summary.json; automatic lookup found " + str(len(matches)) + " matches")
    return matches[0], participant


def generate(recording, root, name="", chosen=None, assigned_id="", dataset_id="", dataset_name=""):
    recording, root = Path(recording).resolve(), Path(root).resolve()
    name = name.strip()
    if len(name) > 120 or any(ord(c) < 32 for c in name):
        raise ValueError("Participant name must be at most 120 characters without line breaks")
    run, participant = resolve_run(recording, chosen)
    labels = {"participant_name": name, "assigned_participant_id": assigned_id or participant,
              "dataset_id": dataset_id, "dataset_name": dataset_name}
    if any(len(value) > 120 or any(ord(c) < 32 for c in value) for value in labels.values()):
        raise ValueError("Dataset labels must be at most 120 characters without line breaks")
    inputs = [recording] + sorted(p for p in (run / filename for filename in (
        "trial_summary.json", "trial_schedule.csv", "object_manifest.csv", "trial_events.csv",
        "voice-library-manifest.json", "nasa_tlx.csv")) if p.is_file())
    before = [fingerprint(p) for p in inputs]
    code = [HERE / "export_run.py"] + sorted(ANALYSIS.glob("*.py"))
    identity = json.dumps([before, labels, [fingerprint(p)["sha256"] for p in code]], sort_keys=True)
    key = hashlib.sha256(identity.encode()).hexdigest()[:20]
    slug = re.sub(r"[^\w-]+", "_", name or participant, flags=re.UNICODE).strip("_")[:60] or "participant"
    destination = root / (slug + "_" + key)
    if destination.exists():
        receipt = json.loads((destination / "desktop-export.json").read_text())
        if all(fingerprint(destination / item["file"])["sha256"] == item["sha256"] for item in receipt["files"]):
            return destination
        raise ValueError("An existing generated export was modified; choose a different export destination")
    root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".run-export-", dir=root) as staging:
        package = Path(staging) / "package"
        manifest = export(run, package, replays=recording)
        if not any(item["path"] == str(recording) for item in manifest["inputs"]):
            raise ValueError("The study exporter did not match this replay to the selected run")
        # Labels are additive. Stable source IDs and research calculations stay unchanged.
        for file in package.glob("*.csv"):
            with file.open(newline="", encoding="utf-8") as stream:
                reader = csv.DictReader(stream)
                fields, rows = reader.fieldnames, list(reader)
            with file.open("w", newline="", encoding="utf-8") as stream:
                writer = csv.DictWriter(stream, fieldnames=list(labels) + fields)
                writer.writeheader()
                for row in rows:
                    writer.writerow({**labels, **row})
        condensed = write_condensed(package)
        manifest.update(labels)
        manifest["condensed_export"] = condensed
        (package / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
        (package / "README.txt").write_text(
            "trials.csv: one performance/subjective summary row per scheduled or recorded round.\n"
            "trial_*_performance_subjective.csv: one round per file; practice is marked separately.\n"
            "trials_condensed.csv: Excel-compatible whiteboard subset, one row per corresponding full-export row.\n"
            "See manifest.json condensed_export for column definitions and scope. Keep trials.csv for quality/status and provenance.\n"
            "Condensed PID remains the recorded source ID; catalog reassignment is retained in trials.csv.\n"
            "Height delta is target minus head height, not cross-relative scheduled height. Subjective performance is unmapped and blank.\n"
            "participant_name, assigned_participant_id, dataset_id and dataset_name are local catalog metadata.\n"
            "participant_id and trial_id remain the original source identities.\n"
            "Blank values are unavailable, not zero. Surveys are included only when present in source data.\n"
            "Review manifest warnings and completion/status columns; a generated CSV does not prove run completion.\n"
            "Retain original replay/audio and separate motion/eye files for reconstruction and acquisition analysis.\n")
        if before != [fingerprint(p) for p in inputs]:
            raise ValueError("Source files changed during export; wait for transfer to finish")
        receipt = {"participant_id": participant, "study_data_path": str(run), **labels, "files": [
            {"file": p.name, "sha256": fingerprint(p)["sha256"]} for p in sorted(package.iterdir())]}
        (package / "desktop-export.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2))
        os.rename(package, destination)
    return destination


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--recording", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--name", default="")
    parser.add_argument("--run", type=Path)
    parser.add_argument("--assigned-id", default="")
    parser.add_argument("--dataset-id", default="")
    parser.add_argument("--dataset-name", default="")
    args = parser.parse_args()
    try:
        print(generate(args.recording, args.output, args.name, args.run, args.assigned_id, args.dataset_id, args.dataset_name))
    except (ValueError, OSError, KeyError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
