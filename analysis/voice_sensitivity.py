"""Planning-only voice sensitivity; synthetic assumptions, not participant results.

Simulate trial-level log search durations, then fit participant voice differences
to an intercept and counterbalanced period order. This candidate aggregate model
is not a frozen analysis plan or a trial-level mixed-effects/censoring model.
"""
import argparse
import csv
import hashlib
import itertools
import json
import math
from pathlib import Path
import platform

import numpy as np
import scipy
from scipy import optimize, special, stats

VERSION = 'voice-sensitivity-proposal-v2-signed-32'


def paired_power(n, dz, alpha):
    """Exact two-sided one-sample t power for normally distributed pair differences."""
    if type(n) is not int or n < 3 or not math.isfinite(dz) or not 0 < alpha < 1:
        raise ValueError('Invalid paired-test parameters')
    cutoff = stats.t.isf(alpha / 2, n - 1)
    # Symmetry expresses both tails as survival functions. The installed SciPy
    # can return NaN for an extreme negative-cutoff CDF despite a finite SF.
    return float(stats.nct.sf(cutoff, n - 1, abs(dz) * math.sqrt(n)) +
                 stats.nct.sf(cutoff, n - 1, -abs(dz) * math.sqrt(n)))


def detectable_dz(n, alpha, power):
    if not alpha < power < 1:
        raise ValueError('Target power must exceed alpha and be below one')
    upper = .5
    while paired_power(n, upper, alpha) < power:
        upper *= 2
    return float(optimize.brentq(lambda d: paired_power(n, d, alpha) - power, 0, upper))


def mean_ar1_variance(m, sigma, rho):
    """Variance of an m-trial mean, stationary AR(1), marginal SD sigma."""
    if type(m) is not int or m < 1 or not math.isfinite(sigma) or sigma < 0 or not -1 < rho < 1:
        raise ValueError('Invalid AR(1) parameters')
    return sigma**2 / m**2 * (m + 2 * sum((m-k)*rho**k for k in range(1, m)))


def wilson(successes, total):
    if total == 0:
        return [None, None]
    z = stats.norm.ppf(.975); p = successes / total
    center = (p + z*z/(2*total)) / (1+z*z/total)
    width = z*math.sqrt(p*(1-p)/total+z*z/(4*total**2))/(1+z*z/total)
    return [max(0., center-width), min(1., center+width)]


def recruitment_for_completion(complete_n, retention, assurance):
    """Minimum enrollment with a stated binomial chance of retaining complete_n."""
    if type(complete_n) is not int or complete_n < 1 or not 0 < retention <= 1 or not 0 < assurance < 1:
        raise ValueError('Invalid recruitment assumptions')
    low, high = complete_n-1, max(complete_n, math.ceil(complete_n/retention))
    while stats.binom.sf(complete_n-1, high, retention) < assurance: high *= 2
    while high-low > 1:
        middle=(low+high)//2
        if stats.binom.sf(complete_n-1,middle,retention) >= assurance: high=middle
        else: low=middle
    return high


