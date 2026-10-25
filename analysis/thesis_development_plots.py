#!/usr/bin/env python3
"""Descriptive figures from immutable, actual headset replay files; no simulated rows.

Scope: all locally mirrored DEV_NEUTRAL recordings dated 2026-10-19/20.
Run once with --data-root; later runs verify every source against source-manifest.json.
"""
import argparse
import csv
import hashlib
import json
import math
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/thesis/development-results'
FIG = ROOT / 'docs/thesis/figures'


def quantile(values, q):
    v = sorted(values)
    if not v:
        return None
    x = (len(v)-1)*q
    lo = math.floor(x)
    return v[lo] + (v[min(lo+1, len(v)-1)]-v[lo])*(x-lo)


def stats(values):
    return dict(n=len(values), median=quantile(values,.5), p95=quantile(values,.95),
                maximum=max(values) if values else None)


def write_csv(name, rows):
    if not rows:
        raise ValueError('No evidence for '+name)
    with (OUT/name).open('w', newline='') as f:
        w=csv.DictWriter(f, fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)


def extract(path, expected_hash=None):
    data=path.read_bytes(); sha=hashlib.sha256(data).hexdigest()
    if expected_hash and sha != expected_hash:
        raise ValueError(f'Source changed since manifest: {path}')
    records=[]; bad=[]; lines=data.splitlines()
    for n,line in enumerate(lines,1):
        try: records.append(json.loads(line))
        except json.JSONDecodeError:
            if n != len(lines): raise ValueError(f'Corrupt interior line {n}: {path}')
            bad.append(n)  # Preserve complete prefix; do not invent a terminal event.
    header=records[0]
    assert header['kind']=='header' and header['sourceType']=='live_headset'
    contexts=[r for r in records if r.get('participantCode')]
    assert {r['participantCode'] for r in contexts}=={'DEV_NEUTRAL'}
    runs={r['runNumber'] for r in contexts}; assert len(runs)==1
    run=runs.pop(); rid=header['recordingId']
    assert all(b['time']>=a['time'] and b['sequence']>a['sequence'] for a,b in zip(records,records[1:]))
    trials={}; bydecision=defaultdict(list); playback=[]; gaze=Counter(); reasons=Counter()
    for r in records:
        kind=r['kind']; tid=r.get('trialId','')
        if kind=='search_start':
            assert tid not in trials, (run,tid,'duplicate start')
            trials[tid]={'recording_id':rid,'run':run,'trial_id':tid,'voice':r['voice'],
                'start_s':r['time'],'end_s':None,'completed':False,'search_seconds':None,
                'observed_search_seconds':0.,'wrong_selections':0,'sample_rows':0,
                'usable_rows':0,'unusable_rows':0,'unknown_rows':0,'pause_events':0}
        t=trials.get(tid)
        if t and not t['completed']:
            t['observed_search_seconds']=max(t['observed_search_seconds'],r.get('searchSeconds',0.))
            if kind=='search_paused':t['pause_events']+=1
            if kind=='selection':
                if r['correct']:
                    t.update(completed=True,search_seconds=r['searchSeconds'],end_s=r['time'])
                    assert t['search_seconds']>0 and t['end_s']>=t['start_s']
                else:t['wrong_selections']+=1
            if kind=='sample' and r.get('searchActive'):
                if r.get('gazeAvailable') is True and r.get('gazeTracked')==1:
                    status='usable'
                elif r.get('gazeTracked')==0 or r.get('gazeAvailable') is False:
                    status='unusable'
                else: status='unknown'
                t['sample_rows']+=1;t[status+'_rows']+=1;gaze[status]+=1
                if status!='usable': reasons[r.get('gazeInvalidReason') or 'unspecified']+=1
        did=r.get('guidanceDecisionId')
        if did: bydecision[(tid,did)].append(r)
        if kind=='audio_playback_start' and tid in trials and r['time']>=trials[tid]['start_s'] and (not trials[tid]['completed']):
            playback.append(r)
    audio=[]; missing=Counter()
    for r in playback:
        did=r.get('guidanceDecisionId'); group=bydecision.get((r['trialId'],did),[])
        earlier=[x for x in group if x['sequence']<=r['sequence']]
        req=[x for x in earlier if x['kind']=='guidance_decision' and x.get('guidanceDecision',{}).get('stage')=='requested']
        ar=[x for x in earlier if x['kind']=='audio_request']
        ready=[x for x in earlier if x['kind']=='audio_ready']
        if not did or len(req)!=1 or len(ar)!=1 or len(ready)!=1:
            missing['missing_or_ambiguous_exact_join']+=1;continue
        q,a,b=req[0],ar[0],ready[0]
        if not q['time']<=a['time']<=b['time']<=r['time']:
            missing['nonmonotonic_stages']+=1;continue
        d=q['guidanceDecision']; category=d.get('category','');reason=d.get('reason','')
        audio.append(dict(recording_id=rid,run=run,trial_id=r['trialId'],decision_id=did,
            requested_s=q['time'],audio_request_s=a['time'],audio_ready_s=b['time'],playback_s=r['time'],
            decision_to_audio_request_s=a['time']-q['time'],audio_request_to_ready_s=b['time']-a['time'],
            ready_to_playback_s=r['time']-b['time'],request_to_playback_s=r['time']-q['time'],
            requested_category=category,requested_reason=reason,played_text=r['text']))
    # Re-validate end boundaries after all events are available.
    assert all(trials[x['trial_id']]['start_s']<=x['playback_s'] and
               (trials[x['trial_id']]['end_s'] is None or x['playback_s']<=trials[x['trial_id']]['end_s']) for x in audio)
    provenance=dict(path=str(path),sha256=sha,recording_id=rid,run=run,utc=header['utc'],
        source_type=header['sourceType'],build_guid=header['buildGuid'],app_version=header['appVersion'],
        trailing_malformed_lines=bad,valid_record_count=len(records),last_record_kind=records[-1]['kind'],
        last_record_time_s=records[-1]['time'],source_bytes=len(data))
    quality=dict(run=run,recording_id=rid,started=len(trials),completed=sum(t['completed'] for t in trials.values()),
        usable_rows=gaze['usable'],unusable_rows=gaze['unusable'],unknown_rows=gaze['unknown'],
        sample_rows=sum(gaze.values()),search_playback_starts=len(playback),exact_latency_matches=len(audio),
        unmatched_playbacks=sum(missing.values()),unusable_reasons=dict(reasons))
    return list(trials.values()),audio,quality,provenance


