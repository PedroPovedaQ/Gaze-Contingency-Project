"""Audit shared gaze JSONL without equating input deliveries, polls, or fixations.

All event thresholds are pilot parameters. Candidate events remain unvalidated,
including when an annotation comparison is available. No workload score is inferred.
"""
import argparse
import csv
import json
import math
import statistics
from collections import defaultdict
from pathlib import Path

DETECTOR_VERSION = 'world-ray-ivt-candidate-v2'


def vector(value):
    return tuple(float(value[k]) for k in ('x', 'y', 'z'))


def angle(a, b):
    a, b = vector(a), vector(b)
    norm = math.sqrt(sum(v*v for v in a)*sum(v*v for v in b))
    if not norm or not math.isfinite(norm):
        return None
    return math.degrees(math.acos(max(-1, min(1, sum(x*y for x, y in zip(a, b))/norm))))


def percentile(values, p):
    if not values:
        return None
    a = sorted(values)
    i = (len(a)-1)*p
    lo = int(i)
    return a[lo]+(a[min(lo+1,len(a)-1)]-a[lo])*(i-lo)


def unit(value):
    v = vector(value)
    length = math.sqrt(sum(x*x for x in v))
    if not length or not math.isfinite(length):
        return None
    return dict(zip(('x', 'y', 'z'), (x/length for x in v)))


def mean_direction(values):
    directions = [unit(v) for v in values]
    if not directions or any(v is None for v in directions):
        return None
    return unit({k: statistics.mean(v[k] for v in directions) for k in ('x', 'y', 'z')})


def valid_interval_seconds(row, dt, max_gap):
    if not row.get('valid'):
        return 0.
    freshness = min(max_gap, float(row.get('maxDeliveryAgeSeconds', max_gap)))
    return max(0, min(dt, freshness-float(row.get('ageSeconds', 0))))


def spatial_bias(accepted, segments):
    """World-frame bias and within-contiguous-segment radial angular spread.

    Radial angular SD = sqrt(sum(angle(sample, segment mean)^2)/(N-S)),
    where S counts segments with at least two samples. This is neither the SD
    of target-error magnitudes nor precision across tracking gaps. Head motion
    remains a possible contributor; no physiological fixation is inferred.
    """
    actual, expected, yaw_errors, pitch_errors = [], [], [], []
    for row in accepted:
        if 'rawWorldOrigin' not in row or 'validationTarget' not in row:
            continue  # Older fixtures/files may lack the geometry for this metric.
        gaze = unit(row['rawWorldDirection'])
        target = unit({k: row['validationTarget'][k]-row['rawWorldOrigin'][k]
                       for k in ('x', 'y', 'z')})
        if gaze is None or target is None:
            continue
        actual.append(gaze); expected.append(target)
        def azimuth(v): return math.degrees(math.atan2(v['x'], v['z']))
        def elevation(v): return math.degrees(math.atan2(v['y'], math.hypot(v['x'], v['z'])))
        # Signed bias is measured minus expected; positive yaw is to the right.
        yaw_errors.append((azimuth(gaze)-azimuth(target)+180) % 360-180)
        pitch_errors.append(elevation(gaze)-elevation(target))
    mean_actual, mean_expected = mean_direction(actual), mean_direction(expected)
    residuals, degrees_of_freedom = [], 0
    for segment in segments:
        if len(segment) < 2:
            continue
        mean = mean_direction([r['rawWorldDirection'] for r in segment])
        if mean is not None:
            residuals.extend(angle(r['rawWorldDirection'], mean)**2 for r in segment)
            degrees_of_freedom += len(segment)-1
    # A circular mean avoids treating -179 and +179 degrees as zero bias.
    sin_yaw = sum(math.sin(math.radians(v)) for v in yaw_errors)
    cos_yaw = sum(math.cos(math.radians(v)) for v in yaw_errors)
    yaw_bias = (math.degrees(math.atan2(sin_yaw, cos_yaw))
                if yaw_errors and math.hypot(sin_yaw, cos_yaw) > 1e-12 else None)
    return dict(mean_direction_offset_deg=angle(mean_actual, mean_expected)
                if mean_actual is not None and mean_expected is not None else None,
                mean_yaw_bias_deg=yaw_bias,
                mean_pitch_bias_deg=statistics.mean(pitch_errors) if pitch_errors else None,
                bias_geometry_samples=len(actual),
                angular_sd_deg=math.sqrt(sum(residuals)/degrees_of_freedom)
                if degrees_of_freedom else None,
                angular_sd_dof=degrees_of_freedom,
                precision_segments=sum(len(s) >= 2 for s in segments))


