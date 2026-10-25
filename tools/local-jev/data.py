"""Synthetic development data; labels express the authored policy, not human helpfulness."""
import copy
import hashlib
import json
import math
from pathlib import Path
import random
import re

ROOT = Path(__file__).resolve().parents[2]
LABELS = ['wait', 'orient', 'refine', 'correct', 'encourage']
MODEL = 'sentence-transformers/all-MiniLM-L6-v2'
MODEL_REVISION = '1110a243fdf4706b3f48f1d95db1a4f5529b4d41'
SOURCE = ROOT / 'analysis/pilots/20261020-runtime-actions/comparison-v3.json'
PHRASE_SOURCE = ROOT / 'Assets/DirectionalHintPolicy.cs'


def direction_words(phrase):
    """Restricted authored navigation vocabulary; not a general semantic parser."""
    words = set(re.findall(r'[a-z]+', phrase.lower()))
    if 'higher' in words: words.add('up')
    if 'lower' in words: words.add('down')
    return tuple(word for word in ('up', 'down', 'left', 'right') if word in words)


def phrases(direction):
    """Subset of DirectionalHintPolicy constants/Variant output, verified by source test."""
    if direction in ('left', 'right'):
        return [f'Look {direction}.', f'Look to your {direction}.', f'Search to your {direction}.',
                f'A little more {direction}.', f'Continue looking {direction}.']
    vertical = 'higher' if 'up' in direction else 'lower'
    suffix = ' and left' if 'left' in direction else ' and right' if 'right' in direction else ''
    return [f'Look {vertical}{suffix}.', f'Look further {vertical}{suffix}.', f'Keep looking {vertical}{suffix}.']


def unpack(payload):
    questions = payload.get('questions', {})
    if set(questions) != {'navigation_action'}:
        raise ValueError('This prototype supports navigation_action only')
    question = questions['navigation_action']
    choices = question.get('criteria', {})
    if question.get('type') != 'choice' or not choices or 'wait' not in choices or not set(choices) <= set(LABELS):
        raise ValueError('Unsupported eligible action set')
    state = payload.get('state', {})
    if not isinstance(state, dict) or state.get('schema') != 'development-action-v1':
        raise ValueError('Expected structured runtime state')
    runtime = state.get('runtime', {})
    for candidate, value in choices.items():
        if not isinstance(value, dict): raise ValueError('Expected typed criteria')
        if candidate != 'wait' and (value.get('locally_eligible') is not True or
                not isinstance(value.get('text'), str) or not 0 < len(value['text']) <= 256):
            raise ValueError('Only prepared, locally eligible phrases are supported')
    silence = runtime.get('seconds_since_speech_ended')
    if type(silence) not in (float, int) or not math.isfinite(silence) or silence < 0:
        raise ValueError('Missing or invalid elapsed silence')
    return state, runtime, runtime.get('evidence', {}), runtime.get('playback', {}), choices


def blocked(payload):
    """Same mandatory safety gates retained outside the learned preference model."""
    _, runtime, evidence, playback, _ = unpack(payload)
    guidance = evidence.get('guidanceState', {})
    return (evidence.get('valid') is not True or evidence.get('geometryAvailable') is not True or
            guidance.get('newGuidanceAllowed') is not True or playback.get('is_speaking') is not False or
            playback.get('is_busy') is not False or evidence.get('controllerOnTarget') is not False or
            evidence.get('targetGazeQualified') is not False or runtime.get('locally_qualified') is not True)


