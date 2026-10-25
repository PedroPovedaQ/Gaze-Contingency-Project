"""Paired warmed local HTTP comparison plus saved-model inference verification."""
import argparse
from http.server import HTTPServer
import json
from pathlib import Path
import threading
import time
import requests
from benchmark import stats
from model import Policy
from serve import handler
from train import read


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--before', type=Path, required=True)
    parser.add_argument('--after', type=Path, required=True)
    parser.add_argument('--device', choices=['cpu','mps'], default='mps')
    args = parser.parse_args(); output = args.after/'paired-local.json'
    if output.exists(): raise ValueError('Refusing overwrite of evidence')
    policies = {name:Policy(path/'model',args.device) for name,path in [('before',args.before),('after',args.after)]}
    fixtures = read(args.after/'data/production.jsonl')
    servers = {}; workers = []; observations = []
    session = requests.Session()
    try:
        for name, policy in policies.items():
            for row in fixtures: policy.predict(row['payload'])
            server = HTTPServer(('127.0.0.1',0), handler(policy)); servers[name] = server
            worker = threading.Thread(target=server.serve_forever,daemon=True); worker.start(); workers.append(worker)
        for repeat in range(5):
            for index,row in enumerate(fixtures):
                for name in (['before','after'] if (repeat+index)%2 else ['after','before']):
                    start=time.perf_counter(); result={}
                    try:
                        response=session.post(f'http://127.0.0.1:{servers[name].server_port}/typesafe/v1/systemone',json=row['payload'],timeout=3)
                        response.raise_for_status(); choice=response.json()['answers']['navigation_action']['choice']
                        if choice not in row['payload']['questions']['navigation_action']['criteria']: raise ValueError('Unoffered choice')
                        result=dict(status='ok',choice=choice,exact=choice==row['label'])
                    except (requests.RequestException,ValueError,KeyError,TypeError,AttributeError): result=dict(status='error')
                    result.update(arm=name,case=row['id'],repeat=repeat,elapsed_ms=(time.perf_counter()-start)*1000)
                    observations.append(result)
        # Exercise the actual saved-model path, including feature-version dispatch and gates.
        fresh=read(args.after/'data/fresh.jsonl'); correct=0; disagreements=[]
        expected={r['id']:r['guarded'] for r in json.loads((args.after/'report.json').read_text())['results']['continued']['fresh']['predictions']}
        for row in fresh:
            choice=policies['after'].predict(row['payload'])['answers']['navigation_action']['choice']
            correct += choice==row['label']
            if choice!=expected[row['id']]: disagreements.append(row['id'])
        report=dict(device=args.device,measurement='Paired warmed local HTTP; 10 regression cases, five repeats, alternating order.',
                    rows=observations,summary={name:dict(success_latency=stats([r['elapsed_ms'] for r in observations if r['arm']==name and r['status']=='ok']),
                    failures=sum(r['status']!='ok' for r in observations if r['arm']==name),exact=sum(r.get('exact',False) for r in observations if r['arm']==name)) for name in policies},
                    fresh_saved_model_verification=dict(n=len(fresh),correct=correct,batch_disagreements=disagreements))
        output.write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps({k:v for k,v in report.items() if k!='rows'},indent=2))
    finally:
        for server in servers.values(): server.shutdown(); server.server_close()
        for worker in workers: worker.join(timeout=5)
        session.close()


if __name__=='__main__': main()