def trial_quality(rows, max_gap=.1):
    """Search-only quality, without connecting pauses or extrapolating trial ends.

    Durations span adjacent saved search rows with the same trial identity. They
    are observed windows, not authoritative trial completion times. Native sensor
    rate, vendor calibration identity and researcher exclusions stay unknown.
    """
    groups = {}
    previous = None
    last_update = None
    for row in rows:
        trial = row.get('sourceTrialId')
        if not trial or row.get('practice') or row.get('phase') != 'search':
            previous = last_update = None
            continue
        key = (row.get('recordingId'), row.get('participant'), row.get('run'), trial)
        if key not in groups:
            groups[key] = dict(rows=[], updates=[], intervals=[], seconds=0., valid_seconds=0.,
                               devices=set(), reasons=set())
        group = groups[key]
        group['rows'].append(row)
        group['devices'].add(row.get('deviceId'))
        if not row.get('valid'):
            group['reasons'].add(row.get('invalidReason') or 'unspecified_invalid')
        if previous is not None and previous[0] == key:
            before = previous[1]
            dt = row['receiptTime']-before['receiptTime']
            group['seconds'] += dt
            group['valid_seconds'] += valid_interval_seconds(before, dt, max_gap)
        else:
            last_update = None
        if row.get('newInputUpdate'):
            group['updates'].append(row)
            if last_update is not None and row.get('deviceId') == last_update.get('deviceId'):
                dt = row['inputEventTime']-last_update['inputEventTime']
                if dt > 0:
                    group['intervals'].append(dt)
            last_update = row
        previous = (key, row)
    result = []
    for (recording, participant, run, trial), group in groups.items():
        updates, intervals = group['updates'], group['intervals']
        valid_updates = sum(bool(r.get('valid')) for r in updates)
        total, valid = group['seconds'], group['valid_seconds']
        result.append(dict(recording_id=recording, participant_id=participant, run_number=run,
                           trial_id=trial, phase='search', observed_rows=len(group['rows']),
                           input_updates=len(updates), valid_input_updates=valid_updates,
                           valid_observation_fraction=valid_updates/len(updates) if updates else None,
                           observed_search_window_seconds=total, valid_seconds=valid,
                           gap_seconds=max(0, total-valid), valid_time_fraction=valid/total if total else None,
                           input_delivery_hz=1/statistics.mean(intervals) if intervals else None,
                           delivery_interval_p90_ms=1000*percentile(intervals, .9) if intervals else None,
                           device_count=len(group['devices']-{None}),
                           invalid_reasons=';'.join(sorted(group['reasons'])),
                           calibration_id=None, calibration_status='vendor_calibration_id_not_recorded',
                           event_algorithm_version=DETECTOR_VERSION, event_status='unvalidated_candidates',
                           exclusion_reason=None, exclusion_status='not_adjudicated',
                           sensor_rate_conclusion='not_established',
                           time_coverage_status='observed_adjacent_search_rows_no_end_extrapolation'))
    return result


