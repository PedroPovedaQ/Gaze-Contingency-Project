import json
import csv
from pathlib import Path
import tempfile
import unittest
from load_data import load_all_summaries, load_nasa_tlx
from voice_blocks import voice_block_reports, pairing_exclusions, write_voice_block_reports


class VoiceBlockTests(unittest.TestCase):
    def current_run(self, root, participant='P001', reverse=False, debug=0, run=1):
        folder=Path(root, participant, f'run_{run:03d}')
        folder.mkdir(parents=True)
        order='selfsimilar_then_neutral' if reverse else 'neutral_then_selfsimilar'
        objectives=[]
        for i in range(debug or 80):
            neutral=(i<40) != reverse
            objectives.append(dict(index=i, completed=True, outcome='completed',
                trial_id=f'{participant}_run{run:03d}_r{i:02d}',
                voice_condition='neutral-male' if neutral else 'selfsimilar',
                time_to_find_seconds=8.0 if neutral else 6.0))
        summary=dict(schema_version=2, participant_id=participant, run_number=run,
                     condition='gaze_aware_voice-blocks-'+order, voice_order=order,
                     session_outcome='completed', rounds_per_block=40,
                     nominal_total_rounds=80, total_rounds=debug or 80,
                     debug_round_override=debug, objectives=objectives)
        (folder/'trial_summary.json').write_text(json.dumps(summary))

    def test_current_eighty_trial_sessions_pair_in_both_voice_orders(self):
        with tempfile.TemporaryDirectory() as directory:
            self.current_run(directory)
            self.current_run(directory, participant='P002', reverse=True)
            rows=load_all_summaries(Path(directory))
            self.assertEqual(set(rows.rounds_per_block),{40})
            self.assertEqual(set(rows.nominal_total_rounds),{80})
            blocks,pairs=voice_block_reports(rows)
            self.assertEqual(len(blocks),4)
            self.assertEqual(set(blocks.trials),{40})
            self.assertEqual(list(pairs.self_minus_neutral_seconds),[-2,-2])
            self.assertTrue(pairing_exclusions(rows).empty)
            output=Path(directory,'reports'); output.mkdir()
            write_voice_block_reports(rows,output)
            with (output/'voice_pair_differences.csv').open(newline='') as file:
                saved=list(csv.DictReader(file))
            self.assertEqual([float(r['self_minus_neutral_seconds']) for r in saved],[-2,-2])

    def test_debug_completion_cannot_substitute_for_a_full_pair(self):
        with tempfile.TemporaryDirectory() as directory:
            self.current_run(directory,debug=45)
            rows=load_all_summaries(Path(directory))
            blocks,pairs=voice_block_reports(rows)
            self.assertEqual(set(blocks.trials),{40,5})
            self.assertTrue(pairs.empty)
            self.assertIn('debug run',pairing_exclusions(rows).iloc[0].reason)

    def test_duplicate_indices_ids_bad_voice_and_duration_exclude_pair(self):
        with tempfile.TemporaryDirectory() as directory:
            self.current_run(directory)
            original=load_all_summaries(Path(directory))
            for column,value,expected in (
                ('round_index',1,'trial indices'),
                ('trial_id',original.iloc[1].trial_id,'trial IDs'),
                ('time_to_find',-1,'search duration'),
                ('time_to_find',float('inf'),'search duration'),
                ('time_to_find',None,'search duration'),
                ('voice_condition','selfsimilar','voice assignment'),
                ('voice_condition',None,'voice assignment'),
                ('voice_order','selfsimilar_then_neutral','voice order'),
                ('voice_order',None,'voice order'),
                ('outcome',None,'trials incomplete'),
                ('session_outcome',None,'session incomplete'),
                ('rounds_per_block',20,'inconsistent rounds_per_block'),
                ('block','B','assignment mismatch'),
                ('block',None,'assignment mismatch')):
                with self.subTest(column=column,value=value):
                    rows=original.copy()
                    rows.loc[0,column]=value
                    self.assertTrue(voice_block_reports(rows)[1].empty)
                    self.assertIn(expected,pairing_exclusions(rows).iloc[0].reason)

    def test_restart_cannot_supply_the_other_voice_block(self):
        with tempfile.TemporaryDirectory() as directory:
            self.current_run(directory,run=1)
            self.current_run(directory,run=2)
            rows=load_all_summaries(Path(directory))
            partial=rows[((rows.run_number==1)&(rows.round_index<40))|
                         ((rows.run_number==2)&(rows.round_index>=40))]
            self.assertEqual(len(partial),80)
            self.assertTrue(voice_block_reports(partial)[1].empty)
            self.assertEqual(len(pairing_exclusions(partial)),2)

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
            self.assertTrue(rows.raw_tlx.isna().all())
            self.assertEqual(list(rows.nasa_tlx_scoring_status), ["missing_scale_metadata"] * 2)
            self.assertEqual(len(set(rows.run_condition)), 2)

    def test_tlx_partial_file_cannot_supply_an_unvalidated_preexisting_total(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory, "nasa_tlx.csv")
            path.write_text("participant_id,mental,raw_tlx,scale_min,scale_max,performance_direction\n"
                            "P001,10,300,0,100,high_is_poor\n")
            rows = load_nasa_tlx(Path(directory))
            self.assertTrue(rows.raw_tlx.isna().all())
            self.assertEqual(rows.nasa_tlx_scoring_status.iloc[0], "missing_or_invalid_dimension")
            path.write_text("participant_id,mental,physical,temporal,performance,effort,frustration,scale_min,scale_max,performance_direction\n"
                            "P001,10,20,30,40,50,60,0,100,high_is_poor\n")
            rows = load_nasa_tlx(Path(directory))
            self.assertEqual(rows.raw_tlx.iloc[0], 35)

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
