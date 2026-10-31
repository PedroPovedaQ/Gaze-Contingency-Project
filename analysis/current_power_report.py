"""Render current-design simulation results and exact paired-score power."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from scipy import optimize, stats

from current_design_power import MAIN, write_csv


def paired_power(n, dz, alpha):
    if n < 3:
        return 0.
    cutoff = stats.t.isf(alpha/2, n-1)
    nc = abs(dz)*math.sqrt(n)
    return float(stats.nct.sf(cutoff, n-1, nc)+stats.nct.sf(cutoff, n-1, -nc))


def enrolled_paired_power(n, dz, alpha, retention):
    """Exact binomial mixture of paired t power under normal pair differences."""
    return sum(stats.binom.pmf(k, n, retention)*paired_power(k, dz, alpha)
               for k in range(3, n+1))


def secondary_rows():
    return [dict(n_enrolled=n, outcome=outcome, dz=d, paired_retention=q,
                 expected_pairs=n*q,
                 power_unadjusted=enrolled_paired_power(n, d, .05, q),
                 power_holm_guaranteed=enrolled_paired_power(n, d, .025, q))
            for n in range(12, 61, 2) for d in [0., .2, .3, .4, .5, .6, .8]
            for q in [.855] for outcome in ["NASA-TLX", "IMI interest/enjoyment"]]


def load_csv(path):
    with path.open() as stream:
        return [{k: (v if k in ["scenario", "failures", "analysis_set"] else float(v))
                 for k, v in row.items()} for row in csv.DictReader(stream)]


def detectable(rows, scenario, target):
    r = sorted([x for x in rows if x["scenario"] == scenario], key=lambda x: -x["ratio"])
    for a, b in zip(r, r[1:]):
        if a["power"] < target <= b["power"]:
            w = (target-a["power"])/(b["power"]-a["power"])
            ratio = math.exp((1-w)*math.log(a["ratio"])+w*math.log(b["ratio"]))
            return dict(reduction=100*(1-ratio), bracket=[a["ratio"], b["ratio"]])
    return dict(reduction=None, bracket=None)


def curve_plot(rows, out, precision=None):
    if precision is not None:
        refined = {(r["scenario"], r["ratio"]): r for r in precision}
        rows = [refined.get((r["scenario"], r["ratio"]), r)
                if r["n_enrolled"] == 60 else r for r in rows]
    colors = ["#6a51a3", "#2171b5", "#008571", "#d47700", "#b62446"]
    fig, axes = plt.subplots(1, 3, figsize=(15, 5.8), sharex=True, sharey=True, facecolor="white")
    for ax, s in zip(axes, MAIN):
        ax.set_facecolor("white")
        for ratio, color in zip([.97, .95, .93, .90, .85], colors):
            r = sorted([r for r in rows if r["scenario"] == s.name and r["ratio"] == ratio],
                       key=lambda r: r["n_enrolled"])
            ns = [v["n_enrolled"] for v in r]
            ax.plot(ns, [100*v["power"] for v in r], color=color, lw=2,
                    label=f"{100*(1-ratio):.0f}% faster (ratio {ratio:.2f})")
            ax.fill_between(ns, [100*v["power_low"] for v in r],
                            [100*v["power_high"] for v in r], color=color, alpha=.09, linewidth=0)
        ax.axhline(80, color="#555555", linestyle="--", lw=1)
        ax.axhline(90, color="#999999", linestyle=":", lw=1)
        ax.set_title(f"{s.name.title()} assumptions\nLog-scale SD: residual {s.residual_sd:.2f}; voice slope {s.slope_sd:.2f}", fontsize=12)
        ax.set_xlabel("Participants enrolled (balanced order)", fontsize=11)
        ax.set_xlim(12, 60)
        ax.set_ylim(0, 102)
        ax.set_xticks([12, 20, 30, 40, 50, 60])
        ax.set_yticks([0, 20, 40, 60, 80, 90, 100])
        ax.grid(axis="y", alpha=.17)
        ax.spines[["top", "right"]].set_visible(False)
    axes[0].set_ylabel("Power for voice contrast (%)", fontsize=12)
    fig.suptitle("64-trial voice study: power depends on participant variability", fontsize=17, y=.99)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="lower center", ncol=3, bbox_to_anchor=(.5, .035), frameon=False, fontsize=11)
    counts = "1,000 simulations/point; 5,000 at N=60" if precision is not None else "1,000 simulations/point"
    fig.text(.5, .012, "Synthetic planning • 25 s deadline • successful trials only • dropout included • 95% Monte Carlo bands • "+counts, ha="center", fontsize=9)
    fig.tight_layout(rect=(0, .18, 1, .91))
    fig.savefig(out/"power-curves.png", dpi=200, facecolor="white")
    fig.savefig(out/"power-curves.pdf", facecolor="white")
    plt.close(fig)


def table(header, rows):
    return "| "+" | ".join(header)+" |\n| "+" | ".join(["---"]*len(header))+" |\n"+"\n".join(
        "| "+" | ".join(map(str, row))+" |" for row in rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    out = args.out
    groups = {kind: load_csv(out/f"{kind}.csv") for kind in ["curves", "grid", "stress", "precision"]}
    combined = [dict(analysis_set=k, **r) for k, rows in groups.items() for r in rows]
    write_csv(out/"results.csv", combined)
    secondary = secondary_rows()
    write_csv(out/"secondary-power.csv", secondary)
    curve_plot(groups["curves"], out, groups["precision"])
    precision = groups["precision"]
    get = lambda name, ratio: next(r for r in precision if r["scenario"] == name and r["ratio"] == ratio)
    thresholds = {s.name: {str(p): detectable(precision, s.name, p) for p in [.8, .9]} for s in MAIN}
    (out/"detectable-effects.json").write_text(json.dumps(thresholds, indent=2)+"\n")
    rows = []
    for s in MAIN:
        t = thresholds[s.name]
        fmt = lambda p: f"about {t[str(p)]['reduction']:.1f}%" if t[str(p)]["reduction"] is not None else ">25% (outside refinement grid)"
        rows.append([s.name.title(), fmt(.8), fmt(.9),
                     f"{100*get(s.name,.90)['power']:.1f}%", f"{100*get(s.name,.85)['power']:.1f}%"])
    headline_table = table(["Assumptions", "Reduction detectable at 80%", "At 90%", "Power for 10% faster", "Power for 15% faster"], rows)
    assumption_table = table(["Assumed value", "Optimistic", "Middle", "Pessimistic"], [
        ["Residual SD, log seconds", .35, .55, .80], ["Participant intercept SD", .15, .30, .50],
        ["Participant voice-slope SD", .10, .20, .35], ["Reference median seconds", 10, 12, 16],
        ["Incomplete-trial probability", "2%", "5%", "10%"], ["Dropout before block 2", "5%", "10%", "20%"]])
    null_rows = [get(s.name, 1.) for s in MAIN]
    null_table = table(["Assumptions", "Null rejection (95% MC interval)", "Slope at boundary", "Failed fits"], [
        [r["scenario"], f"{100*r['power']:.2f}% ({100*r['power_low']:.2f}–{100*r['power_high']:.2f}%)",
         f"{100*r['slope_boundary_rate']:.1f}%", int(r["failed_fits"])] for r in null_rows])
    miss_table = table(["Assumptions / latent ratio", "Neutral timeout", "Self-similar timeout", "Fitted ratio", "Bias in log ratio", "Latent-ratio 95% CI coverage"], [
        [f"{s.name} / {ratio:.2f}", f"{100*r['timeout_rate_neutral']:.1f}%", f"{100*r['timeout_rate_self']:.1f}%",
         f"{math.exp(r['mean_log_estimate']):.3f}", f"{r['bias_vs_latent']:+.3f}", f"{100*r['coverage_latent']:.1f}%"]
        for s in MAIN for ratio in [1., .90, .85] for r in [get(s.name, ratio)]])
    grid = groups["grid"]
    slope_table = table(["Voice-slope SD", "Power: 5% faster", "Power: 10% faster", "Power: 15% faster"], [
        [v]+[f"{100*next(r for r in grid if r['residual_sd']==.55 and r['intercept_sd']==.30 and r['slope_sd']==v and r['ratio']==ratio)['power']:.1f}%"
             for ratio in [.95, .90, .85]] for v in [0., .10, .20, .35, .50]])
    stress_table = table(["Stress case at N=60", "Rejection at conditional βV=0", "Power: 10% faster", "Latent coverage at ratio .90"], [
        [name, f"{100*a['power']:.1f}%", f"{100*b['power']:.1f}%", f"{100*b['coverage_latent']:.1f}%"]
        for name in sorted({r["scenario"] for r in groups["stress"]})
        for a in groups["stress"] if a["scenario"]==name and a["n_enrolled"]==60 and a["ratio"]==1
        for b in groups["stress"] if b["scenario"]==name and b["n_enrolled"]==60 and b["ratio"]==.90])
    secondary_table = table(["Standardized paired effect dz", "Unadjusted power", "Conservative Holm power"], [
        [f"{r['dz']:.1f}", f"{100*r['power_unadjusted']:.1f}%", f"{100*r['power_holm_guaranteed']:.1f}%"]
        for r in secondary if r["n_enrolled"]==60 and r["outcome"]=="NASA-TLX" and r["dz"]>0])
    secondary_thresholds = [optimize.brentq(lambda d: enrolled_paired_power(60, d, .025, .855)-p, 0, 2) for p in [.8, .9]]
    runs = [json.loads((out/f"{k}-run.json").read_text()) for k in groups]
    sims = sum(int(r["repetitions"]) for r in combined)
    failures = sum(int(r["failed_fits"]) for r in combined)
    retries = sum(int(r["optimizer_retries"]) for r in combined)
    most_boundary = max(groups["curves"], key=lambda r: r["slope_boundary_rate"])
    evidence = json.loads((out/"development-evidence.json").read_text())
    early, recent, pooled = (evidence[k] for k in ["earlier_runs", "run30", "combined"])
    mid = thresholds["middle"]
    report = f"""# Current 64-trial voice-study power analysis

