#!/usr/bin/env python3
"""Audit a copied eye-validation session. Never treats exercise completion as measurement validation."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
from validation_accuracy import spatial_accuracy, SETTLE_SECONDS


def read_jsonl(path):
    rows, warnings = [], []
    lines = path.read_text().splitlines()
    for index, line in enumerate(lines):
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            if index == len(lines)-1:
                warnings.append(f'{path.name}: incomplete final line excluded')
            else:
                raise ValueError(f'{path.name}: corrupt line {index+1}')
    return rows, warnings


def prepare_rows(manifest, samples, events):
    """Create explicitly labeled analysis copies; raw phase/trial identifiers stay untouched.

    Continuous saccade targets share one exercise ID so a jump does not cut a detected event.
    Arithmetic problems remain separate; instruction/answer/rating frames are excluded.
    """
    result = []
    starts = {e['step']: e['time'] for e in events if e.get('eventType') == 'stimulus_start'}
    ended = {e['step'] for e in events if e.get('eventType') == 'stimulus_end'}
    schedule = manifest['schedule']
    for raw in samples:
        step = raw.get('validationStep')
        if raw.get('kind') != 'gaze_observation' or raw.get('validationId') != manifest['id']:
            continue
        if type(step) is not int or not 0 <= step < len(schedule):
            raise ValueError('Sample has invalid schedule index')
        if raw.get('validationStage') != 'running' or step not in starts:
            continue
        spec = schedule[step]
        # Pupil arithmetic onset includes removal of the problem text; exclude the first second.
        if spec['exercise'] == 'arithmetic' and raw['receiptTime']-starts[step] < 1:
            continue
        row = copy.deepcopy(raw)
        row['phase'] = 'search'  # Adapter-only input to the existing candidate detector.
        suffix = 'saccades' if spec['exercise'] == 'saccades' else f'step_{step}'
        row['sourceTrialId'] = 'validation_'+manifest['id']+'_'+suffix
        row['validationStimulusComplete'] = step in ended
        result.append(row)
    return result


def summarize(manifest, samples, events):
    samples=[r for r in samples if r.get('validationId') == manifest['id']]
    events=[e for e in events if e.get('validationId',manifest['id']) == manifest['id']]
    schedule = manifest['schedule']
    spatial=spatial_accuracy(manifest,samples,events)
    spatial_steps={t['step']:t for t in spatial['targets']}
    starts = {e['step']: e for e in events if e.get('eventType') == 'stimulus_start'}
    ends = {e['step']: e for e in events if e.get('eventType') == 'stimulus_end'}
    responses = []
    prompts = {}
    for e in events:
        typ = e.get('eventType')
        if typ in ('response_prompt', 'rating_prompt'):
            prompts[(e['step'], 'rating' if typ == 'rating_prompt' else 'answer')] = e['time']
        if typ not in ('answer','answer_missing','difficulty_rating','difficulty_missing','search_selection'):
            continue
        step=e['step'];spec=schedule[step]
        category='rating' if typ.startswith('difficulty') else 'answer'
        onset=prompts.get((step,category))
        recorded=e.get('valueRecorded') is True
        responses.append(dict(step=step, exercise=spec['exercise'], condition=spec['condition'],
            block=spec.get('block',-1), type=typ, source=e.get('source'),
            value=e.get('value') if recorded else None,
            correct=e.get('value') == spec['answer'] if recorded and typ=='answer' else None,
            response_seconds=e['time']-onset if onset is not None else None, time=e['time']))
    steps=[]
    for i,spec in enumerate(schedule):
        rows=[r for r in samples if r.get('kind')=='gaze_observation' and r.get('validationStep')==i and r.get('validationStage')=='running']
        accuracy=spatial_steps.get(i,{}).get('used_ray',{})
        steps.append(dict(step=i, exercise=spec['exercise'], condition=spec['condition'], planned_seconds=spec['seconds'],
            started=i in starts, completed=i in ends, sample_rows=len(rows),
            valid_rows=sum(r.get('valid') is True for r in rows),
            observed_duration_seconds=ends[i]['time']-starts[i]['time'] if i in starts and i in ends else None,
            used_ray_error_mean_deg=accuracy.get('mean_deg'),
            used_ray_error_median_deg=accuracy.get('median_deg'),
            used_ray_error_n=accuracy.get('n',0),
            spatial_settle_seconds=SETTLE_SECONDS if i in spatial_steps else None))
    return dict(protocol=manifest['protocol'],session=manifest['id'],status=manifest.get('status','unknown'),
        measurements_validated=False,steps=steps,responses=responses,spatial_accuracy=spatial,
        interpretation='Pilot recording audit only. Target/cue times are software requests, not ground-truth eye events. '
            'Spatial error is recomputed from recorded 3D geometry after uniform settling and query/delivery deduplication. '
            'It includes any recorded alignment; per-eye and unaligned comparisons are separate. '
            'A missing value is not zero; incomplete steps are retained and flagged.')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('session',type=Path)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--measures',action='store_true',help='Compute exploratory metrics and independent-review traces; needs analysis dependencies')
    args=parser.parse_args()
    if args.out.exists():raise ValueError('Use a new output folder to preserve previous analyses')
    source=args.session
    paths=[source/n for n in ('manifest.json','samples.jsonl','events.jsonl')]
    hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
    manifest=json.loads(paths[0].read_text())
    if manifest.get('protocol') != 'eye-validation-v1':raise ValueError('Unsupported validation protocol')
    if manifest.get('status') == 'in_progress':raise ValueError('Stop the session and copy stable recordings before analysis')
    samples,warnings=read_jsonl(paths[1]);events,other=read_jsonl(paths[2]);warnings+=other
    if hashes!={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}:raise ValueError('Recording changed during read')
    report=summarize(manifest,samples,events);report.update(source_sha256=hashes,warnings=warnings)
    args.out.mkdir(parents=True)
    from csv import DictWriter
    for name,rows in [('steps',report['steps']),('responses',report['responses'])]:
        if rows:
            with (args.out/(name+'.csv')).open('w',newline='') as file:
                writer=DictWriter(file,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
    if args.measures:
        from eye_outcomes import analyze, Config, write_csv, plot
        from pupil_diagnostics import export_review_traces
        adapted=prepare_rows(manifest,samples,events)
        diagnostics=[];metrics,candidates=analyze(adapted,Config(),diagnostics)
        for name,rows in [('exploratory_metrics',metrics),('candidate_events',candidates),('pupil_segments',diagnostics)]:
            if rows:write_csv(args.out/(name+'.csv'),rows)
        if metrics:plot(metrics,args.out)
        export_review_traces(adapted,Config(),args.out/'independent-review')
        report.update(metric_rows=len(metrics),candidate_events=len(candidates),
            analysis_adapter='Derived copies only: running validation frames -> search for existing detector. Arithmetic first second excluded; saccade cue boundaries retained. Never merge with study outcomes.',
            incomplete_step_policy='Recorded prefixes are analyzed as exploratory evidence; steps.csv carries completion per step. No automatic acceptance threshold.')
    (args.out/'report.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps(dict(report=str(args.out/'report.json'),status=report['status'],measurements_validated=False)))

if __name__=='__main__':main()