def validate(config):
    if config.get('version') not in (VERSION, 'voice-sensitivity-proposal-v1'):
        raise ValueError('Unsupported sensitivity configuration')
    for key in ('seed', 'replications'):
        if type(config[key]) is not int or config[key] < (1 if key == 'replications' else 0):
            raise ValueError('Invalid ' + key)
    for key in ('participants', 'trials_per_voice', 'time_ratios', 'benchmark_alphas', 'retention_probabilities', 'scenarios'):
        if not isinstance(config[key], list) or not config[key]: raise ValueError('Empty ' + key)
    if any(type(n) is not int or n < 6 for n in config['participants']): raise ValueError('At least six participants per scenario')
    if any(type(m) is not int or m < 5 or (m % 5 and not (m == 32 and config['version'] == VERSION))
           for m in config['trials_per_voice']):
        raise ValueError('Trials must be 32 for v8 or a legacy multiple of five')
    if len(config['theta_log_effects']) != 5: raise ValueError('Five theta effects required')
    numeric = ['baseline_seconds','participant_intercept_sd','period_log_effect','period_person_sd','within_block_log_change','minimum_block_fraction','alpha','target_power']
    if any(type(config[k]) not in (int,float) or not math.isfinite(config[k]) for k in numeric): raise ValueError('Non-finite parameter')
    if config['baseline_seconds'] <= 0 or config['participant_intercept_sd'] < 0 or config['period_person_sd'] < 0: raise ValueError('Invalid scale')
    if not 0 < config['minimum_block_fraction'] <= 1: raise ValueError('Invalid retained-trial fraction')
    if not 0 < config['alpha'] < config['target_power'] < 1: raise ValueError('Invalid alpha/power')
    if any(type(x) not in (int,float) or not math.isfinite(x) or x <= 0 for x in config['time_ratios']): raise ValueError('Invalid time ratio')
    if any(not 0 < a < config['target_power'] for a in config['benchmark_alphas']): raise ValueError('Invalid benchmark alpha')
    if not 0 < config['completion_assurance'] < 1 or any(not 0 < p <= 1 for p in config['retention_probabilities']): raise ValueError('Invalid retention/assurance')
    if any(type(x) not in (int,float) or not math.isfinite(x) for x in config['theta_log_effects']): raise ValueError('Invalid geometry effect')
    names = set()
    for s in config['scenarios']:
        if not isinstance(s.get('name'), str) or not s['name'] or s['name'] in names: raise ValueError('Missing/duplicate scenario name')
        names.add(s['name'])
        keys = ['voice_slope_sd','trial_sd','ar1','trial_loss','participant_loss','informative_loss_strength']
        if any(type(s[k]) not in (int,float) or not math.isfinite(s[k]) for k in keys): raise ValueError('Invalid scenario parameter')
        if s['voice_slope_sd'] < 0 or s['trial_sd'] <= 0 or not -1 < s['ar1'] < 1: raise ValueError('Invalid variance/correlation')
        if not 0 <= s['trial_loss'] < 1 or not 0 <= s['participant_loss'] < 1: raise ValueError('Invalid loss probability')
        if s['informative_loss_strength'] and (s['voice_slope_sd'] == 0 or s['participant_loss'] == 0): raise ValueError('Informative loss needs baseline loss and heterogeneous slopes')


