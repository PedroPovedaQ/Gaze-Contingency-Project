"""Read-only variance guidance from explicitly labeled historical development data.

Inputs are existing source manifests, never discovered participant directories.
No source files are written; hashes and metadata are rechecked after extraction.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

from current_design_power import write_csv
from thesis_development_plots import extract


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def snapshot(path):
    st = path.stat()
    return dict(sha256=sha(path), bytes=st.st_size, mtime_ns=st.st_mtime_ns)


def summarize(rows):
    completed = [r for r in rows if r["completed"]]
    values = np.array([r["search_seconds"] for r in completed])
    logs = np.log(values)
    runs = sorted({r["run"] for r in completed})
    residuals = []
    for run in runs:
        v = np.log([r["search_seconds"] for r in completed if r["run"] == run])
        residuals.extend(v-v.mean())
    df = len(values)-len(runs)
    return dict(started=len(rows), completed=len(completed), unfinished=len(rows)-len(completed),
                median_seconds=float(np.median(values)), log_sd=float(logs.std(ddof=1)),
                within_run_log_sd=float(np.sqrt(np.square(residuals).sum()/df)) if df > 0 else None,
                within_run_residual_df=df, completed_at_or_above_25=int((values >= 25).sum()),
                maximum_seconds=float(values.max()),
                between_participant_sd=None, voice_slope_sd=None,
                explanation="One development label; runs are not independent participants. No paired voices. "
                "Within-run centering does not adjust for geometry, learning or policy changes.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    manifest = root/"docs/thesis/development-results/source-manifest.json"
    voice_manifest = root/"docs/thesis/development-results/voice-condition-panels/sources.json"
    sources = json.loads(manifest.read_text())["sources"]
    receipts, rows = [], []
    for s in sources:
        path = Path(s["path"])
        before = snapshot(path)
        if before["sha256"] != s["sha256"]:
            raise ValueError(f"Historical source hash mismatch: {path}")
        trial_rows, _, _, _ = extract(path, s["sha256"])
        # extract enforces live_headset and the DEV_NEUTRAL label.
        after = snapshot(path)
        if before != after:
            raise ValueError(f"Source changed while reading: {path}")
        receipts.append(dict(path=str(path), **before, verified_unchanged=True,
                             run=s["run"], recording_id=s["recording_id"]))
        rows.extend(dict(run=t["run"], trial_id=t["trial_id"], voice=t["voice"],
                         completed=t["completed"], search_seconds=t["search_seconds"],
                         source_recording=t["recording_id"]) for t in trial_rows)
    early = summarize(rows)
    s = json.loads(voice_manifest.read_text())["summary"]
    path = Path(s["path"])
    before = snapshot(path)
    if before["sha256"] != s["sha256"]:
        raise ValueError("Run 30 source hash mismatch")
    data = json.loads(path.read_text())
    if data["participant_id"] != "DEV_NEUTRAL" or data["run_number"] != 30:
        raise ValueError("Expected explicitly labeled development run 30")
    latest = [dict(run=30, trial_id=t["trial_id"], voice=t["voice_condition"],
                   completed=t["completed"], search_seconds=t["search_exposure_seconds"] if t["completed"] else None,
                   source_recording="run030_trial_summary")
              for t in data["objectives"] if t["search_started_at"] >= 0]
    if before != snapshot(path):
        raise ValueError("Run 30 source changed while reading")
    receipts.append(dict(path=str(path), **before, verified_unchanged=True, run=30))
    rows.extend(latest)
    args.out.mkdir(parents=True, exist_ok=True)
    target = args.out/"development-evidence.json"
    if target.exists():
        raise ValueError("Refusing to overwrite prior evidence")
    target.write_text(json.dumps(dict(
        status="Historical development evidence; not current-design pilot estimates",
        label="DEV_NEUTRAL", unique_labels=1, self_similar_completed_trials=0,
        scope="Nine manifest-listed October 19–20 replays (runs 20–28), plus October 23 run 30 summary. "
              "Not an exhaustive search of all local data. No unlabeled participant recordings opened.",
        earlier_runs=early, run30=summarize(latest), combined=summarize(rows),
        run30_guidance=data["guidance_policy"], run30_schedule=data["challenge_set"],
        sources=receipts, manifests=[dict(path=str(p), sha256=sha(p)) for p in [manifest, voice_manifest]],
        assumptions="All simulation SDs, baseline times, effects and missingness probabilities are synthetic. "
                    "Observed descriptive SDs only motivate a broad sensitivity range; none is an empirical voice-slope estimate."
    ), indent=2)+"\n")
    write_csv(args.out/"development-trials.csv", rows)
    print(json.dumps(dict(earlier_runs=early, run30=summarize(latest), combined=summarize(rows)), indent=2))


if __name__ == "__main__":
    main()
