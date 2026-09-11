import json
from pathlib import Path
import tempfile
import unittest
from load_data import load_all_summaries, load_nasa_tlx
from voice_blocks import voice_block_reports, pairing_exclusions


class VoiceBlockTests(unittest.TestCase):
    def test_block_questionnaires_are_loaded_from_run_folders(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory, "P001", "run_001")
            folder.mkdir(parents=True)
            (folder / "nasa_tlx.csv").write_text(
                "participant_id,condition,mental,physical,temporal,performance,effort,frustration,run_number,block\n"
                "P001,gaze_aware_voice-neutral-male,10,10,10,10,10,10,1,0\n"
                "P001,gaze_aware_voice-selfsimilar,20,20,20,20,20,20,1,1\n")
            rows = load_nasa_tlx(Path(directory))
            self.assertEqual(list(rows.block), [0, 1])
            self.assertEqual(list(rows.raw_tlx), [60, 120])
            self.assertEqual(len(set(rows.run_condition)), 2)

    def test_two_orders_keep_search_time_and_pair_only_complete_runs(self):
        with tempfile.TemporaryDirectory() as directory:
            for participant, order in (("P001", "neutral_then_selfsimilar"), ("P002", "selfsimilar_then_neutral")):
                path = Path(directory, participant)
                path.mkdir()
                rounds = []
                for i in range(14):
                    neutral = (i < 7) == (participant == "P001")
                    # Long variable transition/announcement delays do not enter search duration.
                    start = 100 + i * 40
                    duration = 8 if neutral else 6
                    rounds.append(dict(index=i, completed=True, outcome="completed", trial_id=f"{participant}_r{i}",
                                       transition_started_at=start - 30, objects_ready_at=start - 10,
                                       search_started_at=start, capture_at=start + duration,
                                       time_to_find_seconds=duration, voice_condition="neutral-male" if neutral else "selfsimilar"))
                (path / "trial_summary.json").write_text(json.dumps(dict(
                    schema_version=2, participant_id=participant, run_number=1, rounds_per_block=7,
                    condition="gaze_aware_voice-blocks-" + order, voice_order=order,
                    session_outcome="completed", objectives=rounds)))
            rows = load_all_summaries(Path(directory))
            blocks, pairs = voice_block_reports(rows)
            self.assertEqual(len(blocks), 4)
            self.assertEqual(set(blocks.trials), {7})
            self.assertEqual(list(pairs.self_minus_neutral_seconds), [-2, -2])
            self.assertTrue((rows.capture_at - rows.search_started_at == rows.time_to_find).all())
            rows.loc[rows.index[rows.participant_id == "P001"][0], "outcome"] = "technical_failure"
            _, pairs = voice_block_reports(rows)
            self.assertEqual(list(pairs.participant_id), ["P002"])
            exclusions = pairing_exclusions(rows)
            self.assertEqual(list(exclusions.participant_id), ["P001"])
            truncated = rows[rows.participant_id == "P002"].iloc[:10]
            self.assertIn("found 10", pairing_exclusions(truncated).iloc[0].reason)
