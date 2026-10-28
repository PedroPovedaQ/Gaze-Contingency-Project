"""Per-stream HTC timing diagnostics; reported pose time is not a sensor clock.

Keep XrTime as Python integers. Subtract integers before converting intervals to
seconds; never use pupil timestamps as gaze timestamps.
"""
import statistics

STREAMS = ('gaze', 'pupil', 'geometric')
STAMP_FIELDS = ('epoch', 'tracker', 'baseSpace', 'session', 'requestedTime', 'returnedTime',
                'beforeNs', 'afterNs', 'result', 'observed')


def stamp(row, stream):
    timing = row.get('htcTiming')
    if timing is None:
        return None
    if not isinstance(timing, dict):
        raise ValueError('HTC timing must be an object')
    value = timing.get(stream)
    if not isinstance(value, dict) or any(type(value.get(k)) is not int for k in STAMP_FIELDS):
        raise ValueError('HTC timestamp fields must be exact integers: ' + stream)
    if value['observed'] not in (0, 1) or value['epoch'] < 0 or value['tracker'] < 0:
        raise ValueError('Invalid HTC timing provenance')
    if value['afterNs'] < value['beforeNs']:
        raise ValueError('Reversed HTC query bracket')
    return value


def usable_stamp(value):
    return value is not None and value['epoch']>0 and value['tracker']>0 and value['observed'] == 1 and value['result'] == 0 and value['returnedTime'] > 0


def clock_identity(row, value):
    return (row.get('recordingId'), row.get('deviceId'), value['epoch'], value['tracker'], value['baseSpace'], value['session'])


def diagnostics(rows):
    output = {}
    for stream in STREAMS:
        counts = dict(observed_queries=0, missing_timestamps=0, failed_queries=0,
                      repeated_times=0, regressions=0, clock_changes=0, unique_times=0)
        intervals, durations, last, last_call, identity = [], [], None, None, None
        for row in rows:
            value = stamp(row, stream)
            if value is None or not value['observed']:
                continue
            key = clock_identity(row, value)
            call = (*key, value['beforeNs'], value['afterNs'])
            if call == last_call:  # Cached Unity heartbeat, not an additional native call.
                continue
            last_call = call
            if key != identity:
                counts['clock_changes'] += int(identity is not None)
                identity, last = key, None
            counts['observed_queries'] += 1
            durations.append((value['afterNs']-value['beforeNs'])/1e6)
            if value['result'] != 0:
                counts['failed_queries'] += 1
                last = None
                continue
            t = value['returnedTime']
            if t <= 0:
                counts['missing_timestamps'] += 1
                last = None
                continue
            if last is not None:
                if t == last:
                    counts['repeated_times'] += 1
                    continue
                if t < last:
                    counts['regressions'] += 1
                else:
                    intervals.append((t-last)/1e9)
            counts['unique_times'] += 1
            last = t
        output[stream] = dict(**counts,
            median_reported_time_hz=1/statistics.median(intervals) if intervals else None,
            max_reported_gap_s=max(intervals) if intervals else None,
            median_query_duration_ms=statistics.median(durations) if durations else None,
            semantics='requested_pose_time' if stream == 'gaze' else 'runtime_reported_capture_time',
            sensor_cadence_validated=False)
    return output
