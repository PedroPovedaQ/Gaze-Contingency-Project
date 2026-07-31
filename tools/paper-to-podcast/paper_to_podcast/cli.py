"""Command-line entrypoint."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import sys

from . import __version__
from .audio import synthesize, validate_mp3
from .audio import label_transcript_for_hosts
from .chunking import chunk_text
from .errors import PaperToPodcastError
from .extract import extract_pdf_text, sha256_file
from .script import create_transcript


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="paper-to-podcast",
        description="Extract a complete academic PDF, build a grounded transcript, and synthesize a validated MP3.",
    )
    parser.add_argument("--version", action="version", version=__version__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    extract = subparsers.add_parser("extract", help="Extract normalized PDF text")
    extract.add_argument("pdf", type=Path)
    extract.add_argument("--out", type=Path, required=True)

    generate = subparsers.add_parser("generate", help="Generate transcript, metadata, and MP3")
    generate.add_argument("pdf", type=Path)
    generate.add_argument("--output-dir", type=Path, required=True)
    generate.add_argument("--slug")
    generate.add_argument("--title")
    generate.add_argument("--instructions-file", type=Path)
    generate.add_argument(
        "--transcript-file",
        type=Path,
        help="Use a reviewed transcript while retaining extraction/chunk provenance",
    )
    generate.add_argument(
        "--script-provider",
        choices=["auto", "openai", "offline"],
        default=os.environ.get("P2P_SCRIPT_PROVIDER", "auto"),
    )
    generate.add_argument(
        "--tts-provider",
        choices=["auto", "voxtral", "openai", "macos"],
        default=os.environ.get("P2P_TTS_PROVIDER", "auto"),
    )
    generate.add_argument(
        "--voxtral-voices",
        default=os.environ.get("P2P_VOXTRAL_VOICES", "alloy,echo"),
        help="Comma-separated WebKasa voice aliases or direct Voxtral voice IDs",
    )
    generate.add_argument(
        "--env-file",
        type=Path,
        help="Load only supported provider keys from a local dotenv file at runtime",
    )
    generate.add_argument(
        "--no-tts-fallback",
        action="store_true",
        help="Fail instead of falling back from an external TTS provider to macOS",
    )
    generate.add_argument("--export-dir", type=Path)
    generate.add_argument(
        "--export-all",
        action="store_true",
        help="Export transcript and metadata in addition to MP3 and source PDF",
    )
    return parser


def _safe_slug(value: str) -> str:
    result = "".join(char.lower() if char.isalnum() else "-" for char in value)
    return "-".join(filter(None, result.split("-")))[:100] or "paper-podcast"


def _load_provider_env(path: Path) -> None:
    allowed = {"MISTRAL_API_KEY", "OPENAI_API_KEY", "AI_GATEWAY_API_KEY"}
    for raw_line in path.expanduser().resolve().read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        name, value = line.split("=", 1)
        name = name.strip()
        if name not in allowed or name in os.environ:
            continue
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
            value = value[1:-1]
        os.environ[name] = value


def _generate(args: argparse.Namespace) -> dict[str, object]:
    if args.env_file:
        _load_provider_env(args.env_file)
    source = args.pdf.expanduser().resolve()
    output_dir = args.output_dir.expanduser().resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    title = args.title or source.stem
    slug = args.slug or _safe_slug(title)
    chunk_chars = int(os.environ.get("P2P_CHUNK_CHARS", "12000"))

    source_text = extract_pdf_text(source)
    chunks = chunk_text(source_text, max_chars=chunk_chars)
    instructions = ""
    if args.instructions_file:
        instructions = args.instructions_file.expanduser().resolve().read_text(encoding="utf-8")

    transcript, summaries, script_provider, script_fallback_reason = create_transcript(
        source_text,
        title=title,
        instructions=instructions,
        provider=args.script_provider,
        transcript_file=args.transcript_file,
        max_chunk_chars=chunk_chars,
    )
    voxtral_voices = [
        part.strip() for part in args.voxtral_voices.split(",") if part.strip()
    ]
    if args.tts_provider == "voxtral":
        transcript = label_transcript_for_hosts(transcript, len(voxtral_voices))
    transcript_path = output_dir / f"{slug}.transcript.txt"
    transcript_path.write_text(transcript.strip() + "\n", encoding="utf-8")
    audio_path = output_dir / f"{slug}.mp3"
    tts_provider, fallback_reason, tts_details = synthesize(
        transcript_path,
        audio_path,
        provider=args.tts_provider,
        allow_fallback=not args.no_tts_fallback,
        voxtral_voices=voxtral_voices,
    )
    validation = validate_mp3(audio_path)

    metadata_path = output_dir / f"{slug}.metadata.json"
    metadata: dict[str, object] = {
        "schema_version": 1,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "tool_version": __version__,
        "title": title,
        "source": {
            "path": str(source),
            "filename": source.name,
            "sha256": sha256_file(source),
            "size_bytes": source.stat().st_size,
            "extracted_characters": len(source_text),
        },
        "full_paper_processing": {
            "chunk_count": len(chunks),
            "max_chunk_characters": chunk_chars,
            "chunk_characters": [len(chunk) for chunk in chunks],
            "summary_count": len(summaries),
        },
        "script_provider": script_provider,
        "script_fallback_reason": script_fallback_reason,
        "tts_provider": tts_provider,
        "tts_fallback_reason": fallback_reason,
        "tts_details": tts_details,
        "transcript": {
            "path": str(transcript_path),
            "characters": len(transcript),
            "words": len(transcript.split()),
        },
        "audio": {"path": str(audio_path), **validation},
    }
    exported: list[str] = []
    export_dir: Path | None = None
    if args.export_dir:
        export_dir = args.export_dir.expanduser().resolve()
        export_dir.mkdir(parents=True, exist_ok=True)
        items = [audio_path, source]
        if args.export_all:
            items.append(transcript_path)
        for item in items:
            destination = export_dir / item.name
            if destination.resolve() != item.resolve():
                shutil.copy2(item, destination)
            exported.append(str(destination))
        if args.export_all:
            exported.append(str(export_dir / metadata_path.name))
    metadata["exported_paths"] = exported
    metadata_path.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    if export_dir and args.export_all:
        shutil.copy2(metadata_path, export_dir / metadata_path.name)
    return {
        "audio": str(audio_path),
        "transcript": str(transcript_path),
        "metadata": str(metadata_path),
        "exported": exported,
        "validation": validation,
    }


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "extract":
            text = extract_pdf_text(args.pdf)
            destination = args.out.expanduser().resolve()
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_text(text + "\n", encoding="utf-8")
            print(json.dumps({"text": str(destination), "characters": len(text)}, indent=2))
            return 0
        result = _generate(args)
        print(json.dumps(result, indent=2))
        return 0
    except (PaperToPodcastError, OSError, ValueError) as error:
        code = error.code if isinstance(error, PaperToPodcastError) else "CLI_ERROR"
        print(f"{code}: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
