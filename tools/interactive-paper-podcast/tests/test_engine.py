from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import tempfile
import unittest

from interactive_podcast.engine import ConversationEngine
from interactive_podcast.models import ChatModel
from interactive_podcast.store import SessionStore


class FakeModel(ChatModel):
    provider = "fake-local"
    model = "tiny-test"

    def complete_json(self, system, prompt):
        self.last_prompt = prompt
        return {
            "answer": "The held-out XGBoost AUC was 0.85, but that does not validate transfer.",
            "discussion_summary": "We distinguished discrimination from headset-specific validation.",
            "memory_update": "Ask one transfer-validity question after reporting a metric.",
        }


class EngineTests(unittest.TestCase):
    def test_question_persists_answer_summary_and_character_evolution(self):
        with tempfile.TemporaryDirectory() as temporary:
            model = FakeModel()
            store = SessionStore(Path(temporary))
            engine = ConversationEngine(store, model)
            session = engine.create(
                "Gaze paper",
                ["The XGBoost model achieved an AUC ROC of 0.85 on held-out participant data."],
                "Episode transcript",
            )
            result = engine.ask(session["id"], "What AUC did XGBoost achieve, and does it transfer to Vive?", 42.5)
            saved = store.load(session["id"])

        self.assertEqual("Maya", result["turn"]["speaker_name"])
        self.assertEqual(42.5, result["turn"]["position_seconds"])
        self.assertEqual([0], result["turn"]["evidence_chunks"])
        self.assertIn("validation", result["run_summary"])
        self.assertEqual(1, saved["characters"][0]["turns"])
        self.assertIn("transfer-validity", saved["characters"][0]["learned"][0])
        self.assertIn("latency_seconds", result["turn"])
        self.assertNotIn("source_chunks", result)

    def test_hosts_alternate_across_interruptions(self):
        with tempfile.TemporaryDirectory() as temporary:
            engine = ConversationEngine(SessionStore(Path(temporary)), FakeModel())
            session = engine.create("Paper", ["fixation validation source"], "Transcript")
            first = engine.ask(session["id"], "What is fixation validation?")
            second = engine.ask(session["id"], "What transfers?")
        self.assertEqual("host", first["turn"]["speaker"])
        self.assertEqual("cohost", second["turn"]["speaker"])

    def test_rejects_invalid_playback_position(self):
        with tempfile.TemporaryDirectory() as temporary:
            engine = ConversationEngine(SessionStore(Path(temporary)), FakeModel())
            session = engine.create("Paper", ["source"], "Transcript")
            with self.assertRaisesRegex(ValueError, "Playback position"):
                engine.ask(session["id"], "Question?", float("nan"))

    def test_concurrent_interruptions_do_not_overwrite_each_other(self):
        with tempfile.TemporaryDirectory() as temporary:
            store = SessionStore(Path(temporary))
            engine = ConversationEngine(store, FakeModel())
            session = engine.create("Paper", ["A source about validation."], "Transcript")
            with ThreadPoolExecutor(max_workers=2) as pool:
                results = list(pool.map(lambda question: engine.ask(session["id"], question), ["First?", "Second?"]))
            saved = store.load(session["id"])

        self.assertEqual(2, len(results))
        self.assertEqual([0, 1], [turn["index"] for turn in saved["turns"]])

    def test_character_memory_rejects_factual_belief_drift(self):
        self.assertEqual(
            "Prefer explicit distinctions and source-grounded uncertainty when answering related questions.",
            ConversationEngine._style_memory("I learned that hover is definitely a fixation."),
        )
        self.assertEqual(
            "Prefer explicit distinctions and source-grounded uncertainty when answering related questions.",
            ConversationEngine._style_memory("Prefer saying NASA-TLX is optional."),
        )

    def test_fallback_excerpt_removes_speaker_markup_and_ends_cleanly(self):
        excerpt = ConversationEngine._clean_excerpt(
            "PODCAST TRANSCRIPT: [host] First grounded sentence. [cohost] " + "Long evidence sentence. " * 80
        )
        self.assertNotIn("[host]", excerpt)
        self.assertNotIn("PODCAST TRANSCRIPT", excerpt)
        self.assertLessEqual(len(excerpt), 851)
        self.assertTrue(excerpt.endswith("."))

    def test_guard_rejects_workload_answer_that_overrides_nasa_tlx(self):
        self.assertFalse(ConversationEngine._answer_passes_guard(
            "What workload measure should remain primary?",
            "Gaze should be the primary workload measure.",
            "NASA-TLX is the primary established subjective workload instrument.",
        ))
        self.assertTrue(ConversationEngine._answer_passes_guard(
            "What workload measure should remain primary?",
            "NASA-TLX should remain primary; gaze is exploratory.",
            "NASA-TLX is the primary established subjective workload instrument.",
        ))

    def test_guard_rejects_irrelevant_or_equivalent_hover_answers(self):
        source = "Object hover dwell is an exploratory proxy and not a validated fixation."
        self.assertFalse(ConversationEngine._answer_passes_guard(
            "Why should object hover remain exploratory?",
            "Trial response time should be the primary measure.",
            source,
        ))
        self.assertFalse(ConversationEngine._answer_passes_guard(
            "Does hover dwell equal fixation?",
            "Hover dwell is a validated fixation.",
            source,
        ))
        self.assertTrue(ConversationEngine._answer_passes_guard(
            "Why should object hover remain exploratory?",
            "Hover dwell is an exploratory proxy, not a locally validated fixation.",
            source,
        ))

    def test_finished_summaries_are_scoped_to_each_run(self):
        with tempfile.TemporaryDirectory() as temporary:
            store = SessionStore(Path(temporary))
            engine = ConversationEngine(store, FakeModel())
            session = engine.create("Paper", ["A grounded source."], "Transcript")
            engine.ask(session["id"], "First run question?")
            first = engine.finish_run(session["id"])
            engine.ask(session["id"], "Second run question?")
            second = engine.finish_run(session["id"])
            empty = engine.finish_run(session["id"])

        self.assertEqual((0, 1, 1), (first["turn_start"], first["turn_end"], first["turn_count"]))
        self.assertEqual((1, 2, 1), (second["turn_start"], second["turn_end"], second["turn_count"]))
        self.assertEqual(0, empty["turn_count"])
        self.assertEqual("No new discussion in this run.", empty["summary"])

    def test_legacy_finished_session_starts_a_new_run_after_prior_turns(self):
        legacy = {
            "turns": [{"discussion_summary": "Prior run."}],
            "run_summaries": [{"turn_count": 1, "summary": "Prior run."}],
        }
        self.assertEqual(1, ConversationEngine._last_finished_turn_index(legacy))
        self.assertEqual("No new discussion in this run.", ConversationEngine._active_run_summary(legacy))
