"""Small dependency-free lexical retrieval for paper grounding."""

from __future__ import annotations

import math
import re
from collections import Counter

TOKEN = re.compile(r"[a-zA-Z][a-zA-Z0-9'-]{2,}")
STOP = {"the", "and", "for", "that", "with", "from", "this", "were", "was", "are", "but", "not", "into", "their", "they", "have", "has", "had"}
EXPANSIONS = {
    "workload": ("nasa", "tlx", "cognitive", "load", "subjective", "questionnaire"),
    "fixation": ("gaze", "dispersion", "velocity", "dwell", "hover"),
    "fixations": ("gaze", "dispersion", "velocity", "dwell", "hover"),
    "transfer": ("generalization", "validity", "headset", "task"),
}


def tokens(text: str) -> list[str]:
    return [word.lower() for word in TOKEN.findall(text) if word.lower() not in STOP]


def retrieve(query: str, chunks: list[str], limit: int = 4) -> list[dict[str, object]]:
    query_terms = tokens(query)
    expanded = [term for token in query_terms for term in EXPANSIONS.get(token, ())]
    query_counts = Counter(query_terms + expanded)
    if not query_counts:
        return []
    document_frequency = Counter()
    tokenized = []
    for chunk in chunks:
        terms = tokens(chunk)
        tokenized.append(Counter(terms))
        document_frequency.update(set(terms))
    scored = []
    total = max(1, len(chunks))
    for index, counts in enumerate(tokenized):
        score = 0.0
        for term, query_weight in query_counts.items():
            if counts[term]:
                score += query_weight * (1 + math.log(counts[term])) * math.log(1 + total / document_frequency[term])
        if score:
            scored.append({"index": index, "score": round(score, 4), "text": chunks[index]})
    return sorted(scored, key=lambda item: (-float(item["score"]), int(item["index"])))[:limit]


def best_excerpt(query: str, text: str, max_chars: int = 1_200) -> str:
    """Select the most query-relevant paragraphs instead of truncating a chunk's front."""
    paragraphs = [part.strip() for part in re.split(r"\n\s*\n", text) if part.strip()]
    if not paragraphs:
        return text[:max_chars]
    query_terms = tokens(query)
    expanded = set(query_terms + [term for token in query_terms for term in EXPANSIONS.get(token, ())])
    ranked = []
    for index, paragraph in enumerate(paragraphs):
        counts = Counter(tokens(paragraph))
        score = sum((3 if term in query_terms else 1) * counts[term] for term in expanded)
        ranked.append((score, index, paragraph))
    selected = sorted(sorted(ranked, key=lambda row: (-row[0], row[1]))[:2], key=lambda row: row[1])
    excerpt = "\n\n".join(row[2] for row in selected)
    return excerpt[:max_chars]
