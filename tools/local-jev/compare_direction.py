"""Paired frozen v8/v9 evaluation, read-only DEV regressions, and warmed Mac CPU timing.

No Jev answers, future samples, participant outcomes, or recordings enter training.
All detailed outputs stay in a new external directory; never writes source data.
"""
import argparse
import copy
import csv
import hashlib
from http.server import HTTPServer
import threading
import requests
import json
from pathlib import Path
import statistics
import time
import numpy as np
import torch
import adaptive_data as a
import phrase_data as p
from model import Policy
from phrase_model import PhrasePolicy
from serve import handler


def read_rows(path):
    return [json.loads(line) for line in path.read_text().splitlines()]


def fingerprint(path):
    stat = path.stat()
    return dict(sha256=hashlib.sha256(path.read_bytes()).hexdigest(),size=stat.st_size,mtime_ns=stat.st_mtime_ns)


def logits(policy, texts):
    encoded = policy.tokenizer(texts,padding=True,truncation=False,return_tensors='pt')
    if encoded['input_ids'].shape[1] > 384: raise ValueError('Context overflow')
    with torch.inference_mode():
        return torch.cat([policy.model(**{k:v[i:i+24] for k,v in encoded.items()}).logits.cpu()
                          for i in range(0,len(texts),24)]).numpy()


def actions(policy, rows):
    values=logits(policy,[a.text(r['payload'],policy.feature_version) for r in rows]); result=[]
    for row, scores in zip(rows,values):
        req=row['payload']; choices=a.unpack(req)[-1]
        raw=a.LABELS[int(scores.argmax())]; masked=max(choices,key=lambda k:scores[a.LABELS.index(k)])
        gated='observe_1s' if a.blocked(req) else masked
        expected=row.get('expected',[row.get('label')])
        result.append(dict(id=row.get('id'),expected=expected,raw=raw,masked=masked,gated=gated,
                           blocked=a.blocked(req),direction=a.direction_words(req)))
    def metrics(rr):
        return dict(n=len(rr),**{k:sum(r[k] in r['expected'] for r in rr) for k in ('raw','masked','gated')})
    per_class={}
    for c in a.LABELS:
        scores=metrics([r for r in result if c in r['expected']])
        if all(len(r['expected'])==1 for r in result):
            scores['precision_recall_f1']={}
            for kind in ('raw','masked','gated'):
                tp=scores[kind];predicted=sum(r[kind]==c for r in result)
                precision=tp/predicted if predicted else 0;recall=tp/scores['n'] if scores['n'] else 0
                scores['precision_recall_f1'][kind]=dict(precision=precision,recall=recall,
                    f1=2*precision*recall/(precision+recall) if precision+recall else 0)
        per_class[c]=scores
    return dict(**metrics(result),per_class=per_class,
                before_gate_violations=sum(r['blocked'] and r['masked']!='observe_1s' for r in result),
                after_gate_violations=sum(r['blocked'] and r['gated']!='observe_1s' for r in result),
                unoffered=sum(r['masked'] not in a.unpack(row['payload'])[-1] for r,row in zip(result,rows)),rows=result)


def phrases(policy, rows):
    texts=[]; groups=[]
    for row in rows:
        features=p.features(row['payload'],policy.feature_version)
        groups.append((len(texts),len(features)));texts.extend(features)
    values=logits(policy,texts)[:,0]; results=[]
    for row,(start,n) in zip(rows,groups):
        state,choices,history,ar=p.unpack(row['payload'])
        winner=int(values[start:start+n].argmax()); phrase=list(choices)[winner]
        used={h['phrase'] for h in history}; opportunity=bool(used.intersection(choices)) and bool(set(choices)-used)
        closed=a.blocked(ar,allow_playback=p.wall_arrival_override(state,choices))
        results.append(dict(action=state['selected_action'],choice=phrase,matched=row['scores'][winner]>=max(row['scores'])-1e-6,
                            blocked=closed,repeat_opportunity=opportunity,avoided=opportunity and phrase not in used))
    def metrics(rr):
        return dict(n=len(rr),before_gates=sum(r['matched'] for r in rr),
                    after_gates=sum(r['blocked'] or r['matched'] for r in rr),
                    no_repeat=sum(r['avoided'] for r in rr),repeat_opportunities=sum(r['repeat_opportunity'] for r in rr))
    return dict(**metrics(results),per_class={c:metrics([r for r in results if r['action']==c]) for c in a.LABELS[1:]},rows=results)


