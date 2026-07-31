"""PDF extraction that never relies on pdf-parse fixtures or process cwd."""

from __future__ import annotations

import hashlib
import os
from pathlib import Path
import re
import subprocess

from .errors import PdfExtractionError

MIN_TEXT_CHARS = 120


def normalize_pdf_text(raw: str) -> str:
    """Normalize extraction noise while retaining paragraph boundaries."""
    return (
        raw.replace("\r", "")
        .replace("\x00", "")
        .strip()
    )


def extract_pdf_text(
    pdf_path: Path,
    *,
    pdftotext_command: str | None = None,
    minimum_chars: int = MIN_TEXT_CHARS,
) -> str:
    """Extract a local PDF via Poppler using an explicit absolute source path."""
    source = pdf_path.expanduser().resolve()
    if not source.is_file():
        raise PdfExtractionError(f"PDF does not exist: {source}")
    if source.stat().st_size == 0:
        raise PdfExtractionError(f"PDF is empty: {source}")
    with source.open("rb") as stream:
        if stream.read(5) != b"%PDF-":
            raise PdfExtractionError(f"File does not have a PDF header: {source}")

    command = pdftotext_command or os.environ.get("P2P_PDFTOTEXT", "pdftotext")
    try:
        completed = subprocess.run(
            [command, "-layout", "-nopgbrk", str(source), "-"],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
    except FileNotFoundError as error:
        raise PdfExtractionError(
            f"PDF extraction command not found: {command}. Install Poppler or set P2P_PDFTOTEXT."
        ) from error
    except subprocess.CalledProcessError as error:
        detail = (error.stderr or "").strip()
        raise PdfExtractionError(
            f"pdftotext failed for {source}: {detail or f'exit {error.returncode}'}"
        ) from error

    text = normalize_pdf_text(completed.stdout)
    visible = re.sub(r"\s+", "", text)
    if len(visible) < minimum_chars:
        raise PdfExtractionError(
            f"PDF yielded too little extractable text ({len(visible)} non-whitespace characters)"
        )
    return text


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()
