"""Adaptive v2 contract and synthetic supervision; never uses device replays to train."""
from collections import Counter
import copy
import hashlib
import json
import math
from pathlib import Path
import random
import re

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/'Assets/SpatialVoice/JevDecisionClient.Adaptive.cs'
LABELS = ['observe_1s', 'observe_2s', 'observe_4s', 'orient_brief', 'orient_explicit',
          'refine_brief', 'refine_explicit', 'correct', 'encourage']
FEATURE_VERSION = 'adaptive-v2-measured-v1'
POLICY = 'jev_adaptive_assistance_v2'


def contract():
    """Read the actual C# literals; fail rather than silently invent a parallel prompt."""
    source = SOURCE.read_text()
    strategies = dict(re.findall(r'\["([a-z0-9_]+)"\] = "([^"\n]+)"', source.split('public static JObject AdaptivePayload')[0]))
    instructions = re.search(r'\["instructions"\] = "([^"\n]+)"', source)
    if set(strategies) != set(LABELS) or not instructions or f'AdaptivePolicy = "{POLICY}"' not in source:
        raise ValueError('Production adaptive contract changed; review local adapter')
    return strategies, instructions[1]


def payload(state, capabilities):
    strategies, instructions = contract()
    if not set(capabilities) <= set(LABELS[3:]): raise ValueError('Unknown capability')
    state = copy.deepcopy(state)
    state['policy'] = POLICY
    return dict(model='typesafe-ai/jev', state=state, questions=dict(assistance=dict(type='choice',
                criteria={k:strategies[k] for k in LABELS if k in LABELS[:3] or k in capabilities},
                instructions=instructions)))


def number(value, optional=False):
    if optional and value is None: return None
    if type(value) not in (int, float) or not math.isfinite(value): raise ValueError('Invalid numeric evidence')
    return float(value)


def unpack(request):
    if not isinstance(request, dict) or set(request.get('questions') or {}) != {'assistance'}:
        raise ValueError('Expected assistance contract')
    question = request['questions']['assistance']
    if not isinstance(question, dict): raise ValueError('Invalid question')
    choices = question.get('criteria')
    if question.get('type') != 'choice' or not isinstance(choices, dict) or not set(LABELS[:3]) <= set(choices) or not set(choices) <= set(LABELS):
        raise ValueError('Unsupported assistance choices')
    if any(not isinstance(v, str) or len(v)>1024 for v in choices.values()): raise ValueError('Invalid criteria')
    state = request.get('state')
    if not isinstance(state, dict) or state.get('schema') != 'adaptive-assistance-v1' or state.get('policy') != POLICY:
        raise ValueError('Expected adaptive v2 state')
    runtime, behavior = state.get('runtime'), state.get('behavior')
    if not isinstance(runtime, dict) or not isinstance(behavior, dict): raise ValueError('Missing state')
    evidence, playback = runtime.get('evidence'), runtime.get('playback')
    if not isinstance(evidence, dict) or not isinstance(playback, dict) or not isinstance(evidence.get('guidanceState'), dict):
        raise ValueError('Missing current evidence')
    recent = behavior.get('recent')
    if not isinstance(recent, dict) or not isinstance(recent.get('samples'), list) or len(recent['samples'])>25:
        raise ValueError('Invalid history window')
    previous_age = float('inf')
    for s in recent['samples']:
        age, error = number(s.get('age_seconds')), number(s.get('angular_error_degrees'))
        # Runtime records sqrt(yaw_error^2 + pitch_error^2), not a great-circle
        # angle; legitimate values can exceed 180 degrees.
        if not 0 <= age <= 12 or age >= previous_age or not 0 <= error <= 360:
            raise ValueError('History must be chronological, bounded and unique')
        previous_age = age
    for name, maximum in [('interventions',8), ('previous_trials',6)]:
        if not isinstance(behavior.get(name, []), list) or len(behavior.get(name, []))>maximum:
            raise ValueError('Invalid behavioral history')
    return state, runtime, evidence, playback, choices


def blocked(request):
    _, runtime, e, p, _ = unpack(request)
    return (e.get('valid') is not True or e.get('geometryAvailable') is not True or
            e['guidanceState'].get('newGuidanceAllowed') is not True or
            p.get('is_busy') is not False or p.get('is_speaking') is not False or
            e.get('controllerOnTarget') is not False or e.get('targetGazeQualified') is not False or
            e.get('gazeWithinTargetVicinity', False) is not False or runtime.get('locally_qualified', True) is not True)


