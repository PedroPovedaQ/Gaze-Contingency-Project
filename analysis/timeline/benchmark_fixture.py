"""Generate clearly synthetic full-session replay data for browser capacity checks."""
import argparse
import json
import math
from pathlib import Path


def generate(path: Path, trials: int = 80, seconds: int = 20, hz: int = 90):
    if min(trials, seconds, hz) <= 0:
        raise ValueError('Trial count, seconds and synthetic cadence must be positive')
    sequence = 0
    point = lambda x,y,z: dict(x=x,y=y,z=z)
    rotation = dict(x=0,y=0,z=0,w=1)
    with path.open('x') as handle:
        def write(kind, time, context=None, **fields):
            nonlocal sequence
            record=dict(schema=2,kind=kind,sequence=sequence,time=time,**(context or {}),**fields)
            handle.write(json.dumps(record,separators=(',',':'))+'\n');sequence+=1
        write('header',0,recordingId='SYNTHETIC-CAPACITY-CHECK',utc='2026-10-01T00:00:00Z',
              sourceType='synthetic-benchmark',origin=point(0,0,0),originRotation=rotation,
              sampling='Synthetic frame cadence; no device or participant measurements')
        for trial in range(trials):
            start=trial*(seconds+3)+1
            context=dict(participantCode='SYNTHETIC_CAPACITY',runNumber=1,round=trial,block=trial//40,
                         trialId=f'trial_{trial+1:03d}',sourceTrialId=f'SYNTHETIC_CAPACITY_run001_r{trial:02d}',
                         sourceRunFolder='SYNTHETIC_CAPACITY_run001',practice=False,
                         voice='neutral' if trial<40 else 'selfsimilar')
            write('transition',start,context)
            objects=[]
            for i in range(168):
                angle=2*math.pi*i/168
                objects.append(dict(id=f'object_{i}',shape='Cube',color='Blue',target=i==0,
                                    planeId=i//21,planeSlot=i%21,mesh='synthetic_cube',
                                    position=point(round(2*math.sin(angle),5),.1+(i%21)*.12,round(2*math.cos(angle),5)),
                                    rotation=rotation,scale=point(.1,.1,.1)))
            write('trial',start+.1,context,objects=objects,targetId='object_0',absoluteTheta=(trial%5)*45,
                  signedTheta=(trial%5)*45,startPlane=0,targetPlane=trial%5)
            write('search_start',start+1,context)
            for frame in range(seconds*hz):
                elapsed=frame/hz; angle=elapsed*.15
                origin=point(0,1.6,0);direction=point(round(math.sin(angle),6),0,round(math.cos(angle),6))
                end=point(direction['x']*2,1.6,direction['z']*2)
                tracked=0 if frame%270 in range(5) else 1
                head=dict(headAvailable=True,headTracked=1,headPosition=origin,headRotation=rotation,
                          xrOriginAvailable=True,xrOriginPosition=point(0,0,0),xrOriginRotation=rotation)
                controller=dict(available=True,rayAvailable=True,tracked=1,triggerHeld=0,triggerValue=0,
                                position=point(.2,1,0),rotation=rotation,rayOrigin=point(.2,1,0),
                                rayDirection=direction,rayEnd=end,hoveredId=f'object_{frame%168}')
                fields=dict(frame=trial*seconds*hz+frame,searchSeconds=elapsed,searchActive=True,visible=True,
                            gazeAvailable=True,gazeTracked=tracked,gazeOrigin=origin,gazeDirection=direction,
                            gazeObservationSequence=trial*seconds*hz+frame,gazeRecordingId='SYNTHETIC_RAW',
                            gazeSource='synthetic',gazeDeliveryAge=.005,gazeReceiptTime=1000+start+1+elapsed,
                            gazeInputEventTime=1000+start+1+elapsed-.005,gazeInvalidReason='' if tracked else 'synthetic_gap',
                            hoveredId=f'object_{frame%168}' if tracked else '',rightController=controller,
                            leftController=dict(available=False,rayAvailable=False,tracked=-1,triggerHeld=-1),
                            readinessActive=False,readinessPaused=False,readinessProgress=0,
                            selectionMethod='controller_ray_trigger_press_v1',**head)
                write('sample',start+1+elapsed,context,**fields)
            fields.update(searchSeconds=seconds,correct=True,objectId='object_0',selectingHand='right')
            fields['rightController']=dict(controller,triggerHeld=1,triggerValue=1)
            write('selection',start+1+seconds,context,**fields)
        write('end',trials*(seconds+3),complete=True,detail='synthetic fixture complete')
    return dict(trials=trials,secondsPerTrial=seconds,syntheticFrameHz=hz,samples=trials*seconds*hz,
                objectsPerTrial=168,sourceBytes=path.stat().st_size)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    parser.add_argument('--trials',type=int,default=80)
    parser.add_argument('--seconds',type=int,default=20)
    parser.add_argument('--hz',type=int,default=90)
    args=parser.parse_args()
    print(json.dumps(generate(args.output,args.trials,args.seconds,args.hz)))
