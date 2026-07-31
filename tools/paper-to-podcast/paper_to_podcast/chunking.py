"""Lossless, paragraph-aware chunking for full-paper processing."""

from __future__ import annotations

import re


def _split_oversized(block: str, max_chars: int) -> list[str]:
    sentences = re.split(r"(?<=[.!?])\s+", block)
    pieces: list[str] = []
    current = ""
    for sentence in sentences:
        if len(sentence) > max_chars:
            if current:
                pieces.append(current)
                current = ""
            for start in range(0, len(sentence), max_chars):
                pieces.append(sentence[start : start + max_chars])
            continue
        candidate = sentence if not current else f"{current} {sentence}"
        if len(candidate) <= max_chars:
            current = candidate
        else:
            pieces.append(current)
            current = sentence
    if current:
        pieces.append(current)
    return pieces


def chunk_text(text: str, max_chars: int = 12_000) -> list[str]:
    """Return bounded chunks without silently dropping any source block."""
    if max_chars < 200:
        raise ValueError("max_chars must be at least 200")
    normalized = text.strip()
    if not normalized:
        return []

    blocks = [part.strip() for part in re.split(r"\n\s*\n", normalized) if part.strip()]
    bounded: list[str] = []
    for block in blocks:
        bounded.extend(
            [block] if len(block) <= max_chars else _split_oversized(block, max_chars)
        )

    chunks: list[str] = []
    current = ""
    for block in bounded:
        candidate = block if not current else f"{current}\n\n{block}"
        if len(candidate) <= max_chars:
            current = candidate
        else:
            chunks.append(current)
            current = block
    if current:
        chunks.append(current)
    return chunks