def reference(payload):
    """Training oracle derived from ActionPayload instructions; not a Jev teacher."""
    _, runtime, evidence, playback, choices = unpack(payload)
    if blocked(payload):
        return 'wait'
    silence = runtime.get('seconds_since_speech_ended')
    if not isinstance(silence, (float, int)) or not math.isfinite(silence):
        return 'wait'
    if 'correct' in choices and evidence['guidanceState'].get('allowedAction', '').startswith('Return'):
        return 'correct'
    history = playback.get('recent_history', {}).get('speech', [])
    last = playback.get('last_spoken_phrase') or (history[-1].get('text', '') if history else '')
    proposed = [v.get('text') for k, v in choices.items() if k not in ('wait', 'encourage')]
    if silence <= 3 and any(last == p or (direction_words(last) and direction_words(last) == direction_words(p)) for p in proposed):
        return 'wait'
    if 'encourage' in choices and silence > 3:
        return 'encourage'
    return next((x for x in ('refine', 'orient') if x in choices), 'wait')


def text(payload):
    """A bounded, explicit feature view. No labels, IDs, outcome or timing of responses."""
    _, runtime, evidence, playback, choices = unpack(payload)
    guidance = evidence.get('guidanceState', {})
    history = playback.get('recent_history', {}).get('speech', [])[-4:]
    values = {key: evidence.get(key) for key in (
        'valid', 'geometryAvailable', 'insideTargetWall', 'controllerOnTarget',
        'targetGazeQualified', 'targetReturnPending', 'targetAcknowledged',
        'yawErrorDegrees', 'pitchErrorDegrees')}
    values.update(phase=guidance.get('phase'), allowedAction=guidance.get('allowedAction'),
                  newGuidanceAllowed=guidance.get('newGuidanceAllowed'),
                  speaking=playback.get('is_speaking'), busy=playback.get('is_busy'),
                  locallyQualified=runtime.get('locally_qualified'),
                  silenceSeconds=runtime.get('seconds_since_speech_ended'),
                  silenceBand='recent' if runtime['seconds_since_speech_ended'] <= 3 else 'long',
                  lastSpeech=playback.get('last_spoken_phrase') or (history[-1].get('text') if history else 'none'))
    # Sorted action IDs make output invariant to JSON insertion order.
    parts = [f'{k}: {v}' for k, v in values.items()]
    for key in sorted(choices):
        candidate = choices[key]
        if not isinstance(candidate, dict):
            raise ValueError('Expected typed candidate metadata')
        parts.append(f'offered {key}: {candidate.get("text") or "silence"}')
    return ' ; '.join(parts)


def synthetic(rng, index, split):
    """Train on cardinal directions; unseen diagonal direction combinations form test."""
    directions = ['left', 'right', 'up', 'down'] if split != 'test' else ['up left', 'up right', 'down left', 'down right']
    direction = rng.choice(directions)
    inside = direction in ('up', 'down') or ' ' in direction or rng.random() < .5
    mode = rng.choice(['ordinary', 'progress', 'return'])
    phrase = rng.choice(phrases(direction))
    choices = {('refine' if inside else 'orient'): {'text': phrase, 'locally_eligible': True}}
    location = ' and '.join(direction.split())
    progress = f'Keep looking {location}.' if 'up' in direction or 'down' in direction else f'Keep going to the {direction}.'
    if mode == 'progress': choices['encourage'] = {'text': progress, 'locally_eligible': True}
    if mode == 'return': choices['correct'] = {'text': f'Go back {location}.', 'locally_eligible': True}
    choices['wait'] = {'text': None}
    silence = round(rng.uniform(.05, 2.9) if rng.random() < .45 else rng.uniform(3.1, 12), 2)
    evidence = dict(valid=True, geometryAvailable=True, insideTargetWall=inside, controllerOnTarget=False,
                    targetGazeQualified=False, targetReturnPending=mode == 'return', targetAcknowledged=mode == 'return',
                    yawErrorDegrees=round((-1 if 'left' in direction else 1) * rng.uniform(8, 35 if inside else 45) if 'left' in direction or 'right' in direction else 0, 2),
                    pitchErrorDegrees=round(rng.uniform(5, 20) if 'up' in direction else rng.uniform(-20, -5) if 'down' in direction else 0, 2),
                    guidanceState=dict(phase='Refining' if inside else 'Turning',
                                       allowedAction='ReturnTarget' if mode == 'return' else 'Direction', newGuidanceAllowed=True))
    opposite = {'left':'right','right':'left','up':'down','down':'up'}[direction.split()[0]]
    playback = dict(is_speaking=False, is_busy=False,
                    last_spoken_phrase=rng.choice(phrases(direction)) if rng.random() < .7 else rng.choice(phrases(opposite)))
    runtime = dict(evidence=evidence, playback=playback, locally_qualified=True, seconds_since_speech_ended=silence)
    if rng.random() < .3:
        changes = [('valid', False), ('geometryAvailable', False), ('controllerOnTarget', True), ('targetGazeQualified', True),
                   ('newGuidanceAllowed', False), ('is_speaking', True), ('is_busy', True), ('locally_qualified', False)]
        # Hold out combined failures, rather than randomly splitting neighboring variants.
        for key, value in rng.sample(changes, 2 if split == 'test' else 1):
            target = playback if key.startswith('is_') else evidence['guidanceState'] if key == 'newGuidanceAllowed' else runtime if key == 'locally_qualified' else evidence
            target[key] = value
    payload = dict(model='local-jev-prototype', state=dict(schema='development-action-v1', runtime=runtime),
                   questions=dict(navigation_action=dict(type='choice', criteria=choices)))
    label = reference(payload)
    return dict(id=f'{split}-{index:05}', family=f'{split}:{direction}:{mode}', payload=payload, label=label,
                acceptable=[label],
                basis='synthetic authored policy, no participant data')


