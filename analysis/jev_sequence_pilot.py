"""Stateful OFFLINE policy experiment, never imported by participant builds.

Production C# qualification and bounded memory; experimental action menus and a
50ms discrete-event audio model. HTTP is real when --live is supplied; playback,
fade, queue delays and trajectories are simulated, never measured headset data.
"""
import argparse
import copy
import json
import subprocess
from pathlib import Path
from guidance_policy_bakeoff import CATALOG, ask
from guidance_action_pilot import stats

ROOT = Path(__file__).resolve().parents[1]
ARMS = ('restricted', 'eligible_actions', 'all_jev', 'jev_overrides')
DT = .05
DEADLINE = .45
CONFIRM = {'ConfirmGaze': 'gaze_confirm', 'ConfirmPointing': 'controller_confirm'}
RETURN = {'ReturnTarget', 'ReturnBearing', 'ReturnWall'}


class Bridge:
    def __init__(self):
        self.proc = subprocess.Popen(['bash', str(ROOT/'scripts/run-guidance-sequence-bridge.sh')],
                                     stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, cwd=ROOT)
    def call(self, op, **values):
        self.proc.stdin.write(json.dumps(dict(op=op, **values))+'\n'); self.proc.stdin.flush()
        line = self.proc.stdout.readline()
        if not line: raise RuntimeError('C# bridge exited')
        reply = json.loads(line)
        if 'error' in reply: raise RuntimeError(reply)
        return reply
    def close(self):
        self.proc.stdin.close(); self.proc.wait(timeout=10); self.proc.stdout.close()


def scenarios():
    # Edits accumulate; time is fixed exogenous movement, not model-generated behavior.
    return {
      'turn_wall_vertical': (10, [(0,dict(yaw=170)),(1.5,dict(yaw=110)),(3,dict(yaw=55)),
          (4.5,dict(inside=True,yaw=-8,pitch=-15)),(6,dict(yaw=0,pitch=-5)),(7,dict(looking=True,pitch=0)),(8,dict(pointing=True))]),
      'overshoot_reacquire': (9, [(0,dict(inside=True,yaw=12,pitch=6)),(1,dict(yaw=0,pitch=0,looking=True)),
          (2,dict(yaw=-10,pitch=-8,looking=False)),(3.5,dict(yaw=0,pitch=0,looking=True)),(5,dict(pointing=True)),(7,dict(selected=True))]),
      'controller_before_gaze': (8, [(0,dict(inside=True,yaw=-10,pitch=-10)),(.4,dict(pointing=True)),
          (1.6,dict(looking=True,yaw=0,pitch=0)),(3,dict(pointing=False,looking=False,yaw=8,pitch=6)),
          (4,dict(pointing=True,looking=True,yaw=0,pitch=0)),(6.5,dict(selected=True))]),
      'gaze_controller_elsewhere': (8, [(0,dict(inside=True,yaw=0,looking=True)),(5,dict(pointing=True)),(7,dict(selected=True))]),
      'overlap_blink_pause': (9, [(0,dict(inside=True,yaw=8,pitch=5,ambiguous=True)),(1,dict(valid=False)),
          (1.4,dict(valid=True,ambiguous=False,looking=True,yaw=0,pitch=0)),(2,dict(valid=False)),
          (2.2,dict(valid=True)),(3,dict(paused=True)),(4,dict(paused=False,looking=False,yaw=-8,pitch=-10)),(6,dict(looking=True,yaw=0,pitch=0))]),
      'rapid_reverse': (7, [(0,dict(yaw=35)),(.35,dict(yaw=-35)),(.7,dict(yaw=35)),(1,dict(valid=False)),
          (1.5,dict(valid=True,yaw=-70)),(3,dict(inside=True,yaw=0,pitch=15)),(5,dict(looking=True,pitch=0))]),
      'ignored_direction_silence': (10, [(0,dict(yaw=90)),(3,dict(yaw=120)),(6,dict(yaw=45)),(8,dict(inside=True,yaw=0,pitch=-10))]),
      'speech_then_confirm': (7, [(0,dict(inside=True,yaw=0,pitch=-15)),(.6,dict(looking=True,pitch=0)),
          (1.6,dict(pointing=True)),(4,dict(looking=False,pointing=False,pitch=12)),(5.5,dict(looking=True,pitch=0))])}


