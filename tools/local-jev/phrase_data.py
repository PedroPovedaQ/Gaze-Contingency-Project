"""Second-stage candidate features. History is actual round playback, never proposals."""
import copy
import math
import random
import adaptive_data as actions

VERSION = 'adaptive-phrase-ranker-v2'
SCOPE = 'assistance_phrase'
ARRIVAL_PHRASES = ("You're in the correct area. Keep looking.", "This is the correct area. Keep looking.",
                   "Stay in this area and keep looking.", "Keep searching in this area.")


def wall_arrival_override(state, choices):
    """Allow phrase selection during playback, never authorize audio interruption."""
    evidence = state['runtime']['evidence']
    return (state.get('override_reason') == 'first_target_wall_entry' and
            state.get('selected_action') == 'refine_explicit' and
            evidence.get('insideTargetWall') is True and evidence.get('wallEntryQualified') is True and
            evidence['guidanceState'].get('allowedAction') == 'Arrival' and
            all(p in ARRIVAL_PHRASES for p in choices))


def unpack(request):
    if not isinstance(request, dict) or set(request.get('questions') or {}) != {SCOPE}:
        raise ValueError('Expected phrase-only request')
    state = request.get('state')
    if not isinstance(state, dict) or state.get('selected_action') not in actions.LABELS[1:]:
        raise ValueError('Expected selected speaking action')
    question = request['questions'][SCOPE]
    if not isinstance(question, dict) or question.get('type') != 'choice':
        raise ValueError('Expected phrase choice')
    choices = question.get('criteria')
    if not isinstance(choices, dict) or not 1 <= len(choices) <= 12:
        raise ValueError('One to twelve phrase candidates required')
    if any(not isinstance(k, str) or not k.strip() or len(k) > 256 or v != k for k, v in choices.items()):
        raise ValueError('Exact bounded phrase candidates required')
    action_request = actions.payload(state, [state['selected_action']])
    actions.unpack(action_request)
    history = state['behavior'].get('phrase_history', [])
    if not isinstance(history, list) or len(history) > 128:
        raise ValueError('Invalid round phrase history')
    seen = set()
    for item in history:
        if not isinstance(item, dict): raise ValueError('Invalid phrase history item')
        phrase, count = item.get('phrase'), item.get('count')
        if not isinstance(phrase, str) or not phrase or len(phrase) > 256 or phrase in seen:
            raise ValueError('Unique bounded historical phrases required')
        seen.add(phrase)
        age = item.get('last_started_age_seconds')
        if type(count) is not int or not 1 <= count <= 100000 or type(age) not in (int, float) or not math.isfinite(age) or age < 0:
            raise ValueError('Invalid playback count/age')
    return state, choices, history, action_request


def features(request):
    state, choices, history, action_request = unpack(request)
    by_phrase = {h['phrase']: h for h in history}
    context = actions.text(action_request)
    result = []
    for phrase in choices:
        played = by_phrase.get(phrase)
        count = played['count'] if played else 0
        words = len(phrase.split())
        result.append(context + f'. selected action: {state["selected_action"]}. candidate phrase: {phrase}'
                      + f' words: {words}; length: {"short" if words <= 4 else "expanded"}.'
                      + f' played this round: {"yes" if count else "no"}; repeat count: {count};'
                      + f' last played age: {played["last_started_age_seconds"] if played else "never"}.')
    return result


def reference_scores(request):
    """Bootstrap supervision only, not called by inference; no participant-benefit claim."""
    state, choices, history, _ = unpack(request)
    counts = {h['phrase']: h['count'] for h in history}
    detail = state['selected_action'].endswith('_explicit') and bool(state['behavior'].get('interventions'))
    return [(-5 * min(counts.get(p, 0), 3)) + (1 if detail else -1) * len(p.split()) / 12 for p in choices]


def examples(menus, count, seed, seen):
    rng = random.Random(seed); result = []
    unique = {(m['action'], tuple(m['phrases'])) for m in menus if len(m['phrases']) > 1}
    unique = sorted(unique)
    for i in range(count * 20):
        action, phrases = rng.choice(unique)
        phrases = list(phrases); rng.shuffle(phrases)
        row = actions.example(rng, i, 'train')
        state = copy.deepcopy(row['payload']['state'])
        state['selected_action'] = action
        state['runtime']['playback'] = {'is_busy': False, 'is_speaking': False}
        state['runtime']['locally_qualified'] = True
        e = state['runtime']['evidence']
        e.update(valid=True, geometryAvailable=True, controllerOnTarget=False, targetGazeQualified=False,
                 gazeWithinTargetVicinity=False, insideTargetWall=action.startswith('refine'), targetReturnPending=action == 'correct')
        e['guidanceState'].update(newGuidanceAllowed=True, allowedAction='ReturnTarget' if action == 'correct' else 'Direction')
        history = []
        # Include no repetitions, one repeated choice, all repeated, and unequal counts.
        for phrase in rng.sample(phrases, rng.randrange(len(phrases) + 1)):
            history.append(dict(phrase=phrase, count=rng.randint(1, 4), last_started_age_seconds=round(rng.uniform(.5, 24), 2)))
        state['behavior']['phrase_history'] = history
        request = dict(state=state, questions={SCOPE: dict(type='choice', criteria={p:p for p in phrases})})
        key = tuple(sorted(features(request)))
        if key in seen: continue
        seen.add(key)
        result.append(dict(payload=request, scores=reference_scores(request), basis='Authored synthetic preference: discourage repeats, then concision/detail; not human judgments'))
        if len(result) == count: return result
    raise RuntimeError('Insufficient unique phrase examples')