def theta_categories(rng, shape, trials):
    """v8 groups have eight signed steps; legacy benchmarks have five magnitudes."""
    categories = np.array([0, 1, 1, 2, 2, 3, 3, 4] if trials == 32 else [0, 1, 2, 3, 4])
    order = rng.random((*shape, trials // len(categories), len(categories))).argsort(axis=-1)
    return categories[order].reshape(*shape, trials)


def simulate(config, scenario, n, m, ratio, seed):
    """Bounded-memory Monte Carlo; each generated participant is an independent unit."""
    rng = np.random.default_rng(seed)
    estimates, rejections, retained, eligible_repetitions = [], 0, [], 0
    delta = math.log(ratio); repetitions = config['replications']
    minimum = math.ceil(config['minimum_block_fraction'] * m)
    for start in range(0, repetitions, 100):
        batch = min(100, repetitions-start)
        # +1 = self-similar in period 2. Odd N leaves one extra participant in one order.
        order = np.tile(np.where(np.arange(n) % 2, -1., 1.), (batch, 1))
        order *= rng.choice([-1., 1.], size=(batch,1))
        condition = np.stack([-order, order], axis=2) / 2
        voice_slope = rng.normal(0, scenario['voice_slope_sd'], (batch,n))
        intercept = rng.normal(0, config['participant_intercept_sd'], (batch,n))
        period_slope = config['period_log_effect'] + rng.normal(0, config['period_person_sd'], (batch,n))
        permutation = theta_categories(rng, (batch,n,2), m)
        geometry = np.asarray(config['theta_log_effects'])[permutation]
        noise = rng.normal(0, scenario['trial_sd'], (batch,n,2,m))
        rho = scenario['ar1']
        for t in range(1,m): noise[...,t] = rho*noise[...,t-1] + math.sqrt(1-rho*rho)*noise[...,t]
        values = (math.log(config['baseline_seconds']) + intercept[:,:,None,None] +
                  (delta+voice_slope)[:,:,None,None]*condition[:,:,:,None] +
                  period_slope[:,:,None,None]*np.array([0.,1.])[None,None,:,None] +
                  config['within_block_log_change']*np.linspace(-.5,.5,m) + geometry + noise)
        observed = rng.random(values.shape) >= scenario['trial_loss']
        count = observed.sum(axis=-1)
        means = np.divide((values*observed).sum(axis=-1), count, out=np.zeros_like(count,dtype=float), where=count>0)
        differences = (means[:,:,1]-means[:,:,0])*order
        probability = np.full((batch,n), scenario['participant_loss'])
        if scenario['informative_loss_strength']:
            probability = special.expit(special.logit(scenario['participant_loss']) +
                          scenario['informative_loss_strength']*voice_slope/scenario['voice_slope_sd'])
        available = (count >= minimum).all(axis=-1) & (rng.random((batch,n)) >= probability)
        for d, o, valid in zip(differences,order,available):
            retained.append(int(valid.sum()))
            if valid.sum() < 4 or len(np.unique(o[valid])) < 2: continue
            x = np.column_stack([np.ones(valid.sum()), o[valid]])
            inverse = np.linalg.inv(x.T@x)
            beta = inverse@x.T@d[valid]
            residual = d[valid]-x@beta
            df = int(valid.sum())-2
            variance = float(residual@residual / df)
            se = math.sqrt(variance*inverse[0,0])
            if not math.isfinite(se) or se <= 0: continue
            p = 2*stats.t.sf(abs(beta[0]/se),df)
            rejections += int(p < config['alpha']); eligible_repetitions += 1
            estimates.append(float(beta[0]))
    return dict(scenario=scenario['name'], participants_enrolled=n, trials_per_voice=m, time_ratio=ratio,
                true_log_ratio=delta, alpha=config['alpha'], replications=repetitions,
                estimable_replications=eligible_repetitions, rejections=rejections,
                rejection_rate_all=rejections/repetitions,
                rejection_rate_estimable=rejections/eligible_repetitions if eligible_repetitions else None,
                mc_interval_all=wilson(rejections,repetitions),
                mean_retained_participants=float(np.mean(retained)),
                mean_estimated_log_ratio=float(np.mean(estimates)) if estimates else None,
                bias_log_ratio=float(np.mean(estimates))-delta if estimates else None,
                interpretation='null rejection rate' if ratio == 1 else 'scenario rejection probability; not proven study power',
                cell_seed=seed)


def write_csv(path, rows):
    with path.open('w', newline='') as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0]), lineterminator='\n'); writer.writeheader()
        for row in rows:
            writer.writerow({k:json.dumps(v) if isinstance(v,list) else v for k,v in row.items()})


