import unittest

from interactive_podcast.retrieval import best_excerpt, retrieve


class RetrievalTests(unittest.TestCase):
    def test_ranks_matching_source_and_returns_stable_index(self):
        chunks = [
            "The participants completed five blocks with NASA TLX after each block.",
            "The XGBoost model reached an AUC ROC of zero point eight five.",
            "Fixations were detected using a dispersion threshold algorithm.",
        ]
        result = retrieve("What AUC did XGBoost reach?", chunks, limit=2)
        self.assertEqual(1, result[0]["index"])

    def test_empty_question_returns_no_results(self):
        self.assertEqual([], retrieve("?", ["source text"]))

    def test_workload_query_expands_to_nasa_tlx(self):
        chunks = [
            "Collider geometry determines object hover behavior.",
            "NASA TLX remains the primary subjective questionnaire for cognitive load.",
        ]
        self.assertEqual(1, retrieve("What workload measure should remain primary?", chunks)[0]["index"])

    def test_excerpt_selects_relevant_later_paragraph(self):
        text = "Hover collider details.\n\nNASA TLX is the primary subjective workload measure.\n\nUnrelated ending."
        excerpt = best_excerpt("What workload measure is primary?", text)
        self.assertIn("NASA TLX", excerpt)
