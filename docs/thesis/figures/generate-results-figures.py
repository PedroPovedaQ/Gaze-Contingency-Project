"""Chapter 4 result figures, drawn from the trial exports or as empty templates until participant data exist.

Without --data every panel keeps its axes, units and conditions and says that data are pending. --simulated instead
draws an illustrative 20-participant dataset (see simulate()) with "Simulated – Awaiting Participant Data" behind it.
With --data the same figures are drawn from every full_run_measures.csv under the given folders. Rows flagged
exclude_from_study_analysis are dropped unless --development is given, which stamps each figure as development
data and writes to /tmp/results-preview instead of the thesis figures folder.

Usage:
  python generate-results-figures.py                                  # templates
  python generate-results-figures.py --simulated                      # illustrative simulated data
  python generate-results-figures.py --data <GazeData exports> [--model rq1_time_ratio.json] [--flow flow.json]
  python generate-results-figures.py --data <synthetic exports> --development

Model estimates (results-stats.tex, used by Chapter 4's text) need statsmodels, which the project environment
does not include:
  uv run --no-project --with statsmodels --with pandas --with scipy --with matplotlib --with pypdf \
      python generate-results-figures.py --simulated

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
MIN_GEE_CLUSTERS = 10
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
    """One survey item across every survey file; the column prefix carries the file name, which can differ by run."""
    names = [c for c in df.columns if c.startswith('survey__') and c.endswith(suffix)]
    if not names:
        return pd.Series(np.nan, index=df.index)
    values = df[names].apply(pd.to_numeric, errors='coerce')
    if (values.notna().sum(axis=1) > 1).any():
        sys.exit(f'more than one survey answer for {suffix} in a row: {names}')
    return values.bfill(axis=1).iloc[:, 0]


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
                   'interrupted_instruction_count'):
        df[column] = pd.to_numeric(df[column], errors='coerce')
    # Gaze validity from distinct source observations per eye (export v3); older exports only have the frame rate.
    if 'eye_left__source_observation_validity_rate' in df:
        df['gaze_validity_rate'] = pd.concat([pd.to_numeric(df[f'eye_{eye}__source_observation_validity_rate'], errors='coerce')
                                              for eye in ('left', 'right')], axis=1).mean(axis=1)
    else:
        df['gaze_validity_rate'] = pd.to_numeric(df.get('gaze_validity_rate'), errors='coerce')
    for measure in ('ipa_candidate_hz', 'fixation_count', 'saccade_frequency_hz'):
        df[measure] = pd.concat([pd.to_numeric(df[f'eye_{eye}__{measure}'], errors='coerce') for eye in ('left', 'right')], axis=1).mean(axis=1)
    df['completion'] = df.time_to_final_selection_seconds.where(df.missed_target == 0)
    # Model covariates: period (block 1 or 2), position within the block, and order (A = SSV first).
    if 'pre_cue_theta_absolute_deg' in df:
        df['absolute_theta_deg'] = pd.to_numeric(df.pre_cue_theta_absolute_deg, errors='coerce')
    # Export v3 records period, position and order; derive them for older exports.
    if 'position_within_block' in df:
        df['period'] = pd.to_numeric(df.period, errors='coerce')
        df['position'] = pd.to_numeric(df.position_within_block, errors='coerce') - 1
        df['order'] = df.condition_order
    else:
        df['period'] = pd.to_numeric(df.block_index, errors='coerce') + 1
        df['position'] = df.sort_values('trial_index').groupby(['participant_id', 'period']).cumcount()
        df['order'] = df.voice_order.map({'selfsimilar_then_neutral': 'A', 'neutral_then_selfsimilar': 'B'})
    # Every participant contributes one run; a second run must be adjudicated before analysis, not pooled.
    runs = df.groupby('participant_id').run_number.nunique()
    if (runs > 1).any() and not development:
        sys.exit('participants with more than one run: ' + ', '.join(runs[runs > 1].index))
    return df


def simulate(n=20, seed=20261103):
    """Illustrative 20-participant dataset in the load() schema. Search time, timeouts, fixation rate, IPA, hints and
    gaze validity are scaled to the October 25 development recordings (median search about 14.5 s, 3 of 22 timeouts,
    about 2.6 fixations/s, IPA 0.4-0.9 extrema/s, 82% valid gaze, about 0.4 hints/s). Saccade rate follows fixation
    rate, as in Chiossi et al. 2024 (about 2.6-3 Hz); the development detector counts only low-head-speed intervals.
    Questionnaire levels, person-level spreads and all SSV effects are assumptions: the development recordings contain
    no paired voices or questionnaires."""
    rng = np.random.default_rng(seed)
    angles = np.repeat([0, 45, 90, 135, 180], [6, 12, 12, 12, 6])
    rows, blocks_ = [], []
    likert_items = (('__voice_item_1', 2.3, 2.5), ('__guo_Q20', 4.6, .1), ('__guo_Q21', 2.4, .4), ('__guo_Q22', 3.2, 1.0),
                    ('__arq_helpfulness', 5.4, .2), ('__arq_relatability', 4.0, .4), ('__arq_appropriate', 5.3, 0),
                    ('__arq_likeability', 4.9, -.1), ('__arq_interact_again', 4.8, .1), ('__voice_item_6', 5.2, .2),
                    ('__voice_item_9', 5.0, .2), ('__voice_item_10', 5.1, -.4), ('__voice_item_7', 5.6, -.3),
                    ('__voice_item_8', 2.0, .5))  # (column, SAV mean, SSV shift)
    for p in range(n):
        pid, intercept, slope_ = f'SIM{p + 1:03d}', rng.normal(0, .22), rng.normal(-.03, .07)
        order = ('SSV', 'SAV') if p % 2 == 0 else ('SAV', 'SSV')
        # Person-level levels: people differ more in eye behavior, exposure and ratings than blocks or trials do.
        validity = np.clip(rng.beta(16, 3.2), .55, .99)
        fix_mean, ipa_mean = rng.normal(2.6, .4), rng.normal(.66, .08)
        hint_rate = max(rng.normal(.40, .09), .12)
        workload, imi = rng.normal(0, 1.4), rng.normal(0, .7, 4)
        likert_person = rng.normal(0, .8, len(likert_items))
        similarity_person = rng.normal(0, 1.2)
        for period, voice in enumerate(order):
            ssv = voice == 'SSV'
            fix_block, ipa_block, validity_block = rng.normal(0, .15), rng.normal(0, .05), rng.normal(0, .06)
            for position, theta in enumerate(rng.permutation(angles)):
                log_t = np.log(9.2) + intercept + .0042 * theta - .07 * period + (slope_ if ssv else 0) + rng.normal(0, .42)
                time = float(np.exp(log_t))
                missed = int(time >= 25)
                time = min(time, 25.0)
                fix_rate = max(fix_mean + fix_block + rng.normal(0, .3), .8)
                hints = rng.poisson(hint_rate * time)
                rows.append(dict(participant_id=pid, voice=voice, period=period + 1, position=position, order='A' if p % 2 == 0 else 'B',
                                 absolute_theta_deg=theta, missed_target=missed, time_to_final_selection_seconds=time,
                                 hint_count=hints, gaze_validity_rate=np.clip(validity + validity_block + rng.normal(0, .04), 0, 1),
                                 interrupted_instruction_count=rng.poisson(.04 * hints),
                                 fixation_count=rng.poisson(fix_rate * time),
                                 saccade_frequency_hz=fix_rate * rng.uniform(.85, .98),
                                 # IPA needs a 2-s clean pupil segment; about half the searches have none.
                                 ipa_candidate_hz=max(rng.normal(ipa_mean + ipa_block - (.02 if ssv else 0), .1), .2)
                                 if time > 4 and rng.random() < .55 else np.nan))
            r = lambda mean, sd, lo, hi: float(np.clip(np.round(rng.normal(mean, sd)), lo, hi))
            tlx = dict(nasa_tlx_mental=r(5.6 + workload - .3 * ssv, 1, 0, 10), nasa_tlx_physical=r(2.4 + .5 * workload, 1, 0, 10),
                       nasa_tlx_temporal=r(5.2 + workload - .2 * ssv, 1, 0, 10),
                       nasa_tlx_performance=r(6.6 - .6 * workload + .2 * ssv, 1, 0, 10),
                       nasa_tlx_effort=r(5.5 + workload - .3 * ssv, 1, 0, 10),
                       nasa_tlx_frustration=r(3.6 + workload - .3 * ssv, 1, 0, 10))
            tlx['nasa_tlx_score'] = np.mean([v if k != 'nasa_tlx_performance' else 10 - v for k, v in tlx.items()])
            tlx.update(nasa_tlx_scale_max=10, nasa_tlx_performance_direction='high_is_good')
            sub = lambda mean, k, shift: float(np.clip(rng.normal(mean + imi[k] + shift * ssv, .65), 1, 7))
            block = dict(participant_id=pid, voice=voice, **tlx,
                         imi_enjoyment_mean=sub(4.9, 0, .2), imi_effort_mean=sub(5.3, 1, 0),
                         imi_pressure_mean=sub(3.1, 2, -.1), imi_value_mean=sub(5.0, 3, .2),
                         agent_copresence_mean=float(np.clip(rng.normal(4.4 + likert_person[0] + .3 * ssv, .6), 1, 7)),
                         agent_intelligence_mean=float(np.clip(rng.normal(4.8 + likert_person[1] + .1 * ssv, .6), 1, 7)),
                         agent_anthropomorphism_mean=float(np.clip(rng.normal(3.9 + likert_person[2] + .2 * ssv, .6), 1, 7)))
            for k, (suffix, sav, shift) in enumerate(likert_items):
                person = similarity_person if k == 0 else likert_person[k]
                block['survey' + suffix] = r(sav + person + shift * ssv, .9 if k else 1.1, 1, 7)
            blocks_.append(block)
    df = pd.DataFrame(rows).merge(pd.DataFrame(blocks_), on=['participant_id', 'voice'])
    df['completion'] = df.time_to_final_selection_seconds.where(df.missed_target == 0)
    return df


def paired(df, values, how='mean'):
    """Participant-by-voice table with both voices present."""
    table = df.assign(v=values).groupby(['participant_id', 'voice']).v.agg(how).unstack()
    return table.reindex(columns=VOICES).dropna()


def geomean(values):
    return np.exp(np.log(values.dropna()).mean())


def blocks(df):
    """One row per participant and voice block for questionnaire scores (block answers repeat on every trial row)."""
    return df.groupby(['participant_id', 'voice'], as_index=False).first()


# Panels
def boxes(ax, samples, positions, voices, width):
    """Filled box per voice: median, quartiles, whiskers to 1.5 IQR, hollow circles for outliers."""
    parts = ax.boxplot([np.asarray(v, float)[~np.isnan(np.asarray(v, float))] for v in samples], positions=positions,
                       widths=width, patch_artist=True, manage_ticks=False, zorder=2,
                       medianprops=dict(color=INK, lw=1.1), whiskerprops=dict(color='#5b6266', lw=.9),
                       capprops=dict(color='#5b6266', lw=.9),
                       flierprops=dict(marker='o', ms=4, mfc='white', mec='#5b6266', mew=.8))
    for patch, voice in zip(parts['boxes'], voices):
        patch.set(facecolor=COLOR[voice], alpha=.6, edgecolor='#5b6266', lw=.9)


def slope(ax, table, ylabel, ylim, log=False, title=None, note=''):
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
    # Faint lines join each participant's two values behind the boxes; small jitter separates ties.
    jitter = np.random.default_rng(0).uniform(-.05, .05, (len(table), 2))
    for (_, row), dx in zip(table.iterrows(), jitter):
        ax.plot([dx[0], 1 + dx[1]], row[list(VOICES)], color='#c9ced1', lw=.6, zorder=1)
    boxes(ax, [table[voice] for voice in VOICES], [0, 1], VOICES, width=.42)
    ax.set_xlabel(f'n = {len(table)}{note}', fontsize=7, color=MUTED)


def grouped(ax, df, columns, labels, ylabel, ylim, highlight=None):
    x = np.arange(len(columns))
    ax.set(xticks=x, ylim=ylim, ylabel=ylabel, xlim=(-.6, len(columns) - .4))
    ax.set_xticklabels(labels)
    if highlight is not None:
        ax.axvspan(highlight - .45, highlight + .45, color='#eef1f3', zorder=0)
    for offset, voice in ((-.19, 'SAV'), (.19, 'SSV')):
        ax.add_patch(plt.Rectangle((0, 0), 0, 0, facecolor=COLOR[voice], alpha=.6, edgecolor='#5b6266', label=voice))
        if df is not None:
            rows = df[df.voice == voice]
            boxes(ax, [rows[column] for column in columns], x + offset, [voice] * len(columns), width=.34)
    ax.legend(frameon=False, ncol=2, loc='upper right', fontsize=7.5, handletextpad=.4, handlelength=1.2)
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
    if development == 'simulated':
        # Behind the data: clear axes backgrounds so the figure-level text shows through.
        for ax in fig.axes:
            ax.set_facecolor('none')
            for patch in ax.patches:
                patch.set_alpha(.55)
        width = fig.get_figwidth()
        fig.text(.5, .5, 'Simulated – Awaiting Participant Data', ha='center', va='center', rotation=18 if width > 6 else 26,
                 fontsize=min(17, 2.2 * width + 2), fontweight='bold', color='#e6e9eb', zorder=-1)
    elif development:
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
    slope(ax, table, 'Voice is similar to mine (1–7)', (.6, 7.4))
    ax.set_yticks(range(1, 8))
    stamp(fig, development)
    save(fig, 'results-manipulation-check', out)


def rq1_time(df, model, out, development):
    fig, axes = plt.subplots(1, 3, figsize=(6.5, 2.6), constrained_layout=True, gridspec_kw={'width_ratios': [1, 1.5, 1.1]})
    table = None if df is None else paired(df, df.completion, geomean)
    slope(axes[0], table, 'Active completion time (s)', (4, 25), log=True, title='(a) Participants')
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


def tlx_0_10(data):
    """NASA-TLX on a common 0–10 scale with Performance oriented so higher means more successful.

    Each response carries its own scale (0–10 from the final survey, 0–100 allowed for nasa_tlx.csv) and
    Performance direction. Responses without both are left out rather than guessed; nasa_tlx_score is already
    reversed correctly by analysis/tlx_scoring.py."""
    data = data.copy()
    scale = pd.to_numeric(data.nasa_tlx_scale_max, errors='coerce').where(lambda m: m.isin([10, 100]))
    direction = data.nasa_tlx_performance_direction.where(data.nasa_tlx_performance_direction.isin(['high_is_good', 'high_is_poor']))
    scale = scale.where(direction.notna())
    for column in ('nasa_tlx_mental', 'nasa_tlx_physical', 'nasa_tlx_temporal', 'nasa_tlx_performance', 'nasa_tlx_effort',
                   'nasa_tlx_frustration', 'nasa_tlx_score'):
        data[column] = pd.to_numeric(data[column], errors='coerce') * 10 / scale
    data['nasa_tlx_performance'] = data.nasa_tlx_performance.where(direction == 'high_is_good', 10 - data.nasa_tlx_performance)
    return data


def rq1_secondary(df, out, development):
    fig, axes = plt.subplots(1, 2, figsize=(6.5, 2.7), constrained_layout=True, gridspec_kw={'width_ratios': [1, 2.6]})
    table = None if df is None else paired(df, df.missed_target * 100)
    slope(axes[0], table, 'Missed targets (% of searches)', (-2, 50), title='(a) Missed targets')
    columns = ['nasa_tlx_mental', 'nasa_tlx_physical', 'nasa_tlx_temporal', 'nasa_tlx_performance', 'nasa_tlx_effort', 'nasa_tlx_frustration', 'nasa_tlx_score']
    labels = ['Mental', 'Physical', 'Temporal', 'Perfor-\nmance*', 'Effort', 'Frustra-\ntion', 'Overall']
    grouped(axes[1], None if df is None else tlx_0_10(blocks(df)), columns, labels, 'NASA-TLX rating (0–10)', (0, 10), highlight=6)
    axes[1].set_title('(b) Perceived workload', fontsize=8.5, loc='left')
    stamp(fig, development)
    save(fig, 'results-rq1-workload', out)


def rq2_eye(df, out, development):
    fig, axes = plt.subplots(1, 3, figsize=(6.5, 2.6), constrained_layout=True)
    for ax, (column, label, ylim, title) in zip(axes, (('ipa_candidate_hz', 'IPA (extrema / s)', (0, 1.5), '(a) Pupillary activity'),
                                                       ('fixation_count', 'Fixations per search', (0, 60), '(b) Fixations'),
                                                       ('saccade_frequency_hz', 'Saccades per second', (0, 5), '(c) Saccades'))):
        # Coverage: the share of searches with a value for this measure.
        note = '' if df is None else f' · {df[column].notna().mean():.0%} of searches'
        slope(ax, None if df is None else paired(df, df[column]), label, ylim, title=title, note=note)
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
    for ax, (column, scale, label, ylim, title) in zip(axes, (('hint_count', 1, 'Spoken hints per search', (0, 15), '(a) Hints'),
                                                              ('gaze_validity_rate', 100, 'Valid gaze observations (%)', (0, 100), '(b) Gaze availability'),
                                                              ('interrupted_instruction_count', 1, 'Interrupted cues per search', (0, 1.5), '(c) Interruptions'))):
        slope(ax, None if df is None else paired(df, df[column] * scale), label, ylim, title=title)
    stamp(fig, development)
    save(fig, 'results-guidance-delivery', out)


def fmt(value, digits=2):
    # \ensuremath gives a true minus sign whether the macro is used in text or in math.
    return f'{value:.{digits}f}'.replace('-', '\\ensuremath{-}')


def p_text(p):
    if not np.isfinite(p):
        return '= n/a'
    return '< .001' if p < .001 else '= ' + f'{p:.3f}'.lstrip('0')


def model_stats(df):
    """Fit the planned models and return LaTeX macro values; needs statsmodels.

    Trial level: log completion time (completed searches) and the eye measures use linear mixed models with voice,
    period, order, position in the block and |theta0| as fixed effects and a participant random intercept and
    random voice slope; missed targets use a GEE logistic model clustered by participant.
    Participant-by-block scores (similarity, NASA-TLX overall, IMI interest/enjoyment) use paired t-tests.
    Missed targets and NASA-TLX form the RQ1 secondary family and are Holm-adjusted.
    """
    import statsmodels.formula.api as smf
    import statsmodels.api as sm
    from statsmodels.stats.multitest import multipletests
    d = df.assign(ssv=(df.voice == 'SSV').astype(float), period2=(df.period == 2).astype(float),
                  orderB=(df.order == 'B').astype(float),
                  position_c=df.position - df.position.mean(), theta_c=(df.absolute_theta_deg - 90) / 45)
    fixed = 'ssv + period2 + orderB + position_c + theta_c'
    macros = {'N': str(d.participant_id.nunique()), 'Trials': str(len(d))}

    def lmm(name, y, rows):
        # A random voice slope keeps trials from counting as independent evidence about the voice effect.
        fit = smf.mixedlm(f'{y} ~ {fixed}', rows, groups=rows.participant_id, re_formula='~ssv').fit(reml=True)
        b, (lo, hi) = fit.params['ssv'], fit.conf_int().loc['ssv']
        macros.update({name + 'B': fmt(b, 3), name + 'Lo': fmt(lo, 3), name + 'Hi': fmt(hi, 3),
                       name + 'Z': fmt(fit.tvalues['ssv']), name + 'P': p_text(fit.pvalues['ssv']), name + 'Obs': str(len(rows))})
        return b, lo, hi

    done = d[d.completion.notna()].assign(log_t=lambda x: np.log(x.completion))
    b, lo, hi = lmm('Time', 'log_t', done)
    macros.update(TimeRatio=fmt(np.exp(b)), TimeRatioLo=fmt(np.exp(lo)), TimeRatioHi=fmt(np.exp(hi)))
    for name, column in (('Ipa', 'ipa_candidate_hz'), ('Fix', 'fixation_count'), ('Sacc', 'saccade_frequency_hz')):
        lmm(name, column, d[d[column].notna()])

    # A GEE's sandwich standard errors need enough clusters; with fewer the model is not reported.
    if d.participant_id.nunique() >= MIN_GEE_CLUSTERS:
        gee = smf.gee(f'missed_target ~ {fixed}', 'participant_id', d, family=sm.families.Binomial(),
                      cov_struct=sm.cov_struct.Exchangeable()).fit()
        bounds = gee.conf_int().loc['ssv']
        macros.update(MissOR=fmt(np.exp(gee.params['ssv'])), MissORLo=fmt(np.exp(bounds.iloc[0])),
                      MissORHi=fmt(np.exp(bounds.iloc[1])), MissZ=fmt(gee.tvalues['ssv']))
        secondary = [gee.pvalues['ssv']]
    else:
        macros.update({k: 'n/a' for k in ('MissOR', 'MissORLo', 'MissORHi', 'MissZ')})
        secondary = [np.nan]

    def paired_t(name, table):
        diff = table.SSV - table.SAV
        if len(diff) < 2 or diff.std(ddof=1) < 1e-9:  # no spread in the differences: the test is undefined
            macros.update({name + k: 'n/a' for k in ('Lo', 'Hi', 'T', 'Dz')}, **{name + 'P': '= n/a'})
            macros.update({name + 'SAV': fmt(table.SAV.mean()), name + 'SSV': fmt(table.SSV.mean()),
                           name + 'Diff': fmt(diff.mean()), name + 'Df': str(len(diff) - 1)})
            return np.nan
        t, p = stats.ttest_rel(table.SSV, table.SAV)
        mean, half = ci(diff)
        macros.update({name + 'SAV': fmt(table.SAV.mean()), name + 'SSV': fmt(table.SSV.mean()), name + 'Diff': fmt(mean),
                       name + 'Lo': fmt(mean - half), name + 'Hi': fmt(mean + half), name + 'T': fmt(t), name + 'Df': str(len(diff) - 1),
                       name + 'P': p_text(p), name + 'Dz': fmt(diff.mean() / diff.std(ddof=1))})
        return p

    b_ = blocks(df)
    paired_t('Sim', paired(b_, survey(b_, '__voice_item_1')))
    secondary.append(paired_t('Tlx', paired(b_, tlx_0_10(b_).nasa_tlx_score)))
    paired_t('Imi', paired(b_, pd.to_numeric(b_.imi_enjoyment_mean, errors='coerce')))
    finite = [p for p in secondary if np.isfinite(p)]
    adjusted = iter(multipletests(finite, method='holm')[1]) if finite else iter(())
    holm = [next(adjusted) if np.isfinite(p) else np.nan for p in secondary]
    macros.update(MissP=p_text(secondary[0]), MissPHolm=p_text(holm[0]), TlxPHolm=p_text(holm[1]))
    return macros


def write_stats(macros, path):
    lines = ['% Generated by generate-results-figures.py; do not edit by hand.']
    lines += [f'\\newcommand{{\\res{k}}}{{{v}}}' for k, v in macros.items()]
    path.write_text('\n'.join(lines) + '\n')
    print('wrote', path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data', nargs='+')
    parser.add_argument('--model')
    parser.add_argument('--flow')
    parser.add_argument('--development', action='store_true')
    parser.add_argument('--simulated', action='store_true')
    args = parser.parse_args()
    df = load(args.data, args.development) if args.data else simulate() if args.simulated else None
    model, macros = None, None
    if df is not None:
        try:
            macros = model_stats(df)
            model = {'estimate': float(macros['TimeRatio']), 'low': float(macros['TimeRatioLo']), 'high': float(macros['TimeRatioHi'])}
        except ImportError:
            print('statsmodels not installed: skipping model estimates; run with uv run --with statsmodels')
    if args.model:
        model = json.loads(Path(args.model).read_text())
    flow = json.loads(Path(args.flow).read_text()) if args.flow else None
    out = Path('/tmp/results-preview') if args.development else F
    out.mkdir(exist_ok=True)
    mode = 'simulated' if args.simulated and not args.data else args.development
    if not args.simulated:
        participant_flow(flow, out)
    manipulation_check(df, out, mode)
    rq1_time(df, model, out, mode)
    rq1_secondary(df, out, mode)
    rq2_eye(df, out, mode)
    rq3_imi(df, out, mode)
    exploratory(df, out, mode)
    delivery(df, out, mode)
    if macros:
        write_stats(macros, out / 'results-stats.tex')


if __name__ == '__main__':
    main()