def summarize(rows, settle=.25, max_gap=.1):
    if not rows:
        raise ValueError('No observations')
    if not math.isfinite(settle) or settle < 0 or not math.isfinite(max_gap) or max_gap <= 0:
        raise ValueError('Settling time must be nonnegative and max gap must be positive and finite')
    times = [float(r['receiptTime']) for r in rows]
    if any(b < a for a,b in zip(times,times[1:])):
        raise ValueError('Receipt times are not monotonic')
    updates = [r for r in rows if r.get('newInputUpdate')]
    intervals = [b['inputEventTime']-a['inputEventTime'] for a,b in zip(updates,updates[1:])
                 if b['inputEventTime'] > a['inputEventTime'] and b.get('deviceId') == a.get('deviceId')]
    elapsed = times[-1]-times[0]
    valid_time = 0
    for a,b in zip(rows,rows[1:]):
        dt=b['receiptTime']-a['receiptTime']
        valid_time += valid_interval_seconds(a, dt, max_gap)
    groups=defaultdict(list)
    for r in rows:
        if r.get('validationTargetPresent'):
            groups[(r.get('validationId'),r.get('validationSet'),r.get('targetId'))].append(r)
    spatial=[]
    for (validation,set_name,target), values in groups.items():
        start=values[0]['receiptTime']
        accepted=[r for r in values if r.get('newInputUpdate') and r.get('valid') and r['receiptTime']-start>=settle]
        errors=[r['targetErrorDegrees'] for r in accepted]
        accepted_ids={id(r) for r in accepted}
        differences, segments, current = [], [], []
        previous = None
        for row in values:
            if not row.get('newInputUpdate'):
                if not row.get('valid'):
                    if current: segments.append(current)
                    current, previous = [], None
                continue
            contiguous = previous is not None and (row.get('deviceId') == previous.get('deviceId') and
                0 < row['inputEventTime']-previous['inputEventTime'] <= max_gap)
            if id(row) not in accepted_ids or not contiguous:
                if current: segments.append(current)
                current = []
            if id(row) in accepted_ids:
                if current:
                    value = angle(current[-1]['rawWorldDirection'], row['rawWorldDirection'])
                    if value is not None: differences.append(value)
                current.append(row)
            previous = row
        if current: segments.append(current)
        settled = [r for r in values if r.get('newInputUpdate') and r['receiptTime']-start >= settle]
        settled_rows = [r for r in values if r['receiptTime']-start >= settle]
        observed_seconds = settled_rows[-1]['receiptTime']-settled_rows[0]['receiptTime'] if settled_rows else 0.
        valid_seconds = sum(valid_interval_seconds(a, b['receiptTime']-a['receiptTime'], max_gap)
                            for a,b in zip(settled_rows,settled_rows[1:]))
        spatial.append(dict(validation_id=validation,set=set_name,target=target,
                            samples=len(accepted),recorded_updates=sum(bool(r.get('newInputUpdate')) for r in values),
                            settled_updates=len(settled), invalid_settled_updates=len(settled)-len(accepted),
                            invalid_update_fraction=(len(settled)-len(accepted))/len(settled) if settled else None,
                            observed_settled_seconds=observed_seconds, valid_seconds=valid_seconds,
                            gap_seconds=max(0,observed_seconds-valid_seconds),
                            valid_time_fraction=valid_seconds/observed_seconds if observed_seconds else None,
                            median_error_deg=percentile(errors,.5),p90_error_deg=percentile(errors,.9),
                            mirrored_origin_median_error_deg=percentile([r['mirroredOriginErrorDegrees'] for r in accepted],.5),
                            precision_pairs=len(differences),
                            rms_s2s_deg=math.sqrt(statistics.mean(x*x for x in differences)) if differences else None,
                            **spatial_bias(accepted, segments)))
    return dict(schema=1,report_version='eye-quality-v2-phase-aware',poll_rows=len(rows),input_updates=len(updates),elapsed_seconds=elapsed,
                input_delivery_hz=1/statistics.mean(intervals) if intervals else None,
                delivery_interval_median_ms=1000*percentile(intervals,.5) if intervals else None,
                delivery_interval_p90_ms=1000*percentile(intervals,.9) if intervals else None,
                sensor_rate_conclusion='not_established',source_capture_time_known=False,
                valid_time_fraction=valid_time/elapsed if elapsed else None,
                gap_seconds=elapsed-valid_time,spatial=spatial,trials=trial_quality(rows,max_gap),
                event_status='unvalidated_candidates',pupil_workload_status='not_inferred',
                warning='Input-state timestamps identify deliveries, not distinct hardware observations. '
                        'RMS-S2S is world-ray precision, with head motion retained separately. '
                        'Spatial error is meaningful only when the participant follows the independent dot.')