def main():
    p=argparse.ArgumentParser();p.add_argument('--data-root',type=Path);args=p.parse_args()
    OUT.mkdir(parents=True,exist_ok=True)
    manifest=OUT/'source-manifest.json'
    if manifest.exists(): sources=json.loads(manifest.read_text())['sources']
    else:
        if not args.data_root:p.error('--data-root required on first run')
        sources=[dict(path=str(f)) for f in sorted(args.data_root.glob('*/recording.jsonl'))
                 if f.parent.name.startswith(('20261019','20261020'))]
    trials=[];audio=[];quality=[];provenance=[]
    for s in sources:
        t,a,q,pr=extract(Path(s['path']),s.get('sha256'));trials+=t;audio+=a;quality.append(q);provenance.append(pr)
    assert len({p['recording_id'] for p in provenance})==len(provenance)
    assert sorted(q['run'] for q in quality)==list(range(20,29))
    assert all(t['sample_rows']==t['usable_rows']+t['unusable_rows']+t['unknown_rows'] for t in trials)
    assert len({(a['recording_id'],a['decision_id']) for a in audio})==len(audio)
    manifest.write_text(json.dumps(dict(scope='All nine locally mirrored DEV_NEUTRAL headset recordings dated 2026-10-19/20; runs 20–28. Development only.',sources=provenance),indent=2)+'\n')
    write_csv('trials.csv',trials);write_csv('speech-delivery.csv',audio)
    write_csv('run-quality.csv',[{k:v for k,v in q.items() if k!='unusable_reasons'} for q in quality])
    completed=[t['search_seconds'] for t in trials if t['completed']]
    summary=dict(recordings=len(quality),started_trials=len(trials),completed_trials=len(completed),
        unfinished_trials=len(trials)-len(completed),completion_seconds=stats(completed),
        speech_request_to_playback_seconds=stats([a['request_to_playback_s'] for a in audio]),
        decision_to_audio_request_seconds=stats([a['decision_to_audio_request_s'] for a in audio]),
        audio_request_to_ready_seconds=stats([a['audio_request_to_ready_s'] for a in audio]),
        ready_to_playback_seconds=stats([a['ready_to_playback_s'] for a in audio]),
        delivered_requests_over_two_seconds=sum(a['request_to_playback_s']>2 for a in audio),
        runs=quality,limitations=['Development sessions under a shared label, not independent participants.',
        'Different builds and unconstrained testing; no causal before/after or voice-condition inference.',
        'Software request-to-playback latency excludes pre-request qualification, prior deferrals and acoustic onset.',
        'Delivered requests only; cancelled or never-played requests do not enter latency distribution.',
        'Gaze usability fraction uses active-search rendered-frame rows, not native sensor rate, accuracy or physiological fixation.',
        'Trailing malformed record in Run 21 excluded; complete recorded prefix retained.',
        'Unfinished searches are listed, not assigned completion times.'])
    (OUT/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    plots(trials,audio,quality)
    confirmation_example(provenance)
    print(json.dumps({k:v for k,v in summary.items() if k not in ['runs','limitations']},indent=2))


def plots(trials,audio,quality):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10,'axes.spines.top':False,
        'axes.spines.right':False,'pdf.fonttype':42,'svg.fonttype':'none'})
    def save(fig,name):
        fig.savefig(FIG/(name+'.pdf'),bbox_inches='tight')
        fig.savefig(FIG/(name+'.png'),dpi=220,bbox_inches='tight');plt.close(fig)
    # Raw observations accompany boxes; deterministic offsets change display only.
    # No kernel densities, confidence intervals or significance tests on dev runs.
    import numpy as np
    palette=['#e8b353','#8dbbd7']
    def distribution(ax, groups, colors, minimum_box_n=3, raw=True):
        for i,(values,color) in enumerate(zip(groups,colors),1):
            values=sorted(values)
            if len(values)>=minimum_box_n:
                ax.boxplot([values],positions=[i],widths=.55,patch_artist=True,
                    showfliers=False,whis=1.5,
                    boxprops=dict(facecolor=color,edgecolor='#4b4b4b',linewidth=1),
                    medianprops=dict(color='#222222',linewidth=1.5),
                    whiskerprops=dict(color='#666666'),capprops=dict(color='#666666'))
            if raw and values:
                # Repeat a fixed offset pattern to separate coincident observations.
                dx=[0.] if len(values)==1 else [((j*7)%17-8)/55 for j in range(len(values))]
                ax.scatter([i+x for x in dx],values,s=18 if len(values)<10 else 7,
                    facecolors='white',edgecolors='#333333',linewidths=.55,
                    alpha=.95 if len(values)<10 else .45,zorder=3)
        ax.set_xlim(.4,len(groups)+.6)
        ax.grid(axis='y',alpha=.16,zorder=0)

    runs=[q['run'] for q in quality]
    fig,axes=plt.subplots(1,2,figsize=(9.4,4.5),gridspec_kw={'width_ratios':[1.8,1]})
    groups=[[t['search_seconds'] for t in trials if t['run']==r and t['completed']] for r in runs]
    distribution(axes[0],groups,[palette[i%2] for i in range(len(runs))])
    axes[0].set_yscale('log')
    axes[0].set_yticks([5,10,20,50,100,300],['5','10','20','50','100','300'])
    axes[0].set_ylim(4,370)
    axes[0].set_xticks(range(1,10),[f'{r}\nn={len(v)}' for r,v in zip(runs,groups)],fontsize=8)
    axes[0].set(xlabel='Development run',ylabel='Completed active-search time (s; log scale)')
    axes[0].set_title('(a) Completed searches',loc='left',fontsize=11)
    x=np.arange(len(runs))
    complete=[q['completed'] for q in quality];unfinished=[q['started']-q['completed'] for q in quality]
    axes[1].bar(x,complete,color=palette[1],label='Completed',width=.7)
    axes[1].bar(x,unfinished,bottom=complete,color=palette[0],label='Unfinished',width=.7,hatch='//',edgecolor='#666666',linewidth=.5)
    axes[1].set_xticks(x,runs,fontsize=8);axes[1].set_yticks(range(7))
    axes[1].set(xlabel='Development run',ylabel='Started searches (count)',ylim=(0,7.5))
    axes[1].set_title('(b) Recorded endpoints',loc='left',fontsize=11)
    axes[1].legend(frameon=False,fontsize=8,loc='upper right');axes[1].grid(axis='y',alpha=.16)
    fig.tight_layout(w_pad=2);save(fig,'development-search-times')

    fig,axes=plt.subplots(1,2,figsize=(9.4,4.5),gridspec_kw={'width_ratios':[1.6,1]})
    groups=[[a['request_to_playback_s']*1000 for a in audio if a['run']==r] for r in runs]
    distribution(axes[0],groups,[palette[i%2] for i in range(len(runs))])
    axes[0].set_xticks(range(1,10),[f'{r}\nn={len(v)}' for r,v in zip(runs,groups)],fontsize=8)
    axes[0].set(xlabel='Development run',ylabel='Request to software playback (ms)',ylim=(0,600))
    axes[0].set_title('(a) Delivered-hint intervals',loc='left',fontsize=11)
    fields=['decision_to_audio_request_s','audio_request_to_ready_s','ready_to_playback_s']
    groups=[[a[k]*1000 for a in audio] for k in fields]
    assert all(v>0 for g in groups for v in g), 'Log-scale stages require positive recorded intervals'
    distribution(axes[1],groups,[palette[0],palette[1],'#b1c9ad'])
    axes[1].set_yscale('log')
    axes[1].set_yticks([.1,1,10,100,1000],['0.1','1','10','100','1000'])
    axes[1].set_ylim(.1,1000)
    axes[1].set_xticks([1,2,3],['Decision\n→ audio\nrequest','Audio\nrequest\n→ ready','Ready\n→ playback'],fontsize=8)
    axes[1].set_ylabel('Stage duration (ms; log scale)')
    axes[1].set_title('(b) Delivery stages (n=339 each)',loc='left',fontsize=11)
    fig.tight_layout(w_pad=2);save(fig,'development-speech-delivery')
    fig,ax=plt.subplots(figsize=(8.8,4.2));x=list(range(len(quality)));bottom=[0.]*len(x)
    for key,label,color in [('usable_rows','Usable','#16617b'),('unusable_rows','Unusable','#dbad55'),('unknown_rows','Unknown','#a4a4a4')]:
        v=[100*q[key]/q['sample_rows'] if q['sample_rows'] else 0 for q in quality]
        ax.bar(x,v,bottom=bottom,label=label,color=color,width=.65);bottom=[a+b for a,b in zip(bottom,v)]
    ax.set_xticks(x,[f"{q['run']}\nn={q['sample_rows']:,}" for q in quality],fontsize=9)
    ax.set(xlabel='Development run / recorded active-search rows',ylabel='Recorded active-search rows (%)',ylim=(0,105))
    ax.legend(loc='lower left',bbox_to_anchor=(0,1),ncol=3,frameon=False);ax.grid(axis='y',alpha=.15)
    fig.tight_layout();save(fig,'development-gaze-usability')


