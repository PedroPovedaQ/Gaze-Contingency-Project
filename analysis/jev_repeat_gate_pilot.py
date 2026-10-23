"""Paired follow-up: suppress requests already blocked by duplicate-direction pacing.
Experimental scheduler only. No participant runtime mutation.
"""
import argparse
import json
from pathlib import Path
from guidance_policy_bakeoff import ask
from guidance_action_pilot import stats
from jev_sequence_pilot import Bridge, scenarios, simulate

def main():
    p=argparse.ArgumentParser();p.add_argument('--keys',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    a.output.mkdir(parents=True,exist_ok=False);key=json.loads(a.keys.read_text())['jev_key'];b=Bridge();runs=[];count=0
    def advisor(payload):
        nonlocal count
        if count>=128:raise RuntimeError('128-request cap')
        count+=1;return ask(key,payload)
    try:
        for repeat in range(2):
            for i,name in enumerate(('ignored_direction_silence','rapid_reverse')):
                for enabled in ((False,True) if (repeat+i)%2==0 else (True,False)):
                    r=simulate(b,name,scenarios()[name],'jev_overrides',advisor,skip_known_repeats=enabled)
                    r.update(repeat=repeat,early_gate=enabled);runs.append(r)
                    with (a.output/'runs.jsonl').open('a') as f:f.write(json.dumps(r)+'\n')
                    print(name,'early_gate',enabled,'requests',len(r['requests']),flush=True)
    finally:b.close()
    summary={}
    for enabled in (False,True):
        rr=[r for r in runs if r['early_gate']==enabled];q=[q for r in rr for q in r['requests']];e=[e for r in rr for e in r['events'] if e['kind']=='simulated_start']
        summary[str(enabled)]=dict(requests=len(q),http=stats([x['seconds'] for x in q]),failures=sum(x['status']!='ok' for x in q),
            simulated_starts=len(e),harmful_evidence_flags=sum(bool(x['flags']) for x in e),
            late_repeat_blocks=sum(x['kind']=='blocked_repetition' for r in rr for x in r['events']))
    report=dict(schema='experimental-repeat-gate-v1',runs=runs,summary=summary,
        basis='Real Mac HTTP; production C# qualification; simulated scheduler. Two trajectories, two repeats, alternating order. No headset or acoustic measurement.')
    (a.output/'report.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(summary,indent=2))
if __name__=='__main__':main()