def observation(frames, now):
    s=dict(valid=True,inside=False,looking=False,pointing=False,controllerValid=True,
           paused=False,selected=False,ambiguous=False,yaw=90,pitch=0)
    for t, edits in frames:
        if t <= now+1e-8: s.update(edits)
    # Ambiguous target ownership never counts as looking at the target.
    if s['ambiguous']: s['looking']=False
    return s


def direction(candidate):
    words=(candidate or '').lower()
    return '_'.join(x for x in ('up','down','left','right') if x in words) or None


def canonical(s, machine):
    action=machine['state']['allowedAction']; d=direction(machine['candidate'])
    if action in CONFIRM: return CONFIRM[action]
    if action=='SelectionReminder': return 'trigger_reminder' if s['pointing'] else 'aim_reminder'
    if action=='None': return 'success' if s['selected'] else 'wait'
    kind='correct' if action in RETURN else 'encourage' if action=='Progress' else 'refine' if s['inside'] else 'orient'
    name=kind+'_'+str(d)
    return name if name in CATALOG else 'wait'


def menu_for(s, machine, arm):
    cue=canonical(s,machine)
    if arm in ('all_jev','jev_overrides'): return dict(CATALOG)
    menu={'wait':CATALOG['wait']}
    if cue!='wait': menu[cue]=CATALOG[cue]
    if arm=='eligible_actions' and cue!='wait' and CATALOG[cue]['direction']:
        d=CATALOG[cue]['direction']
        # Broader choices retain the same valid direction. A useful partial cue
        # is not wrong, but this experiment holds direction fixed for attribution.
        for kind in ('refine', 'correct', 'encourage'):
            if kind=='correct' and machine['state']['allowedAction'] not in RETURN: continue
            if kind=='encourage' and not machine['progressDue']: continue
            name=kind+'_'+d
            if name in CATALOG: menu[name]=CATALOG[name]
    return menu


def evidence_flags(s, machine, action):
    """Only clearly unsupported/counterproductive actions are errors, not style."""
    if action=='wait': return []
    flags=[]
    if s['selected'] and action!='success': return ['post_selection_guidance']
    if s['paused'] or not s['valid']: flags.append('invalid_evidence')
    d=CATALOG[action]['direction']
    if d:
        if s['pointing'] and s['controllerValid']: flags.append('direction_while_pointing_at_target')
        if s['looking']: flags.append('direction_while_looking_at_target')
        # Near the rear seam either turn route may be useful; zero axes unknown.
        if abs(s['yaw'])<150 and ((s['yaw']>3 and 'left' in d) or (s['yaw']< -3 and 'right' in d)):
            flags.append('opposing_horizontal')
        if (s['pitch']>3 and 'down' in d) or (s['pitch']< -3 and 'up' in d): flags.append('opposing_vertical')
    if action=='gaze_confirm' and not (s['looking'] and machine['lookingStable']): flags.append('unqualified_gaze_confirmation')
    if action=='controller_confirm' and not (s['looking'] and s['pointing'] and machine['pointingStable']): flags.append('unqualified_controller_confirmation')
    if action=='success' and not s['selected']: flags.append('unselected_success')
    return flags


