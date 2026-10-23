"""Reproducible isolated production-policy comparisons; never headset timing claims."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
MONO = Path('/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge')
SOURCES = ['GuidanceStateMachine', 'GuidanceStateSnapshot', 'GuidanceSpeechPolicy', 'GuidanceTurnState',
           'DirectionalHintPolicy', 'GazeZoneEntryGate', 'GazeReturnCueGate', 'StudyPromptDelay', 'GazeTargetHistory']
THRESHOLD = 1.0
DIRECTION_ACTIONS = {'Direction', 'Progress', 'Arrival', 'ReturnTarget', 'ReturnBearing', 'ReturnWall'}


def stats(values):
    values=sorted(values)
    return {'count':len(values), 'median':statistics.median(values) if values else None,
            'p95':values[math.ceil(.95*len(values))-1] if values else None,
            'maximum':max(values) if values else None,
            'over_threshold':sum(v>THRESHOLD+1e-6 for v in values)}


def compile_run(ref, snapshots=None):
    with tempfile.TemporaryDirectory(prefix='guidance-paired-') as tmp:
        tmp=Path(tmp)
        inputs=[]
        hashes={}
        for name in SOURCES:
            path='Assets/'+name+'.cs'
            data=(ROOT/path).read_bytes() if ref=='working_tree' else subprocess.check_output(['git','show',f'{ref}:{path}'],cwd=ROOT)
            output=tmp/(name+'.cs');output.write_bytes(data);inputs.append(str(output));hashes[path]=hashlib.sha256(data).hexdigest()
        exe=tmp/'pilot.exe'
        subprocess.run([str(MONO/'bin/mono'),str(MONO/'lib/mono/4.5/csc.exe'),'-nologo','-out:'+str(exe),*inputs,str(ROOT/'scripts/tests/GuidancePairedPilotRunner.cs')],check=True,capture_output=True)
        raw=subprocess.check_output([str(MONO/'bin/mono'),str(exe),*([str(snapshots)] if snapshots else [])],text=True)
    scenarios={}
    for line in raw.splitlines():
        name,time,event,phase,action,valid,gaze,controller,extra=line.split('\t')
        scenarios.setdefault(name,[]).append(dict(time=float(time),event=event,phase=phase,action=action,
            gaze_valid=valid=='True',gaze_on_target=gaze=='True',controller_on_target=controller=='True',detail=extra))
    return scenarios,hashes


def summarize(events):
    qualifications={'gaze':[],'controller':[]}
    pending={}
    delays={'gaze':[],'controller':[]}
    contradictions=0;active=False
    starts=[]
    for e in events:
        if e['event']=='observation':
            conflict=e['controller_on_target'] and e['action'] in DIRECTION_ACTIONS
            if conflict and not active:contradictions+=1
            active=conflict
        if e['event'].startswith('qualification_'):
            kind=e['event'].split('_',1)[1]
            # One episode until playback, rather than one failure per repeated request.
            if kind not in pending:
                pending[kind]=e['time'];qualifications[kind].append(e['time'])
        if e['event']=='simulated_start':
            starts.append(e)
            kind={'ConfirmGaze':'gaze','ConfirmPointing':'controller'}.get(e['detail'])
            if kind in pending:delays[kind].append(round(e['time']-pending.pop(kind),3))
        if e['event'] in ('pause','resume') or e['event']=='observation' and (not e['gaze_valid'] or not e['gaze_on_target']):
            pending.clear()
        elif e['event']=='observation' and not e['controller_on_target']:
            pending.pop('controller',None)
    return dict(controller_conflict_eligibility_episodes=contradictions,
        controller_conflicting_simulated_starts=sum(e['controller_on_target'] and e['detail'] in DIRECTION_ACTIONS for e in starts),
        simulated_gaze_start_delay=stats(delays['gaze']),simulated_controller_start_delay=stats(delays['controller']),
        simulated_gaze_starts=sum(e['detail']=='ConfirmGaze' for e in starts),
        simulated_controller_starts=sum(e['detail']=='ConfirmPointing' for e in starts),
        simulated_total_starts=len(starts),discarded_pending_actions=sum(e['event']=='discard' for e in events),
        unplayed_qualified_at_end={k:v for k,v in pending.items()})


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);ap.add_argument('--network-report',type=Path);ap.add_argument('--recording',type=Path);args=ap.parse_args()
    variants=[]
    snapshot_temp=tempfile.NamedTemporaryFile(mode='w+',suffix='.tsv')
    snapshot_count=0
    if args.recording:
        with args.recording.open() as recording:
            for line in recording:
                try:
                    r=json.loads(line);d=r.get('detail') or ''
                    if '"stage":"request"' not in d:continue
                    data=json.loads(d[d.index('{'):]);e=data.get('payload',{}).get('state',{}).get('runtime',{}).get('evidence',{})
                    required=['valid','insideTargetWall','gazeWithinTargetVicinity','controllerEvidenceValid','controllerOnTarget']
                    if not all(isinstance(e.get(k),bool) for k in required):continue
                    yaw=e.get('yawErrorDegrees');pitch=e.get('pitchErrorDegrees')
                    if not all(isinstance(v,(int,float)) and math.isfinite(v) for v in (yaw,pitch)):continue
                    values=[f'recorded_snapshot_{snapshot_count:03d}',*[e[k] for k in required],yaw,pitch,e.get('verticalPlaneFraction') or 0]
                    snapshot_temp.write('\t'.join(map(str,values))+'\n');snapshot_count+=1
                except (ValueError,TypeError,AttributeError):continue
        snapshot_temp.flush()
    for label,ref in [('earlier','492be9d'),('checkpoint','7299312'),('fixed','working_tree')]:
        data,hashes=compile_run(ref,snapshot_temp.name if snapshot_count else None)
        variants.append(dict(id=label,source_ref=ref,source_hashes=hashes,
            scenarios=[dict(id=name,metrics=summarize(events),timeline=[e for e in events if e['event']!='observation']) for name,events in data.items() if not name.startswith('recorded_snapshot_')],
            metrics={'controller_conflict_eligibility_episodes':sum(summarize(e)['controller_conflict_eligibility_episodes'] for n,e in data.items() if not n.startswith('recorded_snapshot_')),
                     'controller_conflicting_simulated_starts':sum(summarize(e)['controller_conflicting_simulated_starts'] for n,e in data.items() if not n.startswith('recorded_snapshot_')),
                     'recorded_frozen_snapshot_conflicts':sum(summarize(e)['controller_conflict_eligibility_episodes'] for n,e in data.items() if n.startswith('recorded_snapshot_'))}))
    report=dict(schema='guidance-paired-pilots-v1',threshold_seconds=THRESHOLD,
        recorded_snapshot_probes={'count':snapshot_count,'basis':'Recorded request evidence held constant for300ms to probe eligibility only. No inferred history, participant trajectory, or measured timing. Repeated snapshots are probes, not unique delay episodes.'},
        measurement_basis='Isolated production C# state-machine and policy replay, 50 ms simulated ticks and synthetic 400 ms audio. These are not Unity playback, headset, network, acoustic latency, or participant improvement measurements.',
        variants=variants,
        network_comparison=json.loads(args.network_report.read_text()) if args.network_report else {'status':'not_run'},
        unknowns=['Full HintGenerator/VoiceSynthesizer lifecycle and interruption behavior are not executed by this isolated harness.',
                  'Visible-highlight reference filtering and the phrase-aware acknowledgment entry gates are covered by separate runtime regression suites, not by these scripted input trajectories.',
                  'Adapter only discards pending callbacks at their synthetic ready time; production cancels on revision changes earlier. Synthetic late-response delays are not estimates of production delays.',
                  'Simulated starts occur in an adapter, not AudioSource.Play. Recorded software playback must be assessed separately.',
                  'Overlap winner and validity are scripted upstream observations, not a test of ray geometry or hardware acquisition.',
                  'Jev delay/wait/error outcomes in trajectories are injected. Actual provider requests are a separate report.',
                  'Unspoken qualifications interrupted by departure are not classified as missed confirmations without a complete scheduling model.',
                  'Perceived naturalness and behavioral benefit require a wearer pilot; advisory Astra ratings do not establish them.'])
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({v['id']:v['metrics'] for v in variants},indent=2))

if __name__=='__main__':main()
