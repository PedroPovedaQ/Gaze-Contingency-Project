"""Package prepared run timeline data as a local page with optional headset video."""

import argparse
import json
import math
import os
import tempfile
from pathlib import Path


def build(source: Path, output: Path) -> None:
    if output.exists():
        raise FileExistsError(output)
    data = json.loads(source.read_text())
    if not data.get("participant") or not data.get("utc") or not data.get("trials"):
        raise ValueError(
            "Timeline needs participant, recording utc and at least one trial"
        )
    for trial in data["trials"]:
        if (
            type(trial.get("start")) not in (float, int)
            or type(trial.get("end")) not in (float, int)
            or not math.isfinite(trial["start"])
            or not math.isfinite(trial["end"])
            or trial["end"] <= trial["start"]
        ):
            raise ValueError("Invalid trial recording interval")
    assets = Path(__file__).parent / "timeline"
    # Large single JSON text nodes fail in Chromium before JSON.parse can read
    # them. Store each trial separately and parse only the selected trial.
    split_trials = data.get("schema") == "replay-timeline-v1"
    metadata = data
    if split_trials:
        fields = ("id", "sourceId", "label", "targetName", "start", "end", "completeTime", "practice")
        metadata = {**data, "trials": [
            {**{key: trial.get(key) for key in fields}, "payloadId": f"p20-trial-payload-{index}"}
            for index, trial in enumerate(data["trials"])
        ]}

    def encode(value):
        # JSON is untrusted data, including spoken cue strings. Escape delimiters
        # in both metadata and trial blocks; never substitute inside source text.
        return json.dumps(value, ensure_ascii=True, allow_nan=False,
                          separators=(",", ":")).replace("<", "\\u003c")

    payload = encode(metadata)
    fragment = (
        (assets / "template.html")
        .read_text()
        .replace("__VIDEO_CODE__", (assets / "video.js").read_text())
    )
    before, after = fragment.split("__TRIAL_PAYLOADS__")
    before = before.replace("__P20_DATA__", payload)
    css = (assets / "page.css").read_text()
    prefix = (
        """<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; media-src blob:; img-src data:; connect-src 'none'; object-src 'none'; base-uri 'none'">
<title>Recorded run timeline</title><style>"""
        + css
        + "</style></head><body><main>"
    )
    # A serialization failure must not leave a seemingly usable half-page. The
    # same-directory hard link publishes atomically and refuses racing overwrites.
    with tempfile.TemporaryDirectory(prefix=".timeline-", dir=output.parent) as temporary:
        staged = Path(temporary) / "page.html"
        with staged.open("w", encoding="utf-8") as handle:
            handle.write(prefix)
            handle.write(before)
            if split_trials:
                for index, trial in enumerate(data["trials"]):
                    handle.write(f'<script id="p20-trial-payload-{index}" type="application/json">')
                    handle.write(encode(trial))
                    handle.write('</script>\n')
            handle.write(after)
            handle.write("</main></body></html>")
        os.link(staged, output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    build(args.data, args.output)
    print(f"Timeline saved to {args.output}")
