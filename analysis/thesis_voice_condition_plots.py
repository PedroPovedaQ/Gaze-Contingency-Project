#!/usr/bin/env python3
"""Four condition-labelled panels from frozen development evidence, never simulated values."""
import argparse
import csv
import hashlib
import json
import math
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/thesis/development-results/voice-condition-panels'
FIG = ROOT / 'docs/thesis/figures'
CONDITIONS = ('Neutral voice', 'Self-similar voice')
COLORS = ('#E8B44F', '#7199D6')


def condition(voice):
    mapping = {'neutral': CONDITIONS[0], 'neutral-male': CONDITIONS[0],
               'neutral-female': CONDITIONS[0], 'preferred': CONDITIONS[0],
               'selfsimilar': CONDITIONS[1], 'self-similar': CONDITIONS[1],
               'self_similar': CONDITIONS[1], 'clone': CONDITIONS[1], 'cloned': CONDITIONS[1]}
    if voice not in mapping:
        raise ValueError(f'Unknown recorded voice condition: {voice!r}')
    return mapping[voice]


def derive(summary, eye_rows):
    """One development run. Scheduled/not-started rows never enter a denominator."""
    trials = []
    identities = set()
    for obj in summary['objectives']:
        if obj.get('search_started_at', -1) < 0 or obj.get('is_practice') or obj.get('practice'):
            continue
        tid = obj['trial_id']
        if tid in identities:
            raise ValueError('Duplicate trial ID')
        identities.add(tid)
        voice = condition(obj.get('voice_condition', summary.get('voice_condition')))
        completed = obj.get('completed') is True and obj.get('outcome') == 'completed'
        seconds = obj.get('search_exposure_seconds') if completed else None
        if completed and (seconds is None or not math.isfinite(seconds) or seconds <= 0):
            raise ValueError('Completed trial lacks valid active search time')
        wrong = obj.get('wrong_captures')
        first_correct = int(wrong == 0) if completed and isinstance(wrong, int) and wrong >= 0 else None
        deadline = summary.get('active_search_limit_seconds')
        # Only this protocol's recorded 25-second limit supports the specified miss panel.
        known_deadline = deadline == 25
        missed = (0 if completed else 1 if obj.get('outcome') == 'timeout' else None) if known_deadline else None
        trials.append(dict(trial_id=tid, condition=voice, completed=completed,
                           first_selection_correct=first_correct, active_search_seconds=seconds,
                           missed_target=missed, deadline_seconds=deadline,
                           ipa_candidate_hz=None, ipa_eyes=0, pupil_seconds_per_eye=None))
    byid = {t['trial_id']: t for t in trials}
    seen = set()
    pupil = defaultdict(list)
    for row in eye_rows:
        tid = row['trial_id']
        if tid not in byid:
            continue
        if (tid, row['eye']) in seen:
            raise ValueError('Duplicate trial/eye row')
        seen.add((tid, row['eye']))
        if condition(row['voice']) != byid[tid]['condition']:
            raise ValueError('Eye/trial voice mismatch')
        if str(row.get('practice', '')).lower() == 'true':
            raise ValueError('Practice eye row matched study trial')
        if row.get('participant') != summary['participant_id'] or int(row['run']) != summary['run_number']:
            raise ValueError('Eye/trial run mismatch')
        value = row.get('ipa_candidate_hz')
        if value not in ('', None):
            rate = float(value); duration = float(row['pupil_eligible_seconds'])
            if not math.isfinite(rate) or rate < 0 or not math.isfinite(duration) or duration <= 0:
                raise ValueError('Invalid IPA observation')
            pupil[tid].append((rate, duration))
    for tid, values in pupil.items():
        # One point per trial, not two independent observations for the two eyes.
        byid[tid]['ipa_candidate_hz'] = sum(v[0] for v in values) / len(values)
        byid[tid]['pupil_seconds_per_eye'] = sum(v[1] for v in values) / len(values)
        byid[tid]['ipa_eyes'] = len(values)
    return trials


def describe(trials):
    result = {}
    for label in CONDITIONS:
        rows = [r for r in trials if r['condition'] == label]
        first = [r['first_selection_correct'] for r in rows if r['first_selection_correct'] is not None]
        misses = [r['missed_target'] for r in rows if r['missed_target'] is not None]
        times = [r['active_search_seconds'] for r in rows if r['active_search_seconds'] is not None]
        ipa = [r['ipa_candidate_hz'] for r in rows if r['ipa_candidate_hz'] is not None]
        result[label] = dict(started=len(rows), completed=len(times), first_selection_n=len(first),
            first_selection_correct=sum(first), accuracy_percent=100*sum(first)/len(first) if first else None,
            deadline_resolved_n=len(misses), timeout_n=sum(misses),
            missed_percent=100*sum(misses)/len(misses) if misses else None,
            completion_seconds=times, ipa_candidate_hz=ipa,
            ipa_unavailable_trials=len(rows)-len(ipa))
    return result


