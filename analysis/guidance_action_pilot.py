"""Offline action-choice experiment. Never imported by the participant runtime.

Paired frozen-history probes, not closed-loop participant simulations. The action
eligibility adapter is an experimental Python policy, not the Unity state machine.
"""
import argparse
import copy
import hashlib
import json
import math
import time
from pathlib import Path
import urllib.request
import urllib.error

ENDPOINT = 'https://ai-gateway.vercel.sh/typesafe/v1/systemone'
DEADLINE = .450
TRANSMIT = {
    'type': 'choice',
    'instructions': 'Choose speak or wait for an idle navigation assistant. The participant does not know the target. Use seconds_since_speech_ended as elapsed silence, not time since a request. pending_phrase is this proposed cue, not playing audio. A fresh correction after several seconds of silence is useful, even if older advice gave the opposite direction. A 0.1-second cadence is an opportunity, not an obligation. This decision cannot interrupt audio.',
    'criteria': {
        'speak': 'Valid gaze, idle audio, and an actionable proposed cue. Speak for a new direction or qualified event; also speak when silence exceeds 3 seconds and the user still needs guidance.',
        'wait': 'Audio actually playing; invalid gaze; or redundant advice spoken within the past 3 seconds with no new direction/event. Also wait for target confirmations without local qualification or advice that contradicts current evidence.'}}


def blocked(s):
    e, p = s['runtime']['evidence'], s['runtime']['playback']
    return (not e.get('valid') or not e.get('geometryAvailable') or
            e.get('phase') in ('Paused', 'Suspended', 'Opening', 'Completed', 'Stopped', 'Inactive') or
            e.get('gazeWithinTargetVicinity') or e.get('targetGazeQualified') or
            (e.get('controllerEvidenceValid') and e.get('controllerOnTarget')) or
            p.get('is_speaking') or p.get('is_busy'))


def options(s):
    """Conservative experimental action menu; never invent a progress/return fact."""
    result = {'wait': {'text': None, 'reason': 'Remain silent; preserve ongoing audio and avoid redundant advice.'}}
    if blocked(s):
        return result
    e = s['runtime']['evidence']
    text = s.get('proposed_phrase')
    if not text or not s.get('locally_qualified', False):
        return result
    returning = e.get('targetReturnPending') or s.get('proposed_semantics', {}).get('intent') == 'return_to_target'
    kind = 'correct' if returning else 'refine' if e.get('insideTargetWall') else 'orient'
    result[kind] = {'text': text, 'reason': 'Use the locally qualified direction and magnitude.'}
    # Return and refinement may both be defensible after a verified departure.
    # Only rewrite the exact authored return prefix; do not reinterpret arbitrary phrases.
    if returning and e.get('insideTargetWall') and text.startswith('Go back '):
        result['refine'] = {'text': 'Look ' + text[len('Go back '):],
                            'reason': 'Same valid correction direction without emphasizing prior departure.'}
    history = s.get('pilot_history', {})
    if (history.get('measured_progress') is True and history.get('same_direction') is True
            and not e.get('continuationUsed') and not returning):
        direction = history.get('explicit_direction')
        if direction in ('left', 'right', 'up', 'down'):
            result['encourage'] = {'text': 'Keep going ' + direction + '.',
                                    'reason': 'Measured progress on the last explicit direction; no success claim.'}
    return result


def payload(s, arm, menu, policy="v1"):
    state = copy.deepcopy(s)
    state['eligible_actions'] = menu
    if arm == 'baseline':
        question = copy.deepcopy(TRANSMIT)
        return {'model': 'typesafe-ai/jev', 'state': state, 'questions': {'transmit': question}}
    policy_note = '' if policy == 'v1' else (' Prefer encourage over another orient/refine command when measured_progress and same_direction are true, continuationUsed is false, and silence exceeds3 seconds. A verified target departure is a NEW corrective event, even after a recent gaze/pointing acknowledgment: prefer correct if controllerOnTarget is now false and speech is idle. Previous acknowledgment is history, not current target acquisition. Still wait if the last spoken phrase already gives that same corrective direction and there is no newer event. Never infer progress from mere elapsed time.')
    return {'model': 'typesafe-ai/jev', 'state': state, 'questions': {'guidance_action': {
        'type': 'choice',
        'instructions': 'Select one eligible action using current evidence and recent history. The hidden target is unknown to the participant. Orient for a needed coarse turn, refine within its wall, correct after verified departure or overshoot, encourage measured progress, or wait. Do not infer recognition, pointing qualification, or trial completion. Avoid repeating directions during recent silence under 3 seconds unless the direction/event changed. Prefer a useful cue after more than 3 seconds of silence. Never interrupt ongoing speech. Eligible actions already encode valid directions. Choose the conversational function, not geometry.' + policy_note,
        'criteria': menu}}}


