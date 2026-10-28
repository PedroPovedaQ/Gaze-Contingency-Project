import unittest
from live_bridge import validate_control, validate_headset_state

class ValidationControlTests(unittest.TestCase):
    def setUp(self):
        self.state=dict(schema=1, sessionId='headset', revision=5, phase='ready', updatedUtc=300,
                        progress01=0., progressKnown=False, validationAvailable=True,
                        validation=dict(id='v', active=True, revision=3, stage='instruction'),
                        eyeCaptureStatus='recording', eyePupilTimestampAvailable=True)
        self.command=dict(schema=1,id='a'*32,sessionId='headset',observedRevision=5,observedUtc=300,
                          createdUtc=100,action='validation_next',validation=dict(id='v',revision=3))
    def test_fresh_control_and_stop(self):
        self.assertEqual(validate_control(self.command,self.state,101),self.command)
        command=dict(self.command,action='validation_stop',validation=dict(id='v',revision=1))
        self.assertEqual(validate_control(command,self.state,101),command)
    def test_reject_late_step_other_session_invalid_answer(self):
        for payload in [dict(id='other',revision=3),dict(id='v',revision=2)]:
            with self.assertRaises(ValueError):validate_control(dict(self.command,validation=payload),self.state,101)
        with self.assertRaises(ValueError):validate_control(dict(self.command,action='validation_answer',validation=dict(id='v',revision=3,value=4)),self.state,101)
    def test_rating_bounds_and_missing_are_not_zero(self):
        state=dict(self.state,validation=dict(id='v',active=True,revision=3,stage='rating'))
        for value in [0,8,True,None]:
            with self.assertRaises(ValueError):validate_control(dict(self.command,action='validation_answer',validation=dict(id='v',revision=3,value=value)),state,101)
    def test_start_allowlist(self):
        request=dict(mode='full',participant='P001',seed=42,hardFirst=False,notes='glasses')
        command=dict(self.command,action='validation_start',validation=request)
        self.assertEqual(validate_control(command,self.state,101),command)
        self.assertEqual(validate_control(dict(command,validation=dict(request,alignFirst=True)),self.state,101)['validation']['alignFirst'],True)
        for change in [dict(mode='study'),dict(participant='Name'),dict(seed=True),dict(hardFirst='false'),dict(alignFirst='true'),dict(alignFirst=1),dict(notes='x'*501)]:
            with self.assertRaises(ValueError):validate_control(dict(command,validation=dict(request,**change)),self.state,101)
        with self.assertRaises(ValueError):validate_control(command,dict(self.state,validationAvailable=False),101)
    def test_telemetry_survives_receiver_without_certification(self):
        result=validate_headset_state(self.state)
        self.assertTrue(result['eyePupilTimestampAvailable'])
        self.assertEqual(result['eyeCaptureStatus'],'recording')
        self.assertTrue(result['validation']['active'])
        self.assertFalse(result['validation']['measurementsValidated'])
