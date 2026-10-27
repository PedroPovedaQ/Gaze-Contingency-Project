"""Paired, bounded comparison of frozen MiniLM and current Jev assistance-v2 requests."""
import argparse
import hashlib
from http.server import HTTPServer
import json
from pathlib import Path
import threading
import time
import requests
import adaptive_data as data
from model import Policy
from serve import handler
from shadow import latency


def cases(run):
    source=data.ROOT/'scripts/tests/adaptive-device-replay.json'
    rows=[]
    for case in json.loads(source.read_text()):
        rows.append(dict(id=case['name'],source=case['source'],group='device_regression',expected=case['expected'],
                         payload=data.payload(case['state'],case['capabilities'])))
    # Frozen choice: first test row of each class, not selected by model results.
    seen=set()
    for line in (run/'data/test.jsonl').read_text().splitlines():
        row=json.loads(line)
        if row['label'] in seen: continue
        seen.add(row['label'])
        expected=data.LABELS[:3] if row['label'].startswith('observe_') else [row['label']]
        rows.append(dict(id=row['id'],source='synthetic held-out',group='synthetic_test',expected=expected,payload=row['payload']))
    return rows


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--run',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True); parser.add_argument('--live-jev',action='store_true')
    args=parser.parse_args(); args.output.mkdir(parents=True,exist_ok=False)
    rows=cases(args.run)
    (args.output/'cases.json').write_text(json.dumps(rows,indent=2)+'\n')
    policy=Policy(args.run/'model','cpu'); local_rows=[]; cold=None
    for row in rows:
        start=time.perf_counter(); result=policy.predict(row['payload'],guard=False); elapsed=(time.perf_counter()-start)*1000
        if cold is None: cold=elapsed
        answer=result['answers']['assistance']; gated='observe_1s' if data.blocked(row['payload']) else answer['choice']
        local_rows.append(dict(case=row['id'],raw=result['raw_choice'],masked=answer['choice'],gated=gated,
                               probabilities=answer['probabilities'],elapsed_ms=elapsed,
                               expected=row['expected'],reference=data.reference(row['payload'])))
    warm=[]
    for _ in range(3):
        for row in rows:
            start=time.perf_counter(); policy.predict(row['payload'],guard=False); warm.append((time.perf_counter()-start)*1000)
    server=HTTPServer(('127.0.0.1',0),handler(policy)); worker=threading.Thread(target=server.serve_forever,daemon=True); worker.start()
    local=requests.Session(); remote=requests.Session(); records=[]
    try:
        if args.live_jev:
            keys=json.loads((data.ROOT/'Assets/StreamingAssets/api_keys.json').read_text())
            if not keys.get('jev_key'): raise ValueError('No configured Jev key')
            remote.headers['Authorization']='Bearer '+keys['jev_key']
        for repeat in range(2):
            for index,row in enumerate(rows):
                for arm in (['local','jev'] if (index+repeat)%2 else ['jev','local']):
                    if arm=='jev' and not args.live_jev: continue
                    session=remote if arm=='jev' else local
                    url='https://ai-gateway.vercel.sh/typesafe/v1/systemone' if arm=='jev' else f'http://127.0.0.1:{server.server_port}/typesafe/v1/systemone'
                    start=time.perf_counter()
                    item=dict(case=row['id'],arm=arm,repeat=repeat,source=row['source'],group=row['group'])
                    try:
                        response=session.post(url,json=row['payload'],timeout=1.5)
                        response.raise_for_status(); answer=response.json()['answers']['assistance']; choice=answer['choice']
                        if answer.get('type')!='choice' or choice not in row['payload']['questions']['assistance']['criteria']: raise ValueError('Invalid choice')
                        if (time.perf_counter()-start)*1000 > 1500: raise TimeoutError('Exceeded runtime decision deadline')
                        item.update(status='ok',choice=choice,expected_match=choice in row['expected'])
                    except (requests.RequestException,ValueError,TypeError,KeyError,TimeoutError) as exc:
                        item.update(status='error',error_type=type(exc).__name__)
                    item['elapsed_ms']=(time.perf_counter()-start)*1000; records.append(item)
                    with (args.output/'requests.jsonl').open('a') as f: f.write(json.dumps(item)+'\n')
                    print(json.dumps(item),flush=True)
    finally:
        server.shutdown(); server.server_close(); worker.join(5); local.close(); remote.close()
    arms={}
    for arm in sorted(set(r['arm'] for r in records)):
        subset=[r for r in records if r['arm']==arm]; success=[r for r in subset if r['status']=='ok']
        arms[arm]=dict(attempts=len(subset),failures=len(subset)-len(success),expected_matches=sum(r['expected_match'] for r in success),
                       success_latency_ms=latency([r['elapsed_ms'] for r in success]),attempt_latency_ms=latency([r['elapsed_ms'] for r in subset]))
    report=dict(policy=data.POLICY,model=str(args.run/'model'),weights_sha256=hashlib.sha256((args.run/'model/model.safetensors').read_bytes()).hexdigest(),
                contract_sha256=hashlib.sha256(data.SOURCE.read_bytes()).hexdigest(),fixture_sha256=hashlib.sha256((data.ROOT/'scripts/tests/adaptive-device-replay.json').read_bytes()).hexdigest(),
                case_count=len(rows),local_direct=dict(load_ms=policy.load_ms,cold_request_ms=cold,warm_ms=latency(warm),rows=local_rows),http=arms,requests=records,
                interpretation='Selected device failures are regression probes, not held-out participant validation. Synthetic labels are authored; repeated requests are latency samples, not independent cases. CPU compute/loopback versus Mac-to-cloud HTTP, not headset/audio/frame-rate timing. Local HTTP includes external observation guards; raw/masked results are separate. Probabilities uncalibrated.')
    (args.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(arms,indent=2),flush=True)


if __name__=='__main__': main()
