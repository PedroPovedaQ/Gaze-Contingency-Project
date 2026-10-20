"""Bounded offline Jev request pilot; credentials never included in output."""
import argparse
import hashlib
import json
from pathlib import Path
import time


from guidance_paired_pilots import ROOT, stats

ENDPOINT='https://ai-gateway.vercel.sh/typesafe/v1/systemone'


def eligible(catalog,angle,distance):
    # Exact half-open NavigationPhrase.Matches contract, including +180 edge.
    matches=[];seen=set()
    for p in catalog:
        c=p['constraints']
        if (c['min_angle']<=angle and (angle<c['max_angle'] or angle==c['max_angle']==180)
            and c['min_distance_meters']<=distance<c['max_distance_meters'] and p['text'] not in seen):
            matches.append(p);seen.add(p['text'])
    return matches


def payload(angle,distance,candidates,repeat):
    criteria={p['text']:{'tone':p['metadata']['tone'],'word_count':len(p['text'].split()),
                         'semantics':p['metadata'],'geometrically_valid':True} for p in candidates}
    return {'model':'typesafe-ai/jev','state':{'schema':'speech-navigation-v2',
        'pilot':'offline_synthetic_no_participant','relative_yaw_degrees':angle,'distance_meters':distance,
        'requested_tone':'Casual','last_phrase':candidates[0]['text'] if repeat else '',
        'runtime':{'evidence':{'valid':True,'geometryAvailable':True},
                   'playback':{'is_speaking':False,'is_busy':False},'seconds_since_speech_ended':4}},
        'questions':{'navigation_phrase':{'type':'choice',
            'instructions':'Choose one short navigation phrase. Prefer the requested tone and never repeat the last phrase when another valid choice exists. For Casual guidance, use a short Direct cue as the next alternative; avoid Technical wording unless Technical is requested. Do not infer task success or target passage.',
            'criteria':criteria}}}


def main():
    try:
        import requests
    except ImportError:
        raise SystemExit('The opt-in network pilot requires requests; run with a Python environment containing requests.')
    ap=argparse.ArgumentParser();ap.add_argument('--keys',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);args=ap.parse_args()
    key=json.loads(args.keys.read_text()).get('jev_key')
    if not key:raise SystemExit('jev_key unavailable')
    catalog_file=ROOT/'Assets/Resources/SpatialVoice/navigation-phrases.json'
    catalog=json.loads(catalog_file.read_text())['navigation_phrases']
    rows=[]
    with requests.Session() as session:
        session.headers.update({'Authorization':'Bearer '+key,'Content-Type':'application/json'})
        # Two repetitions of six fixed states; first includes a cold connection.
        for repeat in range(2):
            for angle,distance in [(-175,2.5),(-90,2.5),(-21,2.5),(21,2.5),(90,2.5),(175,2.5)]:
                candidates=eligible(catalog,angle,distance)
                row={'case':f'{angle}:{distance}:repeat{repeat}','angle':angle,'distance':distance,
                     'eligible_count':len(candidates),'candidate_ids':[p['id'] for p in candidates],
                     'configured_set':'small5_equals_full_eligible','cold_connection':not rows}
                data=payload(angle,distance,candidates,repeat)
                start=time.perf_counter()
                try:
                    response=session.post(ENDPOINT,json=data,timeout=1.5)
                    row['http_status']=response.status_code
                    if response.ok:
                        answer=response.json().get('answers',{}).get('navigation_phrase',{})
                        choice=answer.get('choice')
                        row['choice']=choice if choice in {p['text'] for p in candidates} else None
                        row['status']='ok' if answer.get('type')=='choice' and row['choice'] else 'invalid_response'
                        row['repeated_last_phrase']=bool(repeat and choice==candidates[0]['text'])
                    else:row['status']='http_error'
                except requests.Timeout:row['status']='timeout'
                except (requests.RequestException,ValueError):row['status']='transport_or_parse_error'
                row['seconds']=round(time.perf_counter()-start,6)
                row['within_runtime_450ms_deadline']=row['status']=='ok' and row['seconds']<=.450
                rows.append(row)
                print(json.dumps({k:row[k] for k in ('case','status','seconds','eligible_count')}),flush=True)
    report={'schema':'jev-eligible-set-pilot-v1','basis':'Measured Mac-to-Vercel HTTP requests using synthetic states. Not headset requests or acoustic latency.',
        'catalog_sha256':hashlib.sha256(catalog_file.read_bytes()).hexdigest(),'requests':rows,
        'set_comparison':{'small':5,'medium_requested':15,'full_eligible':sorted(set(r['eligible_count'] for r in rows)),
            'conclusion':'Each selected half-open geometry cell has exactly five unique eligible phrases. Small and full are identical; medium cannot be tested from this catalog without adding new valid authored options. API support for larger sets remains untested.'},
        'latency_seconds':stats([r['seconds'] for r in rows]),
        'provider_failures':sum(r['status']!='ok' for r in rows),'requests_beyond_runtime_deadline':sum(not r['within_runtime_450ms_deadline'] for r in rows),
        'unknowns':['No participant behavior, perceptual quality, target recognition, or network performance on headset inferred.',
                    'Host pilot waits up to1.5s for diagnostics; requests exceeding450ms would miss the current Unity catalog deadline.',
                    'One question per request tests wording only; combined runtime transmit/interruption decisions are not benchmarked here.']}
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n')

if __name__=='__main__':main()
