import copy
import math
import unittest
from theta_snapshot import flatten, REFERENCE, FIELDS


def snapshot(angle=90, index=0, height=.4):
    return dict(reference=REFERENCE, round=index, gazeSequence=123, time=15.25, gazeReceiptTime=15.24,
                eyeOrigin=dict(x=.2,y=1.3,z=-.1), readinessPosition=dict(x=.2,y=1.5,z=1.9),
                targetPosition=dict(x=.2+2*math.sin(math.radians(angle)), y=1.5+height, z=-.1+2*math.cos(math.radians(angle))),
                assignedSignedDegrees=angle, realizedSignedDegrees=angle, absoluteDegrees=abs(angle),
                assignmentErrorDegrees=0, targetHeightRelativeToCross=height)


class ThetaSnapshotTests(unittest.TestCase):
    def test_full_precision_zero_and_rear_wrap(self):
        for angle in (0,45,-45,90,-90,135,-135,180):
            s=snapshot(angle)
            if angle==180: s['realizedSignedDegrees']=-180
            row=flatten(s,trial_index=0,assigned=angle)
            self.assertEqual(set(row),set(FIELDS))
            self.assertEqual(row['pre_cue_theta_assignment_error_deg'],0)
            self.assertEqual(row['pre_cue_theta_eye_origin_x_m'],.2)
    def test_legacy_missing_is_not_zero(self):
        self.assertEqual(flatten(None),{'pre_cue_theta_status':'not_recorded'})
    def test_corruption_is_rejected(self):
        cases=[('assignmentErrorDegrees',360),('realizedSignedDegrees',450),('assignedSignedDegrees',450),('gazeSequence',True),('time',float('nan')),('round',1),('gazeReceiptTime',20),
               ('reference','wrong'),('assignedSignedDegrees',45),('absoluteDegrees',0),
               ('assignmentErrorDegrees',10),('targetHeightRelativeToCross',0),('eyeOrigin',{}),
               ('targetPosition',dict(x=.2,y=1.9,z=-.1))]
        for key,value in cases:
            with self.subTest(key=key):
                s=copy.deepcopy(snapshot());s[key]=value
                with self.assertRaises(ValueError):flatten(s,trial_index=0,assigned=90)