def parse(answer, arm, menu, state):
    name = 'transmit' if arm == 'baseline' else 'guidance_action'
    row = answer.get('answers', {}).get(name, {})
    choice = row.get('choice')
    allowed = ('speak', 'wait') if arm == 'baseline' else menu
    if row.get('type') != 'choice' or choice not in allowed:
        raise ValueError('invalid typed choice')
    if arm == 'baseline':
        action = 'wait' if choice == 'wait' else next((k for k in menu if k != 'wait'), 'blocked_speech')
        text = None if choice == 'wait' else state.get('proposed_phrase')
    else:
        action, text = choice, menu[choice]['text']
    return action, text


def scripted_cases():
    base = {'runtime': {'evidence': {'valid': True, 'geometryAvailable': True, 'phase': 'Turning',
        'insideTargetWall': False, 'controllerEvidenceValid': True, 'controllerOnTarget': False,
        'gazeWithinTargetVicinity': False, 'targetGazeQualified': False, 'targetReturnPending': False,
        'continuationUsed': False, 'yawErrorDegrees': 150, 'pitchErrorDegrees': 0},
        'playback': {'is_speaking': False, 'is_busy': False, 'last_spoken_phrase': 'The object is to the right.'}},
        'proposed_phrase': 'Turn way right.', 'locally_qualified': True, 'seconds_since_speech_ended': 4,
        'pilot_history': {'measured_progress': False, 'same_direction': False}}
    cases = []
    def add(name, expect, edits=None, evidence=None, playback=None, history=None):
        s = copy.deepcopy(base)
        s.update(edits or {}); s['runtime']['evidence'].update(evidence or {})
        s['runtime']['playback'].update(playback or {}); s['pilot_history'].update(history or {})
        cases.append({'id': name, 'basis': 'scripted', 'state': s, 'expected_actions': expect})
    add('turn_start', ['orient'])
    add('turn_90_progress', ['encourage', 'orient'], evidence={'yawErrorDegrees': 90},
        history={'measured_progress': True, 'same_direction': True, 'explicit_direction': 'right'})
    add('turn_45_progress', ['encourage', 'orient'], edits={'proposed_phrase': 'Look right.'},
        evidence={'yawErrorDegrees': 45}, history={'measured_progress': True, 'same_direction': True, 'explicit_direction': 'right'})
    add('recent_repeat', ['wait'], edits={'seconds_since_speech_ended': .3},
        playback={'last_spoken_phrase': 'Turn way right.'}, history={'same_direction': True})
    add('ignored_direction_long_silence', ['orient'], edits={'seconds_since_speech_ended': 4.5},
        playback={'last_spoken_phrase': 'Turn way right.'}, history={'same_direction': True})
    add('wall_entry_down', ['refine'], edits={'proposed_phrase': 'Look lower.'},
        evidence={'phase': 'Refining', 'insideTargetWall': True, 'yawErrorDegrees': 0, 'pitchErrorDegrees': -20})
    add('vertical_progress', ['refine', 'encourage'], edits={'proposed_phrase': 'A little lower.'},
        evidence={'phase': 'Refining', 'insideTargetWall': True, 'yawErrorDegrees': 0, 'pitchErrorDegrees': -5},
        history={'measured_progress': True, 'same_direction': True, 'explicit_direction': 'down'})
    add('overshoot_return', ['correct', 'refine'], edits={'proposed_phrase': 'Go back up and left.'},
        evidence={'phase': 'Refining', 'insideTargetWall': True, 'targetReturnPending': True, 'yawErrorDegrees': -8, 'pitchErrorDegrees': 6})
    add('gaze_found_controller_elsewhere', ['wait'], evidence={'gazeWithinTargetVicinity': True, 'targetGazeQualified': True})
    add('controller_unqualified_hold', ['wait'], evidence={'controllerOnTarget': True})
    add('blink', ['wait'], evidence={'valid': False})
    add('pause', ['wait'], evidence={'phase': 'Paused'})
    add('speech_playing', ['wait'], playback={'is_speaking': True, 'is_busy': True})
    add('recovery', ['orient'], evidence={'phase': 'Turning'})
    return cases