def direction_pairs():
    # New fixed probes; authored before either candidate is evaluated. Each pair
    # has identical v8 feature text, different physically possible gaze direction.
    rows=[]
    for i,(side,start) in enumerate((('left','large turn left'),('right','large turn right'),('right','behind'))):
        for way in ('along','opposed'):
            state=dict(schema='adaptive-assistance-v1',runtime=dict(locally_qualified=True,seconds_since_speech_ended=2,
                playback=dict(is_busy=False,is_speaking=False),evidence=dict(valid=True,geometryAvailable=True,
                insideTargetWall=False,targetReturnPending=False,controllerOnTarget=False,targetGazeQualified=False,
                gazeWithinTargetVicinity=False,vicinityErrorDegrees=174,guidanceState=dict(newGuidanceAllowed=True,allowedAction='Direction'))),
                behavior=dict(recent=dict(samples=[dict(age_seconds=j*.5,angular_error_degrees=174-j*2,direction=side)
                            for j in range(8,-1,-1)],error_reduction_degrees=-16),interventions=[],previous_trials=[],wrong_selections=0),
                direction=dict(schema='adaptive-direction-v1',needed=side,
                    moving=(side if way=='along' else 'left' if side=='right' else 'right')+' away',started=start))
            state['runtime']['evidence']['yawErrorDegrees'] = (1 if side=='right' else -1) * (-174 if way=='along' else 174)
            state['runtime']['evidence']['pitchErrorDegrees'] = 0
            rows.append(dict(id=f'paired-{i}-{way}',payload=a.payload(state,['orient_brief','orient_explicit']),
                             label='observe_1s' if way=='along' else 'orient_explicit'))
    for i in range(0,len(rows),2):
        assert a.text(rows[i]['payload'],a.PROGRESS_FEATURE_VERSION)==a.text(rows[i+1]['payload'],a.PROGRESS_FEATURE_VERSION)
    return rows


def phrase_regressions(template):
    result=[]
    for action,phrases in [('correct',['Go back. That was the correct area.']),
                           ('refine_explicit',list(p.ARRIVAL_PHRASES))]:
        req=copy.deepcopy(template['payload']);state=req['state'];state['selected_action']=action
        state['behavior']['phrase_history']=[]
        e=state['runtime']['evidence'];e.update(targetReturnPending=action=='correct',insideTargetWall=True)
        e['guidanceState'].update(allowedAction='ReturnTarget' if action=='correct' else 'Arrival')
        if action=='refine_explicit':
            state['override_reason']='first_target_wall_entry';e['wallEntryQualified']=True
            state['runtime']['playback'].update(is_busy=True,is_speaking=True)
        req['questions']={p.SCOPE:dict(type='choice',criteria={x:x for x in phrases})}
        result.append(dict(payload=req,scores=p.reference_scores(req)))
    return result


def real_regressions(cases, archive):
    """Match the five already-selected DEV requests, never relabel from Jev responses."""
    result=[]; receipts=[]
    for run in ('run_033','run_034'):
        path=next((archive/'GazeData/DEV_NEUTRAL').glob(run+'*/trial_events.csv'))
        before=fingerprint(path); wanted={c['state']['decision_id']:c for c in cases if c.get('source')==run}
        found=set()
        with path.open() as stream:
            for event in csv.DictReader(stream):
                detail=event.get('detail',''); offset=detail.find('{')
                if event.get('event_type')!='jev_trace' or offset<0: continue
                trace=json.loads(detail[offset:]); key=trace.get('request_id')
                if trace.get('stage')!='request' or key not in wanted: continue
                case=wanted[key]; state=copy.deepcopy(trace['payload']['state'])
                # Explicit historical adapter: retired waits removed by the current
                # offered menu, policy identifier upgraded; measured state unchanged.
                req=a.payload(state,case['capabilities'])
                result.append(dict(id=case['name'],payload=req,expected=case['expected']))
                found.add(key)
        if found != set(wanted): raise ValueError('Missing recorded regression requests')
        after=fingerprint(path)
        if before!=after: raise RuntimeError('Source changed while evaluating')
        receipts.append(dict(source=str(path.relative_to(archive)),before=before,after=after,requests=len(found)))
    return result,receipts


