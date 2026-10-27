"""Reproducible four-panel development report; never certifies physiological validity.

Reads explicitly selected frozen runs and raw eye files. Raw query-clock candidates
are separate from validated outcomes. No automatic approval or threshold tuning.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from dataclasses import asdict
import hashlib
import importlib.metadata
import json
import math
from pathlib import Path
import csv
import sys

import numpy as np

from eye_outcomes import Config, VERSION, analyze, write_csv
from eye_quality import angle, read_recording, recording_status
from thesis_voice_condition_plots import condition, CONDITIONS, COLORS
from tlx_scoring import score_tlx, DIMENSIONS

VERSION_REPORT = 'voice-eye-measures-v1'
EYE_GATES = ['independent_detector_validation_not_established',
             'task_wide_spatial_accuracy_and_drift_acceptance_not_established',
             'sensor_capture_clock_and_sampling_independence_not_established',
             'prespecified_trial_coverage_and_exclusion_policy_not_established']


def finite_number(value):
    return type(value) in (int, float) and math.isfinite(value)


def build_trials(summaries, metrics):
    trials = {}
    for summary in summaries:
        for obj in summary.get('objectives', []):
            start = obj.get('search_started_at')
            if obj.get('is_practice') or obj.get('practice') or not finite_number(start) or start < 0:
                continue
            tid = obj['trial_id']
            if not tid or tid in trials:
                raise ValueError('Missing/duplicate trial identity: ' + str(tid))
            duration = obj.get('search_exposure_seconds')
            if duration is not None and (not finite_number(duration) or duration < 0):
                raise ValueError('Invalid active search duration: ' + tid)
            outcome = obj.get('outcome')
            resolved = (outcome == 'completed' and obj.get('completed') is True) or outcome == 'timeout'
            trials[tid] = dict(trial_id=tid, participant=summary['participant_id'],
                run=summary['run_number'], block=obj.get('block'),
                condition=condition(obj.get('voice_condition', summary.get('voice_condition'))),
                session_mode=summary.get('session_mode', 'unknown'),
                session_outcome=summary.get('session_outcome', 'unknown'), outcome=outcome,
                active_search_seconds=duration, plot_eligible=resolved,
                candidate_fixation_count_mean_per_eye=None, candidate_saccade_hz_mean_per_eye=None,
                fixation_coverage_min_eye=None, saccade_coverage_min_eye=None,
                last_target_fixation_s=None, last_fixation_status='unavailable_no_validated_target_AOI_event_join',
                eye_research_eligible=False, eye_measurement_status='no_raw_eye_metrics',
                recording_id='', recording_complete=None,
                research_gate_reasons=';'.join(EYE_GATES))
    matched = defaultdict(dict)
    for m in metrics:
        tid = m['trial_id']
        if tid not in trials:
            continue
        t = trials[tid]
        if (m.get('participant') != t['participant'] or m.get('run') != t['run'] or
                condition(m.get('voice')) != t['condition'] or m.get('practice') is not False):
            raise ValueError('Eye/summary identity or condition mismatch: ' + tid)
        side = m.get('eye')
        if side not in ('left', 'right') or side in matched[tid]:
            raise ValueError('Duplicate/unknown trial eye; reconcile recordings explicitly: ' + tid)
        matched[tid][side] = m
    for tid, eyes in matched.items():
        t = trials[tid]
        t['eye_measurement_status'] = 'exploratory_query_clock_candidates_not_validated'
        if set(eyes) != {'left', 'right'}:
            t['eye_measurement_status'] = 'unavailable_binocular_pair_incomplete'
            continue
        if eyes['left']['recording_id'] != eyes['right']['recording_id']:
            raise ValueError('Eyes belong to different recordings: ' + tid)
        t['recording_id'] = eyes['left']['recording_id']
        for prefix, field in [('fixation', 'candidate_fixation_count_mean_per_eye'),
                              ('saccade', 'candidate_saccade_hz_mean_per_eye')]:
            values, coverages = [], []
            for m in eyes.values():
                count, seconds = m.get(prefix+'_count'), m.get(prefix+'_eligible_seconds')
                if count is None or seconds is None:
                    continue
                if (not finite_number(count) or count < 0 or count != int(count) or
                        not finite_number(seconds) or seconds < 0):
                    raise ValueError('Invalid count or eligible duration: ' + tid)
                if seconds <= 0:
                    continue
                values.append(count if prefix == 'fixation' else count/seconds)
                duration = t['active_search_seconds']
                if duration is not None and duration > 0:
                    coverages.append(seconds/duration)
            if len(values) == 2:
                t[field] = sum(values)/2
            if len(coverages) == 2:
                t[prefix+'_coverage_min_eye'] = min(coverages)
                if max(coverages) > 1.01:
                    # Different clocks/exposure or a broken join must never be silently clamped.
                    t['plot_eligible'] = False
                    t['eye_measurement_status'] = 'inconsistent_eligible_time_exceeds_search_duration'
    return list(trials.values())


def score_surveys(responses, trials):
    blocks = defaultdict(set)
    for t in trials:
        blocks[(t['participant'], t['run'], t['block'])].add(t['condition'])
    unique = {}
    for r in responses:
        # Never infer a block from row order, trial count or a run-wide score.
        participant = r.get('participant_id')
        try:
            run, block = int(str(r['run_number'])), int(str(r['block']))
        except (KeyError, TypeError, ValueError):
            raise ValueError('Survey requires explicit integer run_number and block') from None
        if not participant or run < 0 or block not in (0, 1):
            raise ValueError('Invalid survey identity')
        key = (participant, run, block)
        voice = r.get('voice_condition') or str(r.get('condition', '')).split('_voice-')[-1]
        label = condition(voice)
        # Same block responses repeated across flat trial rows are not independent ratings.
        signature = tuple(str(r.get(k, '')) for k in (*DIMENSIONS, 'scale_min', 'scale_max',
                          'performance_direction', 'response_id')) + (label,)
        if key in unique:
            if unique[key][0] != signature:
                raise ValueError('Conflicting responses for one participant/run/block')
            continue
        score = score_tlx(r)
        joined = blocks.get(key) == {label}
        value = dict(participant=participant, run=run, block=block, condition=label,
            response_id=r.get('response_id', ''), **{k: r.get(k) for k in DIMENSIONS}, **score,
            join_status='matched_block' if joined else 'no_matching_block',
            plot_eligible=joined and score['nasa_tlx_scoring_status'] == 'scored_all_six_dimensions')
        unique[key] = (signature, value)
    return [v for _, v in unique.values()]


def spatial_diagnostics(rows, config):
    """Per-eye known-target errors. Quick alignment is not held-out validation."""
    groups = defaultdict(list)
    for row in rows:
        if row.get('validationTargetPresent') and row.get('validationId') and row.get('targetId'):
            groups[(row['recordingId'], row['validationId'], row.get('validationSet'), row['targetId'])].append(row)
    result = []
    for (rid, vid, subset, target), group in groups.items():
        start = group[0]['receiptTime']
        for side in ('left', 'right'):
            errors, changes, seen = [], [], set()
            previous = None
            for r in group:
                t = r.get('htcQueryReceiptTime')
                if not finite_number(t) or t <= 0 or t in seen:
                    if not r.get('valid') or not r.get('focused') or not r.get(side+'Valid'):
                        previous = None
                    continue
                seen.add(t)
                valid = (r.get('valid') and r.get('focused') and r.get('deviceAvailable') and
                         r.get('htcGazeAvailable') and r.get(side+'Valid') and
                         abs(r['receiptTime']-t) <= config.max_gap_s and r['receiptTime']-start >= .25)
                if not valid:
                    previous = None
                    continue
                try:
                    expected = {a: r['validationTarget'][a]-r[side+'WorldOrigin'][a] for a in 'xyz'}
                    d = r[side+'WorldDirection']
                    error = angle(d, expected)
                except (KeyError, TypeError, ValueError):
                    error = None
                if error is None:
                    previous = None
                    continue
                errors.append(error)
                if previous and 0 < t-previous[0] <= config.max_gap_s:
                    change = angle(previous[1], d)
                    if change is not None:
                        changes.append(change**2)
                previous = (t, d)
            result.append(dict(recording_id=rid, validation_id=vid, validation_set=subset,
                target_id=target, eye=side, queries=len(errors),
                median_error_deg=float(np.median(errors)) if errors else None,
                p90_error_deg=float(np.percentile(errors, 90)) if errors else None,
                rms_successive_ray_change_deg=math.sqrt(sum(changes)/len(changes)) if changes else None,
                acceptance='not_established', note='Includes head motion; not fixation precision or approval'))
    return result


def plot(trials, surveys, out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 11, 'pdf.fonttype': 42,
                         'axes.spines.top': False, 'axes.spines.right': False})
    fig, axs = plt.subplots(2, 2, figsize=(11, 8))
    panels = [
        ('(a) Last target fixation duration', 'Duration (s)', 'last_target_fixation_s', trials),
        ('(b) Candidate fixation count', 'Mean count per eye in usable intervals', 'candidate_fixation_count_mean_per_eye', trials),
        ('(c) Candidate saccade frequency', 'Mean per-eye rate (events / eligible s)', 'candidate_saccade_hz_mean_per_eye', trials),
        ('(d) Perceived workload · Raw TLX', 'Unweighted mean, rescaled to 0–100', 'nasa_tlx_score_0_100', surveys)]
    for ax, (title, unit, field, rows) in zip(axs.flat, panels):
        ax.set_title(title, loc='left', fontsize=13, pad=18)
        ax.set_ylabel(unit, fontsize=10)
        ax.set_xticks([1, 2], ['Neutral\nvoice', 'Self-similar\nvoice'])
        ax.set_xlim(.5, 2.5)
        ax.grid(axis='y', alpha=.15); ax.set_axisbelow(True)
        for x, (label, color) in enumerate(zip(CONDITIONS, COLORS), 1):
            eligible = [r for r in rows if r['condition'] == label and r.get('plot_eligible') and r.get(field) is not None]
            values = [r[field] for r in eligible]
            if not values:
                message = ('Unavailable\nTarget-fixation validation\nand AOI join pending' if field == 'last_target_fixation_s'
                           else 'No scored\nblock responses' if field == 'nasa_tlx_score_0_100' else 'No eligible\nrecorded measurements')
                ax.text(x, .48, message, transform=ax.get_xaxis_transform(), ha='center', va='center', fontsize=10, color='#626970')
                continue
            if len(values) >= 3:
                bp = ax.boxplot([values], positions=[x], widths=.34, showfliers=False,
                    patch_artist=True, manage_ticks=False, medianprops={'color':'#222'})
                bp['boxes'][0].set_facecolor(color); bp['boxes'][0].set_alpha(.5)
            offsets = np.linspace(-.08, .08, len(values)) if len(values)>1 else [0]
            ax.scatter(x+np.array(offsets), values, s=34, color=color, edgecolor='#333', zorder=3)
            ax.text(x, 1.015, f"n = {len(values)} {'blocks' if rows is surveys else 'resolved trials'}",
                transform=ax.get_xaxis_transform(), ha='center', fontsize=9)
        ax.set_ylim(bottom=0)
        if field == 'nasa_tlx_score_0_100': ax.set_ylim(0, 100)
        elif field == 'last_target_fixation_s': ax.set_ylim(0, 1)
    fig.suptitle('Recorded development data · eye-event validation pending', fontsize=16, y=.98)
    fig.subplots_adjust(left=.10, right=.97, top=.87, bottom=.14, wspace=.4, hspace=.55)
    fig.text(.5, .055, 'Different development runs; no paired voice effect or significance test. Missing data are not zero.', ha='center', fontsize=10, color='#555')
    fig.text(.5, .025, 'Counts use usable intervals; saccade rates use restricted head-motion intervals. Coverage is reported separately.', ha='center', fontsize=9, color='#555')
    for ext in ('png', 'pdf', 'svg'): fig.savefig(out/f'research-measures.{ext}', dpi=200)
    plt.close(fig)

    fig, ax = plt.subplots(figsize=(11, 4.5), constrained_layout=True)
    for field, marker, label in [('fixation_coverage_min_eye', 'o', 'Fixation eligible time'),
                                 ('saccade_coverage_min_eye', 's', 'Saccade eligible time')]:
        values = [100*t[field] if t[field] is not None else np.nan for t in trials]
        ax.scatter(range(len(trials)), values, marker=marker, label=label, s=35)
    ax.set_xticks(range(len(trials)), [f"{'N' if t['condition']==CONDITIONS[0] else 'S'}{t['run']:02d}:{t['trial_id'].rsplit('_',1)[-1]}" for t in trials], rotation=65, ha='right', fontsize=8)
    ax.set_ylabel('Minimum across eyes (% of active search time)')
    ax.set_ylim(0, max(105, ax.get_ylim()[1])); ax.grid(axis='y', alpha=.2)
    ax.legend(frameon=False, fontsize=9, loc='lower right')
    ax.set_title('Coverage audit · no automatic acceptance threshold\nN = neutral; S = self-similar. Incomplete source files remain flagged in the report.', fontsize=11)
    for ext in ('png', 'pdf', 'svg'): fig.savefig(out/f'measurement-coverage.{ext}', dpi=180)
    plt.close(fig)


def fingerprint(path):
    path = Path(path).resolve()
    digest = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024), b''): digest.update(block)
    return dict(path=str(path), sha256=digest.hexdigest(), bytes=path.stat().st_size)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--summary', type=Path, action='append', required=True)
    parser.add_argument('--eye', type=Path, action='append', required=True)
    parser.add_argument('--survey', type=Path, action='append', default=[])
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    if args.out.exists(): parser.error('Choose a new output directory; existing evidence is never overwritten')
    sources = args.summary + args.eye + args.survey
    sources += [p.parent/'end_status.json' for p in args.eye if (p.parent/'end_status.json').exists()]
    if len({str(p.resolve()) for p in sources}) != len(sources): parser.error('Duplicate input path')
    manifest = [fingerprint(p) for p in sources]
    summaries = [json.loads(p.read_text()) for p in args.summary]
    config = Config()
    metrics, events, spatial, recordings = [], [], [], []
    recording_ids = set()
    for path in args.eye:
        rows, warnings = read_recording(path)
        rid = rows[0]['recordingId']
        if rid in recording_ids: raise ValueError('Duplicate recording identity')
        recording_ids.add(rid)
        m, e = analyze(rows, config)
        metrics.extend(m); events.extend(e)
        spatial.extend(spatial_diagnostics(rows, config))
        recordings.append(dict(recording_id=rid, path=str(path.resolve()), rows=len(rows), warnings=warnings,
            integrity=recording_status(path, rows, warnings), validated=False))
    trials = build_trials(summaries, metrics)
    recording_by_id = {r['recording_id']: r for r in recordings}
    for trial in trials:
        recording = recording_by_id.get(trial['recording_id'])
        if recording:
            trial['recording_complete'] = recording['integrity'].get('complete') is True
            if not trial['recording_complete']:
                trial['research_gate_reasons'] += ';raw_recording_incomplete'
    responses = []
    for path in args.survey:
        with path.open(encoding='utf-8-sig', newline='') as f: responses.extend(csv.DictReader(f))
    surveys = score_surveys(responses, trials)
    for before in manifest:
        if fingerprint(before['path']) != before: raise ValueError('Source changed during analysis: '+before['path'])
    args.out.mkdir(parents=True)
    for name, rows in [('trials', trials), ('eye-metrics', metrics), ('candidate-events', events),
                        ('spatial-quality', spatial), ('tlx-responses', surveys)]:
        if rows: write_csv(args.out/(name+'.csv'), rows)
    plot(trials, surveys, args.out)
    report = dict(version=VERSION_REPORT, detector_version=VERSION, config=asdict(config),
        command=[sys.executable, str(Path(__file__).resolve()), *sys.argv[1:]],
        eye_research_ready=False, eye_gate_reasons=EYE_GATES,
        last_target_fixation_status='unavailable_no_validated_target_AOI_event_join',
        tlx_scored_blocks=sum(r['plot_eligible'] for r in surveys),
        inputs=manifest, recordings=recordings,
        spatial_diagnostic_rows=len(spatial),
        target_validation_sets=sorted({r['validation_set'] or 'unknown' for r in spatial}),
        trial_count=len(trials), resolved_trials=sum(t['plot_eligible'] for t in trials),
        nonresolved_or_inconsistent_trials=[t['trial_id'] for t in trials if not t['plot_eligible']],
        unmatched_eye_trial_ids=sorted({m['trial_id'] for m in metrics}-{t['trial_id'] for t in trials}),
        interpretation='Development diagnostics only. Eyes and trials are not independent participants. No voice-effect inference.',
        last_fixation_definition='Pending: last independently detected fixation intersecting the recorded target AOI before controller selection, excluding pause/gap boundaries and reporting censored events separately.',
        coverage_definition='Per-eye eligible interval seconds / recorded active search seconds; minimum of the two eyes. No extrapolation or automatic acceptance threshold.',
        source_clock='HTC query receipt, not verified sensor capture; summary exposure is a duration, not a timestamp join.',
        code=[fingerprint(Path(__file__).with_name(p)) for p in ('research_measures.py', 'eye_outcomes.py', 'eye_quality.py', 'tlx_scoring.py', 'thesis_voice_condition_plots.py')],
        versions={p:importlib.metadata.version(p) for p in ('numpy','scipy','PyWavelets','matplotlib')})
    (args.out/'measurement-status.json').write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
    print(json.dumps(dict(output=str(args.out.resolve()), trials=len(trials), eye_research_ready=False,
                         scored_tlx_blocks=report['tlx_scored_blocks'])))


if __name__ == '__main__': main()
