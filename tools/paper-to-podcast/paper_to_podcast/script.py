"""Full-paper map/reduce scripting plus a credential-free offline fallback."""

from __future__ import annotations

import os
from pathlib import Path
import re

from .chunking import chunk_text
from .errors import ScriptGenerationError
from .openai_client import generate_text

SUMMARY_INSTRUCTIONS = """You summarize one chunk of an academic paper for a later podcast script.
Preserve study design, sample, apparatus, measures, preprocessing thresholds, feature engineering,
model evaluation, numerical results, limitations, and uncertainty. Do not invent facts. Write
compact prose and mention that this is one chunk when context is incomplete."""

SCRIPT_INSTRUCTIONS = """You are an academic podcast writer. Write an accessible but rigorous
single-host spoken transcript grounded only in the supplied chunk summaries. Cover the full paper,
distinguish measures from interpretations, explain technical terms, retain important numerical
results and limitations, and close with responsible implications. Output only the spoken transcript."""


def _sentences(text: str) -> list[str]:
    compact = re.sub(r"\s+", " ", text).strip()
    return [part.strip() for part in re.split(r"(?<=[.!?])\s+", compact) if part.strip()]


def offline_summary(chunk: str, max_sentences: int = 12) -> str:
    """Deterministic coverage fallback that samples the beginning, middle, and end."""
    sentences = _sentences(chunk)
    if len(sentences) <= max_sentences:
        return " ".join(sentences)
    positions = {
        round(index * (len(sentences) - 1) / (max_sentences - 1))
        for index in range(max_sentences)
    }
    return " ".join(sentences[index] for index in sorted(positions))


def create_transcript(
    source_text: str,
    *,
    title: str,
    instructions: str = "",
    provider: str = "auto",
    transcript_file: Path | None = None,
    max_chunk_chars: int = 12_000,
) -> tuple[str, list[str], str, str | None]:
    chunks = chunk_text(source_text, max_chars=max_chunk_chars)
    if not chunks:
        raise ScriptGenerationError("Source text was empty after extraction")

    if transcript_file:
        transcript = transcript_file.expanduser().resolve().read_text(encoding="utf-8").strip()
        if len(transcript) < 200:
            raise ScriptGenerationError("Reviewed transcript must contain at least 200 characters")
        return transcript, [offline_summary(chunk) for chunk in chunks], "reviewed-transcript", None

    automatic = provider.lower() == "auto"
    selected = provider.lower()
    if automatic:
        selected = "openai" if os.environ.get("OPENAI_API_KEY") else "offline"
    if selected not in {"openai", "offline"}:
        raise ScriptGenerationError(f"Unknown script provider: {provider}")

    if selected == "openai":
        try:
            summaries = [
                generate_text(
                    f"Chunk {index + 1} of {len(chunks)}:\n\n{chunk}",
                    instructions=SUMMARY_INSTRUCTIONS,
                )
                for index, chunk in enumerate(chunks)
            ]
            prompt = (
                f"Paper title: {title}\n\n"
                f"Project-specific direction:\n{instructions or 'None'}\n\n"
                + "\n\n".join(
                    f"CHUNK SUMMARY {index + 1}/{len(summaries)}\n{summary}"
                    for index, summary in enumerate(summaries)
                )
            )
            transcript = generate_text(prompt, instructions=SCRIPT_INSTRUCTIONS)
            return transcript, summaries, "openai", None
        except ScriptGenerationError as error:
            if not automatic:
                raise
            fallback_reason = str(error)
    else:
        fallback_reason = None

    summaries = [offline_summary(chunk) for chunk in chunks]
    transcript = (
        f"Welcome. This episode examines {title}. "
        "It was generated locally from the complete extracted paper, with each source section "
        "represented in a bounded chunk. The following is an accessible extractive briefing.\n\n"
        + "\n\n".join(summaries)
        + "\n\nThis local fallback preserves broad paper coverage, but its wording should be "
        "reviewed before publication."
    )
    provider_name = (
        "offline-extractive-after-openai-failure"
        if fallback_reason
        else "offline-extractive"
    )
    return transcript, summaries, provider_name, fallback_reason
