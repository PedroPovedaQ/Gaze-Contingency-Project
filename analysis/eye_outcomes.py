"""Exploratory HTC eye outcomes. Query-clock candidates, never validated physiology.

No interpolation across trials, invalid observations or >25 ms query gaps.
I-DT uses elapsed time (not a sample-count surrogate). Pupil processing follows
Duchowski et al. 2018, with explicit nearest-neighbour maxima and roundoff floor.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import importlib.metadata
from dataclasses import asdict, dataclass
from pathlib import Path

import numpy as np
import pywt
from scipy.spatial.transform import Rotation

from eye_quality import read_recording, compare_annotations
from eye_timing import stamp, usable_stamp, clock_identity

VERSION = 'htc-eye-candidates-v3-stream-dedup-target-aoi'


@dataclass(frozen=True)
class Config:
    max_gap_s: float = .025
    fixation_min_s: float = .083
    dispersion_deg: float = 1.8
    saccade_speed_deg_s: float = 60.
    saccade_min_s: float = .020
    saccade_max_s: float = .100
    head_speed_max_deg_s: float = 20.
    invalid_padding_s: float = .200
    pupil_min_segment_s: float = 2.
    pupil_min_hz: float = 60.
    pupil_max_p95_relative_jitter: float = .5


def vec(row, name, axes='xyz'):
    try:
        value = np.array([row[name][a] for a in axes], dtype=float)
        return value if np.all(np.isfinite(value)) and np.linalg.norm(value) > 0 else None
    except (KeyError, TypeError, ValueError):
        return None


def angular_speed(vectors, times):
    unit = vectors / np.linalg.norm(vectors, axis=1)[:, None]
    angles = np.rad2deg(np.arccos(np.clip(np.sum(unit[1:] * unit[:-1], axis=1), -1, 1)))
    return angles / np.diff(times)


def idt(times, vectors, config):
    """Greedy dispersion detector; sum of horizontal/vertical ranges in degrees.

    Unwrap azimuth within a segment so crossing +/-180 is not a 360-degree jump.
    Output inclusive sample indices; minimum duration is endpoint elapsed time.
    """
    yaw = np.unwrap(np.arctan2(vectors[:, 0], vectors[:, 2]))
    pitch = np.arctan2(vectors[:, 1], np.hypot(vectors[:, 0], vectors[:, 2]))
    angles = np.rad2deg(np.column_stack((yaw, pitch)))
    events, start = [], 0
    while start < len(times):
        end = int(np.searchsorted(times, times[start] + config.fixation_min_s - 1e-10))
        if end >= len(times):
            break
        if np.ptp(angles[start:end+1], axis=0).sum() > config.dispersion_deg:
            start += 1
            continue
        while end + 1 < len(times) and np.ptp(angles[start:end+2], axis=0).sum() <= config.dispersion_deg:
            end += 1
        events.append((start, end))
        start = end + 1
    return events


def true_runs(mask):
    edges = np.diff(np.r_[False, mask, False].astype(int))
    return list(zip(np.flatnonzero(edges == 1), np.flatnonzero(edges == -1)))


def ipa(values, duration):
    """Sym16 level-2 detail modulus maxima / valid segment seconds.

    Uses nearest neighbours at all interior positions (text definition), rather
    than Listing 2's apparent penultimate-index typo. Tiny numerical residuals
    below a scale-aware floating-point floor are suppressed. Not an ICA score.
    """
    values = np.asarray(values, dtype=float)
    if duration <= 0 or not np.all(np.isfinite(values)) or len(values) < 124:
        raise ValueError('IPA requires finite values, positive duration and >=124 samples')
    detail = pywt.wavedec(values, 'sym16', mode='periodization', level=2)[1] / 2.
    magnitude = np.abs(detail)
    left, right = np.r_[magnitude[0], magnitude[:-1]], np.r_[magnitude[1:], magnitude[-1]]
    maxima = np.where((magnitude >= left) & (magnitude >= right) &
                      ((magnitude > left) | (magnitude > right)), magnitude, 0.)
    floor = 1e-10 * max(1., float(np.max(np.abs(values))))
    maxima[maxima <= floor] = 0.
    threshold = float(np.std(maxima) * np.sqrt(2 * np.log2(len(maxima))))
    count = int(np.count_nonzero((maxima >= threshold) & (maxima > 0)))
    return count, count / duration


def fresh_search_segments(rows, config, stream='gaze'):
    """Deduplicate HTC query receipts globally; cached rows do not become samples.

    A query taken before reveal is not carried into search. Context boundaries,
    invalid/focus/device loss, and nonmonotonic time all break continuity.
    """
    segments, segment, previous_key = [], [], None
    last_query = None
    last_native, native_identity = None, None
    for row in rows:
        native = stamp(row,stream)
        identity = clock_identity(row,native) if native is not None else None
        key = (row.get('recordingId'), row.get('participant'), row.get('run'),
               row.get('sourceTrialId'), row.get('phase'), row.get('practice'), row.get('deviceId'), identity)
        t = row.get('htcQueryReceiptTime')
        usable = (row.get('phase') == 'search' and row.get('sourceTrialId') and
                  row.get('valid') and row.get('focused') and row.get('deviceAvailable') and
                  row.get('htcGazeAvailable' if stream=='gaze' else 'pupilAvailable') and isinstance(t, (int, float)) and t > 0 and
                  (native is None or usable_stamp(native)) and
                  abs(row['receiptTime'] - t) <= config.max_gap_s)
        if key != previous_key or not usable:
            if segment:
                segments.append(segment)
            segment = []
        previous_key = key
        if not isinstance(t, (int, float)) or t <= 0:
            continue
        if last_query is not None and t == last_query:
            continue
        backwards = last_query is not None and t < last_query
        last_query = t
        if native is not None:
            if identity != native_identity:
                last_native, native_identity = None, identity
            if usable_stamp(native):
                native_time = native['returnedTime']
                if last_native == native_time:
                    continue
                backwards |= last_native is not None and native_time < last_native
                # A large source-time gap breaks the segment even if query delivery is regular.
                if last_native is not None and (native_time-last_native)/1e9 > config.max_gap_s:
                    if segment: segments.append(segment)
                    segment = []
                last_native = native_time
            else:
                last_native = None
        if not usable or backwards:
            if segment:
                segments.append(segment)
            segment = []
            continue
        if segment and t - segment[-1]['htcQueryReceiptTime'] > config.max_gap_s:
            segments.append(segment)
            segment = []
        segment.append(row)
    if segment:
        segments.append(segment)
    return segments


def eye_segments(segment, side, config, pupil=False, excluded_times=()):
    times = np.array([r['htcQueryReceiptTime'] for r in segment])
    if pupil:
        valid = np.array([bool(r.get('pupilAvailable') and r.get(side+'PupilValid')) and
                          isinstance(r.get(side+'PupilMm'), (int, float)) and
                          math.isfinite(r[side+'PupilMm']) and 1 <= r[side+'PupilMm'] <= 10
                          for r in segment])
    else:
        valid = np.array([bool(r.get(side+'Valid')) and vec(r, side+'WorldDirection') is not None
                          for r in segment])
    # Closed eye or unavailable pupil/gaze is excluded, never filled by interpolation.
    closed = np.array([bool(r.get('opennessAvailable') and r.get(side+'OpennessValid')) and
                       r.get(side+'Openness', 1) < .2 for r in segment])
    bad_times = np.r_[times[~valid | closed], excluded_times]
    for t in bad_times:
        valid[np.abs(times - t) <= config.invalid_padding_s] = False
    for start, end in true_runs(valid):
        if end - start >= 2:
            yield segment[start:end]


def pupil_screen(times, config):
    """Return all failed checks, before any interpolation; stable ordered reasons."""
    times = np.asarray(times, dtype=float)
    dt = np.diff(times)
    if len(dt) == 0 or not np.all(np.isfinite(times)) or np.any(dt <= 0):
        return dict(reasons=['invalid_timing'], duration_s=0., median_query_hz=None,
                    p95_relative_jitter=None, grid_points=0), np.array([])
    median = float(np.median(dt))
    duration = float(times[-1] - times[0])
    jitter = float(np.percentile(np.abs(dt-median)/median, 95))
    grid = np.arange(times[0], times[-1], median)
    reasons = []
    if duration < config.pupil_min_segment_s: reasons.append('short_segment')
    if 1/median < config.pupil_min_hz: reasons.append('low_query_rate')
    if jitter > config.pupil_max_p95_relative_jitter: reasons.append('irregular_query_timing')
    if len(grid) < 124: reasons.append('insufficient_wavelet_points')
    return dict(reasons=reasons, duration_s=duration, median_query_hz=1/median,
                p95_relative_jitter=jitter, grid_points=len(grid)), grid


def analyze(rows, config=Config(), pupil_diagnostics=None):
    groups, events = {}, []
    for row in rows:
        if row.get('phase') != 'search' or not row.get('sourceTrialId'):
            continue
        key = (row['recordingId'], row.get('participant'), row.get('run'), row['sourceTrialId'])
        for side in ('left', 'right'):
            groups.setdefault((*key, side), dict(recording_id=key[0], participant=key[1], run=key[2],
                trial_id=key[3], eye=side, voice=row.get('voice'), practice=row.get('practice'),
                query_count=0, direction_seconds=0., saccade_eligible_seconds=0.,
                fixation_count=0, fixation_eligible_seconds=0., saccade_count=0, pupil_eligible_seconds=0., ipa_extrema=0,
                pupil_segments_accepted=0, pupil_segments_rejected=0))
    # Preserve invalid intervals dropped by the shared-stream segmenter for edge padding.
    invalid = {}
    for row in rows:
        trial = row.get('sourceTrialId')
        t = row.get('htcQueryReceiptTime')
        if not trial or not isinstance(t, (int, float)) or t <= 0:
            continue
        for side in ('left', 'right'):
            if not row.get('valid') or not row.get('focused') or not row.get('deviceAvailable'):
                invalid.setdefault((trial, side), []).append(t)
    for segment in fresh_search_segments(rows, config):
        first = segment[0]
        key = (first['recordingId'], first.get('participant'), first.get('run'), first['sourceTrialId'])
        for side in ('left', 'right'):
            group = groups[(*key, side)]
            group['query_count'] += len(segment)
            for samples in eye_segments(segment, side, config, excluded_times=invalid.get((key[3], side), ())):
                times = np.array([r['htcQueryReceiptTime'] for r in samples])
                directions = np.array([vec(r, side+'WorldDirection') for r in samples])
                duration = float(times[-1] - times[0])
                group['direction_seconds'] += duration
                if duration >= config.fixation_min_s:
                    group['fixation_eligible_seconds'] += duration
                def add(kind, start, end):
                    event = dict(recording_id=key[0], trial_id=key[3], eye=side,
                        kind='candidate_'+kind, start=float(times[start]), end=float(times[end]),
                        duration_s=float(times[end]-times[start]), validated=False,
                        target_id='', target_aoi_fraction=None, boundary_censored=start==0 or end==len(times)-1)
                    if kind=='fixation':
                        window=samples[start:end+1]
                        targets={r.get('taskTargetId') for r in window}
                        if len(targets)==1 and all(r.get('taskAoiAvailable') is True and r.get('taskTargetId') for r in window):
                            event['target_id']=next(iter(targets))
                            event['target_aoi_fraction']=sum(r.get(side+'HitObjectId')==event['target_id'] for r in window)/len(window)
                    events.append(event)
                fixations = idt(times, directions, config)
                group['fixation_count'] += len(fixations)
                for start, end in fixations:
                    add('fixation', start, end)
                head_fields=['htcHeadWorldRotation' if r.get('htcTiming') is not None else 'headRotation' for r in samples]
                head_ok = np.array([bool(r.get('htcHeadPoseValid' if r.get('htcTiming') is not None else 'headValid')) and
                                    vec(r, field, 'xyzw') is not None for r,field in zip(samples,head_fields)])
                # No missing head-pose replacement, even for a stationary-looking ray.
                for begin, finish in true_runs(head_ok):
                    if finish - begin < 2:
                        continue
                    q = Rotation.from_quat([vec(r,field,'xyzw') for r,field in zip(samples[begin:finish],head_fields[begin:finish])])
                    local = q.inv().apply(directions[begin:finish])
                    dt = np.diff(times[begin:finish])
                    head_speed = np.rad2deg((q[:-1].inv()*q[1:]).magnitude()) / dt
                    eligible = head_speed <= config.head_speed_max_deg_s
                    group['saccade_eligible_seconds'] += float(dt[eligible].sum())
                    fast = angular_speed(local, times[begin:finish]) >= config.saccade_speed_deg_s
                    for a, b in true_runs(fast & eligible):
                        elapsed = times[begin+b] - times[begin+a]
                        # Require observed below-threshold bracketing, not segment-edge guesses.
                        if b-a < 2 or a == 0 or b == len(fast) or not eligible[a-1] or not eligible[b]:
                            continue
                        if config.saccade_min_s - 1e-10 <= elapsed <= config.saccade_max_s + 1e-10:
                            group['saccade_count'] += 1
                            add('saccade', begin+a, begin+b)
    # Pupil acquisition is independent of gaze pose requests: do not discard a new
    # pupil observation just because the gaze timestamp repeats (or vice versa).
    for segment in fresh_search_segments(rows, config, stream='pupil'):
        first=segment[0]
        key=(first['recordingId'],first.get('participant'),first.get('run'),first['sourceTrialId'])
        for side in ('left','right'):
            group=groups[(*key,side)]
            for samples in eye_segments(segment, side, config, pupil=True, excluded_times=invalid.get((key[3], side), ())):
                native=stamp(samples[0],'pupil')
                if native is not None:
                    origin=native['returnedTime']
                    times=np.array([(stamp(r,'pupil')['returnedTime']-origin)/1e9 for r in samples])
                else:
                    times = np.array([r['htcQueryReceiptTime'] for r in samples])
                screen, grid = pupil_screen(times, config)
                if pupil_diagnostics is not None:
                    pupil_diagnostics.append(dict(recording_id=key[0], trial_id=key[3], eye=side,
                        start=samples[0]['htcQueryReceiptTime'], end=samples[-1]['htcQueryReceiptTime'], samples=len(samples),
                        processing_clock='runtime_pupil_capture_ns' if native is not None else 'legacy_query_receipt',
                        first_failure=screen['reasons'][0] if screen['reasons'] else 'accepted',
                        failed_checks=';'.join(screen['reasons']),
                        **{k:v for k,v in screen.items() if k != 'reasons'}))
                if screen['reasons']:
                    group['pupil_segments_rejected'] += 1
                    continue
                values = np.interp(grid, times, [r[side+'PupilMm'] for r in samples])
                analyzed_duration = float(grid[-1]-grid[0])
                count, _ = ipa(values, analyzed_duration)
                group['ipa_extrema'] += count
                group['pupil_eligible_seconds'] += analyzed_duration
                group['pupil_segments_accepted'] += 1
    output = []
    for group in groups.values():
        target_events=[e for e in events if e['recording_id']==group['recording_id'] and
                       e['trial_id']==group['trial_id'] and e['eye']==group['eye'] and
                       e['kind']=='candidate_fixation' and e['target_aoi_fraction']==1.]
        last_target=max(target_events,key=lambda e:e['end']) if target_events else None
        group['last_target_candidate_s']=(last_target['duration_s'] if last_target and not last_target['boundary_censored'] else None)
        group['last_target_candidate_status']=('boundary_censored' if last_target and last_target['boundary_censored'] else
                                              'candidate_all_samples_on_target' if last_target else 'no_complete_target_candidate')
        group['saccade_frequency_hz'] = (group['saccade_count']/group['saccade_eligible_seconds']
                                         if group['saccade_eligible_seconds'] else None)
        group['ipa_candidate_hz'] = (group['ipa_extrema']/group['pupil_eligible_seconds']
                                     if group['pupil_eligible_seconds'] else None)
        if not group['fixation_eligible_seconds']:
            group['fixation_count'] = None
        if not group['saccade_eligible_seconds']:
            group['saccade_count'] = None
        group['status'] = 'exploratory_candidate_not_validated'
        group['clock'] = 'gaze_duration=query_receipt;pupil=runtime_capture_when_present_else_query;neither_sensor_validated'
        output.append(group)
    return output, events


def compare_reviewed_windows(events, reference, windows, recording_id):
    """Only score wholly observed events in explicitly reviewed time windows."""
    for row in [*reference, *windows]:
        if row.get('recording_id') != recording_id or row.get('eye') not in ('left', 'right'):
            raise ValueError('Annotation recording/eye mismatch')
        row['start'], row['end'] = float(row['start']), float(row['end'])
        if not math.isfinite(row['start']) or not math.isfinite(row['end']) or row['end'] <= row['start']:
            raise ValueError('Invalid annotation interval')
    def covered(event):
        return any(w['eye']==event['eye'] and w['trial_id']==event['trial_id'] and
                   w['start']<=event['start'] and event['end']<=w['end'] for w in windows)
    if any(r.get('kind') not in ('fixation','saccade') or not covered(r) for r in reference):
        raise ValueError('Reference labels must be known events wholly inside reviewed windows')
    selected = [e for e in events if covered(e)]
    return dict(status='comparison_only_not_validation_approval',
        unscored_candidate_count=len(events)-len(selected), tolerance_seconds=.05,
        by_eye_and_kind={side+'_'+kind: compare_annotations(
            [e for e in selected if e['eye']==side and e['kind']=='candidate_'+kind],
            [r for r in reference if r['eye']==side and r['kind']==kind])
            for side in ('left','right') for kind in ('fixation','saccade')})


def write_csv(path, rows, fields=None):
    with path.open('w', newline='') as file:
        writer = csv.DictWriter(file, fieldnames=fields or list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def plot(rows, out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    trials = list(dict.fromkeys(r['trial_id'] for r in rows))
    fig, axes = plt.subplots(2, 2, figsize=(10, 6.4), constrained_layout=True)
    for ax, field, title, unit in zip(axes.flat,
            ('fixation_count', 'saccade_frequency_hz', 'ipa_candidate_hz', 'pupil_eligible_seconds'),
            ('(a) Fixation Count (candidate)', '(b) Saccade Frequency (restricted)',
             '(c) Index of Pupillary Activity (exploratory)', '(d) Usable Pupil Duration'),
            ('Fixation count', 'Saccade frequency [events/s]', 'IPA [extrema/s]', 'Usable pupil duration [s]')):
        for side, color, marker, offset in [('left', '#a36c00', 'o', -.08), ('right', '#24679e', 's', .08)]:
            selected = [r for r in rows if r['eye'] == side]
            x = [trials.index(r['trial_id'])+offset for r in selected]
            y = [r[field] if r[field] is not None else np.nan for r in selected]
            ax.scatter(x, y, color=color, marker=marker, label=side.title()+' eye', s=32)
            for xpos, value in zip(x, y):
                if not np.isfinite(value):
                    ax.text(xpos, .04, 'N/A', transform=ax.get_xaxis_transform(), rotation=90, fontsize=7)
        ax.set_xticks(range(len(trials)), [t.rsplit('_', 1)[-1] for t in trials])
        ax.set_xlabel('Recorded trial ID suffix')
        ax.set_ylabel(unit)
        ax.set_title(title, loc='left', fontsize=10)
        ax.set_ylim(bottom=0)
        ax.grid(axis='y', alpha=.2)
        ax.spines[['top', 'right']].set_visible(False)
    axes[0, 0].legend(frameon=False, fontsize=8)
    fig.suptitle('Recorded development data — query-clock candidates, detector validation pending', fontsize=11)
    fig.savefig(out/'eye-candidates.pdf')
    fig.savefig(out/'eye-candidates.png', dpi=180)
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('recording', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--plot', action='store_true')
    parser.add_argument('--review-traces', action='store_true', help='Export unlabeled raw traces and blank independent-review templates')
    parser.add_argument('--sensitivity', action='store_true', help='Report exploratory threshold variants without choosing an optimum')
    parser.add_argument('--annotations', type=Path, help='Independent labels: eye,kind,start,end,trial_id,recording_id; query clock seconds')
    parser.add_argument('--annotation-windows', type=Path, help='Exhaustively reviewed intervals: recording_id,eye,trial_id,start,end')
    args = parser.parse_args()
    if bool(args.annotations) != bool(args.annotation_windows):
        parser.error('--annotations and --annotation-windows are required together')
    before = hashlib.sha256(args.recording.read_bytes()).hexdigest()
    rows, warnings = read_recording(args.recording)
    if hashlib.sha256(args.recording.read_bytes()).hexdigest() != before:
        raise RuntimeError('Recording changed during read; retry on a stable copied snapshot')
    config = Config()
    pupil_diagnostics = []
    metrics, events = analyze(rows, config, pupil_diagnostics)
    args.out.mkdir(parents=True, exist_ok=True)
    if pupil_diagnostics:
        write_csv(args.out/'pupil_segments.csv', pupil_diagnostics)
    if metrics:
        write_csv(args.out/'eye_metrics.csv', metrics)
    write_csv(args.out/'candidate_eye_events.csv', events,
              ['recording_id','trial_id','eye','kind','start','end','duration_s','validated'])
    report = dict(version=VERSION, analysis_code_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), source=str(args.recording.resolve()), sha256=before,
        rows=len(rows), warnings=warnings, config=asdict(config),
        validated=False, physiological_validation='pending_independent_labels_and_headset_validation',
        timing='Software query receipts; distinct query timestamps do not prove distinct sensor observations or 120 Hz.',
        ipa_method='sym16 level2 periodization; median-interval within-segment resampling; nearest-neighbour modulus maxima; numeric floor 1e-10*max(1,abs(pupil)); universal hard threshold',
        interpretation='Development diagnostics only; no microsaccades, last target fixation, workload or voice-condition effect established.',
        versions={name: importlib.metadata.version(name) for name in ('numpy', 'scipy', 'PyWavelets', 'matplotlib')}, metrics=metrics)
    from pupil_diagnostics import audit, plot_quality
    report['pupil_quality'] = audit(rows, config, pupil_diagnostics, metrics)
    (args.out/'pupil_quality.json').write_text(json.dumps(report['pupil_quality'], indent=2, allow_nan=False)+'\n')
    if args.plot:
        plot_quality(report['pupil_quality'], args.out)
    if args.review_traces:
        from pupil_diagnostics import export_review_traces
        export_review_traces(rows, config, args.out/'independent-review')
    if args.sensitivity:
        from dataclasses import replace
        sensitivity = []
        for label, variant in [('narrow', replace(config, dispersion_deg=1., fixation_min_s=.100, saccade_speed_deg_s=80.)),
                               ('default', config),
                               ('broad', replace(config, dispersion_deg=2.5, fixation_min_s=.060, saccade_speed_deg_s=40.))]:
            measures = metrics if label == 'default' else analyze(rows, variant)[0]
            for item in measures:
                sensitivity.append(dict(variant=label, trial_id=item['trial_id'], eye=item['eye'],
                    fixation_count=item['fixation_count'], saccade_frequency_hz=item['saccade_frequency_hz'],
                    dispersion_deg=variant.dispersion_deg, fixation_min_s=variant.fixation_min_s,
                    saccade_speed_deg_s=variant.saccade_speed_deg_s))
        if sensitivity:
            write_csv(args.out/'threshold_sensitivity.csv', sensitivity)
        report['threshold_sensitivity'] = 'Three illustrative joint parameter variants; not a validation or automatic parameter selection.'
    if args.annotations:
        with args.annotations.open(newline='') as file:
            reference = list(csv.DictReader(file))
        with args.annotation_windows.open(newline='') as file:
            windows = list(csv.DictReader(file))
        report['annotation_comparison'] = compare_reviewed_windows(events, reference, windows, rows[0]['recordingId'])
    (args.out/'report.json').write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
    if args.plot and metrics:
        plot(metrics, args.out)
    print(json.dumps(dict(out=str(args.out), trials=len(metrics)//2, events=len(events), validated=False)))


if __name__ == '__main__':
    main()