def measurements(request):
    state, runtime, e, p, choices = unpack(request)
    behavior = state['behavior']
    samples = [s for s in behavior['recent']['samples'] if s['age_seconds'] <= 4]
    span = samples[0]['age_seconds']-samples[-1]['age_seconds'] if samples else 0
    gap = max((a['age_seconds']-b['age_seconds'] for a,b in zip(samples,samples[1:])), default=0)
    reliable = bool(samples and samples[-1]['age_seconds']<=.75 and span>=2 and gap<=1.25)
    delta = samples[0]['angular_error_degrees']-samples[-1]['angular_error_degrees'] if samples else 0
    steps = [a['angular_error_degrees']-b['angular_error_degrees'] for a,b in zip(samples,samples[1:])]
    rising = sum(d>0 for d in steps)/len(steps) if steps else 0
    # Same 3-degree/4-second trend bands as AdaptiveGuidanceHistory.Trend; not a class label.
    trend = 'unknown' if not reliable else 'approaching' if delta>3 else 'receding' if delta < -3 else 'little_change'
    sustained = reliable and delta>3 and rising>=.75
    cues = behavior.get('interventions', [])
    last = cues[-1] if cues else {}
    response = number(last.get('error_reduction_after_3s'), optional=True)
    return dict(span=span, gap=gap, delta=delta, fraction=rising, trend=trend, sustained=sustained,
                last_strategy=last.get('strategy','none'), response=response,
                response_status=last.get('response_status','none'), cues=len(cues))


def text(request):
    """Measured summaries, no oracle decisions, IDs, Jev answers or future observations."""
    state, runtime, e, p, choices = unpack(request)
    b = state['behavior']; m = measurements(request)
    def boolean(v): return 'yes' if v is True else 'no' if v is False else 'unknown'
    pairs = [('valid', e.get('valid')), ('geometry',e.get('geometryAvailable')),
             ('allowed',e['guidanceState'].get('newGuidanceAllowed')), ('busy',p.get('is_busy')),
             ('speaking',p.get('is_speaking')), ('gaze target',e.get('targetGazeQualified')),
             ('vicinity',e.get('gazeWithinTargetVicinity',False)), ('controller target',e.get('controllerOnTarget')),
             ('local',runtime.get('locally_qualified',True)), ('inside wall',e.get('insideTargetWall')),
             ('acknowledged historically',e.get('targetAcknowledged')), ('return pending',e.get('targetReturnPending'))]
    parts = [f'{k}: {boolean(v)}' for k,v in pairs]
    error = number(e.get('vicinityErrorDegrees'), optional=True)
    if error is None:
        error = math.hypot(number(e.get('yawErrorDegrees',0)),number(e.get('pitchErrorDegrees',0)))
    old_gain = number(b['recent'].get('error_reduction_degrees'),optional=True)
    response = m['response']
    # Bands expose measurements to a text encoder that is weak at numeric arithmetic.
    response_band = 'unknown' if response is None else 'improved' if response>3 else 'not_improved'
    parts += [f'event: {e["guidanceState"].get("allowedAction","unknown")}',
              f'error degrees: {error:.1f}; distance: {"far" if error>60 else "near"}',
              f'recent trend: {m["trend"]}; sustained: {boolean(m["sustained"])}',
              f'recent reduction: {m["delta"]:.1f}; span: {m["span"]:.1f}; gap: {m["gap"]:.1f}; improving fraction: {m["fraction"]:.2f}',
              f'older reduction: {old_gain}', f'last cue: {m["last_strategy"]}; response: {response_band}; status: {m["response_status"]}',
              f'cue count: {m["cues"]}; wrong selections: {b.get("wrong_selections",0)}',
              'previous outcomes: '+','.join(str(t.get('outcome','unknown')) for t in b.get('previous_trials',[])),
              'offered: '+','.join(sorted(choices))]
    return '. '.join(parts)


def reference(request):
    """Authored supervision approximating v2, NOT human ground truth or model inference."""
    _, _, e, _, choices = unpack(request)
    m = measurements(request)
    if blocked(request): return 'observe_1s'
    if 'correct' in choices and (e.get('targetReturnPending') is True or e['guidanceState'].get('allowedAction') in ('ReturnTarget','ReturnWall','ReturnBearing')):
        return 'correct'
    if m['trend']=='unknown': return 'observe_1s'
    if m['sustained']: return 'observe_4s'
    if m['trend']=='approaching': return 'encourage' if 'encourage' in choices and m['last_strategy']!='encourage' else 'observe_2s'
    family = 'refine' if e.get('insideTargetWall') else 'orient'
    error = number(e.get('vicinityErrorDegrees'),optional=True)
    if error is None: error = math.hypot(number(e.get('yawErrorDegrees',0)),number(e.get('pitchErrorDegrees',0)))
    explicit = m['cues']>0 or error>60
    preferred = [family+'_explicit',family+'_brief'] if explicit else [family+'_brief',family+'_explicit']
    return next((s for s in preferred if s in choices),'observe_1s')


