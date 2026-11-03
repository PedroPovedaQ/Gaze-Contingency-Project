"""Chapter 4 result figures, drawn from the trial exports or as empty templates until participant data exist.

Without --data every panel keeps its axes, units and conditions and says that data are pending; nothing is simulated.
With --data the same figures are drawn from every full_run_measures.csv under the given folders. Rows flagged
exclude_from_study_analysis are dropped unless --development is given, which stamps each figure as development
data and writes to /tmp/results-preview instead of the thesis figures folder.

Usage:
  python generate-results-figures.py                                  # templates
  python generate-results-figures.py --data <GazeData exports> [--model rq1_time_ratio.json] [--flow flow.json]
  python generate-results-figures.py --data <synthetic exports> --development

rq1_time_ratio.json holds the mixed-model contrast: {"estimate": 0.95, "low": 0.90, "high": 1.01}.
flow.json holds participant-flow counts keyed like FLOW_KEYS below.
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from matplotlib.patches import FancyBboxPatch
from scipy import stats

F = Path(__file__).resolve().parent
plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 8.5, 'pdf.fonttype': 42,
                     'axes.spines.top': False, 'axes.spines.right': False})
VOICES = ('SAV', 'SSV')
COLOR = {'SAV': '#C8962A', 'SSV': '#3F6FB5'}
INK, MUTED = '#2a3034', '#8a9296'
PENDING = 'Awaiting\nparticipant data'
FLOW_KEYS = ('consented', 'ineligible', 'order_a', 'order_b', 'stopped_a', 'stopped_b', 'analyzed')


def pending(ax, text=PENDING):
    ax.text(.5, .5, text, transform=ax.transAxes, ha='center', va='center', color=MUTED, style='italic', fontsize=8)


def ci(values):
    values = np.asarray(values, float)
    values = values[~np.isnan(values)]
    if len(values) < 2:
        return (values.mean() if len(values) else np.nan), np.nan
    return values.mean(), stats.t.ppf(.975, len(values) - 1) * values.std(ddof=1) / np.sqrt(len(values))


# Data
def survey(df, suffix):
    names = [c for c in df.columns if c.startswith('survey__') and c.endswith(suffix)]
    return pd.to_numeric(df[names[0]], errors='coerce') if names else pd.Series(np.nan, index=df.index)


def load(folders, development):
    frames = [pd.read_csv(p, encoding='utf-8-sig', low_memory=False) for d in folders for p in Path(d).rglob('full_run_measures.csv')]
    if not frames:
        sys.exit('no full_run_measures.csv found under ' + ', '.join(folders))
    df = pd.concat(frames, ignore_index=True)
    df = df[pd.to_numeric(df.is_practice, errors='coerce') == 0]
    if not development:
        df = df[pd.to_numeric(df.exclude_from_study_analysis, errors='coerce') == 0]
    # The SAV is exported as neutral-<voice gender>, the SSV as selfsimilar.
    voice = df.voice_condition.astype(str)
    df = df.assign(voice=np.where(voice.str.startswith('neutral'), 'SAV', np.where(voice == 'selfsimilar', 'SSV', None))).dropna(subset=['voice']).copy()
    for column in ('time_to_final_selection_seconds', 'missed_target', 'absolute_theta_deg', 'hint_count',
                   'gaze_validity_rate', 'interrupted_instruction_count'):
        df[column] = pd.to_numeric(df[column], errors='coerce')
    for measure in ('ipa_candidate_hz', 'fixation_count', 'saccade_frequency_hz'):
        df[measure] = pd.concat([pd.to_numeric(df[f'eye_{eye}__{measure}'], errors='coerce') for eye in ('left', 'right')], axis=1).mean(axis=1)
    df['completion'] = df.time_to_final_selection_seconds.where(df.missed_target == 0)
    return df


def paired(df, values, how='mean'):
    """Participant-by-voice table with both voices present."""
    table = df.assign(v=values).groupby(['participant_id', 'voice']).v.agg(how).unstack()
    return table.reindex(columns=VOICES).dropna()


def geomean(values):
    return np.exp(np.log(values.dropna()).mean())


def blocks(df):
    """One row per participant and voice block for questionnaire scores."""
    return df.groupby(['participant_id', 'voice'], as_index=False).first()


# Panels
def slope(ax, table, ylabel, ylim, log=False, title=None):
    ax.set(xlim=(-.45, 1.45), xticks=[0, 1], xticklabels=VOICES, ylabel=ylabel, ylim=ylim)
    if log:
        ax.set_yscale('log')
        ax.set_ylim(ylim)
        ax.set_yticks([t for t in (1, 2, 5, 10, 20) if ylim[0] <= t <= ylim[1]])
        ax.yaxis.set_major_formatter(matplotlib.ticker.FuncFormatter(lambda y, _: f'{y:g}'))
        ax.yaxis.set_minor_formatter(matplotlib.ticker.NullFormatter())
    if title:
        ax.set_title(title, fontsize=8.5, loc='left')
    for label, voice in zip(ax.get_xticklabels(), VOICES):
        label.set_color(COLOR[voice])
        label.set_fontweight('bold')
    if table is None or table.empty:
        pending(ax)
        return
    for _, row in table.iterrows():
        ax.plot([0, 1], row[list(VOICES)], color='#c3c8cb', lw=.8, zorder=1)
    for x, voice in enumerate(VOICES):
        ax.scatter(np.full(len(table), x), table[voice], s=12, color=COLOR[voice], alpha=.75, lw=0, zorder=2)
        mean, half = ci(table[voice])
        ax.errorbar(x + (.22 if x else -.22), mean, yerr=half, fmt='D', ms=4.5, color=INK, capsize=2.5, lw=1.1, zorder=3)
    ax.text(.98, .02, f'n = {len(table)}', transform=ax.transAxes, ha='right', va='bottom', fontsize=7, color=MUTED)


def grouped(ax, df, columns, labels, ylabel, ylim, highlight=None):
    x = np.arange(len(columns))
    ax.set(xticks=x, ylim=ylim, ylabel=ylabel, xlim=(-.6, len(columns) - .4))
    ax.set_xticklabels(labels)
    if highlight is not None:
        ax.axvspan(highlight - .45, highlight + .45, color='#eef1f3', zorder=0)
    for offset, voice in ((-.13, 'SAV'), (.13, 'SSV')):
        ax.errorbar([], [], fmt='o', color=COLOR[voice], label=voice)
        if df is None:
            continue
        rows = df[df.voice == voice]
        for i, column in enumerate(columns):
            mean, half = ci(rows[column])
            ax.errorbar(i + offset, mean, yerr=half, fmt='o', ms=4.5, color=COLOR[voice], capsize=2.5, lw=1.1)
    ax.legend(frameon=False, ncol=2, loc='upper right', fontsize=7.5, handletextpad=.2)
    if df is None:
        pending(ax)


def forest(ax, diffs, labels, xlabel, xlim):
    y = np.arange(len(labels))[::-1]
    ax.set(yticks=y, xlim=xlim, xlabel=xlabel, ylim=(-.7, len(labels) - .3))
    ax.set_yticklabels(labels)
    ax.axvline(0, color=MUTED, lw=.8, ls='--')
    ax.spines['left'].set_visible(False)
    ax.tick_params(axis='y', length=0)
    if diffs is None:
        pending(ax)
        return
    for yy, values in zip(y, diffs):
        mean, half = ci(values)
        ax.errorbar(mean, yy, xerr=half, fmt='o', ms=4, color=INK, capsize=2, lw=1)


def stamp(fig, development):
    if development:
        fig.suptitle('DEVELOPMENT DATA · NOT STUDY RESULTS', color='#b3261e', fontsize=8, fontweight='bold')


def save(fig, name, out):
    path = out / f'{name}.pdf'
    fig.savefig(path)
    fig.savefig(Path('/tmp') / f'{name}.png', dpi=130)
    plt.close(fig)
    if out == F:
        subprocess.run([sys.executable, str(F / 'normalize-pdf-font-maps.py'), str(path)], check=True)
    print('wrote', path)


# Figures
def participant_flow(flow, out):
    fig, ax = plt.subplots(figsize=(6.5, 2.9))
    fig.subplots_adjust(0, 0, 1, 1)
    ax.set(xlim=(0, 100), ylim=(0, 60))
    ax.axis('off')
    n = lambda key: f'n = {flow[key]}' if flow and key in flow else 'n = ___'

    def box(x, y, w, lines, edge=INK, h=8.4):
        ax.add_patch(FancyBboxPatch((x - w / 2, y - h / 2), w, h, boxstyle='round,pad=0.4,rounding_size=1.2', fc='white', ec=edge, lw=1))
        ax.text(x, y, '\n'.join(lines), ha='center', va='center', fontsize=7.8, linespacing=1.35)

    def arrow(x0, y0, x1, y1):
        ax.annotate('', (x1, y1), (x0, y0), arrowprops=dict(arrowstyle='-|>', color=INK, lw=.9, shrinkA=0, shrinkB=0))

    box(50, 54, 46, ['Consented and completed audio intake', n('consented')])
    box(88, 45, 23, ['Ineligible at review', 'or withdrew', n('ineligible')], MUTED, 12)
    arrow(50, 49.4, 50, 40.6)
    arrow(50, 45, 75.9, 45)
    ax.plot([24, 76], [36.5, 36.5], color=INK, lw=.9)
    ax.plot([50, 50], [40.6, 36.5], color=INK, lw=.9)
    for x, order, first, second, key, stop in ((24, 'Order A', 'SSV', 'SAV', 'order_a', 'stopped_a'), (76, 'Order B', 'SAV', 'SSV', 'order_b', 'stopped_b')):
        arrow(x, 36.5, x, 32.6)
        box(x, 28, 44, [f'{order}: {first} block, then {second} block', n(key)], COLOR[first])
        arrow(x, 23.4, x, 13.6)
        ax.text(x + 2, 18.5, f'Stopped or incomplete: {n(stop)}', fontsize=7.2, color=MUTED, va='center')
        ax.plot([x, x + 1.5], [18.5, 18.5], color=MUTED, lw=.8)
    ax.plot([24, 76], [13.6, 13.6], color=INK, lw=.9)
    arrow(50, 13.6, 50, 10.6)
    box(50, 6, 40, ['Included in the voice comparison', n('analyzed')])
    if not flow:
        ax.text(98, 3, PENDING.replace('\n', ' '), ha='right', color=MUTED, style='italic', fontsize=7.5)
    save(fig, 'results-participant-flow', out)


def manipulation_check(df, out, development):
    fig, ax = plt.subplots(figsize=(3.2, 2.7), constrained_layout=True)
    table = None if df is None else paired(blocks(df), survey(blocks(df), '__voice_item_1'))
    slope(ax, table, 'Voice is similar to mine (1–7)', (1, 7))
    stamp(fig, development)
    save(fig, 'results-manipulation-check', out)


def rq1_time(df, model, out, development):
    fig, axes = plt.subplots(1, 3, figsize=(6.5, 2.6), constrained_layout=True, gridspec_kw={'width_ratios': [1, 1.5, 1.1]})
    table = None if df is None else paired(df, df.completion, geomean)
    slope(axes[0], table, 'Active completion time (s)', (1, 25), log=True, title='(a) Participant means')
    ax = axes[1]
    ax.set(xticks=[0, 45, 90, 135, 180], xlim=(-12, 192), ylim=(0, 25), xlabel=r'Starting angle $|\theta_0|$ (°)',
           ylabel='Active completion time (s)')
    ax.set_title('(b) By starting angle', fontsize=8.5, loc='left')
    for offset, voice in ((-3, 'SAV'), (3, 'SSV')):
        if df is None:
            ax.plot([], [], 'o-', color=COLOR[voice], label=voice)
            continue
        rows = df[df.voice == voice]
        angles, means, halves = [], [], []
        for angle, group in rows.groupby('absolute_theta_deg'):
            per_person = group.groupby('participant_id').completion.agg(geomean)
            mean, half = ci(per_person)
            angles.append(angle + offset), means.append(mean), halves.append(half)
        ax.errorbar(angles, means, yerr=halves, fmt='o-', ms=4, color=COLOR[voice], capsize=2, lw=1.1, label=voice)
    ax.legend(frameon=False, loc='upper left', fontsize=7.5)
    if df is None:
        pending(ax)
    ax = axes[2]
    ax.set(xscale='log', xlim=(.8, 1.25), ylim=(-1, 1), yticks=[], xlabel='Time ratio, SSV / SAV')
    ax.set_xticks([.8, .9, 1, 1.1, 1.25])
    ax.xaxis.set_major_formatter(matplotlib.ticker.FuncFormatter(lambda x, _: f'{x:g}'))
    ax.xaxis.set_minor_formatter(matplotlib.ticker.NullFormatter())
    ax.spines['left'].set_visible(False)
    ax.axvline(1, color=MUTED, lw=.8, ls='--')
    ax.set_title('(c) Adjusted contrast', fontsize=8.5, loc='left')
    ax.text(.82, -.85, 'SSV faster', fontsize=7, color=COLOR['SSV'])
    ax.text(1.22, -.85, 'SAV faster', fontsize=7, color=COLOR['SAV'], ha='right')
    if model:
        ax.errorbar(model['estimate'], 0, xerr=[[model['estimate'] - model['low']], [model['high'] - model['estimate']]],
                    fmt='D', ms=5, color=INK, capsize=3, lw=1.2)
    else:
        pending(ax, 'Model estimate pending')
    stamp(fig, development)
    save(fig, 'results-rq1-completion-time', out)


def rq1_secondary(df, out, development):
    fig, axes = plt.subplots(1, 2, figsize=(6.5, 2.7), constrained_layout=True, gridspec_kw={'width_ratios': [1, 2.6]})
    table = None if df is None else paired(df, df.missed_target * 100)
    slope(axes[0], table, 'Missed targets (% of searches)', (0, 100), title='(a) Missed targets')
    columns = ['nasa_tlx_mental', 'nasa_tlx_physical', 'nasa_tlx_temporal', 'nasa_tlx_performance', 'nasa_tlx_effort', 'nasa_tlx_frustration', 'nasa_tlx_score']
    labels = ['Mental', 'Physical', 'Temporal', 'Perfor-\nmance*', 'Effort', 'Frustra-\ntion', 'Overall']
    data = None
    if df is not None:
        data = blocks(df)
        for column in columns:
            data[column] = pd.to_numeric(data[column], errors='coerce')
    grouped(axes[1], data, columns, labels, 'NASA-TLX rating (0–10)', (0, 10), highlight=6)
    axes[1].set_title('(b) Perceived workload', fontsize=8.5, loc='left')
    stamp(fig, development)
    save(fig, 'results-rq1-workload', out)


def rq2_eye(df, out, development):
    fig, axes = plt.subplots(1, 3, figsize=(6.5, 2.6), constrained_layout=True)
    for ax, (column, label, ylim, title) in zip(axes, (('ipa_candidate_hz', 'IPA (extrema / s)', (0, 2), '(a) Pupillary activity'),
                                                       ('fixation_count', 'Fixations per search', (0, 60), '(b) Fixations'),
                                                       ('saccade_frequency_hz', 'Saccades per second', (0, 5), '(c) Saccades'))):
        slope(ax, None if df is None else paired(df, df[column]), label, ylim, title=title)
    stamp(fig, development)
    save(fig, 'results-rq2-eye-measures', out)


def rq3_imi(df, out, development):
    fig, ax = plt.subplots(figsize=(4.6, 2.7), constrained_layout=True)
    columns = ['imi_enjoyment_mean', 'imi_effort_mean', 'imi_pressure_mean', 'imi_value_mean']
    data = None
    if df is not None:
        data = blocks(df)
        for column in columns:
            data[column] = pd.to_numeric(data[column], errors='coerce')
    grouped(ax, data, columns, ['Interest/\nenjoyment', 'Effort/\nimportance', 'Pressure/\ntension', 'Value/\nusefulness'],
            'IMI subscale mean (1–7)', (1, 7.6), highlight=0)
    stamp(fig, development)
    save(fig, 'results-rq3-imi', out)


EXPLORATORY = [('agent_copresence_mean', 'Co-presence'), ('agent_intelligence_mean', 'Perceived intelligence'),
               ('agent_anthropomorphism_mean', 'Anthropomorphism'), ('__guo_Q20', 'Appealing'), ('__guo_Q21', 'Eerie'),
               ('__guo_Q22', 'Familiar'), ('__arq_helpfulness', 'Helpful (ARQ)'), ('__arq_relatability', 'Relatable (ARQ)'),
               ('__arq_appropriate', 'Appropriate (ARQ)'), ('__arq_likeability', 'Likeable (ARQ)'),
               ('__arq_interact_again', 'Interact again (ARQ)'), ('__voice_item_6', 'Trusted the guidance'),
               ('__voice_item_9', 'Relied on the hints'), ('__voice_item_10', 'Voice sounded natural'),
               ('__voice_item_7', 'Comfortable hearing the voice'), ('__voice_item_8', 'Uncanny or unsettling')]


def exploratory(df, out, development):
    fig, ax = plt.subplots(figsize=(5.2, 4.2), constrained_layout=True)
    diffs = None
    if df is not None:
        data, diffs = blocks(df), []
        for column, _ in EXPLORATORY:
            values = survey(data, column) if column.startswith('__') else pd.to_numeric(data[column], errors='coerce')
            table = paired(data, values)
            diffs.append(table.SSV - table.SAV)
    forest(ax, diffs, [label for _, label in EXPLORATORY], 'Paired difference, SSV − SAV (1–7 scale points)', (-3, 3))
    stamp(fig, development)
    save(fig, 'results-exploratory-ratings', out)


def delivery(df, out, development):
    fig, axes = plt.subplots(1, 3, figsize=(6.5, 2.6), constrained_layout=True)
    for ax, (column, scale, label, ylim, title) in zip(axes, (('hint_count', 1, 'Spoken hints per search', (0, 20), '(a) Hints'),
                                                              ('gaze_validity_rate', 100, 'Valid gaze samples (%)', (0, 100), '(b) Gaze availability'),
                                                              ('interrupted_instruction_count', 1, 'Interrupted cues per search', (0, 3), '(c) Interruptions'))):
        slope(ax, None if df is None else paired(df, df[column] * scale), label, ylim, title=title)
    stamp(fig, development)
    save(fig, 'results-guidance-delivery', out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data', nargs='+')
    parser.add_argument('--model')
    parser.add_argument('--flow')
    parser.add_argument('--development', action='store_true')
    args = parser.parse_args()
    df = load(args.data, args.development) if args.data else None
    model = json.loads(Path(args.model).read_text()) if args.model else None
    flow = json.loads(Path(args.flow).read_text()) if args.flow else None
    out = Path('/tmp/results-preview') if args.development else F
    out.mkdir(exist_ok=True)
    participant_flow(flow, out)
    manipulation_check(df, out, args.development)
    rq1_time(df, model, out, args.development)
    rq1_secondary(df, out, args.development)
    rq2_eye(df, out, args.development)
    rq3_imi(df, out, args.development)
    exploratory(df, out, args.development)
    delivery(df, out, args.development)


if __name__ == '__main__':
    main()