def make_payload(s, machine, arm, playing, ended, now, catalog):
    state=dict(observation=s, guidance_state=machine['state'], candidate_direction=machine['candidate'],
               gaze_hold_qualified=machine['lookingStable'], controller_hold_qualified=machine['pointingStable'],
               target_acknowledged=machine['acknowledged'], measured_turn_progress=machine['progressDue'],
               history=machine['history'], seconds_since_speech_ended=round(now-ended,3),
               playback=None if playing is None else dict(text=playing['text'], elapsed=round(now-playing['time'],3),
                   remaining=round(playing['end']-now,3)),
               local_action=canonical(s,machine), evidence_basis='synthetic trajectory; production C# qualification; simulated audio starts')
    return dict(model='typesafe-ai/jev',state=state,questions={
        'action':dict(type='choice',instructions='''Guide a seated participant to a hidden target using the supplied state and recent speech history. observation.yaw is signed target error: negative left, positive right, near ±180 behind; observation.pitch positive means above, negative below. Start with horizontal turning outside the target wall, refine vertically/diagonally inside it. A useful partial direction is permitted. Never claim pointing from gaze alone, or success from either: observation.selected alone proves correct trigger selection. gaze_confirm needs looking=true and gaze_hold_qualified=true; controller_confirm needs pointing=true, controllerValid=true, looking=true and controller_hold_qualified=true. Repeated continuous acquisition is already consumed when local_action=wait. Protect invalid tracking and pauses with wait. A controller hit suppresses directions away while its hold qualifies. Encourage when measured_turn_progress is true; say go back only when the current action or history supports a departure/overshoot. Prefer useful short advice over silence longer than 3 seconds when guidance is allowed. Avoid recent repetition unless there is a new event or direction. Preserve ongoing speech and short endings. Use'''+' the the explicit current guidance_state, not guessed hold times. History contains only simulated playback starts/outcomes, not proposals. The candidate_direction and signs describe current geometry. A qualified local_action is an available cue, not a phrase you must match. Correct trigger selection alone proves completion. Do not repeatedly confirm continuous gaze. Do not invent overshoot from ordinary turning. If newGuidanceAllowed is false, wait unless selected=true allows success.', criteria=catalog),
        'delivery':dict(type='choice',instructions='Choose now only if idle, after_current to preserve a playing phrase, interrupt only if current speech is misleading and evidence justifies it. Protect short endings.',criteria={'now':'Start now if idle.','after_current':'Wait until current phrase ends and recheck.','interrupt':'Fade existing phrase before starting.'})})