def plot(summary, path):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 10,
        'axes.spines.top': False, 'axes.spines.right': False, 'pdf.fonttype': 42})
    fig, axs = plt.subplots(2, 2, figsize=(9, 7.2))
    titles = ('(a) First-selection accuracy', '(b) Missed targets',
              '(c) Search completion time', '(d) Index of Pupillary Activity')
    ylabels = ('Accuracy (%)', 'Missed targets (%)', 'Active search time (s)', 'Exploratory IPA (events/s)')
    for ax, title, ylabel in zip(axs.flat, titles, ylabels):
        ax.set_title(title, loc='left', fontsize=12, pad=14)
        ax.set_ylabel(ylabel)
        ax.set_xticks([1, 2], ['Neutral\nvoice', 'Self-similar\nvoice'])
        ax.set_xlim(.5, 2.5)
        ax.grid(axis='y', alpha=.15); ax.set_axisbelow(True)
    axs[0, 0].set_ylim(0, 105); axs[0, 1].set_ylim(0, 105)
    axs[1, 0].set_ylim(bottom=0); axs[1, 1].set_ylim(bottom=0)
    def missing(ax, x, message):
        ax.text(x, .48, message, transform=ax.get_xaxis_transform(), ha='center', va='center', color='#626970', fontsize=10)
    for x, (label, color) in enumerate(zip(CONDITIONS, COLORS), 1):
        d = summary[label]
        for ax, key, count, note in (
            (axs[0, 0], 'accuracy_percent', 'first_selection_n', 'completed searches'),
            (axs[0, 1], 'missed_percent', 'deadline_resolved_n', 'resolved searches')):
            if d[key] is None:
                missing(ax, x, 'No recorded\ncondition data' if not d['started'] else 'Unavailable\nNo recorded\n25-s deadline')
            else:
                ax.scatter([x], [d[key]], s=75, color=color, edgecolor='#333333', zorder=4)
                numerator = d['first_selection_correct'] if key == 'accuracy_percent' else d['timeout_n']
                ax.annotate(f"{d[key]:g}% ({numerator}/{d[count]})", (x, d[key]), xytext=(0, -23), textcoords='offset points', ha='center')
                ax.text(x, .04, f"{d[count]} {note}", transform=ax.get_xaxis_transform(), ha='center', fontsize=8)
        for ax, key in ((axs[1, 0], 'completion_seconds'), (axs[1, 1], 'ipa_candidate_hz')):
            values = d[key]
            if not values:
                missing(ax, x, 'No recorded\ncondition data' if not d['started'] else 'No usable\nmeasurements')
                continue
            if len(values) >= 3:
                bp = ax.boxplot([values], positions=[x], widths=.32, showfliers=False, patch_artist=True,
                                manage_ticks=False, medianprops={'color': '#222222'})
                bp['boxes'][0].set_facecolor(color); bp['boxes'][0].set_alpha(.55)
            offsets = [0] if len(values) == 1 else [(.15*i/(len(values)-1)-.075) for i in range(len(values))]
            ax.scatter([x+o for o in offsets], values, color=color, edgecolor='#333333', s=35, zorder=4)
            ax.text(x, .96, f"n = {len(values)} trials", transform=ax.get_xaxis_transform(), va='top', ha='center', fontsize=9)
            ax.set_ylim(0, max(ax.get_ylim()[1], max(values)*1.22))
    fig.suptitle('Recorded development data · Run 30', fontsize=14, y=.99)
    fig.subplots_adjust(left=.10, right=.97, top=.90, bottom=.12, wspace=.40, hspace=.56)
    fig.text(.5, .025, 'Neutral observations only; no voice-condition effect estimated. Missing data are not zero.',
             ha='center', fontsize=9, color='#515962')
    for ext in ('pdf', 'png'):
        fig.savefig(path.with_suffix('.'+ext), dpi=220)
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--summary', type=Path)
    parser.add_argument('--eyes', type=Path)
    args = parser.parse_args(); OUT.mkdir(parents=True, exist_ok=True)
    manifest_path = OUT/'sources.json'
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text())
    else:
        if not args.summary or not args.eyes:
            parser.error('First run requires --summary and --eyes')
        manifest = {key: {'path': str(path.resolve()), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                    for key, path in (('summary', args.summary), ('eyes', args.eyes))}
        manifest_path.write_text(json.dumps(manifest, indent=2)+'\n')
    for item in manifest.values():
        if hashlib.sha256(Path(item['path']).read_bytes()).hexdigest() != item['sha256']:
            raise ValueError('Source changed since snapshot: '+item['path'])
    raw = json.loads(Path(manifest['summary']['path']).read_text())
    eyes = list(csv.DictReader(Path(manifest['eyes']['path']).open()))
    trials = derive(raw, eyes); result = describe(trials)
    with (OUT/'plotted-trials.csv').open('w') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(trials[0])); writer.writeheader(); writer.writerows(trials)
    (OUT/'summary.json').write_text(json.dumps(result, indent=2)+'\n')
    plot(result, FIG/'development-voice-condition-panels')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
