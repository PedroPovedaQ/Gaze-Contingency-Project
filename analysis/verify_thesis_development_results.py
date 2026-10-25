#!/usr/bin/env python3
"""Independent arithmetic and source checks for the actual-data thesis figures."""
import csv
import hashlib
import json
import statistics
from collections import Counter
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/thesis/development-results'
trials=list(csv.DictReader((OUT/'trials.csv').open()))
audio=list(csv.DictReader((OUT/'speech-delivery.csv').open()))
summary=json.loads((OUT/'summary.json').read_text())
manifest=json.loads((OUT/'source-manifest.json').read_text())
results=[]
for source in manifest['sources']:
    data=Path(source['path']).read_bytes()
    assert hashlib.sha256(data).hexdigest()==source['sha256']
    selection={};starts=set();rows=Counter();bad=[]
    for n,line in enumerate(data.splitlines(),1):
        try:r=json.loads(line)
        except json.JSONDecodeError:bad.append(n);continue
        if r['kind']=='header':assert r['sourceType']=='live_headset'
        if r['kind']=='search_start':starts.add(r['trialId'])
        if r['kind']=='selection' and r['correct']:
            assert r['trialId'] not in selection
            selection[r['trialId']]=r['searchSeconds']
        if r['kind']=='sample' and r['searchActive']:
            rows[r['trialId']]+=1
    runtrials=[t for t in trials if t['recording_id']==source['recording_id']]
    assert {t['trial_id'] for t in runtrials}==starts
    assert {t['trial_id'] for t in runtrials if t['completed']=='True'}==set(selection)
    for t in runtrials:
        assert int(t['sample_rows'])==rows[t['trial_id']]
        if t['completed']=='True':assert float(t['search_seconds'])==selection[t['trial_id']]
        else:assert t['search_seconds']==''
    assert bad==source['trailing_malformed_lines']
    results.append({'run':source['run'],'hash_verified':True,'starts_and_selections_verified':True,'row_counts_verified':True})
assert len(trials)==summary['started_trials']
assert statistics.median([float(t['search_seconds']) for t in trials if t['completed']=='True'])==summary['completion_seconds']['median']
for a in audio:
    parts=sum(float(a[k]) for k in ['decision_to_audio_request_s','audio_request_to_ready_s','ready_to_playback_s'])
    assert abs(parts-float(a['request_to_playback_s']))<1e-10
assert len(audio)==summary['speech_request_to_playback_seconds']['n']
cross=json.loads((OUT/'summary-crosscheck.json').read_text())
for source in cross['sources']:
    data=Path(source['path']).read_bytes();assert hashlib.sha256(data).hexdigest()==source['sha256']
    s=json.loads(data)
    for t in trials:
        if int(t['run'])!=source['run'] or t['completed']!='True':continue
        index=int(t['trial_id'].split('_')[-1])-1
        o=next(o for o in s['objectives'] if o['index']==index)
        assert o['completed'] and abs(float(t['search_seconds'])-o['time_to_find_seconds'])<.011
report=dict(status='passed',source_checks=results,exact_join_stage_arithmetic=True,
            companion_summary_checks=20,synthetic_rows_used=0,
            scope='Observed-source and arithmetic checks; not hardware accuracy, acoustic validation or study inference.')
(OUT/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
