"""PDF and transcript preparation for an interactive session."""

from __future__ import annotations

from pathlib import Path
import re
import subprocess


def extract_pdf(path: Path) -> str:
    source = path.expanduser().resolve()
    if not source.is_file():
        raise ValueError(f"PDF does not exist: {source}")
    with source.open("rb") as source_file:
        header = source_file.read(5)
    if header != b"%PDF-":
        raise ValueError(f"File is not a PDF: {source}")
    try:
        completed = subprocess.run(
            ["pdftotext", "-layout", "-nopgbrk", str(source), "-"],
            check=True, capture_output=True, text=True, encoding="utf-8", errors="replace",
        )
    except FileNotFoundError as error:
        raise ValueError("pdftotext is required; install Poppler and try again") from error
    except subprocess.CalledProcessError as error:
        detail = (error.stderr or "unknown extraction error").strip()
        raise ValueError(f"PDF extraction failed: {detail}") from error
    text = completed.stdout.replace("\r", "").strip()
    if len(re.sub(r"\s+", "", text)) < 120:
        raise ValueError("PDF yielded too little extractable text")
    return text


def chunks(text: str, max_chars: int = 8_000) -> list[str]:
    blocks = [part.strip() for part in re.split(r"\n\s*\n", text) if part.strip()]
    result: list[str] = []
    current = ""
    for block in blocks:
        pieces = [block[index:index + max_chars] for index in range(0, len(block), max_chars)]
        for piece in pieces:
            candidate = piece if not current else f"{current}\n\n{piece}"
            if len(candidate) <= max_chars:
                current = candidate
            else:
                result.append(current)
                current = piece
    if current:
        result.append(current)
    return result


def transcript_from_source(text: str, max_paragraphs: int = 24) -> str:
    paragraphs = [re.sub(r"\s+", " ", part).strip() for part in re.split(r"\n\s*\n", text) if part.strip()]
    return "\n\n".join(paragraphs[:max_paragraphs])
