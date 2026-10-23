"""Describe persisted software eye deliveries without inventing sensor samples.

Availability is observational: an unobserved signal is not proof that hardware
lacks support. HTC values can be copied between input updates, so their counters
use distinct recorded query receipt times, not the number of JSONL rows.
"""
import csv
import json
import math
import statistics


CLOCKS = [
    dict(field='receiptTime', clock='Unity realtime seconds', meaning='Host capture/receipt',
         known=True, frame='Application monotonic clock; not sensor exposure'),
    dict(field='inputEventTime', clock='Unity Input System lastUpdateTime', meaning='Input-state delivery',
         known=True, frame='Application realtime domain; freshness is not sensor latency'),
    dict(field='htcQueryReceiptTime', clock='Unity realtime seconds', meaning='HTC query invocation',
         known=True, frame='Host time; values may be copied until next query'),
    dict(field='nativeCaptureTime', clock='unknown', meaning='Physical sensor capture', known=False,
         frame='Not exposed by this recording schema'),
    dict(field='nativeSampleSequence', clock='not applicable', meaning='Distinct sensor observation identity',
         known=False, frame='JSONL sequence counts application rows only'),
    dict(field='requestedPoseTime', clock='unknown', meaning='Requested/predicted OpenXR pose time',
         known=False, frame='Not retained by this acquisition adapter'),
    dict(field='worldDirection/worldOrigin', clock='receiptTime context', meaning='Used gaze ray',
         known=True, frame='Unity world; origin in metres, direction unitless'),
    dict(field='leftNativeOrigin/rightNativeOrigin', clock='htcQueryReceiptTime context', meaning='Vendor raw pose',
         known=True, frame='Right-handed OpenXR app space; metres, separate per-eye validity'),
]


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def describe(values):
    """Linear-interpolated p90, consistent with eye_quality.percentile."""
    if not values:
        return dict(count=0, median_ms=None, p90_ms=None, maximum_ms=None, interval_hz=None)
    ordered = sorted(values)
    at = .9*(len(ordered)-1)
    low = int(at)
    p90 = ordered[low]+(ordered[min(low+1, len(ordered)-1)]-ordered[low])*(at-low)
    return dict(count=len(values), median_ms=1000*statistics.median(values), p90_ms=1000*p90,
                maximum_ms=1000*max(values), interval_hz=len(values)/sum(values))