**Protocol proposal — recommend enrolling 60 participants, with 30 assigned to each voice order.** This includes dropout: the middle scenario expects about 54 participants with observations in both blocks. At N=60, the successful-completion mixed model detects approximately **{mid['0.8']['reduction']:.1f}% faster search at 80% power** and **{mid['0.9']['reduction']:.1f}% faster search at 90% power**, under the middle assumptions below. These are synthetic planning estimates, not measured voice effects or a promise of power.

{headline_table}

**A 3% time reduction is out of reach within N≤60 across these main scenarios. A 5% reduction is out of reach under the middle and pessimistic scenarios; it is only near the 80% boundary under optimistic assumptions.** Sixty is the largest feasible candidate requested, selected for a roughly 10% target reduction under middle assumptions, not a mathematically guaranteed minimum. For a firm 90% goal, plan around the larger detectable reduction above or revisit the participant ceiling. The smallest scientifically meaningful effect remains Pedro's substantive decision. More trials cannot substitute for more participants when voice responses vary substantially.

**Important limit:** these powers concern rejection by a model fitted to observed successful searches. Deadline censoring biases that model's estimate of the latent, all-assigned-search time ratio. More participants do not remove that bias. This analysis fulfills the planned successful-time LMM simulation; it does not establish adequate power for a censored survival model or solve the primary-estimand issue. Preregister a complementary completion-probability/time-to-event analysis and the interpretation of successful-time results.