def example(rng, index, split):
    mode = rng.choice(['stall','recede','oscillate','steady','mixed','insufficient'])
    departure = rng.random()<.28
    inside = rng.random()<.5
    error = rng.uniform(2,160 if not inside else 45)
    period = rng.choice([.50,.55]) if split!='test' else rng.choice([.51,.6])
    count = int(12/period)+1
    samples=[]
    for i in range(count):
        age = (count-1-i)*period
        # The older window can improve even while the most recent trajectory retreats.
        if age>4: value=error+rng.uniform(-20,20)+(age-4)*rng.uniform(0,6)
        elif mode=='steady': value=error+age*4
        elif mode=='recede': value=error-age*2
        elif mode=='mixed': value=error+age*2+ (4 if i%2 else -4)
        elif mode=='oscillate': value=error+(5 if i%2 else -5)
        else: value=error+rng.uniform(-.5,.5)
        samples.append(dict(age_seconds=round(age,3),angular_error_degrees=round(min(179,max(1,value)),2),direction='left' if inside else 'right'))
    if mode=='insufficient': samples=samples[-rng.choice([1,2,3]):]
    family='refine' if inside else 'orient'
    caps=[family+'_brief',family+'_explicit']
    # Do not leak the departure label through the existence of a capability alone.
    if departure or rng.random()<.15: caps.append('correct')
    if mode in ('steady','mixed') and rng.random()<.65: caps.append('encourage')
    e=dict(valid=True,geometryAvailable=True,insideTargetWall=inside,targetGazeQualified=False,
           gazeWithinTargetVicinity=False,controllerOnTarget=False,targetAcknowledged=rng.random()<.5,
           targetReturnPending=departure,vicinityErrorDegrees=round(error,2),
           yawErrorDegrees=round(error if not inside else 0,2),pitchErrorDegrees=round(error if inside else 0,2),
           guidanceState=dict(newGuidanceAllowed=True,allowedAction=rng.choice(['ReturnTarget','ReturnWall','ReturnBearing']) if departure else 'Direction'))
    p=dict(is_busy=False,is_speaking=False)
    runtime=dict(evidence=e,playback=p,locally_qualified=True,seconds_since_speech_ended=rng.choice([None,rng.uniform(0,14)]))
    cues=[]
    if rng.random()<.5:
        cues=[dict(strategy=rng.choice([family+'_brief',family+'_explicit','encourage']),
                   error_reduction_after_3s=rng.choice([None,round(rng.uniform(-15,20),2)]),
                   response_status='observed_not_causal')]
    b=dict(schema='adaptive-behavior-v1',recent=dict(samples=samples,error_reduction_degrees=round(samples[0]['angular_error_degrees']-samples[-1]['angular_error_degrees'],2)),
           interventions=cues,previous_trials=[dict(outcome=rng.choice(['TimedOut','Completed'])) for _ in range(rng.randrange(4))],wrong_selections=rng.randrange(3))
    if rng.random()<.18:
        gates=[(e,'valid',False),(e,'geometryAvailable',False),(e,'controllerOnTarget',True),
               (e,'targetGazeQualified',True),(e,'gazeWithinTargetVicinity',True),(p,'is_busy',True),
               (p,'is_speaking',True),(e['guidanceState'],'newGuidanceAllowed',False),(runtime,'locally_qualified',False)]
        for obj,key,value in rng.sample(gates, 3 if split=='test' else 1): obj[key]=value
    req=payload(dict(schema='adaptive-assistance-v1',runtime=runtime,behavior=b),caps)
    label=reference(req)
    return dict(id=f'{split}-{index}',family=mode+(':departure' if departure else ''),payload=req,label=label,
                basis='Authored synthetic policy, not participant helpfulness or Jev distillation')


def generate(output):
    output.mkdir(parents=True,exist_ok=False)
    manifest=dict(policy=POLICY,features=FEATURE_VERSION,supervision='crossed-departure-v2',source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                  interpretation='Synthetic authored policy. Test holds different sample cadences and triple guard conflicts; no device cases used for training or selection.',files={})
    seen=set()
    for split,count,seed in [('train',2700,26102611),('valid',540,26102612),('test',540,26102613)]:
        rng=random.Random(seed); rows=[]; counts=Counter()
        for i in range(100000):
            row=example(rng,i,split); key=text(row['payload'])
            if counts[row['label']]>=count//len(LABELS) or key in seen: continue
            seen.add(key); rows.append(row); counts[row['label']]+=1
            if len(rows)==count: break
        if len(rows)!=count: raise RuntimeError(f'Insufficient samples: {counts}')
        rng.shuffle(rows); path=output/f'{split}.jsonl'
        path.write_text(''.join(json.dumps(r,sort_keys=True)+'\n' for r in rows))
        manifest['files'][split]=dict(n=count,labels=dict(counts),seed=seed,sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    (output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    return manifest
