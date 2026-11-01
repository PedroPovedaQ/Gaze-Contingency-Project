import copy
from pathlib import Path
import tempfile
import unittest
import uuid

from participant_intake import create, load, patch, sync_fit, validate_fit
from live_bridge import validate_headset_state, validate_control


def fit_fixture():
    fit = validate_fit(None)
    fit.update(viewSeparation=dict(recorded=True,millimetres=64.1,rangeMm=.2,windowSeconds=2.1,samples=42,
               source='Unity_OpenXR_head_leftEyePosition_rightEyePosition',method='median_stable_window_v1',recordedUtc='2026-11-01T15:00:00Z'),
               eyeSeparation=dict(recorded=True,millimetres=63.9,rangeMm=.4,windowSeconds=2.1,samples=42,
               source='XR_HTC_eye_tracker_gazePose_position',method='median_stable_window_v1',recordedUtc='2026-11-01T15:00:00Z'),
               autoIpdStatus='unavailable',separationChange='change_observed',separationChangeMm=1.2,
               separationChangeUtc='2026-11-01T14:59:58Z',setupSampleCount=120)
    return fit


class ParticipantFitTests(unittest.TestCase):
    def test_roundtrip_and_legacy_preservation(self):
        fit=fit_fixture();self.assertEqual(validate_fit(fit),fit)
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp)/uuid.uuid4().hex;create(folder,'P004','A')
            sync_fit(folder,folder.name,'P004',fit)
            self.assertEqual(load(folder)['fit'],fit)
            for session,participant in [('wrong','P004'),(folder.name,'P005')]:
                with self.assertRaises(ValueError):sync_fit(folder,session,participant,fit)
            patch(folder,withdrawnUtc=123)
            with self.assertRaises(ValueError):sync_fit(folder,folder.name,'P004',fit)
        legacy=validate_fit(None);legacy.update(ipdRecorded=True,participantIpdMm=62,ipdSource='ruler')
        self.assertEqual(validate_fit(legacy),legacy)

    def test_bad_window_rejected(self):
        for field,value in [('millimetres',float('nan')),('millimetres',0),('samples',1),('windowSeconds',.1),('rangeMm',2),('source','')]:
            fit=fit_fixture();fit['viewSeparation'][field]=value
            with self.assertRaises(ValueError):validate_fit(fit)
        fit=validate_fit(None)
        fit['viewSeparation']=dict(recorded=False,millimetres=0,rangeMm=0,windowSeconds=0,samples=0,source='',method='',recordedUtc='')
        self.assertEqual(validate_fit(fit),fit)
        fit['viewSeparation']['millimetres']=64
        with self.assertRaises(ValueError):validate_fit(fit)

    def test_status_and_manual_fallback_are_session_bound(self):
        state=dict(schema=1,sessionId='headset',revision=10,phase='ready',updatedUtc=299,progress01=0,
                   progressKnown=False,participantSessionId='a'*32,participantCode='P004',fit=fit_fixture())
        self.assertEqual(validate_headset_state(state)['fit'],state['fit'])
        command=dict(schema=1,id='b'*32,sessionId='headset',observedRevision=10,observedUtc=299,createdUtc=299,
                     action='set_fit_fallback',participantSessionId='a'*32,fit=validate_fit(None))
        command['fit'].update(ipdRecorded=True,participantIpdMm=63,ipdSource='pupillometer')
        with self.assertRaises(ValueError):validate_control(command,state,300)
        state['fit']=validate_fit(None);validate_control(command,state,300)
        command['participantSessionId']='c'*32
        with self.assertRaises(ValueError):validate_control(command,state,300)
