"""Conversation engine: grounded answers, summaries, and character evolution."""

from __future__ import annotations

from datetime import datetime, timezone
import math
import re
import threading
import time
import uuid

from .models import ChatModel, ModelError
from .retrieval import best_excerpt, retrieve
from .store import SessionStore

SYSTEM = """You are a rigorous interactive academic podcast host. Answer from SOURCE EXCERPTS and conversation only. Clearly state uncertainty. Never treat object hover/dwell as a validated fixation. Return valid JSON with exactly three short string values: answer (at most 3 sentences), discussion_summary (1 sentence), and memory_update (at most 15 words). memory_update must begin with an imperative such as Prefer, Ask, Explain, Challenge, Clarify, Use, Lead, or State and describe a conversational habit—not a factual belief."""


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def default_characters() -> list[dict[str, object]]:
    return [
        {"id": "host", "name": "Maya", "voice": "alloy", "core": "Precise, curious methodologist who explains assumptions.", "learned": [], "turns": 0},
        {"id": "cohost", "name": "Theo", "voice": "echo", "core": "Skeptical applied researcher who tests transfer claims.", "learned": [], "turns": 0},
    ]


class ConversationEngine:
    def __init__(self, store: SessionStore, model: ChatModel):
        self.store = store
        self.model = model
        self._lock = threading.RLock()

    def create(self, title: str, source_chunks: list[str], transcript: str, audio_path: str | None = None) -> dict[str, object]:
        timestamp = now()
        session = {
            "id": uuid.uuid4().hex[:16], "title": title, "created_at": timestamp, "updated_at": timestamp,
            "source_chunks": source_chunks, "transcript": transcript, "audio_path": audio_path,
            "characters": default_characters(), "turns": [], "run_summaries": [], "run_summary": "No discussion yet.",
            "last_finished_turn_index": 0,
            "playback": {"position_seconds": 0.0, "status": "ready"},
            "model": {"provider": self.model.provider, "name": self.model.model},
        }
        self.store.save(session)
        return session

    def ask(self, session_id: str, question: str, position_seconds: float = 0.0) -> dict[str, object]:
        with self._lock:
            return self._ask(session_id, question, position_seconds)

    def _ask(self, session_id: str, question: str, position_seconds: float) -> dict[str, object]:
        session = self.store.load(session_id)
        question = question.strip()
        if not question:
            raise ValueError("Question cannot be empty")
        if len(question) > 4_000:
            raise ValueError("Question exceeds the 4,000 character limit")
        if not math.isfinite(position_seconds) or position_seconds < 0:
            raise ValueError("Playback position must be a finite non-negative number")
        characters = session["characters"]
        character = characters[len(session["turns"]) % len(characters)]
        evidence = retrieve(question, session["source_chunks"], limit=2)
        history = [
            {"question": turn["question"], "answer": turn["answer"][:500]}
            for turn in session["turns"][-4:]
        ]
        prompt = (
            f"HOST PROFILE: {character['name']} — {character['core']} Learned style: {'; '.join(character['learned']) or 'none yet'}\n"
            f"RUN SUMMARY: {session['run_summary']}\nRECENT DISCUSSION: {history}\nQUESTION AT {position_seconds:.1f}s: {question}\n"
            "SOURCE EXCERPTS:\n" + "\n\n".join(
                f"[{item['index']}] {best_excerpt(question, str(item['text']))}" for item in evidence
            )
        )
        started = time.monotonic()
        answer_mode = "model"
        try:
            result = self.model.complete_json(SYSTEM, prompt)
            answer = str(result.get("answer", "")).strip()
            discussion_summary = str(result.get("discussion_summary", "")).strip()
            memory_update = self._style_memory(str(result.get("memory_update", "")))
            if not answer:
                raise ModelError("Model returned no answer")
            evidence_text = " ".join(best_excerpt(question, str(item["text"])) for item in evidence)
            if not self._answer_passes_guard(question, answer, evidence_text):
                raise ModelError("Model answer contradicted a critical source invariant")
        except ModelError:
            answer_mode = "source-fallback"
            if not evidence:
                answer = "I could not find a grounded passage for that question in the indexed paper."
            else:
                answer = "The most relevant source passage says: " + self._clean_excerpt(
                    best_excerpt(question, str(evidence[0]["text"]))
                )
            discussion_summary = f"Asked about {question[:120]}. Grounded answer: {answer[:260]}"
            memory_update = "Prefer concise, source-first answers when the model is unavailable."
        turn = {
            "index": len(session["turns"]), "asked_at": now(), "position_seconds": position_seconds,
            "question": question, "speaker": character["id"], "speaker_name": character["name"],
            "answer": answer, "evidence_chunks": [item["index"] for item in evidence],
            "discussion_summary": discussion_summary, "latency_seconds": round(time.monotonic() - started, 3),
            "answer_mode": answer_mode,
        }
        session["turns"].append(turn)
        character["turns"] += 1
        if memory_update and memory_update not in character["learned"]:
            character["learned"] = (character["learned"] + [memory_update])[-8:]
        session["run_summary"] = self._active_run_summary(session)
        session["playback"] = {"position_seconds": position_seconds, "status": "interrupted"}
        session["updated_at"] = now()
        self.store.save(session)
        return {"turn": turn, "character": character, "run_summary": session["run_summary"], "model": session["model"]}

    def finish_run(self, session_id: str) -> dict[str, object]:
        with self._lock:
            return self._finish_run(session_id)

    def _finish_run(self, session_id: str) -> dict[str, object]:
        session = self.store.load(session_id)
        start = self._last_finished_turn_index(session)
        end = len(session["turns"])
        summary = self._summarize_turns(session["turns"][start:end])
        record = {
            "ended_at": now(), "turn_start": start, "turn_end": end,
            "turn_count": end - start, "summary": summary,
        }
        session["run_summaries"].append(record)
        session["run_summary"] = summary
        session["last_finished_turn_index"] = end
        session["playback"]["status"] = "finished"
        session["updated_at"] = now()
        self.store.save(session)
        return record

    @staticmethod
    def _active_run_summary(session: dict[str, object]) -> str:
        start = ConversationEngine._last_finished_turn_index(session)
        return ConversationEngine._summarize_turns(session["turns"][start:])

    @staticmethod
    def _last_finished_turn_index(session: dict[str, object]) -> int:
        if "last_finished_turn_index" in session:
            return int(session["last_finished_turn_index"])
        summaries = session.get("run_summaries", [])
        if summaries:
            last = summaries[-1]
            return int(last.get("turn_end", last.get("turn_count", 0)))
        return 0

    @staticmethod
    def _summarize_turns(turns: list[dict[str, object]]) -> str:
        summaries = [turn.get("discussion_summary") or f"Asked: {turn['question']}" for turn in turns[-8:]]
        return " ".join(str(item) for item in summaries).strip() or "No new discussion in this run."

    @staticmethod
    def _style_memory(value: str) -> str:
        memory = re.sub(r"\s+", " ", value).strip()[:240]
        allowed = ("prefer ", "ask ", "explain ", "challenge ", "clarify ", "use ", "be ", "lead ", "state ")
        factual_markers = (" is ", " are ", " was ", " were ", " equals ", "auc", "nasa", "fixation", "hover", "result")
        if memory.lower().startswith(allowed) and not any(marker in f" {memory.lower()} " for marker in factual_markers):
            return memory
        return "Prefer explicit distinctions and source-grounded uncertainty when answering related questions."

    @staticmethod
    def _clean_excerpt(value: str) -> str:
        excerpt = value.replace("PODCAST TRANSCRIPT:", "")
        excerpt = re.sub(r"\[(?:host|cohost|host3|host4)\]\s*", "", excerpt)
        excerpt = re.sub(r"\s+", " ", excerpt).strip()
        if len(excerpt) <= 850:
            return excerpt
        shortened = excerpt[:850]
        boundary = max(shortened.rfind(". "), shortened.rfind("? "), shortened.rfind("! "))
        return shortened[: boundary + 1] if boundary >= 300 else shortened.rstrip() + "…"

    @staticmethod
    def _answer_passes_guard(question: str, answer: str, evidence: str) -> bool:
        query = question.lower()
        response = answer.lower()
        source = evidence.lower()
        if "workload" in query and ("primary" in query or "measure" in query):
            if "nasa-tlx" in source or "nasa tlx" in source:
                return "nasa-tlx" in response or "nasa tlx" in response
        if "hover" in query or "dwell" in query:
            if "not a validated fixation" in source or "exploratory" in source or "proxy" in source:
                invalid_equivalence = any(phrase in response for phrase in ("is a validated fixation", "are validated fixations"))
                preserves_distinction = (
                    ("hover" in response or "dwell" in response)
                    and any(phrase in response for phrase in ("exploratory", "proxy", "not validated", "not a fixation"))
                )
                return not invalid_equivalence and preserves_distinction
        return True
