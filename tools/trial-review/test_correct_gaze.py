import math
import unittest
from correct_gaze import match_profile, unmirror

class CorrectionChecks(unittest.TestCase):
    def test_known_reflection_and_translation(self):
        self.assertEqual(unmirror([4,5,6],[1,2,3],dict(x=0,y=0,z=0,w=1)),[-2,5,6])

    def test_rotated_reference_and_round_trip(self):
        q=dict(x=0,y=math.sin(math.pi/4),z=0,w=math.cos(math.pi/4))
        corrected=unmirror([4,5,6],[1,2,3],q)
        for a,b in zip(corrected,[4,5,0]):self.assertAlmostEqual(a,b)
        for a,b in zip(unmirror(corrected,[1,2,3],q),[4,5,6]):self.assertAlmostEqual(a,b)

    def test_reject_unknown_recording_and_bad_reference(self):
        with self.assertRaises(ValueError):match_profile({},'unknown')
        for q in [dict(x=0,y=0,z=0,w=0),dict(x=0,y=float('nan'),z=0,w=1)]:
            with self.assertRaises(ValueError):unmirror([1,2,3],[0,0,0],q)

if __name__=='__main__':unittest.main()