def run(config_path, out, plots=True):
    if out.exists(): raise ValueError('Output exists; choose a new directory')
    source = config_path.read_bytes(); config = json.loads(source); validate(config)
    script_hash = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    benchmark = [dict(participants=n,alpha=a,target_power=config['target_power'],
                      detectable_dz=detectable_dz(n,a,config['target_power']))
                 for n,a in itertools.product(config['participants'],config['benchmark_alphas'])]
    cells = itertools.product(config['scenarios'],config['participants'],config['trials_per_voice'],config['time_ratios'])
    simulation = []
    for scenario,n,m,ratio in cells:
        # Cell identity keeps results stable when config ordering changes.
        key=json.dumps([config['seed'],scenario,n,m,ratio],sort_keys=True).encode()
        seed=int.from_bytes(hashlib.sha256(key).digest()[:8],'big')
        simulation.append(simulate(config,scenario,n,m,ratio,seed))
    variance = []
    for scenario,m in itertools.product(config['scenarios'],config['trials_per_voice']):
        v = scenario['voice_slope_sd']**2+config['period_person_sd']**2+2*mean_ar1_variance(m,scenario['trial_sd'],scenario['ar1'])
        variance.append(dict(scenario=scenario['name'],trials_per_voice=m,complete_pair_contrast_sd=math.sqrt(v),
                             limit_sd_at_infinite_trials=math.hypot(scenario['voice_slope_sd'],config['period_person_sd']),
                             assumption='complete balanced geometry; stationary within-block residuals; independent blocks; fixed period adjusted'))
    recruitment = [dict(analyzable_participants=n,retention_probability=p,
                        expected_count_enrollment=math.ceil(n/p),assurance=config['completion_assurance'],
                        enrollment_for_assurance=recruitment_for_completion(n,p,config['completion_assurance']),
                        assumption='independent equal completion probability; no informative loss; not a selected target')
                   for n,p in itertools.product(config['participants'],config['retention_probabilities'])]
    out.mkdir(parents=True)
    (out/'configuration.json').write_bytes(source)
    report=dict(version=config['version'],status='protocol_proposal_synthetic_assumptions_not_final_sample_size',
                configuration_sha256=hashlib.sha256(source).hexdigest(),
                script_sha256=script_hash,
                python=platform.python_version(),numpy=np.__version__,scipy=scipy.__version__,
                benchmark=benchmark,variance=variance,simulation=simulation,recruitment=recruitment,
                limits='No participant data. Candidate order-adjusted log-mean contrast, not a fitted mixed-effects or censored model. Ratios/variances/missingness are hypothetical. No final endpoint, alpha, missingness rule, N or recruitment target selected. No voice-catalog or item-population generalization; no carryover model. Informative loss can cause bias, including false positives under the null.')
    (out/'sensitivity.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    write_csv(out/'paired_benchmark.csv',benchmark); write_csv(out/'variance.csv',variance); write_csv(out/'simulation.csv',simulation)
    write_csv(out/'recruitment.csv',recruitment)
    if plots:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        fig,axes=plt.subplots(1,len(config['time_ratios']),figsize=(5*len(config['time_ratios']),5),squeeze=False)
        colors=plt.get_cmap('tab10')
        for ax,ratio in zip(axes[0],config['time_ratios']):
            for scenario,m in itertools.product(config['scenarios'],config['trials_per_voice']):
                rows=sorted((r for r in simulation if r['scenario']==scenario['name'] and r['trials_per_voice']==m and r['time_ratio']==ratio),key=lambda r:r['participants_enrolled'])
                color=colors(config['scenarios'].index(scenario))
                x=[r['participants_enrolled'] for r in rows]
                ax.plot(x,[r['rejection_rate_all'] for r in rows],color=color,marker='o',linestyle='-' if m==max(config['trials_per_voice']) else '--',label=f"{scenario['name'].replace('_',' ')}, {m}/voice")
                ax.fill_between(x,[r['mc_interval_all'][0] for r in rows],[r['mc_interval_all'][1] for r in rows],color=color,alpha=.08)
            ax.axhline(config['alpha'] if ratio==1 else config['target_power'],color='gray',linestyle=':')
            ax.set(title=f'Hypothetical time ratio {ratio:g}',xlabel='Enrolled participants',ylabel='Rejection probability',ylim=(0,1))
        handles,labels=axes[0,0].get_legend_handles_labels()
        fig.legend(handles,labels,fontsize=7,loc='lower center',ncol=5)
        fig.suptitle('Synthetic planning scenarios — informative loss can inflate false positives')
        fig.tight_layout(rect=(0,.12,1,.96));fig.savefig(out/'sensitivity.png',dpi=160);plt.close(fig)
    return report


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',type=Path,required=True);parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--no-plots',action='store_true');args=parser.parse_args()
    try:
        result=run(args.config,args.out,not args.no_plots)
        print(f"Saved {len(result['simulation'])} synthetic scenarios; final study power remains unestablished.")
    except (OSError,ValueError,KeyError,TypeError) as error: raise SystemExit(str(error)) from None
