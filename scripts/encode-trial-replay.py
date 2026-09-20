#!/usr/bin/env python3
"""Encode a completed Unity replay export into a labeled MP4 without overwriting files."""
import argparse
import json
import shutil
import subprocess
from pathlib import Path


def encode(folder, output=None):
    folder = Path(folder).resolve(strict=True)
    manifest = json.loads((folder / "export.json").read_text())
    if manifest.get("schema") != 1 or not manifest.get("complete"):
        raise ValueError("Export is unsupported or incomplete")
    frames, fps = manifest["frames"], manifest["fps"]
    if not isinstance(frames, int) or not 1 <= frames <= 72000 or not isinstance(fps, int) or not 1 <= fps <= 120:
        raise ValueError("Invalid export frame count or rate")
    for index in range(frames):
        if not (folder / f"frame_{index:06d}.png").is_file():
            raise ValueError(f"Missing frame {index}")
    if not (folder / "audio.wav").is_file():
        raise ValueError("Missing audio mix")
    executable = shutil.which("ffmpeg")
    if not executable:
        raise ValueError("ffmpeg is required; install it and retry")
    output = Path(output).resolve() if output else folder / "demo.mp4"
    if output.exists():
        raise ValueError("Output already exists; choose a new filename")
    source_type = manifest.get("sourceType", "unknown")
    label = "SYNTHETIC DEMO - NO PARTICIPANT DATA" if source_type == "synthetic_demo" else "RECORDED TRIAL REPLAY"
    if source_type == "desktop_rehearsal":
        label = "DESKTOP MOUSE REHEARSAL - NOT HEADSET DATA"
    if manifest.get("warning"):
        label += " - INCOMPLETE COVERAGE"
    # Label is selected from constants, never from an untrusted recording field.
    overlay = f"drawtext=text='{label}':x=20:y=20:fontsize=24:fontcolor=white:box=1:boxcolor=black@0.75"
    command = [executable, "-nostdin", "-n", "-framerate", str(fps), "-i", str(folder / "frame_%06d.png"),
               "-i", str(folder / "audio.wav"), "-vf", overlay, "-frames:v", str(frames),
               "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart", str(output)]
    subprocess.run(command, check=True)
    return output


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("export_folder", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        print(encode(args.export_folder, args.output))
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Replay encoding failed: {error}\n")