def simulate(bridge, name, scenario, arm, advisor, request_sink=None, skip_known_repeats=False, wake_on_state_change=False):
    bridge.call('reset'); duration, frames=scenario
    events=[]; requests=[]; episodes=[]; active_episode=None
    playing=pending=queued=None; ended=-10.; next_request=0.; prior_action=None; silence=0.; max_silence=0.
    last_signature=None
    last_start=None; selected_spoken=False; last_pause=False; last_dir_end=-10.; last_dir=None
    def event(t,kind,**kw): events.append(dict(time=round(t,3),kind=kind,**kw))
    def stop(t, interrupted):
        nonlocal playing, ended, last_dir_end, last_dir
        if playing:
            bridge.call('end',now=t,interrupted=interrupted)
            event(t,'simulated_end',action=playing['choice'],interrupted=interrupted)
            if CATALOG[playing['choice']]['direction']: last_dir_end=t;last_dir=CATALOG[playing['choice']]['direction']
            playing=None;ended=t
    def start(t, choice, source, machine, s):
        nonlocal playing,last_start,active_episode,selected_spoken
        if choice=='wait': return
        flags=evidence_flags(s,machine,choice)
        repeat=bool(last_start and last_start['choice']==choice and t-last_start['end']<3)
        repeat_review=repeat and machine['state']['allowedAction'] not in ('Progress','ConfirmGaze','ConfirmPointing')
        # Assumption shared across policies: short recorded clip takes 1.0s;
        # reminders 1.8s. This is NOT inferred from TTS or measured WAVs.
        length=1.8 if choice.endswith('reminder') else 1.0
        playing=dict(time=t,end=t+length,text=CATALOG[choice]['text'],choice=choice)
        last_start=dict(playing)
        a=machine['state']['allowedAction']
        commit=(CONFIRM.get(a)==choice if a in CONFIRM else choice.endswith('reminder') if a=='SelectionReminder' else bool(CATALOG[choice]['direction']))
        bridge.call('start',now=t,text=playing['text'],action=a,commit=commit)
        event(t,'simulated_start',action=choice,text=playing['text'],source=source,flags=flags,
              phase=machine['state']['phase'],qualified_action=machine['state']['allowedAction'],repeat_review=repeat_review)
        if choice=='success': selected_spoken=True
        if active_episode and CONFIRM.get(active_episode['action'])==choice and not flags:
            active_episode['start']=round(t,3); active_episode['latency']=round(t-active_episode['qualified'],3)
    for tick in range(int(duration/DT)+1):
        t=round(tick*DT,6);s=observation(frames,t)
        machine=bridge.call('step',now=t,dt=DT,**s)
        state=machine['state']; action=state['allowedAction']; cue=canonical(s,machine)
        signature=(state['revision'],action,machine['candidate'])
        if wake_on_state_change and signature!=last_signature:
            if pending or queued:event(t,'state_change_cancels_pending')
            pending=queued=None;next_request=t
        last_signature=signature
        if playing and t+1e-8>=playing['end']: stop(t,playing.get('fading',False))
        if s['paused'] and not last_pause:
            stop(t,True);pending=queued=None;event(t,'pause_cancel')
        last_pause=s['paused']
        if action in CONFIRM and (active_episode is None or active_episode['action']!=action):
            if active_episode: episodes.append(active_episode)
            active_episode=dict(action=action,qualified=t,start=None,latency=None,waiting={})
        elif action not in CONFIRM and active_episode:
            episodes.append(active_episode);active_episode=None
        protected=arm!='all_jev'
        # Priority overrides happen before processing an old response, as in the
        # production state machine. Controller hit suppresses directional speech.
        if protected and playing and CATALOG[playing['choice']]['direction'] and s['controllerValid'] and s['pointing']:
            
            if not playing.get('fading'): event(t,'simulated_fade_requested')
            playing['fading']=True;playing['end']=min(playing['end'],t+.15)
        priority=protected and action not in ('None','Direction')
        gap=.1 if action in CONFIRM else .25
        if protected and s['selected'] and not selected_spoken: priority=True;cue='success'
        if priority and not playing and t-ended>=gap-1e-8 and cue!='wait':
            pending=queued=None; start(t,cue,'local_priority',machine,s)
        else:
            if pending and t+1e-8>=pending['due']:
                r=pending;pending=None
                if r['status']!='ok' or r['seconds']>DEADLINE:
                    event(t,'request_deadline_or_failure',request=r['id']);next_request=max(next_request,t+.6)
                elif r['action']!='wait': queued=r
                else:event(t,'model_wait',request=r['id'])
            if queued:
                r=queued;choice=r['action']; invalid=evidence_flags(s,machine,choice)
                same=bridge.call('check',now=t,revision=r['revision'],action=r['machine_action'])['allowed']
                # Safe alternatives may use the same still-valid action ticket;
                # geometry, qualification, timing and current controller evidence win.
                if protected and (not same or invalid):
                    event(t,'blocked_stale_or_invalid',choice=choice,flags=invalid,request=r['id']);queued=None
                elif playing and r['delivery']=='interrupt' and not protected:
                    stop(t,True);start(t,choice,'jev_interrupt',machine,s);queued=None
                elif not playing and (not protected or t-ended>=gap-1e-8):
                    d=CATALOG[choice]['direction']
                    if protected and d and d==last_dir and t-last_dir_end<3:
                        event(t,'blocked_repetition',request=r['id']);queued=None
                    else:start(t,choice,'jev',machine,s);queued=None
        # Both protected and unprotected policies use bounded requests, but all-Jev
        # must decide silence/confirmation itself; overrides bypass that network.
        requestable=not pending and not queued and not playing and t>=next_request and (not s['selected'] or not selected_spoken)
        if protected: requestable=requestable and state['newGuidanceAllowed'] and action=='Direction' and not s['ambiguous']
        if skip_known_repeats and protected and action=='Direction' and direction(machine['candidate'])==last_dir and t-last_dir_end<3:
            requestable=False
        if requestable:
            menu=menu_for(s,machine,arm);body=make_payload(s,machine,arm,playing,ended,t,menu)
            r=advisor(body);r=dict(r,id=f'{name}:{arm}:{len(requests)}',time=t,revision=state['revision'],machine_action=action)
            r['due']=round(t+min(r['seconds'],DEADLINE),6)
            requests.append(dict(r,payload=body))
            if request_sink: request_sink(dict(r,payload=body,sequence=name,arm=arm))
            pending=r;next_request=t+.6
            event(t,'request',id=r['id'])
        searching=not playing and action!='None' and not s['paused'] and not s['selected']
        silence=silence+DT if searching else 0.;max_silence=max(max_silence,silence)
        if active_episode and active_episode['start'] is None:
            why='existing_speech' if playing else 'jev_pending' if pending else 'scheduler_or_policy'
            active_episode['waiting'][why]=round(active_episode['waiting'].get(why,0)+DT,3)
        if action!=prior_action:event(t,'qualified_action',action=action,phase=state['phase']);prior_action=action
    if active_episode:episodes.append(active_episode)
    return dict(sequence=name,arm=arm,seconds=duration,events=events,episodes=episodes,requests=requests,
                max_actionable_silence=round(max_silence,3),basis='production qualification; simulated speech')


