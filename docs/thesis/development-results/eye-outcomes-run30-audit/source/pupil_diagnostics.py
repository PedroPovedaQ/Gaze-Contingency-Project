"""Explain pupil exclusions without relaxing detector gates or filling missing data."""
from collections import Counter, defaultdict
import math
import numpy as np


def audit(rows, config, segments, metrics):
    search = [r for r in rows if r.get('phase') == 'search' and r.get('sourceTrialId')]
    intervals = []
    repeated_queries = 0
    unique = []
    # Never measure a gap across a phase/trial/device boundary.
    previous_key = None
    previous_time = None
    for row in rows:
        key = tuple(row.get(k) for k in ('recordingId','participant','run','sourceTrialId','phase','deviceId'))
        t = row.get('htcQueryReceiptTime')
        if key != previous_key:
            previous_time = None
        previous_key = key
        if row.get('phase') != 'search' or not row.get('sourceTrialId') or not isinstance(t,(int,float)) or not math.isfinite(t) or t <= 0:
            continue
        if previous_time is not None:
            if t == previous_time:
                repeated_queries += 1
                continue
            if t > previous_time:
                intervals.append(t-previous_time)
        previous_time = t
        unique.append(row)
    sides = {}
    for side in ('left','right'):
        values = [r.get(side+'PupilMm') for r in unique]
        same_pairs = pairs = 0
        last_key = last_value = None
        for row, value in zip(unique, values):
            key = tuple(row.get(k) for k in ('recordingId','sourceTrialId','deviceId'))
            if key == last_key and isinstance(value,(int,float)) and isinstance(last_value,(int,float)):
                pairs += 1
                same_pairs += value == last_value
            last_key, last_value = key, value
        chosen = [s for s in segments if s['eye'] == side]
        counts = Counter(s['first_failure'] for s in chosen)
        durations = defaultdict(float)
        for s in chosen: durations[s['first_failure']] += s['duration_s']
        sides[side] = dict(raw_search_rows=len(search),
            reported_valid_pupil_rows=sum(bool(r.get('pupilAvailable') and r.get(side+'PupilValid')) for r in search),
            identical_value_adjacent_distinct_query_fraction=same_pairs/pairs if pairs else None,
            segment_counts_by_first_failure=dict(counts), segment_seconds_by_first_failure=dict(durations),
            retained_resampled_seconds=sum(r['pupil_eligible_seconds'] for r in metrics if r['eye']==side))
    return dict(search_rows=len(search), distinct_query_rows=len(unique), repeated_query_rows=repeated_queries,
        within_search_query_intervals=len(intervals), gaps_over_limit=sum(d>config.max_gap_s for d in intervals),
        query_interval_ms_percentiles={str(p):float(np.percentile(intervals,p)*1000) if intervals else None for p in (50,95,99,100)},
        eyes=sides,
        interpretation='First-failure segment durations are mutually exclusive within the screened segments, not a full trial-time denominator. Missing, padded, singleton and between-segment time is outside these totals. Equal pupil values do not prove cached sensor samples. Query rate is not sensor rate.',
        physiological_validation='pending', gate_config=dict(max_gap_s=config.max_gap_s,
            pupil_min_segment_s=config.pupil_min_segment_s, pupil_min_hz=config.pupil_min_hz,
            pupil_max_p95_relative_jitter=config.pupil_max_p95_relative_jitter))


