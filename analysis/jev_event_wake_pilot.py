"""All-Jev choices: timed polling versus wake/cancel on actual state transitions."""
import argparse,json
from pathlib import Path
from guidance_policy_bakeoff import ask
from guidance_action_pilot import stats
from jev_sequence_pilot import Bridge, scenarios, simulate

def main():
    p=argparse.ArgumentParser();p.add_argument('--keys',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=False);key=json.loads(a.keys.read_text())['jev_key'];b=Bridge();runs=[];count=0
    def advisor(payload):
        nonlocal count
        if count>=160:raise RuntimeError('160-request cap')
        count+=1;return ask(key,payload)
    try:
        for i,name in enumerate(('turn_wall_vertical','overshoot_reacquire','controller_before_gaze','gaze_controller_elsewhere','rapid_reverse','speech_then_confirm')):
            for enabled in ((False,True) if i%2==0 else (True,False)):
                r=simulate(b,name,scenarios()[name],'all_jev',advisor,wake_on_state_change=enabled)
                r['event_wake']=enabled;runs.append(r)
                with (a.output/'runs.jsonl').open('a') as f:f.write(json.dumps(r)+'\n')
                print(name,'event_wake',enabled,'requests',len(r['requests']),flush=True)
    finally:b.close()
    summary={}
    for enabled in (False,True):
        rr=[r for r in runs if r['event_wake']==enabled];q=[q for r in rr for q in r['requests']];ep=[e for r in rr for e in r['episodes']]
        summary[str(enabled)]=dict(requests=len(q),http=stats([x['seconds'] for x in q]),failures=sum(x['status']!='ok' for x in q),
            confirmation_latency=stats([e['latency'] for e in ep if e['latency'] is not None]),unconfirmed_episodes=sum(e['start'] is None for e in ep),
            playback_flags=[dict(sequence=r['sequence'],**e) for r in rr for e in r['events'] if e['kind']=='simulated_start' and e['flags']])
    report=dict(schema='experimental-event-wake-v1',runs=runs,summary=summary,
        basis='Real Mac HTTP; C# qualification; simulated audio. Six paired trajectories, one repeat, alternating order. All choices still go to Jev, only pending cancellation and polling wake change. No participant runtime change.')
    (a.output/'report.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(summary,indent=2))
if __name__=='__main__':main()
