"""Second experiment: split authored wording families before generating examples."""
import copy
import hashlib
import json
import random
from collections import Counter
from data import LABELS, PHRASE_SOURCE, direction_words, reference, synthetic, text, unpack

FEATURE_VERSION = 'compact-v2'


def model_text(payload, version=None):
    if version is None:
        return text(payload)  # Existing checkpoints retain their original representation.
    if version != FEATURE_VERSION:
        raise ValueError('Unknown model feature version')
    _, runtime, evidence, playback, choices = unpack(payload)
    guidance = evidence.get('guidanceState', {})
    history = playback.get('recent_history', {}).get('speech', [])
    last = playback.get('last_spoken_phrase') or (history[-1].get('text', '') if history else '')
    def boolean(value):
        return 'yes' if value is True else 'no' if value is False else 'unknown'
    fields = [('gaze valid', evidence.get('valid')), ('geometry available', evidence.get('geometryAvailable')),
              ('guidance allowed', guidance.get('newGuidanceAllowed')), ('local qualification', runtime.get('locally_qualified')),
              ('speaking', playback.get('is_speaking')), ('busy', playback.get('is_busy')),
              ('controller on target', evidence.get('controllerOnTarget')), ('gaze on target', evidence.get('targetGazeQualified')),
              ('inside wall', evidence.get('insideTargetWall')), ('target acknowledged', evidence.get('targetAcknowledged')),
              ('return pending', evidence.get('targetReturnPending'))]
    parts = [f'{key}: {boolean(value)}' for key, value in fields]
    silence = runtime['seconds_since_speech_ended']
    parts += [f'event: {guidance.get("allowedAction")}',
              f'silence: {"recent" if silence <= 3 else "long"} ({silence} seconds)',
              f'last direction: {" ".join(direction_words(last)) or "none"}', f'last speech: {last}']
    for key, candidate in sorted(choices.items()):
        phrase = candidate.get('text') or ''
        parts.append(f'offer {key}: {phrase or "silence"}; direction: {" ".join(direction_words(phrase)) or "none"}')
    return '. '.join(parts)


def wording(direction, split):
    if direction in ('left', 'right'):
        patterns = {'train': ['Look {}.', 'Search to your {}.', 'Continue looking {}.'],
                    'valid': ['Look to your {}.', 'A little more {}.'],
                    'fresh': ['Scan to your {}.', 'Look more to your {}.']}
        return [p.format(direction) for p in patterns[split]]
    vertical = 'higher' if 'up' in direction else 'lower'
    suffix = ' and left' if 'left' in direction else ' and right' if 'right' in direction else ''
    patterns = {'train': ['Look {}.', 'Keep looking {}.'], 'valid': ['Look further {}.'],
                'fresh': ['A little {}.', 'Just a little {}.']}
    return [p.format(vertical + suffix) for p in patterns[split]]


def example(rng, index, split):
    row = synthetic(rng, index, 'test' if rng.random() < .5 else 'train')
    payload = row['payload']; _, runtime, evidence, playback, choices = unpack(payload)
    # Reset inherited synthetic safety failure; this experiment controls split-specific gates.
    evidence.update(valid=True, geometryAvailable=True, controllerOnTarget=False, targetGazeQualified=False)
    guidance = evidence['guidanceState']; guidance['newGuidanceAllowed'] = True
    runtime['locally_qualified'] = True
    playback.update(is_busy=False, is_speaking=False)
    action = 'refine' if 'refine' in choices else 'orient'
    direction = ' '.join(direction_words(choices[action]['text']))
    phrase = rng.choice(wording(direction, split)); choices[action]['text'] = phrase
    same = rng.random() < .5
    opposite = {'left':'right', 'right':'left', 'up':'down', 'down':'up'}[direction.split()[0]]
    last = rng.choice(wording(direction if same else opposite, split))
    # Half the rows use actual recent_history representation rather than a convenience field.
    if rng.random() < .5:
        playback.pop('last_spoken_phrase', None)
        playback['recent_history'] = {'speech': [{'text': last}]}
    else:
        playback['last_spoken_phrase'] = last
    runtime['seconds_since_speech_ended'] = rng.choice([2.999, 3.0, 3.001]) if rng.random() < .2 else round(rng.uniform(.01, 10), 4)
    gates = [(evidence, 'valid', False), (evidence, 'geometryAvailable', False),
             (evidence, 'controllerOnTarget', True), (evidence, 'targetGazeQualified', True),
             (guidance, 'newGuidanceAllowed', False), (playback, 'is_speaking', True),
             (playback, 'is_busy', True), (runtime, 'locally_qualified', False)]
    if rng.random() < .22:
        for obj, key, value in rng.sample(gates, 3 if split == 'fresh' else rng.choice([1, 2])):
            if rng.random() < .15: obj.pop(key)
            else: obj[key] = value
    label = reference(payload)
    return dict(id=f'{split}-{index:06}', family=f'{split}:{direction}:{guidance["allowedAction"]}',
                payload=payload, label=label, acceptable=[label],
                basis='Synthetic authored policy; split by cue wording before generation, no participant data')


def generate_continuation(output):
    output.mkdir(parents=True, exist_ok=False)
    manifest = dict(schema=2, feature_version=FEATURE_VERSION,
                    split='Disjoint offered orient/refine wording families; fresh set also holds out triple gate failures',
                    phrase_source_sha256=hashlib.sha256(PHRASE_SOURCE.read_bytes()).hexdigest(), files={})
    all_inputs = set()
    for split, count, seed in [('train', 2400, 202610251), ('valid', 400, 202610252), ('fresh', 500, 202610253)]:
        rng = random.Random(seed); counts = Counter(); rows = []
        for index in range(100000):
            row = example(rng, index, split)
            key = model_text(row['payload'], FEATURE_VERSION)
            if counts[row['label']] >= count//len(LABELS) or key in all_inputs: continue
            rows.append(row); counts[row['label']] += 1; all_inputs.add(key)
            if len(rows) == count: break
        if len(rows) != count: raise RuntimeError('Insufficient unique examples')
        rng.shuffle(rows); path = output/f'{split}.jsonl'
        path.write_text(''.join(json.dumps(row, sort_keys=True)+'\n' for row in rows))
        manifest['files'][split] = dict(count=count, seed=seed, labels=dict(counts), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    (output/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
    return manifest