def summarize(runs):
    result={}
    for arm in ARMS:
        rr=[r for r in runs if r['arm']==arm]; req=[q for r in rr for q in r['requests']]
        ep=[e for r in rr for e in r['episodes']]; starts=[e for r in rr for e in r['events'] if e['kind']=='simulated_start']
        counts={}
        for e in starts:
            for flag in e['flags']:counts[flag]=counts.get(flag,0)+1
        result[arm]=dict(sequences=len(rr),http_requests=len(req),http_latency=stats([q['seconds'] for q in req]),
            failures=sum(q['status']!='ok' for q in req),beyond_450ms=sum(q['seconds']>DEADLINE for q in req),
            simulated_starts=len(starts),repeat_review_candidates=sum(e.get('repeat_review',False) for e in starts),flags=counts,qualified_episodes=len(ep),missed_episodes=sum(e['start'] is None for e in ep),
            simulated_confirmation_latency=stats([e['latency'] for e in ep if e['latency'] is not None]),
            delayed_episodes_over_500ms=sum(e['latency'] is not None and e['latency']>.5 for e in ep),
            max_actionable_silence=max((r['max_actionable_silence'] for r in rr),default=0))
    return result


def run(output, live=False, keys=None, max_requests=320, replay=None):
    output.mkdir(parents=True,exist_ok=False); bridge=Bridge();runs=[];count=0
    key=json.loads(keys.read_text())['jev_key'] if live else None
    prior=iter(json.loads(line) for line in replay.read_text().splitlines()) if replay else None
    def advisor(body):
        nonlocal count
        if count>=max_requests:raise RuntimeError('Request cap reached; partial JSONL preserved')
        count+=1
        if prior is not None:
            saved=next(prior)
            if saved['payload']!=body:raise ValueError('Replay payload differs; cannot reuse response')
            return {k:saved[k] for k in ('status','seconds','action','delivery','request_bytes') if k in saved}
        if live:return ask(key,body)
        choice=body['state']['local_action']
        if choice not in body['questions']['action']['criteria']:choice='wait'
        return dict(status='ok',seconds=.2,action=choice,delivery='now',request_bytes=len(json.dumps(body)))
    def sink(row):
        with (output/'requests.jsonl').open('a') as f:f.write(json.dumps(row)+'\n')
    try:
        for i,(name,scenario) in enumerate(scenarios().items()):
            arms=ARMS[i%4:]+ARMS[:i%4]
            for arm in arms:
                result=simulate(bridge,name,scenario,arm,advisor,sink);runs.append(result)
                with (output/'runs.jsonl').open('a') as f:f.write(json.dumps(result)+'\n')
                print(name,arm,'requests',len(result['requests']),flush=True)
    finally:bridge.close()
    report=dict(schema='stateful-jev-pilot-v2',live_http=live or replay is not None,response_replay=bool(replay),runs=runs,summary=summarize(runs),catalog=CATALOG,
       limits=dict(max_requests=max_requests,http_timeout_seconds=3,runtime_deadline_seconds=DEADLINE,tick_seconds=DT),
       limitations=['Actual production C# qualification and speech-memory classes; experimental Python menus/audio scheduler, not full Unity playback.',
       'Scripted movement is fixed across arms; speech history is policy-specific, but no participant behavior is simulated.',
       'Playback starts/durations/fades and confirmation delays are simulated. Only HTTP timing is measured on Mac.',
       'One run per sequence/policy; no statistical or human-perceived quality claim. No participant build changes.',
       'Small vs broader vs unfiltered full action menu; this is not 500 authored phrase comparison.'])
    (output/'report.json').write_text(json.dumps(report,indent=2)+'\n');return report


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--live',action='store_true');p.add_argument('--keys',type=Path);p.add_argument('--replay',type=Path);a=p.parse_args()
    if a.live and not a.keys:p.error('--live requires --keys')
    result=run(a.output,a.live,a.keys,replay=a.replay);print(json.dumps(result['summary'],indent=2))