def generate(output):
    output.mkdir(parents=True, exist_ok=False)
    manifest = dict(schema=1, seed=20261025, split='cardinal train/validation; diagonal paraphrases and combined failures test',
                    labels='Authored instructions, not subjective human helpfulness or unfiltered teacher outputs', files={})
    manifest['phrase_source_sha256'] = hashlib.sha256(PHRASE_SOURCE.read_bytes()).hexdigest()
    for split, count, seed in [('train', 1200, 20261025), ('valid', 240, 20261026), ('test', 320, 20261027)]:
        rng = random.Random(seed)
        rows, counts, i = [], dict.fromkeys(LABELS, 0), 0
        # Balance labels; test has no orient because its held-out diagonals are inside-wall.
        active = LABELS if split != 'test' else ['wait', 'refine', 'correct', 'encourage']
        while len(rows) < count:
            row = synthetic(rng, i, split); i += 1
            if row['label'] not in active or counts[row['label']] >= count // len(active): continue
            counts[row['label']] += 1; rows.append(row)
        rng.shuffle(rows)
        path = output / f'{split}.jsonl'
        path.write_text(''.join(json.dumps(row, sort_keys=True) + '\n' for row in rows))
        manifest['files'][split] = dict(count=len(rows), labels=counts, sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    # Entire old production-builder cases are untouched external test evidence, never training rows.
    rows = []
    seen = set()
    for old in json.loads(SOURCE.read_text())['rows']:
        if old['arm'] != 'actions' or old['case'] in seen: continue
        seen.add(old['case'])
        payload = copy.deepcopy(old['action_payload'])
        label = 'wait' if old['case'] == 'recent_repeat' else 'correct' if old['case'].startswith('return_') else 'encourage' if 'progress' in old['case'] else 'refine' if old['case'] == 'left_stationary_inside' else 'orient'
        rows.append(dict(id=old['case'], family='production_csharp_holdout', payload=payload, label=label,
                         acceptable=[label] if label != 'correct' else ['correct', 'refine'], basis='Independent expected contract; stored Jev response is not the label'))
    path = output / 'production.jsonl'; path.write_text(''.join(json.dumps(r) + '\n' for r in rows))
    manifest['production_source_sha256'] = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    manifest['files']['production'] = dict(count=len(rows), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return manifest