def confirmation_example(provenance):
    """Previously investigated Run 20 episode; observations, not counterfactual output."""
    source=next(s for s in provenance if s['run']==20)
    events=[]
    for line in Path(source['path']).open():
        r=json.loads(line)
        if r.get('trialId')!='trial_004' or not 321.4<=r['time']<=324.7:continue
        kind=r['kind']; d=r.get('guidanceDecision',{})
        item=dict(time_s=r['time'],kind=kind,decision_id=r.get('guidanceDecisionId',''),
                  text='',outcome='',gaze_qualified='',controller_qualified='')
        if kind=='guidance_decision' and d.get('stage')=='requested' and d.get('reason')=='gaze_target_vicinity_hold':
            item['text']=d['phrase']
        elif kind in ['transmission_state','transmission_decision']:
            payload=json.loads(r['detail'].split(';',1)[1])
            item['decision_id']=payload['decision_id']
            if kind=='transmission_state':
                if payload.get('reason')!='gaze_target_vicinity_hold':continue
                e=payload['runtime']['evidence'];item['gaze_qualified']=e['targetGazeQualified']
                item['controller_qualified']=e['controllerTargetHoldQualified'];item['text']=payload['proposed_phrase']
            else:item['outcome']=payload['outcome'];item['text']=payload['phrase']
        elif kind in ['audio_playback_start','audio_playback_end']:
            item['text']=r['text']
        else:continue
        events.append(item)
    states=[e for e in events if e['kind']=='transmission_state']
    decisions=[e for e in events if e['kind']=='transmission_decision']
    starts=[e for e in events if e['kind']=='audio_playback_start']
    ends=[e for e in events if e['kind']=='audio_playback_end']
    assert len(states)==3 and [e['outcome'] for e in decisions]==['wait','wait','speak']
    assert all(e['gaze_qualified'] and not e['controller_qualified'] for e in states)
    assert len(starts)==len(ends)==1 and starts[0]['decision_id']==states[-1]['decision_id']
    write_csv('confirmation-example.csv',events)
    import matplotlib.pyplot as plt
    fig,ax=plt.subplots(figsize=(8.8,3.5))
    origin=next(e['time_s'] for e in events if e['kind']=='guidance_decision')
    for state,decision in zip(states,decisions):
        assert state['decision_id']==decision['decision_id']
        x=state['time_s']-origin;width=decision['time_s']-state['time_s']
        ax.scatter(x,2,marker='D',color='#16617b',s=34,zorder=4)
        ax.broken_barh([(x,width)],(.8,.4),facecolors='#dbad55' if decision['outcome']=='wait' else '#16617b')
        ax.text(x+width/2,1.3,decision['outcome'].capitalize(),ha='center',fontsize=9)
    a=starts[0]['time_s']-origin;b=ends[0]['time_s']-origin
    ax.broken_barh([(a,b-a)],(-.2,.4),facecolors='#16617b')
    ax.text((a+b)/2,.3,starts[0]['text'],ha='center',fontsize=9)
    ax.annotate('',xy=(a,2.45),xytext=(0,2.45),arrowprops=dict(arrowstyle='<->',color='#333333'))
    ax.text(a/2,2.56,f'{a:.2f} s from first qualified hint request to playback',ha='center',fontsize=9)
    ax.set_yticks([0,1,2],['Software speech','Jev transmission\nrequest → response','Gaze-qualified\nstate snapshots'])
    ax.set(xlabel=f'Seconds since session time {origin:.3f} s (Run 20, Trial 4)',ylim=(-.4,2.85),xlim=(-.08,b+.12))
    ax.grid(axis='x',alpha=.2);fig.tight_layout()
    fig.savefig(FIG/'development-confirmation-sequence.pdf',bbox_inches='tight')
    fig.savefig(FIG/'development-confirmation-sequence.png',dpi=220,bbox_inches='tight');plt.close(fig)

if __name__=='__main__':main()
