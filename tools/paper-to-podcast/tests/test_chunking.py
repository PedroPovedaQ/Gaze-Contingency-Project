import unittest

from paper_to_podcast.chunking import chunk_text


class ChunkTextTests(unittest.TestCase):
    def test_preserves_all_paragraphs_in_order(self):
        source = "\n\n".join(f"Paragraph {index}. " + ("x" * 90) for index in range(12))
        chunks = chunk_text(source, max_chars=240)

        self.assertGreater(len(chunks), 1)
        self.assertTrue(all(len(chunk) <= 240 for chunk in chunks))
        reconstructed = "\n\n".join(chunks)
        for index in range(12):
            self.assertIn(f"Paragraph {index}.", reconstructed)
        self.assertLess(
            reconstructed.index("Paragraph 2."),
            reconstructed.index("Paragraph 10."),
        )

    def test_splits_a_single_oversized_block(self):
        source = "A" * 701
        chunks = chunk_text(source, max_chars=200)

        self.assertEqual([200, 200, 200, 101], [len(chunk) for chunk in chunks])
        self.assertEqual(source, "".join(chunks))

    def test_rejects_unsafe_tiny_chunk_size(self):
        with self.assertRaisesRegex(ValueError, "at least 200"):
            chunk_text("text", max_chars=199)

    def test_empty_text_returns_no_chunks(self):
        self.assertEqual([], chunk_text(" \n "))


if __name__ == "__main__":
    unittest.main()