For the 10% middle-assumption contrast, the refined estimate is **{100*get('middle',.90)['power']:.1f}% power (95% Monte Carlo interval {100*get('middle',.90)['power_low']:.1f}–{100*get('middle',.90)['power_high']:.1f}%)**. The lower bound is slightly below 80%, so this is an approximately 80% design, not a certified ≥80% guarantee. In a correlated-random-effects stress case, rejection at a zero conditional voice coefficient rises to about 9%; that assumption cannot be treated as harmless.

## Scope and current design

**Implemented design inputs:** two voice blocks × 32 measured trials, within participant, 25-second active-search deadline, one uncounted practice trial. The simulation uses four independently shuffled eight-step groups per block: 0, +45, −45, +90, −90, +135, −135, 180 degrees. Thus each signed step appears four times and absolute-angle counts are 4/8/8/8/4. Order is exactly counterbalanced at enrollment; dropout can unbalance retained observations. There are 168 objects per trial, held constant and therefore absent from the statistical model.

Source: `Assets/ChallengeSet.cs`, v8 `readiness-target-angle-v8-signed-steps-32-one-practice`, checked against the saved production C# allocation and schedule CSV in `work_log/receipts/2026-10-30-schedule-v8/`. The simulation reproduces its angle-allocation/shuffle distribution. It does not replay .NET random-number bytes, object identities, height placements or the practice-target trajectory. None enters this candidate outcome generator. Exact pre-cue angles are **assumed** to be nominal magnitude plus uniform ±3° jitter clipped to [0,180]; this is not measured headset error. The model fits those simulated exact angles.