def safety(action, phrase, row):
    fields = [('evidence','valid',False),('evidence','geometryAvailable',False),
              ('evidence','controllerOnTarget',True),('evidence','targetGazeQualified',True),
              ('evidence','gazeWithinTargetVicinity',True),('playback','is_busy',True),('playback','is_speaking',True),
              ('guidanceState','newGuidanceAllowed',False),('runtime','locally_qualified',False)]
    checks=[]
    for section,key,value in fields:
        req=copy.deepcopy(row['payload']);state=req['state'];runtime=state['runtime']
        destination = runtime if section=='runtime' else runtime['evidence']['guidanceState'] if section=='guidanceState' else runtime[section]
        destination[key]=value
        ar=a.payload(state,[state['selected_action']])
        result=action.predict(ar)
        action_safe=result['answers']['assistance']['choice']=='observe_1s'
        rejected=False
        try: phrase.predict(req)
        except ValueError: rejected=True
        checks.append(dict(field=section+'.'+key,action_observed=action_safe,phrase_rejected=rejected))
    return checks


def timing(action, phrase, rows, http=False):
    # Same selected-action test requests for paired full speaking decisions.
    samples=[]; direct=[]; phrase_times=[]; choices=[]
    server = HTTPServer(('127.0.0.1',0),handler(action,phrase)) if http else None
    if server:
        worker=threading.Thread(target=server.serve_forever,daemon=True);worker.start()
    def predict(policy, request):
        if not server: return policy.predict(request)
        response=requests.post(f'http://127.0.0.1:{server.server_port}/typesafe/v1/systemone',json=request,timeout=3)
        response.raise_for_status();return response.json()
    for _ in range(3):
        for row in rows:
            state=copy.deepcopy(row['payload']['state']); selected=state.pop('selected_action')
            req=a.payload(state,[selected])
            begin=time.perf_counter(); ar=predict(action,req); action_ms=(time.perf_counter()-begin)*1000
            choice=ar['answers']['assistance']['choice']
            if choice!='observe_1s':
                pr=copy.deepcopy(row['payload']);pr['state']['selected_action']=choice
                phrase_start=time.perf_counter(); predict(phrase,pr);phrase_ms=(time.perf_counter()-phrase_start)*1000
                samples.append((time.perf_counter()-begin)*1000);phrase_times.append(phrase_ms)
            direct.append(action_ms);choices.append(choice)
    if server:
        server.shutdown();server.server_close();worker.join(5)
    def stats(xs):
        return dict(n=len(xs),median=statistics.median(xs),p95=float(np.percentile(xs,95)),minimum=min(xs),maximum=max(xs)) if xs else dict(n=0)
    return dict(direct_ms=stats(direct),complete_two_stage_ms=stats(samples),phrase_ms=stats(phrase_times),
                observations_skipping_stage_two=choices.count('observe_1s'),
                scope=('Warm Mac CPU loopback HTTP, both sequential decisions' if http else 'Warm Mac CPU direct calls, both sequential decisions')+'; excludes headset transport and audio. Same requests, differing speaking counts reported.')


