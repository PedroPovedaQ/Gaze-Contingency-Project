#!/usr/bin/env python3
"""Locally transcribe a prepared MP3 library and flag script mismatches for review.

Requires local openai-whisper/model weights and ffmpeg. Never contacts a speech API,
changes audio, or treats an ASR mismatch as proof that a clip is bad.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


def words(text):
    return re.findall(r"[a-z0-9]+", text.lower().replace("’", "'").replace("'", ""))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--model", choices=["base", "small"], default="base")
    parser.add_argument("--review", type=Path, help="Only recheck mismatches from an earlier report")
    args = parser.parse_args()
    import torch
    import whisper

    torch.set_num_threads(4)
    model = whisper.load_model(args.model, device="cpu")
    clips = json.loads(args.manifest.read_text())["clips"]
    review = None if args.review is None else {
        row["clip"] for row in json.loads(args.review.read_text()) if not row["matches"]
    }
    results = []
    seen = set()
    for item in clips:
        clip = item["clip_id"]
        if clip in seen or (review is not None and clip not in review):
            continue
        seen.add(clip)
        # Manifest IDs are plain names, never paths supplied by arbitrary JSON.
        if not re.fullmatch(r"tts_[a-fA-F0-9]{64}", clip):
            raise ValueError("Unexpected clip ID")
        audio = args.cache / (clip + ".mp3")
        if not audio.exists():
            row = dict(clip=clip, expected=item["text"], matches=False, error="missing_audio")
        else:
            transcript = model.transcribe(str(audio), language="en", fp16=False,
                                          temperature=0, condition_on_previous_text=False)["text"].strip()
            row = dict(clip=clip, expected=item["text"], transcript=transcript,
                       matches=words(item["text"]) == words(transcript), model=args.model,
                       sha256=hashlib.sha256(audio.read_bytes()).hexdigest())
        results.append(row)
        args.output.write_text(json.dumps(results, indent=2) + "\n")
        if not row["matches"]:
            print(json.dumps(row), flush=True)
        elif len(results) % 25 == 0:
            print(f"Checked {len(results)} clips", flush=True)
    print(f"Checked {len(results)}; {sum(not row['matches'] for row in results)} need review", flush=True)


if __name__ == "__main__":
    main()