The Conductor thesis's `chapters/04-results.tex` Section 4.1 and `chapters/unused/planned-statistical-analysis.tex` supplied the model. The latter's 40/80 trial references are historical and are replaced here by current 32/64 counts. Earlier sensitivity/power outputs were not used as current power evidence. No thesis files, participant files, headset state or voice services were changed.

## Model and test actually simulated

**Protocol proposal:** for successful active-search times, fit

`log(T) ~ 1 + V + period + order + a + a² + position + (1 | participant) + (V | participant)`

with independent random intercept and slope components. Here V=−0.5 for selected/neutral and +0.5 for self-similar; period=−0.5/+0.5; order=−1/+1; a=|theta₀|/90−1; position runs linearly from −0.5 to +0.5 within each 32-trial block. The angle function is quadratic (two degrees of freedom); position is linear (one). Zero-degree trials remain included. Random intercept–slope covariance is fixed to zero to avoid estimating a weakly supported correlation at small N. The slope absorbs participant-specific voice **and block** variation; with one block per voice it cannot distinguish the two.

Each dataset gets a genuine trial-level Gaussian restricted-maximum-likelihood fit. The implementation profiles residual variance and uses exact participant cross-products, Woodbury inversion and the determinant lemma for speed. It never substitutes a regression on participant mean times. Reference checks compare its likelihood, analytic gradient, fixed coefficient, GLS covariance and variance estimates to a direct dense calculation and [statsmodels MixedLM](https://www.statsmodels.org/stable/generated/statsmodels.regression.mixed_linear_model.MixedLM.html).

Test V two-sided at α=.05 using its GLS coefficient/SE and a Student t reference with **number of participants having successful observations in both blocks minus 2** degrees of freedom. This is an explicit small-sample approximation, **not** Satterthwaite or Kenward–Roger; changing that test requires updated power calculations. Null simulations check its behavior. The nominal 95% interval uses the same t cutoff. Optimization permits exact zero variance components; a slope variance ratio <10⁻⁶ is reported as a boundary/singular slope (effectively an intercept-only fit at zero). Intercept boundaries are also recorded. There is no significance-based random-effect selection. Failed optimization gets one deterministic retry; unresolved failures count as non-rejections in headline power, with conditional-on-fit power separately available.

In the independent, centered random-effects generator without censoring, exp(βV) is both the conditional voice time ratio and the common-angle-standardized marginal arithmetic ratio: the symmetric voice coding gives both voices equal random-effect and residual retransformation factors. No voice×angle interaction is generated. That equality does not extend automatically to censored successful samples, correlated random effects, or real data. The correlated-effects stress test intentionally violates it. For the real study, define standardization and retransformation explicitly.

## What came from development data

**Observed historical evidence:** a read-only audit of nine manifest-listed DEV_NEUTRAL headset replays from October 19–20 (runs 20–28), plus the October 23 run-30 trial summary. Source SHA-256, byte size and modification time were checked before and after reading; all ten sources were unchanged. `development-evidence.json` contains paths/hashes and scope, and `development-trials.csv` contains the extracted historical timings. No unidentified participant files were opened.

- Earlier runs: {early['started']} started, {early['completed']} completed, {early['unfinished']} unfinished; median completed time **{early['median_seconds']:.3f} s**, raw log-time SD **{early['log_sd']:.3f}**, within-run-centered log SD **{early['within_run_log_sd']:.3f}** ({early['within_run_residual_df']} residual df). {early['completed_at_or_above_25']} successful searches reached/exceeded 25 s; the longest was {early['maximum_seconds']:.3f} s and was retained.
- Run 30: {recent['started']} started / {recent['completed']} completed searches; median **{recent['median_seconds']:.3f} s**, raw log-time SD **{recent['log_sd']:.3f}** ({recent['within_run_residual_df']} residual df); {recent['completed_at_or_above_25']} reached/exceeded 25 s. The next transitioning trial had no search start and is not counted as an attempted search.
- Combined: {pooled['started']} started, {pooled['completed']} completed; raw log SD **{pooled['log_sd']:.3f}**, within-run-centered SD **{pooled['within_run_log_sd']:.3f}** ({pooled['within_run_residual_df']} df); {pooled['completed_at_or_above_25']} of {pooled['completed']} completed searches reached/exceeded 25 s. These are historical exceedances, **not observed current-design timeout rates**. Within-run centering does not adjust for geometry or learning. Sparse runs do not justify a reliable geometry-adjusted variance model.

All use one development label and neutral voice, with no paired self-similar outcomes. **Neither between-participant intercept SD nor voice-slope SD is empirically estimable from this evidence.** Runs must not be treated as different participants. The recordings predate the current layout/model/deadline; run 30 identifies `gaze-directional-v26-visible-reference` and the v5 40-trial quick schedule. They are weak guidance only. No questionnaire variance or standardized effect was inferred from them.

## Synthetic assumptions and sensitivity grid

**All generator parameters below are assumptions.** The observed spread motivates exploring roughly .35–.80 residual SD; .55 is a planning choice near the descriptive within-run spread, not an estimated current-study residual SD. The 10/12/16-second reference medians are deliberately faster than the historical completed medians and must be checked in a current-design pilot. Pessimistic is a scenario label, not an empirical worst-case bound.

{assumption_table}

Reference median is at 90°, centered position/period and V=0, with zero random effects. Shared assumed fixed effects are −.08 for period 2 versus period 1, −.10 over a block, +.30a−.08a² for angle and zero order effect. Intercept and slope are normal and independent, residuals are normal and independent in the main scenarios. Ratios are 1.00 (null), .97, .95, .93, .90, .85; every even enrollment 12–60 is simulated. Additional N=60 precision points are .925, .875, .80 and .75.

The separate **full factorial variance grid at N=60** crosses residual SD {{.35,.55,.80}}, intercept SD {{.15,.30,.50}} and slope SD {{0,.10,.20,.35,.50}} at all six main ratios, with the middle timing/missingness assumptions. This separates variance dimensions instead of confounding them only in three named scenarios. A slope SD of .20 means a one-SD participant contrast multiplier exp(.20)=1.22; .50 means 1.65. These are wide synthetic heterogeneity assumptions.

At N=60 with middle residual/intercept SD and missingness, changing only voice-slope SD gives:

{slope_table}

## Censoring, incomplete trials and dropout

Each simulated search has a latent lognormal completion time. If an exposed, otherwise complete trial reaches 25 s it is a timeout: it has **no completion time** in the LMM, not a 25-second success. Timeout rates divide by attempted searches with known latent timeout/success status; technical/incomplete trials and post-dropout unexposed trials are excluded from that denominator and reported separately. No durations are imputed and no slow successful trials below the deadline are trimmed.

Incomplete trials are independent Bernoulli omissions at the probabilities above. Dropout is an independent participant event before block 2; block-1 data remain in the LMM. It is not wholesale deletion of that participant. Retrospective paired questionnaires are unavailable for those dropouts. Main scenarios assume missing completely at random apart from the deadline. Informative missingness is examined separately, not asserted absent in real sessions.

{miss_table}

Ratio-dependent timeout rates are a real implication of the generator: faster self-similar trials cross the deadline less often. Voice effects are centered symmetrically around a common baseline, so changing the ratio shifts neutral times slightly upward and self-similar times downward; this is not a simulation that holds the neutral distribution fixed. The successful-only fitted contrast generally attenuates toward 1. The coverage column asks whether the nominal interval covers the **latent** log ratio; poor coverage demonstrates estimand/selection bias, not Monte Carlo error. Do not interpret an apparently precise successful-time ratio as the all-assigned-trial benefit.

The fitted-ratio column is exp(mean estimated log coefficient). Bias and coverage summarize successful model fits; unresolved failures have no estimate or interval and remain in the power denominator as non-rejections.

Stress scenarios retain middle settings except: no effective deadline; within-block AR(1)=.3; incomplete-trial log odds increase by 2×(latent log time−log 12); or intercept–slope correlation=.5. All run at N=30 and 60. They are failure-mode checks, not fitted corrections:

{stress_table}

Serial dependence or informative missingness can change rejection/error rates. The correlated-effects case also makes the marginal arithmetic ratio differ from exp(βV); neither marginal equivalence nor a robust all-trial interpretation is claimed there. Its elevated conditional-null rejection is a warning about the combination of censoring and covariance misspecification, not evidence of extra useful power. A current-design pilot must check these assumptions before treating the middle scenario as a recruitment guarantee.

## Null behavior, boundary fits and Monte Carlo precision

N=60 null checks (5,000 datasets per main scenario):

{null_table}

Total **{sims:,} generated datasets / attempted mixed-model fits**, across {len(combined):,} cells. There were **{failures:,} unresolved failed fits** (counted as non-rejections) and **{retries:,} optimizer retries**. `results.csv` contains per-cell failure reasons, boundary rates, retained participants, missing/timeout denominators, latent bias, latent coverage, power, MC SE and 95% Wilson intervals. Boundary fits are not silently discarded. Boundary rates are among successful fits; failures have no interpretable variance estimates.

In the main curves, slope-boundary frequency reaches **{100*most_boundary['slope_boundary_rate']:.1f}%** ({most_boundary['scenario']}, N={int(most_boundary['n_enrolled'])}, ratio {most_boundary['ratio']:.2f}). Small samples therefore often cannot distinguish a small slope variance from zero. The factorial grid additionally includes a true zero-slope case; boundary estimates there are expected and are still reported, not recoded as optimizer failures.

Main curves and factorial grid use 1,000 replicates/cell: maximum power MC SE 1.58 percentage points, about ±3.1 points for a 95% interval near 50% power. Stress cells use 2,000: maximum SE 1.12 points. N=60 refinement uses 5,000: maximum SE .71 points; around 80% power it is .57 points (about ±1.1 points at 95%). These quantify simulation noise, **not uncertainty in the assumed variances**. Headline detectable reductions interpolate linearly in log ratio between adjacent refinement points; they are approximate, not exact optimization thresholds. Brackets are saved in `detectable-effects.json`. Curves show 95% Monte Carlo intervals and may wiggle slightly; no monotonic smoothing is used. Repeated cells across runs use the same seed prefix and are **not independent replications to pool**.

## NASA-TLX and IMI interest/enjoyment

One score per participant per voice gives **one paired difference per outcome per participant**. Sixty-four trials do not create 64 questionnaire observations. Define dz as the mean paired difference divided by the SD of paired differences (not a between-person standardized effect). Exact two-sided noncentral-t power is integrated over binomial retention, assuming normal pair differences. Assumed pair retention is .90 for both blocks × .95 for a complete pair of scores = .855, or **51.3 expected usable pairs from 60 enrollees**. No empirical score SD is available to translate dz to raw scale points.

**Open multiplicity decision:** propose a two-outcome family containing NASA-TLX total and IMI interest/enjoyment. Holm tests the smaller p at .025 and, if rejected, the other at .05. The .025 power below is a conservative per-outcome guarantee/lower bound for Holm, and unadjusted .05 power an upper bound; actual Holm power depends on both true effects and their dependence. No exact joint Holm power is claimed. A larger family would require recalculation.

{secondary_table}

At N=60, the conservative Holm bound reaches 80% at **dz≈{secondary_thresholds[0]:.3f}**, and 90% at **dz≈{secondary_thresholds[1]:.3f}**. Both outcomes have the same standardized-effect curve; these are not claims that their true effects or scale variances are equal. `secondary-power.csv` gives all even N=12–60 and dz=0,.2,.3,.4,.5,.6,.8. These are analytic probabilities with **no Monte Carlo error**. Score eligibility, complete-subscale rules and the secondary family must be fixed before collection; missing scores are not zeros.

## Reproduction and artifacts

Scripts: `analysis/current_design_power.py`, `analysis/current_power_report.py`, `analysis/power_development_audit.py`; tests: `analysis/test_current_design_power.py`, `analysis/test_current_power_report.py`, and `analysis/test_power_development_audit.py`. Pinned dependencies are in `analysis/power/requirements-current-design.txt`. Set BLAS thread limits to avoid oversubscription. Fixed master seed **20261031** and SHA-256-derived per-cell seeds are stable across worker counts; each dataset is independently generated within its cell. Processes are CPU workers, not agent delegation.

```sh
python -m venv /tmp/gaze-power-reproduce
/tmp/gaze-power-reproduce/bin/pip install -r analysis/power/requirements-current-design.txt
export OPENBLAS_NUM_THREADS=1 VECLIB_MAXIMUM_THREADS=1
# Use a new output directory; simulation and evidence commands refuse overwrites.
PY=/tmp/gaze-power-reproduce/bin/python
OUT=/tmp/gaze-power-reproduced
$PY analysis/current_design_power.py --out "$OUT" --kind curves --repetitions 1000 --workers 6
$PY analysis/current_design_power.py --out "$OUT" --kind grid --repetitions 1000 --workers 6
$PY analysis/current_design_power.py --out "$OUT" --kind stress --repetitions 2000 --workers 6
$PY analysis/current_design_power.py --out "$OUT" --kind precision --repetitions 5000 --workers 6
$PY analysis/power_development_audit.py --out "$OUT"
$PY analysis/current_power_report.py --out "$OUT"
$PY -m unittest discover -s analysis -p 'test_current*power*.py' -v
$PY -m unittest discover -s analysis -p 'test_power_development_audit.py' -v
```

Raw development source paths are local and read-only; the simulation is independent of them. Without those files, copy the frozen `development-evidence.json` into the new output directory to render the report, clearly retaining its historical provenance. `*-run.json` records versions, seed, wall time and simulation-source hash. `input-provenance.json` records the runtime base commit and hashes of the schedule and thesis plan read for this task. Run wall times (some jobs overlapped): {', '.join(r['kind']+' '+str(round(r['seconds'],1))+' s' for r in runs)}. No GPU, microphone or headset is needed.

`results.csv` consolidates all trial-level simulations; individual run CSVs preserve their own cells. `secondary-power.csv` contains the paired-score results. `power-curves.png` and `.pdf` are the same white-background figure. `development-evidence.json` and `development-trials.csv` keep the observed inputs distinct from synthetic outputs. Validation receipts and the local commit are recorded in the coordination DONE post and work log. Nothing was pushed or published to Overleaf.
"""
    (out/"REPORT.md").write_text(report)
    manifest = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir()
                if p.is_file() and p.name != "artifact-sha256.json"}
    (out/"artifact-sha256.json").write_text(json.dumps(manifest, indent=2, sort_keys=True)+"\n")
    print(headline_table)


if __name__ == "__main__":
    main()
