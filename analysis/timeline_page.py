"""Package prepared run timeline data as a local page with optional headset video."""

import argparse
import json
from pathlib import Path


def build(source: Path, output: Path) -> None:
    data = json.loads(source.read_text())
    if not data.get("participant") or not data.get("utc") or not data.get("trials"):
        raise ValueError(
            "Timeline needs participant, recording utc and at least one trial"
        )
    for trial in data["trials"]:
        if (
            not isinstance(trial.get("start"), (float, int))
            or not isinstance(trial.get("end"), (float, int))
            or trial["end"] <= trial["start"]
        ):
            raise ValueError("Invalid trial recording interval")
    assets = Path(__file__).parent / "timeline"
    # JSON is untrusted data, including spoken cue strings. Escape HTML script delimiters.
    payload = json.dumps(data, ensure_ascii=True, allow_nan=False).replace(
        "<", "\\u003c"
    )
    fragment = (
        (assets / "template.html")
        .read_text()
        .replace("__VIDEO_CODE__", (assets / "video.js").read_text())
    )
    fragment = fragment.replace("__P20_DATA__", payload)
    css = (assets / "page.css").read_text()
    page = (
        """<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; media-src blob:; img-src data:; connect-src 'none'; object-src 'none'; base-uri 'none'">
<title>Recorded run timeline</title><style>"""
        + css
        + "</style></head><body><main>"
        + fragment
        + "</main></body></html>"
    )
    with output.open("x") as handle:
        handle.write(page)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    build(args.data, args.output)
    print(f"Timeline saved to {args.output}")
