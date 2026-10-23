"""Bounded advisory review of synthetic sequence evidence, no participant records."""
import argparse
import hashlib
import json
import subprocess
import tempfile
from pathlib import Path
from guidance_codex_judge import _codex_executable, _environment, _provider_failure
from jev_sequence_pilot import ARMS, evidence_flags


def compact(report):
    runs=[]
    for run in report['runs']:
        requests=[]
        for q in run['requests']:
            state=q['payload']['state'];obs=state['observation']
            m=dict(lookingStable=state['gaze_hold_qualified'],pointingStable=state['controller_hold_qualified'])
            requests.append(dict(id=q['id'],time=q['time'],seconds=q['seconds'],action=q.get('action'),
                request_flags=evidence_flags(obs,m,q['action']) if q.get('action') else [],
                observation=obs,phase=state['guidance_state']['phase'],allowed=state['local_action'],
                history=state['history'],silence=state['seconds_since_speech_ended']))
        runs.append(dict(id=run['sequence']+':'+run['arm'],sequence=run['sequence'],arm=run['arm'],
            episodes=run['episodes'],events=[e for e in run['events'] if e['kind']!='request'],requests=requests))
    return dict(runs=runs,summary=report['summary'],limitations=report['limitations'])


def main():
    p=argparse.ArgumentParser();p.add_argument('report',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
    if a.output.exists():raise ValueError('Refusing overwrite')
    raw=a.report.read_bytes();report=json.loads(raw);evidence=compact(report)
    ids=[r['id'] for r in evidence['runs']]
    item=dict(type='object',additionalProperties=False,required=['policy','assessment','evidence_ids'],properties={
        'policy':dict(type='string',enum=list(ARMS)),'assessment':dict(type='string'),
        'evidence_ids':dict(type='array',items=dict(type='string',enum=ids))})
    schema=dict(type='object',additionalProperties=False,required=['recommendation','policies','caveats'],properties={
        'recommendation':dict(type='string'),'caveats':dict(type='string'),
        'policies':dict(type='array',minItems=4,maxItems=4,items=item)})
    prompt='''Evaluate these complete fixed-trajectory offline guidance sequences. You are an advisory judge, not a human participant. Use supplied evidence only; no tools. The actual C# state machine qualifies events. The speech scheduler and 1.0/1.8 second audio durations are simulated, not a headset recording. Mac HTTP times are measured. Speech history follows each policy's simulated starts, not counterfactual user behavior. Distinguish gaze looking, controller pointing, and successful trigger selection. Compare request-time observation to playback state: a valid-at-request hint becoming stale is a scheduling/final-check problem, not proof of a bad Jev choice. Repeated progress at a new milestone and qualified reacquisition are not automatically unnecessary. A useful partial direction is not wrong. Treat uncertain usefulness as unknown. Count unique confirmation episodes, flag missing confirmations separately from observed delay, and assess whole speech sequences for repetition/silence/interruptions. Zero simulated delay means same 50ms tick, not zero real latency. This runner requests only when audio is idle, so it does NOT validate interruption judgments during ongoing speech. Only one run per policy per trajectory; different numbers of qualified episodes depend on prior consumption. Protected policies have local priority, geometry/revision checks and repeated-direction suppression. Restricted means a reference action menu, NOT the exact production phrase/pacing runtime. Full menu policies have about30 action choices, not500 authored phrases. Do not assert superiority or recommend deployment on these tests alone. Specifically explain what remains of the roughly2second delay concern and what evidence is absent. Return one assessment per policy with cited run IDs.\nEVIDENCE:\n'''+json.dumps(evidence)
    env=_environment();exe=_codex_executable(env)
    with tempfile.TemporaryDirectory(prefix='sequence-review-') as tmp:
        tmp=Path(tmp);sp=tmp/'schema.json';op=tmp/'result.json';sp.write_text(json.dumps(schema))
        login=subprocess.run([exe,'login','status'],env=env,cwd=tmp,capture_output=True,text=True,timeout=15)
        if login.returncode or 'logged in using chatgpt' not in (login.stdout+login.stderr).lower():raise RuntimeError('ChatGPT CLI login required')
        result=subprocess.run([exe,'exec','--model','gpt-6-astra','--ephemeral','--ignore-user-config','--sandbox','read-only',
            '--skip-git-repo-check','--output-schema',str(sp),'-o',str(op),'-'],input=prompt,env=env,cwd=tmp,capture_output=True,text=True,timeout=240)
        if result.returncode:raise RuntimeError(_provider_failure(result.stdout,result.stderr) or 'judge_failed')
        value=json.loads(op.read_text())
        if sorted(x['policy'] for x in value['policies'])!=sorted(ARMS):raise ValueError('Missing policy')
        if any(not x['evidence_ids'] or not set(x['evidence_ids'])<=set(ids) for x in value['policies']):raise ValueError('Unsupported evidence')
        a.output.write_text(json.dumps(dict(model='gpt-6-astra',advisory=True,report_sha256=hashlib.sha256(raw).hexdigest(),result=value),indent=2)+'\n')
        print('Validated advisory sequence review:',a.output)

if __name__=='__main__':main()