def timing(rows):
    """Split interval pairs at participant/run/phase/practice/device boundaries.

    Invalid deliveries still count as deliveries. Heartbeats retain outages but
    do not count as new input events. Equal gaze coordinates are never deduplicated.
    """
    groups, intervals = {}, []
    previous = last_update = None
    seen_deliveries = set()
    missing_sequences = 0
    for row in rows:
        key = tuple(row.get(k) for k in ('recordingId', 'participant', 'run', 'phase', 'practice', 'deviceId'))
        if key not in groups:
            groups[key] = dict(rows=0, updates=0, valid_updates=0, heartbeats=0, duplicate_flags=0,
                               unknown_times=0, nonincreasing_times=0, row_intervals=[], intervals=[],
                               frames=set(), valid_rows=0, distinct_updates=0)
        group = groups[key]
        group['rows'] += 1
        group['valid_rows'] += bool(row.get('valid'))
        if row.get('frame') is not None:
            group['frames'].add(row['frame'])
        if previous is not None:
            old_key, old = previous
            if row.get('recordingId') == old.get('recordingId'):
                missing_sequences += max(0, row['sequence']-old['sequence']-1)
            if key == old_key:
                delta = row['receiptTime']-old['receiptTime']
                if delta > 0: group['row_intervals'].append(delta)
            else:
                last_update = None
        if row.get('newInputUpdate'):
            group['updates'] += 1
            group['valid_updates'] += bool(row.get('valid'))
            delivered = row.get('inputEventTime')
            if not row.get('deviceAvailable') or not finite(delivered) or delivered <= 0:
                group['unknown_times'] += 1
                last_update = None
            else:
                identity = (*key, delivered)
                if identity in seen_deliveries:
                    group['duplicate_flags'] += 1
                else:
                    seen_deliveries.add(identity)
                    group['distinct_updates'] += 1
                if last_update is not None:
                    delta = delivered-last_update
                    if delta > 0:
                        group['intervals'].append(delta)
                        intervals.append(dict(phase=row.get('phase'), participant=row.get('participant'),
                                              run=row.get('run'), practice=row.get('practice'),
                                              device_id=row.get('deviceId'), sequence=row['sequence'],
                                              interval_ms=delta*1000))
                    else:
                        group['nonincreasing_times'] += 1
                last_update = delivered
        else:
            group['heartbeats'] += 1
        previous = (key, row)
    phases = []
    for key, group in groups.items():
        recording, participant, run, phase, practice, device = key
        delivered = describe(group['intervals'])
        polls = describe(group['row_intervals'])
        phases.append(dict(recording_id=recording, participant_id=participant, run_number=run,
                           phase=phase or 'unknown', practice=practice, device_id=device,
                           recorded_rows=group['rows'], input_update_flags=group['updates'],
                           distinct_known_input_deliveries=group['distinct_updates'],
                           valid_input_update_flags=group['valid_updates'],
                           valid_input_fraction=group['valid_updates']/group['updates'] if group['updates'] else None,
                           heartbeat_rows=group['heartbeats'], distinct_render_frame_ids=len(group['frames']),
                           duplicate_update_flags=group['duplicate_flags'], unknown_delivery_times=group['unknown_times'],
                           nonincreasing_delivery_times=group['nonincreasing_times'],
                           delivery_interval_pairs=delivered['count'], delivery_interval_hz=delivered['interval_hz'],
                           delivery_interval_median_ms=delivered['median_ms'], delivery_interval_p90_ms=delivered['p90_ms'],
                           delivery_interval_max_ms=delivered['maximum_ms'],
                           recorded_row_interval_hz=polls['interval_hz'],
                           native_samples=None, native_drops=None, sensor_rate_conclusion='not_established'))
    return dict(version='gaze-timing-v1', phases=phases, intervals=intervals,
                missing_application_row_sequences=missing_sequences,
                native_samples=None, native_drops=None, sensor_rate_conclusion='not_established')


def capabilities(rows):
    """HTC counters use query IDs to avoid treating copied fields as new polls."""
    queries = {}
    for row in rows:
        stamp = row.get('htcQueryReceiptTime')
        if finite(stamp) and stamp > 0:
            queries.setdefault((row.get('recordingId'), stamp), row)
    native_rows = list(queries.values())
    definitions = [
        ('combined_gaze', 'deviceAvailable', 'valid', rows, 'persisted_rows_including_heartbeats', 'Unity_world_metres'),
        ('left_eye_gaze', 'htcGazeAvailable', 'leftValid', native_rows, 'distinct_host_query_times', 'OpenXR_app_metres_and_Unity_world'),
        ('right_eye_gaze', 'htcGazeAvailable', 'rightValid', native_rows, 'distinct_host_query_times', 'OpenXR_app_metres_and_Unity_world'),
        ('left_pupil', 'pupilAvailable', 'leftPupilValid', native_rows, 'distinct_host_query_times', 'millimetres'),
        ('right_pupil', 'pupilAvailable', 'rightPupilValid', native_rows, 'distinct_host_query_times', 'millimetres'),
        ('left_openness', 'opennessAvailable', 'leftOpennessValid', native_rows, 'distinct_host_query_times', 'unitless_0_to_1'),
        ('right_openness', 'opennessAvailable', 'rightOpennessValid', native_rows, 'distinct_host_query_times', 'unitless_0_to_1'),
    ]
    result = []
    for signal, available, valid, samples, denominator, units in definitions:
        exposed = sum(r.get(available) is True for r in samples)
        usable = sum(r.get(available) is True and r.get(valid) is True for r in samples)
        logged = sum(available in r and valid in r for r in samples)
        evidence = ('no_timed_query_observed' if denominator == 'distinct_host_query_times' and not samples
                    else 'valid_observed' if usable else 'available_no_valid_observation'
                    if exposed else 'not_observed_or_unavailable' if logged else 'not_logged')
        result.append(dict(signal=signal, evidence=evidence,
                           observation_basis=denominator, observation_count=len(samples),
                           flag_pairs_present=logged, available_observations=exposed, valid_observations=usable,
                           valid_fraction=usable/logged if logged else None, units=units,
                           native_rate_hz=None, hardware_support_conclusion='not_determined_by_absence'))
    return result