def evaluate_offset(rows, fit_id, heldout_id, settle=.25):
    """Estimate a constant angular bias on fit targets; evaluate only on another run.

    This diagnostic does not change live gaze. An improvement is not validation approval.
    """
    if fit_id == heldout_id:
        raise ValueError('Fit and held-out validation IDs must differ')
    grouped=defaultdict(list)
    for row in rows:
        if row.get('newInputUpdate') and row.get('validationTargetPresent'):
            grouped[(row.get('validationId'),row.get('targetId'))].append(row)
    def angles(v):
        x,y,z=vector(v)
        return math.degrees(math.atan2(x,z)),math.degrees(math.atan2(y,math.hypot(x,z)))
    def wrap(v):return (v+180)%360-180
    prepared=[]
    for (run,target),group in grouped.items():
        start=group[0]['receiptTime']
        for r in group:
            if not r.get('valid') or r['receiptTime']-start<settle:continue
            expected={k:r['validationTarget'][k]-r['rawWorldOrigin'][k] for k in ('x','y','z')}
            actual=angles(r['rawWorldDirection']);wanted=angles(expected)
            prepared.append((run,target,r,expected,actual,wanted))
    fit=[r for r in prepared if r[0]==fit_id and r[2].get('validationSet')=='fit']
    held=[r for r in prepared if r[0]==heldout_id and r[2].get('validationSet')=='heldout']
    if not fit or not held:
        raise ValueError('Need valid separate fit and heldout targets after the settling window')
    yaw=statistics.median(wrap(r[5][0]-r[4][0]) for r in fit)
    pitch=statistics.median(r[5][1]-r[4][1] for r in fit)
    by_target=defaultdict(list)
    for _,target,r,expected,actual,_ in held:
        az,el=map(math.radians,(actual[0]+yaw,actual[1]+pitch))
        corrected=dict(x=math.cos(el)*math.sin(az),y=math.sin(el),z=math.cos(el)*math.cos(az))
        by_target[target].append((angle(r['rawWorldDirection'],expected),angle(corrected,expected)))
    comparisons=[dict(target=k,raw_median_deg=statistics.median(x[0] for x in v),
                      corrected_median_deg=statistics.median(x[1] for x in v)) for k,v in by_target.items()]
    worsened=[r['target'] for r in comparisons if r['corrected_median_deg']>r['raw_median_deg']]
    return dict(model='constant_world_yaw_pitch_bias_diagnostic',fit_id=fit_id,heldout_id=heldout_id,
                yaw_offset_deg=yaw,pitch_offset_deg=pitch,targets=comparisons,worsened_targets=worsened,
                decision='reject_worsened_regions' if worsened else 'candidate_needs_review',applied_to_runtime=False)


def candidate_events(rows, velocity=60, min_fixation=.1, max_gap=.1, closed=.2):
    """Basic world-ray I-VT candidates + independent bilateral openness candidates.

    Never call invalid tracking a blink, bridge a gap, or mix trial/validation contexts.
    World-ray I-VT must be compared against head-aware alternatives before adoption.
    """
    events=[]
    previous=None
    segment=None
    blink=None
    context=None
    def flush():
        nonlocal segment
        if segment and (segment['kind']!='candidate_fixation' or segment['end']-segment['start']>=min_fixation):
            events.append(segment)
        segment=None
    for row in rows:
        key=(row.get('sourceTrialId'),row.get('validationId'),row.get('targetId'),row.get('phase'),
             row.get('deviceId'))
        if key!=context:
            flush();previous=None;blink=None;context=key
        if not row.get('newInputUpdate'):
            if not row.get('valid'):
                flush();previous=None
                if blink is not None and row['receiptTime']-blink[1]>max_gap:blink=None
            continue
        t=row['inputEventTime']
        # A blink may have invalid gaze, but must have two independently valid openness signals.
        openness_ok=all(row.get(k) for k in ('opennessAvailable','leftOpennessValid','rightOpennessValid'))
        if not openness_ok:
            blink=None
        elif row['leftOpenness']<=closed and row['rightOpenness']<=closed:
            if blink is None: blink=[t,t]
            elif t-blink[1]>max_gap: blink=[t,t]
            else: blink[1]=t
        elif blink is not None:
            if t-blink[1]<=max_gap and .04<=t-blink[0]<=.5:
                events.append(dict(kind='candidate_blink',start=blink[0],end=t,
                                   trial_id=row.get('sourceTrialId'),validation_id=row.get('validationId')))
            blink=None
        if not row.get('valid'):
            flush();previous=None;continue
        if previous is None:
            previous=row;continue
        dt=t-previous['inputEventTime']
        if not 0<dt<=max_gap:
            flush();previous=row;continue
        error=angle(previous['rawWorldDirection'],row['rawWorldDirection'])
        if error is None:
            flush();previous=None;continue
        kind='candidate_saccade' if error/dt>velocity else 'candidate_fixation'
        if segment is None or segment['kind']!=kind:
            flush();segment=dict(kind=kind,start=previous['inputEventTime'],end=t,
                                trial_id=row.get('sourceTrialId'),validation_id=row.get('validationId'))
        else:
            segment['end']=t
        previous=row
    flush()
    return events


