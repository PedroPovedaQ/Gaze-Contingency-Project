"""Descriptive target error, not a calibration certificate or sensor-rate estimate.

Use one observation per source query/delivery, with fixed windows for every dot.
Never fit an offset to the same observations whose accuracy is being reported.
"""
import math
import statistics

VERSION = 'validation-spatial-error-v2'
SETTLE_SECONDS = .5
SENSITIVITY_SECONDS = (.25, .5, 1., 1.5)


def vector(value):
    try:
        result = tuple(float(value[k]) for k in ('x', 'y', 'z'))
        return result if all(math.isfinite(x) for x in result) else None
    except (KeyError, TypeError, ValueError):
        return None


def angular_error(row, origin='worldOrigin', direction='worldDirection'):
    o, d, t = (vector(row.get(k)) for k in (origin, direction, 'validationTarget'))
    if any(v is None for v in (o, d, t)):
        return None
    target = tuple(b-a for a, b in zip(o, t))
    if sum(x*x for x in d) <= 1e-16 or sum(x*x for x in target) <= 1e-16:
        return None
    cross = (d[1]*target[2]-d[2]*target[1], d[2]*target[0]-d[0]*target[2],
             d[0]*target[1]-d[1]*target[0])
    return math.degrees(math.atan2(math.sqrt(sum(x*x for x in cross)),
                                   sum(a*b for a, b in zip(d, target))))


def observation_key_time(row):
    identity = (row.get('recordingId'), row.get('deviceId'), row.get('raySource'))
    if row.get('raySource') == 'HTC_binocular_mean':
        time = row.get('htcQueryReceiptTime')
        # Host query identity only. A native returned pose time is not proven
        # sensor identity, and may be zero. Cached heartbeat copies share this key.
        stamp = (row.get('htcTiming') or {}).get('gaze') or {}
        if stamp.get('observed') == 1:
            key = (*identity, *(stamp.get(k) for k in
                    ('epoch', 'tracker', 'baseSpace', 'session', 'beforeNs', 'afterNs')))
        else:
            key = (*identity, time)
    elif row.get('raySource') == 'OpenXR_EXT':
        key = (*identity, row.get('inputEventTime'))
        time = row.get('receiptTime')  # First observed delivery; not capture time.
        if not isinstance(row.get('inputEventTime'), (int, float)) or not math.isfinite(row['inputEventTime']):
            return None
    else:
        return None
    if not isinstance(time, (int, float)) or not math.isfinite(time) or time <= 0:
        return None
    return key, time


def unique_observations(manifest, samples):
    seen = set()
    for row in samples:
        if row.get('kind') != 'gaze_observation' or row.get('validationId') != manifest['id']:
            continue
        observation = observation_key_time(row)
        if observation is None or observation[0] in seen:
            continue
        seen.add(observation[0])  # Deduplicate BEFORE phase/window/validity filtering.
        yield row, observation[1]


def window_rows(manifest, samples, events, step, settle=SETTLE_SECONDS):
    start = next((e['time'] for e in events if e.get('validationId', manifest['id']) == manifest['id']
                  and e.get('eventType') == 'stimulus_start' and e.get('step') == step), None)
    end = next((e['time'] for e in events if e.get('validationId', manifest['id']) == manifest['id']
                and e.get('eventType') == 'stimulus_end' and e.get('step') == step), None)
    if start is None or end is None:
        return []  # An incomplete prefix is not a completed-dot accuracy estimate.
    return [r for r, time in unique_observations(manifest, samples)
            if r.get('validationStep') == step and r.get('validationStage') == 'running'
            and r.get('valid') is True and r.get('validationTargetPresent') is True
            and start+settle <= time < end]


def stats(values):
    values = sorted(x for x in values if x is not None)
    if not values:
        return dict(n=0, mean_deg=None, median_deg=None, p90_deg=None)
    position = (len(values)-1)*.9
    low = int(position)
    p90 = values[low]+(values[min(low+1, len(values)-1)]-values[low])*(position-low)
    return dict(n=len(values), mean_deg=statistics.mean(values),
                median_deg=statistics.median(values), p90_deg=p90)


def spatial_accuracy(manifest, samples, events):
    targets = []
    for step, spec in enumerate(manifest['schedule']):
        if spec['exercise'] not in ('spatial', 'drift'):
            continue
        rows = window_rows(manifest, samples, events, step)
        windows = []
        for settle in SENSITIVITY_SECONDS:
            selected = window_rows(manifest, samples, events, step, settle)
            windows.append(dict(settle_seconds=settle, **stats(angular_error(r) for r in selected)))
        sources = sorted({r['raySource'] for r in rows})
        per_eye = {}
        for side in ('left', 'right'):
            per_eye[side] = stats(angular_error(r, side+'WorldOrigin', side+'WorldDirection') for r in rows
                                  if r.get('htcGazeAvailable') is True and r.get(side+'Valid') is True)
        discrepancies = [abs(angular_error(r)-r['usedTargetErrorDegrees']) for r in rows
                         if angular_error(r) is not None and isinstance(r.get('usedTargetErrorDegrees'), (float, int))
                         and math.isfinite(r['usedTargetErrorDegrees'])]
        targets.append(dict(step=step, yaw_deg=spec.get('yaw'), pitch_deg=spec.get('pitch'),
                            forward_distance_m=spec.get('distance'), sources=sources,
                            alignment_applied=any(r.get('alignmentApplied') is True for r in rows),
                            used_ray=stats(angular_error(r) for r in rows),
                            unaligned_ray=stats(angular_error(r, 'unalignedWorldOrigin', 'unalignedWorldDirection') for r in rows),
                            per_eye=per_eye, sensitivity=windows,
                            max_recorded_angle_discrepancy_deg=max(discrepancies) if discrepancies else None))
    aggregates = []
    for index, settle in enumerate(SENSITIVITY_SECONDS):
        means = [t['sensitivity'][index]['mean_deg'] for t in targets]
        available = [m for m in means if m is not None]
        aggregates.append(dict(settle_seconds=settle, targets_with_data=len(available),
                               equal_target_mean_deg=statistics.mean(available) if available else None))
    return dict(version=VERSION, settle_seconds=SETTLE_SECONDS, targets=targets, sensitivity=aggregates,
                observation_unit='HTC host query or EXT input delivery; unique sensor samples not established',
                window='First 0.5 s excluded uniformly; remaining observations before stimulus_end. '
                       'Post-hoc descriptive window, not independently labeled fixation.',
                aggregation='Equal weight per target with available data; missing targets reported, never zero.',
                limitations=['No offset fitted or subtracted using this validation recording.',
                             'Headset calibration status is user-reported, not verified by this receipt.',
                             'Target onset is a software marker; eye capture/display synchronization is unverified.',
                             'Errors include residual eye/head movement; they are not certified device accuracy.'])