def holdout_cases():
    # Mirrored/opposite-axis cases created before the revised network run.
    cases = scripted_cases()
    selected = []
    for case in cases:
        if case['id'] not in ('turn_90_progress','vertical_progress','overshoot_return','recent_repeat','ignored_direction_long_silence'):
            continue
        case = copy.deepcopy(case); case['id'] = 'holdout_' + case['id']
        s = case['state']; e = s['runtime']['evidence']
        e['yawErrorDegrees'] *= -1; e['pitchErrorDegrees'] *= -1
        def mirror(text):
            for a,b in [('right','left'),('lower','higher'),('down','up')]:
                text = text.replace(a,'__swap__').replace(b,a).replace('__swap__',b)
            return text
        s['proposed_phrase'] = mirror(s['proposed_phrase'])
        s['runtime']['playback']['last_spoken_phrase'] = mirror(s['runtime']['playback']['last_spoken_phrase'])
        if 'explicit_direction' in s['pilot_history']:
            s['pilot_history']['explicit_direction'] = mirror(s['pilot_history']['explicit_direction'])
        selected.append(case)
    return selected


def recorded_cases(path):
    rows = [json.loads(line) for line in path.read_text().splitlines()]
    # Consecutive retry episodes plus return/refinement cases: keep their observed history.
    wanted = {397, 1052, 1190, 2554, 3235, 3266, 3472, 3503, 3529, 3556, 3581, 3606, 3632, 3778, 3802, 3828}
    result = []
    history = []
    previous_trial = None
    for row in rows:
        trial = row.get("group", {}).get("trial_group")
        if trial != previous_trial:
            history = []
            previous_trial = trial
        s = row['input']['payload']['state']
        if isinstance(s, str): s = json.loads(s)
        e = s.get('runtime', {}).get('evidence', {})
        seq = row['source']['request_sequence']
        if seq in wanted and list(row['input']['payload']['questions']) == ['transmit']:
            # Explicit allowlist: no identifiers, personal fields, paths or credentials leave host.
            evidence_keys = ['valid', 'geometryAvailable', 'phase', 'insideTargetWall', 'controllerEvidenceValid',
                'controllerOnTarget', 'gazeWithinTargetVicinity', 'targetGazeQualified', 'targetReturnPending',
                'continuationUsed', 'yawErrorDegrees', 'pitchErrorDegrees', 'gazeAgeSeconds', 'vicinityAmbiguous']
            p = s['runtime']['playback']
            state = {'runtime': {'evidence': {k: e.get(k) for k in evidence_keys},
                'playback': {k: p.get(k) for k in ['is_speaking', 'is_busy', 'last_spoken_phrase', 'current_phrase', 'speech_remaining_seconds']}},
                'proposed_phrase': s.get('proposed_phrase'), 'proposed_semantics': s.get('proposed_semantics'),
                'locally_qualified': s.get('locally_qualified'), 'seconds_since_speech_ended': s.get('seconds_since_speech_ended'),
                'recent_observations': history[-4:], 'pilot_history': {'measured_progress': None, 'same_direction': None}}
            result.append({'id': f'recorded_{seq}', 'basis': 'recorded_frozen_history', 'state': state,
                'expected_actions': None, 'source_time': row['source']['request_time_seconds']})
        if e:
            history.append({'time': row['source']['request_time_seconds'], 'phase': e.get('phase'),
                'yaw': e.get('yawErrorDegrees'), 'pitch': e.get('pitchErrorDegrees'),
                'last_spoken': s.get('runtime', {}).get('playback', {}).get('last_spoken_phrase')})
    return result