def compare_annotations(events, reference, tolerance=.05):
    remaining=set(range(len(reference)))
    matches=[]
    for event in events:
        choices=[i for i in remaining if reference[i]['kind']==event['kind'].removeprefix('candidate_')
                 and abs(float(reference[i]['start'])-event['start'])<=tolerance
                 and abs(float(reference[i]['end'])-event['end'])<=tolerance
                 # Unity serializes absent string IDs as empty cells, while CSV
                 # references normalize them to None. Both mean no such context;
                 # nonempty IDs must still match exactly.
                 and (reference[i].get('trial_id') or None)==(event.get('trial_id') or None)
                 and (reference[i].get('validation_id') or None)==(event.get('validation_id') or None)]
        if choices:
            best=min(choices,key=lambda i:abs(float(reference[i]['start'])-event['start']))
            remaining.remove(best)
            matches.append(abs(float(reference[best]['start'])-event['start']))
    tp=len(matches)
    return dict(true_positive=tp,false_positive=len(events)-tp,false_negative=len(remaining),
                precision=tp/len(events) if events else None,recall=tp/len(reference) if reference else None,
                median_onset_error_seconds=statistics.median(matches) if matches else None,
                status='comparison_only_not_validation_approval')


def csv_rows(path,rows):
    keys=list(dict.fromkeys(k for r in rows for k in r))
    with path.open('w',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=keys);writer.writeheader();writer.writerows(rows)


def flat(row):
    result={}
    for key,value in row.items():
        if isinstance(value,dict):
            for axis,v in value.items():result[key+'_'+axis]=v
        else: result[key]=value
    if not row.get('valid'):
        for prefix in ('worldOrigin_', 'worldDirection_'):
            for key in list(result):
                if key.startswith(prefix): result[key] = None
    if not row.get('poseValid', True):
        for prefix in ('rawLocalOrigin_', 'rawLocalRotation_', 'rawWorldOrigin_', 'rawWorldDirection_'):
            for key in list(result):
                if key.startswith(prefix): result[key] = None
    if not row.get('deviceAvailable', True):
        result['inputEventTime']=result['ageSeconds']=None
    for side in ('left','right'):
        if not row.get(side+'Valid'):
            for key in list(result):
                if key.startswith(side+'Native') or key.startswith(side+'World'): result[key] = None
        if not row.get(side+'PupilValid'): result[side+'PupilMm']=None
        if not row.get(side+'OpennessValid'): result[side+'Openness']=None
    if not row.get('validationTargetPresent') or not row.get('valid'):
        result['targetErrorDegrees']=result['mirroredOriginErrorDegrees']=None
    return result