def write_csv(path, rows, fallback_columns):
    with path.open('w', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]) if rows else fallback_columns)
        writer.writeheader(); writer.writerows(rows)


def write_outputs(out, rows, source_path, end_status, plot=True):
    report = timing(rows)
    report['capabilities'] = capabilities(rows)
    metadata_path = source_path.parent/'capabilities.json'
    report['recording_metadata'] = json.loads(metadata_path.read_text()) if metadata_path.exists() else None
    report['recording_end_status'] = end_status
    report['clocks_and_frames'] = CLOCKS
    report['limitations'] = ('Counters describe persisted application data, not all polls or native sensor samples. '
                            'Intervals split at phase/device/participant/run/practice boundaries. '
                            'Missing application sequences are not sensor drops. Query time is not acquisition time. '
                            'Negative evidence cannot distinguish unsupported fields from temporarily unavailable tracking.')
    (out/'timing_report.json').write_text(json.dumps(report, indent=2, allow_nan=False))
    write_csv(out/'phase_delivery_quality.csv', report['phases'], ['phase', 'recorded_rows'])
    write_csv(out/'delivery_intervals.csv', report['intervals'], ['phase', 'interval_ms'])
    write_csv(out/'signal_capabilities.csv', report['capabilities'], ['signal', 'evidence'])
    write_csv(out/'clock_frame_dictionary.csv', CLOCKS, ['field', 'clock', 'meaning', 'known', 'frame'])
    if plot:
        plot_timing(out, rows, report)
    return report


def plot_timing(out, rows, report):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt

    figure, (interval_axis, age_axis) = plt.subplots(2, 1, figsize=(10, 7), constrained_layout=True)
    values = [r['interval_ms'] for r in report['intervals']]
    if values:
        interval_axis.hist(values, bins=min(60, max(10, int(math.sqrt(len(values))))),
                           range=(0, max(25, max(values)*1.05)), color='#285a83')
        interval_axis.axvline(1000/120, color='#b24137', linestyle='--', label='8.33 ms reference (120 Hz); not evidence of sensor rate')
        interval_axis.legend(fontsize=8)
    else:
        interval_axis.text(.5, .5, 'No usable consecutive delivery timestamps', ha='center', transform=interval_axis.transAxes)
    interval_axis.set(xlabel='Input-state delivery interval (ms)', ylabel='Interval count', title='Recorded software deliveries')
    origin = rows[0]['receiptTime']
    ages = [(r['receiptTime']-origin, 1000*(r['receiptTime']-r['inputEventTime']), bool(r.get('valid')))
            for r in rows if r.get('deviceAvailable') and finite(r.get('inputEventTime')) and r['inputEventTime'] > 0
            and r['receiptTime'] >= r['inputEventTime']]
    for valid, label, color in ((True, 'Saved valid rows', '#285a83'), (False, 'Saved invalid rows', '#b24137')):
        chosen = [(t, a) for t, a, flag in ages if flag == valid]
        if chosen:
            age_axis.scatter([x[0] for x in chosen], [x[1] for x in chosen], s=2, c=color, label=label, rasterized=True)
    if ages:
        age_axis.legend(fontsize=8)
    else:
        age_axis.text(.5, .5, 'No recorded delivery-age evidence', ha='center', transform=age_axis.transAxes)
    age_axis.set(xlabel='Host receipt time since first saved row (s)', ylabel='Input delivery age at receipt (ms)',
                 title='Software freshness; not sensor-to-display latency')
    fixture = (report.get('recording_metadata') or {}).get('synthetic_fixture', False)
    figure.suptitle(('Synthetic fixture — ' if fixture else 'Eye acquisition timing — ') +
                   'native 120 Hz NOT ESTABLISHED', fontsize=13)
    figure.savefig(out/'delivery_timing.png', dpi=160)
    figure.savefig(out/'delivery_timing.svg')
    plt.close(figure)