def stats(values):
    values = sorted(values)
    if not values: return {'n': 0}
    n = len(values)
    return {'n': n, 'median': (values[(n-1)//2]+values[n//2])/2, 'p95': values[math.ceil(.95*n)-1], 'max': values[-1]}


def summarize(rows):
    result = {}
    for arm in ('baseline', 'autonomous'):
        rr = [r for r in rows if r['arm'] == arm]
        network = [r for r in rr if not r.get('local_only')]
        scored = [r for r in rr if r.get('expected_actions') is not None and r['status'] == 'ok']
        result[arm] = {'network_requests': len(network), 'local_gate_cases': len(rr)-len(network),
            'latency_seconds': stats([r['seconds'] for r in network if r['status'] == 'ok']),
            'errors': sum(r['status'] != 'ok' for r in network),
            'late_over_450ms': sum(r['status'] == 'ok' and r['seconds'] > DEADLINE for r in network),
            'scripted_rubric_matches': sum(r['action'] in r['expected_actions'] for r in scored),
            'scripted_scored': len(scored),
            'model_scripted_matches': sum(r['action'] in r['expected_actions'] for r in scored if not r['local_only']),
            'model_scripted_scored': sum(not r['local_only'] for r in scored),
            'advisory_actions': {a: sum(r.get('action') == a for r in rr) for a in ['orient','encourage','correct','refine','wait']}}
    return result


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--keys', type=Path, required=True)
    ap.add_argument('--benchmark', type=Path, required=True); ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--repeats', type=int, default=2); ap.add_argument('--policy', choices=['v1','v2'], default='v1')
    ap.add_argument('--holdout', action='store_true'); args = ap.parse_args()
    key = json.loads(args.keys.read_text())['jev_key']
    cases = scripted_cases() + recorded_cases(args.benchmark) + (holdout_cases() if args.holdout else [])
    rows = []; args.output.mkdir(parents=True, exist_ok=False)
    for rep in range(args.repeats):
        for index, case in enumerate(cases):
            s = case['state']; menu = options(s)
            for arm in (('baseline','autonomous') if (index+rep)%2 == 0 else ('autonomous','baseline')):
                row = {'case': case['id'], 'basis': case['basis'], 'repeat': rep, 'arm': arm,
                    'expected_actions': case['expected_actions'], 'eligible_actions': list(menu), 'seconds': 0,
                    'status': 'ok', 'local_only': len(menu) == 1}
                if len(menu) == 1:
                    row.update(action='wait', text=None)
                else:
                    body = json.dumps(payload(s, arm, menu, args.policy)).encode()
                    row['request_bytes'] = len(body); start = time.perf_counter()
                    try:
                        req = urllib.request.Request(ENDPOINT, body, {'Authorization':'Bearer '+key,'Content-Type':'application/json'})
                        with urllib.request.urlopen(req, timeout=2) as response:
                            answer = json.loads(response.read(65536))
                        row['action'], row['text'] = parse(answer, arm, menu, s)
                    except urllib.error.HTTPError as err: row.update(status='http_error', http_status=err.code)
                    except (OSError, ValueError, KeyError, TypeError): row['status'] = 'transport_or_schema_error'
                    row['seconds'] = round(time.perf_counter()-start, 6)
                # Software decision only: no audio is generated, queued or played in this pilot.
                row['usable_within_deadline'] = row['status']=='ok' and row['seconds'] <= DEADLINE
                rows.append(row)
                with (args.output/'requests.jsonl').open('a') as f: f.write(json.dumps(row)+'\n')
                print(case['id'], arm, row['status'], row.get('action'), row['seconds'], flush=True)
    report = {'schema': 'jev-action-pilot-v1', 'basis': 'Paired frozen-history Mac-to-Vercel decision probes, not headset playback or participant outcomes.',
        'policy': args.policy, 'source_sha256': hashlib.sha256(args.benchmark.read_bytes()).hexdigest(), 'cases': cases, 'requests': rows,
        'summary': summarize(rows), 'limitations': [
            'Python action eligibility is experimental; production Unity still owns a single allowed action.',
            'Recorded cases have no human action-preference ground truth. Scripted expected sets are an engineering rubric.',
            'Frozen history is shared across arms, not updated with counterfactual answers; no closed-loop behavior claim.',
            '2s diagnostic transport timeout; answers after450ms would be late for the runtime.',
            'Confirmation, completion and interruption are local/excluded; no acoustic, playback delay or participant improvement measurement.',
            'No generated pilot phrases were added to participant audio libraries.']}
    (args.output/'report.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report['summary'],indent=2))

if __name__ == '__main__': main()
