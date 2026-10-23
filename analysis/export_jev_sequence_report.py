"""Export reproducible pilot comparison and import into existing Trial Review UI."""
import hashlib
import html
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from jev_sequence_pilot import ROOT, ARMS


def main():
    folder=ROOT/'analysis/pilots/20261020-stateful-jev-validated'
    r=json.loads((folder/'report.json').read_text());repeat=json.loads((ROOT/'analysis/pilots/20261020-jev-early-gate/report.json').read_text())
    wake=json.loads((ROOT/'analysis/pilots/20261020-jev-event-wake/report.json').read_text())
    judge=json.loads((folder/'astra-review.json').read_text())
    def ms(n):return f'{n*1000:.0f}'
    lines=['# Jev sequence experiment — October 20, 2026','',
        '**Measured:** Mac-to-Vercel HTTP. **Simulated:** playback, audio duration, pauses, fades and movement. The production C# state machine and short speech memory execute in the runner. No participant runtime or headset build changed.','',
        '## Paired policy comparison','',
        '| Policy | Requests | HTTP median / p95 (ms) | Confirmation median / max (simulated ms) | Unconfirmed episodes |',
        '|---|---:|---:|---:|---:|']
    for arm in ARMS:
        s=r['summary'][arm];h=s['http_latency'];c=s['simulated_confirmation_latency']
        lines.append(f"| {arm} | {s['http_requests']} | {ms(h['median'])} / {ms(h['p95'])} | {ms(c['median'])} / {ms(c['max'])} | {s['missed_episodes']} / {s['qualified_episodes']} |")
    lines+=['','146 real requests, zero transport/schema failures; five responses exceeded the 450 ms experimental deadline. A zero simulated delay means playback began in the same 50 ms tick, not zero real latency. Episode counts depend on earlier action consumption; denominators are not a matched participant outcome.',
        '', 'The six flagged all-Jev playback events were valid against the request-time evidence. The observations changed before playback. Two became opposing directions, one directed away during target gaze, two confirmations no longer had qualified current gaze, and one pointing cue arrived after selection. These are stale delivery/final-check issues, not six demonstrated bad Jev choices. Useful partial cues, milestone encouragement and reacquisition are not graded wrong for differing from an exact phrase.',
        '', '## Follow-up: early repetition gate','',
        'Skip a request when the same ordinary direction is still blocked by the existing three-second repetition interval. Fresh events and direction changes remain eligible. Two trajectories × two repetitions, alternating order.']
    for name,s in repeat['summary'].items():
        lines.append(f"- Early gate {name}: {s['requests']} real requests; {s['simulated_starts']} simulated starts; {s['late_repeat_blocks']} responses rejected late for repetition; {s['harmful_evidence_flags']} flagged starts.")
    lines+=['','This reduced calls by about 68% (31 to 10), without reducing the number of simulated starts. It is a small experimental result, not proven wearer benefit or a deployment decision.',
        '', '## Follow-up: all-Jev with event-triggered wake and cancellation','',
        'Every spoken action still comes from Jev. The revised scheduler cancels pending advice and allows an immediate request when the production state/action/direction changes. Six trajectories, one repeat, alternating order.']
    for name,s in wake['summary'].items():
        c=s['confirmation_latency']
        lines.append(f"- Event wake {name}: {s['requests']} requests; simulated confirmation median {ms(c['median'])} ms, max {ms(c['max'])} ms; {s['unconfirmed_episodes']} unconfirmed episodes; {len(s['playback_flags'])} flagged starts.")
    lines+=['','Event wake reduced the simulated median from 800 to 400 ms and flagged starts from six to zero, while raising requests from 48 to 67 (about 40%). This runner polls at 600 ms; the current production HintIntervalSeconds is 100 ms. The measured improvement therefore does not establish a similar gain in the current app.', '', 'The remaining unconfirmed gaze episode was speech_then_confirm: gaze qualified at 0.75 s while a direction clip continued until 1.50 s; controller entry at 1.60 s cancelled the pending decision, the replacement exceeded the 450 ms deadline, and qualified pointing at 2.60 s superseded the gaze acknowledgment. Pointing was spoken at 2.95 s. This is one unconfirmed gaze episode, not multiple failures. The longest delivered event-wake confirmation (1.05 s) comprised 0.80 s of existing speech and 0.25 s waiting for Jev.', '', '## What happened to the two-second delays?','',
        'The initial comparison did not reproduce a delivered confirmation above 1.4 s. Protected policies topped out at 0.6 s, mostly waiting for the simulated existing phrase to finish plus the 100 ms acknowledgment gap. This does not establish that the headset issue is fixed: clip duration is assumed (1.0 s, or 1.8 s for reminders), there is no real AudioSource or acoustic measurement, and unconfirmed episodes must not be excluded from interpretation.',
        '', '## Advisory Astra review','',judge['result']['recommendation'],'',judge['result']['caveats'],
        '', '## Remaining limitations and decision','',
        '- Keep the experimental action policies out of participant builds. The restricted arm is a reference action policy, not the exact Unity phrase and scheduling pipeline.',
        '- Follow-ups change simulated scheduling; they do not validate every production cancellation path. Port any selected change behind a development flag, then re-run the production voice/isolation tests and collect a headset run.',
        '- Requests in this runner begin only while audio is idle. Existing speech may delay confirmation, but live Jev interruption judgments are not benchmarked here.',
        '- Model history follows its own simulated speech, but movements are fixed across policies; there is no simulated participant adapting to the advice.',
        '- Catalog has roughly 30 action entries, not 500 authored phrases. No result supports a full 500-phrase deployment.',
        '- Need wearer evidence for audible onset, naturalness, voice-condition equivalence, and real network tails.',
        '', '## Representative timelines','']
    selected=('turn_wall_vertical','overshoot_reacquire','rapid_reverse')
    for run in r['runs']:
        if run['sequence'] not in selected or run['arm'] not in ('all_jev','jev_overrides'):continue
        lines+=['### '+run['sequence']+' — '+run['arm'],'','| Simulated time | Event |','|---:|---|']
        for e in run['events']:
            if e['kind'] not in ('simulated_start','blocked_stale_or_invalid','request_deadline_or_failure'):continue
            desc=e.get('text',e.get('choice',e.get('request','')))
            lines.append(f"| {e['time']:.2f} s | {e['kind']}: {desc}; {', '.join(e.get('flags',[]))} |")
        lines+=['']
    lines+=['## Reproduction','',
        'Run `python3 -m unittest discover -s analysis -p test_jev_sequence_pilot.py` for runner checks. Live entry points are `analysis/jev_sequence_pilot.py`, `analysis/jev_repeat_gate_pilot.py`, and `analysis/jev_event_wake_pilot.py`; each requires an ignored key file and a new output directory. Never put keys in reports. Raw request bodies and returned choices are saved in each pilot folder. The validated main report replays the exact saved responses and requires identical parsed payloads; it makes no extra model requests.','']
    doc=ROOT/'docs/testing/2026-10-20-jev-stateful-sequences.md';doc.write_text('\n'.join(lines))
    # Escaped standalone readable report, usable without a web server/dependencies.
    page='<!doctype html><meta charset="utf-8"><title>Jev sequence pilot</title><style>body{background:#171717;color:#eee;font:16px/1.6 system-ui;max-width:1100px;margin:40px auto;padding:20px}h1{color:#ffc904}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}a{color:#ffc904}</style><h1>Jev sequence pilot</h1><pre>'+html.escape('\n'.join(lines))+'</pre>'
    (folder/'comparison.html').write_text(page)
    desktop=Path.home()/'Desktop/Jev sequence comparison — 2026-10-20.html';desktop.write_text(page)
    sources=['Assets/GuidanceStateMachine.cs','Assets/GuidanceStateSnapshot.cs','Assets/SpatialVoice/SpeechDecisionMemory.cs','scripts/tests/GuidanceSequenceBridge.cs','analysis/jev_sequence_pilot.py']
    (folder/'provenance.json').write_text(json.dumps({f:hashlib.sha256((ROOT/f).read_bytes()).hexdigest() for f in sources},indent=2)+'\n')
    shared=Path.home()/'Documents/Trial Review Evaluations/guidance-paired-pilots.json';shared.parent.mkdir(parents=True,exist_ok=True)
    if shared.exists():
        existing=json.loads(shared.read_text())
        if existing.get('schema')!='guidance-paired-pilots-v1':raise ValueError('Unknown existing report; preserved')
        backup=shared.with_name('guidance-paired-pilots-before-stateful-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S')+'.json')
        shutil.copy2(shared,backup)
    else:existing=dict(schema='guidance-paired-pilots-v1',variants=[],measurement_basis='',threshold_seconds=.5,unknowns=[])
    existing['variants']=[v for v in existing['variants'] if not v['id'].startswith('stateful-20261020-')]
    for arm in ARMS:
        examples=[]
        for run in r['runs']:
            if run['arm']!=arm or run['sequence'] not in selected:continue
            examples.append(dict(id=run['sequence'],timeline=[dict(time=e['time'],event=e['kind'],action=e.get('text',e.get('choice',''))) for e in run['events'] if e['kind'] in ('simulated_start','blocked_stale_or_invalid')]))
        existing['variants'].append(dict(id='stateful-20261020-'+arm,source_ref='C# qualification; experimental simulated audio',metrics=r['summary'][arm],scenarios=examples))
    existing['measurement_basis']='Mixed offline pilot datasets. Stateful 20261020: production C# qualification; simulated playback/trajectories; measured Mac HTTP. Older variants retain their own source refs. No headset/acoustic or participant outcomes.'
    existing['stateful_followups']=dict(early_gate=repeat['summary'],event_wake=wake['summary'])
    existing['unknowns']=list(dict.fromkeys(existing.get('unknowns',[])+r['limitations']))
    tmp=shared.with_suffix('.tmp')
    # Keep an independent import artifact alongside the experiment, with follow-up
    # metrics rendered by the existing viewer's generic metrics list.
    existing['variants'].append(dict(id='stateful-20261020-followups',source_ref='simulated scheduler only',metrics=dict(early_gate=repeat['summary'],event_wake={k:{n:v for n,v in x.items() if n!='playback_flags'} for k,x in wake['summary'].items()})))
    tmp.write_text(json.dumps(existing,indent=2)+'\n');tmp.replace(shared)
    (folder/'viewer-import.json').write_text(json.dumps(existing,indent=2)+'\n')
    print('Report:',doc,'\nDesktop:',desktop,'\nTrial Review import:',shared)

if __name__=='__main__':main()