def read_recording(path):
    """Read a valid recording prefix; recover only an interrupted final JSON line."""
    rows, warnings = [], []
    def reject_constant(value):
        raise ValueError("Non-finite JSON measurement: " + value)
    with path.open() as recording:
        for line_number, line in enumerate(recording, 1):
            if not line.strip():
                continue
            try:
                row = json.loads(line, parse_constant=reject_constant)
            except json.JSONDecodeError:
                if rows and not line.rstrip().endswith('}') and recording.read(1) == '':
                    warnings.append('Interrupted final JSON line omitted; retained prefix only.')
                    break
                raise ValueError(f'Corrupt eye JSON at line {line_number}') from None
            if row.get('schema') != 1 or row.get('kind') != 'gaze_observation':
                raise ValueError(f'Unsupported eye record at line {line_number}')
            if not isinstance(row.get('sequence'), int) or row['sequence'] < 0:
                raise ValueError(f'Invalid observation sequence at line {line_number}')
            if not row.get('recordingId'):
                raise ValueError(f'Missing recording identity at line {line_number}')
            if rows and (row['sequence'] <= rows[-1]['sequence'] or row['recordingId'] != rows[0]['recordingId']):
                raise ValueError(f'Mixed recording identity or unordered sequence at line {line_number}')
            if not isinstance(row.get('receiptTime'), (int,float)) or not math.isfinite(row['receiptTime']) or row['receiptTime'] < 0:
                raise ValueError(f'Invalid receipt timestamp at line {line_number}')
            rows.append(row)
    if not rows:
        raise ValueError('No observations')
    return rows, warnings


def recording_status(path, rows, warnings):
    receipt = path.parent/'end_status.json'
    result = json.loads(receipt.read_text()) if receipt.exists() else dict(complete=False, reason='No clean-close receipt; recording may be active or interrupted')
    # A clean-close marker cannot override an incomplete or mismatched file.
    if warnings or (result.get('acceptedRows') is not None and result['acceptedRows'] != len(rows)):
        result = dict(result, complete=False, reason='Recovered prefix or row-count mismatch')
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('recording',type=Path)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--annotations',type=Path,help='CSV kind,start,end,trial_id,validation_id; same input-event clock')
    parser.add_argument('--fit-id')
    parser.add_argument('--heldout-id')
    parser.add_argument('--velocity',type=float,default=60)
    parser.add_argument('--min-fixation',type=float,default=.1)
    parser.add_argument('--max-gap',type=float,default=.1)
    parser.add_argument('--settle',type=float,default=.25)
    parser.add_argument('--no-plots',action='store_true',help='Export timing/capability tables without matplotlib figures')
    args=parser.parse_args()
    rows, warnings = read_recording(args.recording)
    report=summarize(rows,settle=args.settle,max_gap=args.max_gap)
    report['recording_warnings'] = warnings
    report['recording_end_status'] = recording_status(args.recording, rows, warnings)
    events=candidate_events(rows,velocity=args.velocity,min_fixation=args.min_fixation,max_gap=args.max_gap)
    report['detector']=dict(version=DETECTOR_VERSION,velocity=args.velocity,
                            min_fixation=args.min_fixation,max_gap=args.max_gap,settle=args.settle,
                            thresholds_validated=False)
    if args.fit_id or args.heldout_id:
        if not args.fit_id or not args.heldout_id:parser.error('Both --fit-id and --heldout-id are required')
        report['offset_evaluation']=evaluate_offset(rows,args.fit_id,args.heldout_id,args.settle)
    if args.annotations:
        with args.annotations.open(newline='') as f:
            reference=list(csv.DictReader(f))
        for r in reference:
            for k in ('trial_id','validation_id'):r[k]=r.get(k) or None
        report['annotation_comparison']=compare_annotations(events,reference)
        report['annotation_comparison_by_kind']={kind:compare_annotations(
            [e for e in events if e['kind']=='candidate_'+kind],
            [r for r in reference if r['kind']==kind]) for kind in ('fixation','saccade','blink')}
    args.out.mkdir(parents=True,exist_ok=False)
    (args.out/'quality_report.json').write_text(json.dumps(report,indent=2,allow_nan=False))
    csv_rows(args.out/'gaze_observations.csv',[flat(r) for r in rows])
    csv_rows(args.out/'candidate_eye_events.csv',events)
    csv_rows(args.out/'spatial_validation.csv',report['spatial'])
    csv_rows(args.out/'trial_eye_quality.csv',report['trials'])
    from gaze_timing import write_outputs
    write_outputs(args.out, rows, args.recording, report['recording_end_status'], plot=not args.no_plots)
    print(f'Wrote {args.out}; sensor 120 Hz remains NOT ESTABLISHED; events are unvalidated candidates.')


if __name__=='__main__': main()
