import unittest
import math
from validation_session import prepare_rows,summarize
from validation_accuracy import spatial_accuracy

class ValidationSessionTests(unittest.TestCase):
    def setUp(self):
        self.manifest=dict(id='v',protocol='eye-validation-v1',status='aborted',schedule=[
            dict(exercise='arithmetic',condition='easy',seconds=10,answer=5),
            dict(exercise='saccades',condition='saccades',seconds=2,answer=0)])
        self.events=[dict(eventType='stimulus_start',step=0,time=1),dict(eventType='response_prompt',step=0,time=11),
            dict(eventType='answer_missing',step=0,time=31,value=0,valueRecorded=False),dict(eventType='stimulus_start',step=1,time=32)]
    def row(self,**kw):
        return dict(kind='gaze_observation',validationId='v',validationStep=0,validationStage='running',receiptTime=2,phase='validation',**kw)
    def test_adapter_excludes_prompt_and_leaves_raw_unchanged(self):
        rows=[self.row(),dict(self.row(),receiptTime=1.5),dict(self.row(),validationStage='instruction'),dict(self.row(),validationId='other')]
        result=prepare_rows(self.manifest,rows,self.events)
        self.assertEqual(len(result),1);self.assertEqual(rows[0]['phase'],'validation')
        self.assertEqual(result[0]['phase'],'search');self.assertFalse(result[0]['validationStimulusComplete'])
    def test_missing_answers_and_interrupted_steps_are_explicit(self):
        report=summarize(self.manifest,[],self.events)
        self.assertIsNone(report['responses'][0]['value']);self.assertIsNone(report['responses'][0]['correct'])
        self.assertFalse(report['steps'][0]['completed']);self.assertIsNone(report['steps'][0]['used_ray_error_mean_deg'])
        self.assertFalse(report['measurements_validated'])
    def test_saccade_transition_not_discarded_as_settling(self):
        row=dict(self.row(),validationStep=1,receiptTime=32.01)
        self.assertEqual(len(prepare_rows(self.manifest,[row],self.events)),1)

    def spatial_fixture(self):
        manifest=dict(id='v',protocol='eye-validation-v1',status='complete',schedule=[
            dict(exercise='drift',condition='drift',seconds=2.5,answer=0,yaw=0,pitch=0,distance=2.4)])
        events=[dict(eventType='stimulus_start',step=0,time=1),dict(eventType='stimulus_end',step=0,time=3.5)]
        return manifest,events

    def spatial_row(self,time,error=3,**kw):
        row=self.row(valid=True,validationTargetPresent=True,raySource='HTC_binocular_mean',
                     htcQueryReceiptTime=time,worldOrigin=dict(x=0,y=0,z=0),
                     worldDirection=dict(x=math.sin(math.radians(error)),y=0,z=math.cos(math.radians(error))),
                     validationTarget=dict(x=0,y=0,z=2.4),usedTargetErrorDegrees=99)
        row.update(receiptTime=time,**kw)
        return row

    def test_spatial_report_recomputes_geometry_and_does_not_weight_cached_rows(self):
        m,e=self.spatial_fixture()
        rows=[self.spatial_row(1.6,2),self.spatial_row(2,6)]
        rows += [dict(rows[1],receiptTime=2.01+i*.01) for i in range(20)]
        report=summarize(m,rows,e)
        self.assertAlmostEqual(report['steps'][0]['used_ray_error_mean_deg'],4)
        self.assertEqual(report['steps'][0]['used_ray_error_n'],2)

    def test_cached_pre_window_query_cannot_become_a_settled_sample(self):
        m,e=self.spatial_fixture()
        r=self.spatial_row(1.4,30)
        rows=[r,dict(r,receiptTime=1.7),self.spatial_row(1.8,3)]
        result=spatial_accuracy(m,rows,e)['targets'][0]['used_ray']
        self.assertEqual(result['n'],1);self.assertAlmostEqual(result['mean_deg'],3)

    def test_spatial_excludes_other_sessions_invalid_geometry_and_after_end(self):
        m,e=self.spatial_fixture()
        rows=[self.spatial_row(1.7,validationId='other'),self.spatial_row(1.8,valid=False),
              self.spatial_row(1.9,worldDirection=dict(x=0,y=0,z=0)),self.spatial_row(4),self.spatial_row(2)]
        result=spatial_accuracy(m,rows,e)['targets'][0]['used_ray']
        self.assertEqual(result['n'],1);self.assertAlmostEqual(result['mean_deg'],3)

    def test_incomplete_spatial_target_has_no_accuracy_estimate(self):
        m,e=self.spatial_fixture()
        result=spatial_accuracy(m,[self.spatial_row(2)],e[:1])['targets'][0]['used_ray']
        self.assertEqual(result['n'],0);self.assertIsNone(result['mean_deg'])