def adoption(report):
    before=report['models']['v8'];after=report['models']['v9'];reasons=[]
    for name in ('existing_regression','real_development'):
        for old,new in zip(before['actions'][name]['rows'],after['actions'][name]['rows']):
            if old['gated'] in old['expected'] and new['gated'] not in new['expected']:
                reasons.append(name+': regressed '+str(new['id']))
    for kind in ('masked','gated'):
        if after['actions']['v8_test'][kind]<before['actions']['v8_test'][kind]:
            reasons.append('Frozen v8 action test '+kind+' agreement decreased')
    for metric in ('before_gates','no_repeat'):
        if after['phrases']['v8_test'][metric]<before['phrases']['v8_test'][metric]:
            reasons.append('Frozen v8 phrase test '+metric+' decreased')
    for name in ('v8_test','v9_test'):
        if after['actions'][name]['before_gate_violations']>before['actions'][name]['before_gate_violations']:
            reasons.append(name+': raw closed-gate advice increased')
        if after['phrases'][name]['no_repeat']<before['phrases'][name]['no_repeat']:
            reasons.append(name+': repeat avoidance decreased')
    if any(not r['action_observed'] or not r['phrase_rejected'] for r in after['safety']):
        reasons.append('Gate regression')
    if any(r['after_gate_violations'] or r['unoffered'] for r in after['actions'].values()):
        reasons.append('Gate or offered-menu regression')
    for scope,metric in (('latency','direct_ms'),('http_latency','complete_two_stage_ms')):
        old=before[scope][metric];new=after[scope][metric]
        if not old['n'] or not new['n']:reasons.append('No complete latency evidence: '+scope);continue
        if new['median']>1.25*old['median'] or new['p95']>1.5*old['p95']:
            reasons.append('Latency outside predefined range: '+scope+'/'+metric)
    return dict(adopt=not reasons,reasons=reasons,
                criterion='Existing accepted regressions preserved, frozen v8 aggregates/repeats nondecreasing, no added closed-gate advice, unchanged gates/menu, direct and complete HTTP median <=1.25x and p95 <=1.5x.')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--before',type=Path,required=True);parser.add_argument('--after',type=Path,required=True)
    parser.add_argument('--archive',type=Path,default=Path.home()/'Documents/Trial Review Live/data')
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args();args.output.mkdir(exist_ok=False,parents=True)
    cases=json.loads((args.before/'reassurance-and-device-cases.json').read_text())
    regression=[dict(id=c['name'],payload=a.payload(c['state'],c['capabilities']),expected=c['expected']) for c in cases]
    real,receipts=real_regressions(cases,args.archive)
    sets={'v8_test':read_rows(args.before/'data/test.jsonl'),'v9_test':read_rows(args.after/'data/test.jsonl'),
          'existing_regression':regression,'real_development':real,'direction_pairs':direction_pairs()}
    phrase_sets={'v8_test':read_rows(args.before/'phrase/data/test.jsonl'),'v9_test':read_rows(args.after/'phrase/data/test.jsonl')}
    # Fixed first 12 phrase test states with open gates, across actions; no result-based selection.
    latency_rows=[r for r in phrase_sets['v8_test'] if not a.blocked(p.unpack(r['payload'])[-1])][:12]
    phrase_sets['singleton_and_arrival_regression']=phrase_regressions(latency_rows[0])
    report=dict(interpretation='Authored-label imitation and development regression only; not better participant guidance.',sources=receipts,models={})
    for name,path in [('v8',args.before),('v9',args.after)]:
        print('Evaluating '+name,flush=True); action=Policy(path/'model');phrase=PhrasePolicy(path/'phrase/model')
        item=dict(action_sha256=fingerprint(path/'model/model.safetensors')['sha256'],phrase_sha256=fingerprint(path/'phrase/model/model.safetensors')['sha256'],
                  actions={key:actions(action,rows) for key,rows in sets.items()},
                  phrases={key:phrases(phrase,rows) for key,rows in phrase_sets.items()})
        # Warm both stages before measuring; do not count loading or first invocation.
        action.predict(a.payload(latency_rows[0]['payload']['state'],[latency_rows[0]['payload']['state']['selected_action']]))
        phrase.predict(latency_rows[0]['payload'])
        item['safety']=safety(action,phrase,latency_rows[0])
        item['latency']=timing(action,phrase,latency_rows)
        item['http_latency']=timing(action,phrase,latency_rows,http=True);report['models'][name]=item
        (args.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps({k:{s:{f:v for f,v in x.items() if f not in ('rows','per_class')} for s,x in item[k].items()} for k in ('actions','phrases')}),flush=True)
        del action,phrase
    report['disagreements']={key:[dict(v8=x,v9=y) for x,y in zip(report['models']['v8']['actions'][key]['rows'],report['models']['v9']['actions'][key]['rows']) if x['gated']!=y['gated']] for key in sets}
    report['adoption']=adoption(report)
    for receipt in receipts:
        if fingerprint(args.archive/receipt['source'])!=receipt['before']: raise RuntimeError('Recording changed')
    (args.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n')

if __name__=='__main__':main()
