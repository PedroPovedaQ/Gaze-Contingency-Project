"""Mac end-to-end request measurements; no headset/Wi-Fi/acoustic claims."""
import argparse
from collections import Counter
from http.server import HTTPServer
import json
from pathlib import Path
import random
import threading
import time
import requests
import numpy as np
from data import LABELS, ROOT, SOURCE, reference, text
from model import Policy
from serve import handler


def stats(samples):
    return dict(n=len(samples),median_ms=float(np.median(samples)),p95_ms=float(np.percentile(samples,95)),
                min_ms=float(min(samples)),max_ms=float(max(samples))) if samples else None


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--run',type=Path,required=True)
    parser.add_argument('--live-jev',action='store_true',help='At most 20 synthetic requests using the existing configured key')
    args=parser.parse_args()
    output=args.run/'benchmark.json'
    if output.exists() or (args.run/'benchmark-requests.jsonl').exists():
        raise ValueError('Refusing overwrite or mixing of measured evidence')
    fixtures=[json.loads(line) for line in (args.run/'data/production.jsonl').read_text().splitlines()]
    results={'measurement':'Mac local inference/loopback HTTP vs stored and optionally fresh Mac-to-Jev HTTP. No headset or acoustic measurement.',
             'devices':{},'http_rows':[]}
    for device in ('cpu','mps'):
        policy=Policy(args.run/'model',device)
        begin=time.perf_counter(); policy.predict(fixtures[0]['payload']); cold=(time.perf_counter()-begin)*1000
        for row in fixtures: policy.predict(row['payload'])
        measured=[]; choices=[]
        for repeat in range(10):
            for row in fixtures:
                begin=time.perf_counter(); result=policy.predict(row['payload'])
                measured.append((time.perf_counter()-begin)*1000)
                choices.append(result['answers']['navigation_action']['choice']==row['label'])
        results['devices'][device]=dict(load_ms=policy.load_ms,cold_request_ms=cold,warm_request=stats(measured),
                                       exact_agreement=sum(choices)/len(choices))
        del policy
    device=min(results['devices'],key=lambda d:results['devices'][d]['warm_request']['median_ms'])
    policy=Policy(args.run/'model',device)
    for row in fixtures:policy.predict(row['payload'])
    server=HTTPServer(('127.0.0.1',0),handler(policy))
    worker=threading.Thread(target=server.serve_forever,daemon=True); worker.start()
    remote=None
    if args.live_jev:
        keys=json.loads((ROOT/'Assets/StreamingAssets/api_keys.json').read_text())
        key=keys.get('jev_key')
        if not key: raise ValueError('Configured Jev key unavailable')
        remote=requests.Session();remote.headers['Authorization']='Bearer '+key
    local=requests.Session()
    try:
        # Alternated paired ordering. Cold first remote request remains in reported results.
        for repeat in range(2):
            for i,row in enumerate(fixtures):
                arms=['local','jev'] if (i+repeat)%2 else ['jev','local']
                for arm in arms:
                    if arm=='jev' and remote is None:continue
                    session=remote if arm=='jev' else local
                    url='https://ai-gateway.vercel.sh/typesafe/v1/systemone' if arm=='jev' else f'http://127.0.0.1:{server.server_port}/typesafe/v1/systemone'
                    begin=time.perf_counter(); result={}
                    try:
                        response=session.post(url,json=row['payload'],timeout=3)
                        response.raise_for_status(); answer=response.json()['answers']['navigation_action']
                        choice=answer['choice']
                        if answer.get('type')!='choice' or choice not in row['payload']['questions']['navigation_action']['criteria']:
                            raise ValueError('Invalid answer')
                        result=dict(choice=choice,status='ok',exact=choice==row['label'],acceptable=choice in row['acceptable'])
                    except (requests.RequestException,ValueError,KeyError,TypeError,AttributeError):result=dict(status='error')
                    result.update(case=row['id'],repeat=repeat,arm=arm,elapsed_ms=(time.perf_counter()-begin)*1000)
                    results['http_rows'].append(result)
                    # Preserve completed observations even if a later request/process fails.
                    with (args.run/'benchmark-requests.jsonl').open('a') as evidence:
                        evidence.write(json.dumps(result)+'\n')
                    print(json.dumps(result),flush=True)
        results['http']={arm:dict(attempt_latency=stats([r['elapsed_ms'] for r in results['http_rows'] if r['arm']==arm]),
                          success_latency=stats([r['elapsed_ms'] for r in results['http_rows'] if r['arm']==arm and r['status']=='ok']),
                          failures=sum(r['status']!='ok' for r in results['http_rows'] if r['arm']==arm),
                          exact=sum(r.get('exact',False) for r in results['http_rows'] if r['arm']==arm),
                          acceptable=sum(r.get('acceptable',False) for r in results['http_rows'] if r['arm']==arm))
                         for arm in sorted(set(r['arm'] for r in results['http_rows']))}
        old=[r for r in json.loads(SOURCE.read_text())['rows'] if r['arm']=='actions' and not r['error']]
        results['historical_jev_oct20']=stats([r['elapsed_ms'] for r in old])
        timings=[]
        for _ in range(100):
            for row in fixtures:
                begin=time.perf_counter(); reference(row['payload']); timings.append((time.perf_counter()-begin)*1000)
        results['authored_rule_baseline']=dict(latency=stats(timings), unique_cases=len(fixtures),
            exact=sum(reference(r['payload'])==r['label'] for r in fixtures),
            acceptable=sum(reference(r['payload']) in r['acceptable'] for r in fixtures))
        results['selected_local_device']=device
        output.write_text(json.dumps(results,indent=2)+'\n')
        print(json.dumps({k:v for k,v in results.items() if k!='http_rows'},indent=2))
    finally:
        server.shutdown();server.server_close();worker.join(timeout=5);local.close()
        if remote is not None:remote.close()


if __name__=='__main__':main()