def plot_quality(report, out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    reasons=['short_segment','low_query_rate','irregular_query_timing','insufficient_wavelet_points','accepted']
    labels=['Too short','Low query rate','Irregular timing','Too few points','Accepted']
    fig, ax=plt.subplots(figsize=(8,3.7),constrained_layout=True)
    x=np.arange(len(reasons))
    for side,offset,color in [('left',-.18,'#a36c00'),('right',.18,'#24679e')]:
        vals=[report['eyes'][side]['segment_seconds_by_first_failure'].get(r,0) for r in reasons]
        bars=ax.bar(x+offset,vals,.35,label=side.title()+' eye',color=color)
        ax.bar_label(bars,fmt='%.1f',fontsize=8,padding=2)
    ax.set_xticks(x,labels);ax.set_ylabel('Screened segment duration [s]')
    ax.set_title('Pupil processing: first failed check or acceptance')
    ax.spines[['top','right']].set_visible(False);ax.legend(frameon=False)
    fig.savefig(out/'pupil-quality.png',dpi=180);fig.savefig(out/'pupil-quality.pdf');plt.close(fig)


def export_review_traces(rows, config, out):
    """Unlabeled traces for independent review; never generate ground-truth labels."""
    from eye_outcomes import fresh_search_segments, vec, write_csv
    from scipy.spatial.transform import Rotation
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    out.mkdir(parents=True, exist_ok=True)
    groups=defaultdict(list)
    for segment in fresh_search_segments(rows,config):
        key=(segment[0]['recordingId'],segment[0]['sourceTrialId'])
        groups[key].append(segment)
    for index,((recording,trial),segments) in enumerate(groups.items(),1):
        fig,axes=plt.subplots(3,1,figsize=(11,7),sharex=True,constrained_layout=True)
        records=[]
        for segment in segments:
            t=[r['htcQueryReceiptTime'] for r in segment]
            for side,color in [('left','#a36c00'),('right','#24679e')]:
                yaw=[];pitch=[];pupil=[]
                for r in segment:
                    d=vec(r,side+'WorldDirection');q=vec(r,'headRotation','xyzw')
                    valid=bool(r.get(side+'Valid') and r.get('headValid') and d is not None and q is not None)
                    local=Rotation.from_quat(q).inv().apply(d) if valid else None
                    y=float(np.degrees(np.arctan2(local[0],local[2]))) if valid else None
                    p=float(np.degrees(np.arctan2(local[1],np.hypot(local[0],local[2])))) if valid else None
                    v=r.get(side+'PupilMm')
                    pupil_valid=bool(r.get('pupilAvailable') and r.get(side+'PupilValid') and isinstance(v,(int,float)) and math.isfinite(v) and 1<=v<=10)
                    records.append(dict(recording_id=recording,trial_id=trial,eye=side,query_time=r['htcQueryReceiptTime'],
                        yaw_deg=y,pitch_deg=p,pupil_mm=v if pupil_valid else None,
                        direction_and_head_valid=valid,pupil_valid=pupil_valid))
                    yaw.append(y if y is not None else np.nan);pitch.append(p if p is not None else np.nan);pupil.append(v if pupil_valid else np.nan)
                # Break plotted wrap crossings; a +/-180 boundary is not a saccade.
                yaw=np.asarray(yaw);yaw[np.r_[False,np.abs(np.diff(yaw))>180]]=np.nan
                for ax,values in zip(axes,(yaw,pitch,pupil)):
                    ax.plot(t,values,color=color,lw=.65,label=side.title()+' eye')
        for ax,label in zip(axes,('Eye-in-head yaw [deg]','Eye-in-head pitch [deg]','Pupil diameter [mm]')):
            ax.set_ylabel(label);ax.grid(alpha=.2);ax.spines[['top','right']].set_visible(False)
        handles,labels=axes[0].get_legend_handles_labels()
        unique=dict(zip(labels,handles));axes[0].legend(unique.values(),unique.keys(),frameon=False)
        axes[-1].set_xlabel('HTC query-clock time [s]')
        fig.suptitle(f'{trial}: independent review traces (no detector labels)')
        fig.savefig(out/f'trial-{index:02d}.pdf');plt.close(fig)
        write_csv(out/f'trial-{index:02d}.csv',records)
    (out/'annotations.csv').write_text('recording_id,eye,kind,start,end,trial_id\n')
    (out/'annotation-windows.csv').write_text('recording_id,eye,trial_id,start,end\n')
    (out/'README.md').write_text('Independent review packet. Traces use query time, not sensor capture time. '
        'No detector output is overlaid. Only eligible search segments are drawn; gaps stay gaps. '
        'Review raw records for missingness. Eye/head poses are software-time matched, not sensor synchronized. '
        'Choose exhaustively reviewed windows and label fixation/saccade start/end independently; leave uncertain '
        'windows unscored. Empty CSV templates are not completed annotations. Do not treat target instructions '
        'or algorithm agreement as physiological ground truth.\n')
