"""Power curves for the thesis, redrawn at print size from the simulation's curves.csv.

Usage: python generate-power-curves.py <path/to/curves.csv>
"""
import sys
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import pandas as pd

out = Path(__file__).resolve().parent / 'power-curves.pdf'
plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 9, 'pdf.fonttype': 42})
data = pd.read_csv(sys.argv[1])
ratios = [(.97, '3%', '#7a5fb0'), (.95, '5%', '#2f6fbf'), (.93, '7%', '#1e8f6e'), (.90, '10%', '#d9822b'), (.85, '15%', '#c8324d')]
fig, axes = plt.subplots(1, 3, figsize=(6.5, 2.9), sharey=True, constrained_layout=True)
for ax, scenario in zip(axes, ('optimistic', 'middle', 'pessimistic')):
    rows = data[data.scenario == scenario]
    for ratio, label, color in ratios:
        row = rows[abs(rows.ratio - ratio) < 1e-9].sort_values('n_enrolled')
        ax.fill_between(row.n_enrolled, row.power_low * 100, row.power_high * 100, color=color, alpha=.15, lw=0)
        ax.plot(row.n_enrolled, row.power * 100, color=color, lw=1.5, label=label)
    ax.axhline(80, color='#555555', lw=.8, ls='--')
    ax.set(title=scenario.capitalize(), xlim=(12, 60), ylim=(0, 100), xticks=[12, 20, 30, 40, 50, 60], yticks=[0, 20, 40, 60, 80, 100])
    ax.spines[['top', 'right']].set_visible(False)
    ax.set_xlabel('Participants enrolled')
axes[0].set_ylabel('Power (%)')
handles, labels = axes[0].get_legend_handles_labels()
fig.legend(handles, labels, title='Search time reduction', loc='outside lower center', ncol=5, frameon=False)
fig.savefig(out)
fig.savefig('/tmp/power-curves-preview.png', dpi=130)
