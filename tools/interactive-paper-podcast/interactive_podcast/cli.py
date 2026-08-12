"""CLI for preparing and serving interactive episodes."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys
import webbrowser

from .engine import ConversationEngine
from .models import configured_model
from .server import App, build_server
from .source import chunks, extract_pdf, transcript_from_source
from .store import SessionStore


PACKAGE_ROOT = Path(__file__).resolve().parent.parent


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(prog="interactive-paper-podcast")
    sub = result.add_subparsers(dest="command", required=True)
    prepare = sub.add_parser("prepare", help="Create a persistent interactive session")
    prepare.add_argument("pdf", type=Path)
    prepare.add_argument("--title")
    prepare.add_argument("--transcript", type=Path)
    prepare.add_argument("--audio", type=Path)
    prepare.add_argument("--data-dir", type=Path, default=PACKAGE_ROOT / "data")
    prepare.add_argument("--model-provider", choices=["auto", "ollama", "openai"], default="auto")
    serve = sub.add_parser("serve", help="Run the local interactive player")
    serve.add_argument("--data-dir", type=Path, default=PACKAGE_ROOT / "data")
    serve.add_argument("--model-provider", choices=["auto", "ollama", "openai"], default="auto")
    serve.add_argument("--host", default="127.0.0.1")
    serve.add_argument("--port", type=int, default=8765)
    serve.add_argument("--open", action="store_true")
    return result


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    model = configured_model(args.model_provider)
    store = SessionStore(args.data_dir)
    engine = ConversationEngine(store, model)
    if args.command == "prepare":
        source_text = extract_pdf(args.pdf)
        if args.transcript and not args.transcript.expanduser().is_file():
            raise ValueError(f"Transcript does not exist: {args.transcript.expanduser().resolve()}")
        if args.audio and not args.audio.expanduser().is_file():
            raise ValueError(f"Audio does not exist: {args.audio.expanduser().resolve()}")
        transcript = args.transcript.read_text(encoding="utf-8") if args.transcript else transcript_from_source(source_text)
        audio_path = str(args.audio.expanduser().resolve()) if args.audio else None
        indexed_chunks = chunks(source_text) + [f"PODCAST TRANSCRIPT:\n{part}" for part in chunks(transcript, 4_000)]
        session = engine.create(args.title or args.pdf.stem, indexed_chunks, transcript, audio_path)
        print(session["id"])
        return 0
    app = App(engine, store, PACKAGE_ROOT / "web")
    server = build_server(args.host, args.port, app)
    url = f"http://{args.host}:{server.server_port}"
    print(f"Interactive podcast running at {url}", flush=True)
    if args.open:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
