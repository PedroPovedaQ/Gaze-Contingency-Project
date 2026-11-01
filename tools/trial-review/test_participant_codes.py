import json
from pathlib import Path
import tempfile
import unittest
import uuid
import subprocess
import sys

from participant_codes import known_codes, suggest, duplicate
from participant_intake import create


class ParticipantCodeTests(unittest.TestCase):
    def test_highest_across_intake_catalog_archive_and_headset(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); intake=root/'intake'; archive=root/'archive'
            create(intake/uuid.uuid4().hex,'P003','A')
            create(intake/uuid.uuid4().hex,'P900000000008','A',development=True)
            (archive/'GazeData/P012').mkdir(parents=True)
            catalog=root/'catalog.json'; catalog.write_text(json.dumps({'participants':[{'id':'P009'}],
                'datasets':[{'participantId':'P005','assignments':[{'previousId':'P014'}]}]}))
            used,dev=known_codes(intake,catalog,[archive])
            self.assertEqual(suggest(used,dev),'P015')
            self.assertEqual(suggest(used,dev,headset='P020'),'P020')
            self.assertEqual(suggest(used,dev,True),'P900000000009')
            self.assertTrue(duplicate('P0003',used))

    def test_deleted_stopped_and_withdrawn_codes_remain_used(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            folder=root/uuid.uuid4().hex; create(folder,'P001','A')
            data=json.loads((folder/'session.json').read_text());data['withdrawnUtc']=123;data['phase']='withdrawn'
            (folder/'session.json').write_text(json.dumps(data))
            self.assertEqual(suggest(*known_codes(root)),'P002')
            (folder/'session.json').unlink()
            self.assertEqual(suggest(*known_codes(root)),'P002')
            with self.assertRaisesRegex(ValueError,'Duplicate'): create(root/uuid.uuid4().hex,'P001','A')
            create(root/uuid.uuid4().hex,'P002','A')
            self.assertEqual(suggest(*known_codes(root)),'P003')

    def test_historical_development_number_does_not_advance_study(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            folder=root/uuid.uuid4().hex;folder.mkdir()
            (folder/'session.json').write_text(json.dumps({'participantCode':'P999','development':True}))
            self.assertEqual(suggest(*known_codes(root)),'P001')
            with self.assertRaises(ValueError): create(root/uuid.uuid4().hex,'P900000000001','A')

    def test_corrupt_inventory_cannot_reset_sequence(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);(root/'bad').mkdir();(root/'bad/session.json').write_text('{')
            with self.assertRaises(ValueError):known_codes(root)

    def test_concurrent_reservations_cannot_reuse_a_code(self):
        with tempfile.TemporaryDirectory() as tmp:
            script="from pathlib import Path; import uuid,sys; from participant_intake import create; create(Path(sys.argv[1])/uuid.uuid4().hex,'P001','A')"
            jobs=[subprocess.Popen([sys.executable,'-c',script,tmp],cwd=Path(__file__).parent,stdout=subprocess.PIPE,stderr=subprocess.PIPE) for _ in range(2)]
            results=[job.communicate(timeout=15) for job in jobs]
            self.assertEqual(sorted(job.returncode for job in jobs),[0,1])
            self.assertTrue(any(b'Duplicate participant code' in err for _,err in results))
            self.assertEqual(suggest(*known_codes(Path(tmp))),'P002')

    def test_headset_active_code_is_known_even_before_archive_sync(self):
        with tempfile.TemporaryDirectory() as tmp:
            used,dev=known_codes(tmp,known=['P021'])
            self.assertEqual(suggest(used,dev),'P022')
            with self.assertRaisesRegex(ValueError,'Duplicate'):
                create(Path(tmp)/uuid.uuid4().hex,'P021','A',known=['P021'])

    def test_explicit_study_usage_wins_over_old_development_label(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);intake=root/'intake';folder=intake/'old';folder.mkdir(parents=True)
            (folder/'session.json').write_text(json.dumps({'participantCode':'P040','development':True}))
            archive=root/'archive/P040';archive.mkdir(parents=True)
            (archive/'intake-assignment-v2.json').write_text(json.dumps({'participantCode':'P040','development':False}))
            self.assertEqual(suggest(*known_codes(intake,archives=[archive])),'P041')
