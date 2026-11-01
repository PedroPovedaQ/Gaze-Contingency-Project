# Current 96-trial voice-study power analysis

**Protocol proposal — initial enrollment remains Pedro's decision: N=20, 10 per voice order.** Under middle assumptions, a 10% latent time reduction has **38.82% power with 96 trials versus 34.66% with 64 trials: a gain of 4.16 percentage points**. These are synthetic planning estimates, not observed voice effects. Fifty percent more trials does not imply fifty percent more power.

## N=20 and gain over 64 trials

| Scenario | Reduction | 64-trial power | 96-trial power (95% MC interval) | Gain (percentage points) |
| --- | --- | --- | --- | --- |
| optimistic | 5% | 35.07% | 39.08% (38.13–40.04%) | +4.01 |
| optimistic | 7% | 60.01% | 66.51% (65.58–67.43%) | +6.50 |
| optimistic | 10% | 89.31% | 93.11% (92.60–93.59%) | +3.80 |
| optimistic | 15% | 99.85% | 99.91% (99.83–99.95%) | +0.06 |
| middle | 5% | 11.71% | 13.76% (13.10–14.45%) | +2.05 |
| middle | 7% | 19.61% | 20.80% (20.02–21.61%) | +1.19 |
| middle | 10% | 34.66% | 38.82% (37.87–39.78%) | +4.16 |
| middle | 15% | 66.28% | 73.71% (72.84–74.56%) | +7.43 |
| pessimistic | 5% | 6.54% | 7.38% (6.88–7.91%) | +0.84 |
| pessimistic | 7% | 8.20% | 9.27% (8.72–9.85%) | +1.07 |
| pessimistic | 10% | 11.49% | 12.99% (12.35–13.66%) | +1.50 |
| pessimistic | 15% | 21.40% | 25.67% (24.82–26.54%) | +4.27 |

Both designs use 10,000 replicates per scenario/effect at N=20, including null and 3% cells in the CSV. Maximum MC SE is 0.50 percentage points per estimate; the 95% interval is at most about ±0.98 points. The master seed and per-cell hash policy are unchanged. Different trial counts consume random draws differently: these are not paired datasets. We report each design's uncertainty, not a paired-difference confidence interval. Refinements replace the 1,000-replicate seed prefix; they are never pooled with it.

**N needed for 80% at a 10% reduction, middle assumptions:** N=52 (26 per order), estimated power 80.70%. This is the first tested even enrollment whose point estimate reaches 80%, not a guaranteed threshold. All even N=12–60 were evaluated. Participant voice-response variation limits the benefit of additional trials.

| Enrollment | Power for 10% faster | 95% MC interval | Replicates |
| --- | --- | --- | --- |
| 44 | 73.60% | 72.36–74.80% | 5000 |
| 46 | 75.22% | 74.00–76.40% | 5000 |
| 48 | 78.36% | 77.20–79.48% | 5000 |
| 50 | 79.00% | 77.85–80.11% | 5000 |
| 52 | 80.70% | 79.58–81.77% | 5000 |
| 54 | 82.64% | 81.57–83.66% | 5000 |
| 56 | 83.12% | 82.06–84.13% | 5000 |
| 58 | 83.62% | 82.57–84.62% | 5000 |
| 60 | 86.02% | 85.03–86.95% | 5000 |

**Secondary outcomes at N=20:** 17.1 expected usable questionnaire pairs under .855 retention; conservative Holm (.025) power reaches 80% at **dz≈0.814** for each of NASA-TLX and IMI interest/enjoyment. This is unchanged by increasing trial count because the same 20 people supply the paired scores. At N=60 the corresponding value remains dz≈0.442.

## Session length

**Protocol proposal, not observed timing:** 96 × 25 seconds gives a 40-minute active-search ceiling. Assuming 5–10 seconds readiness/opening/feedback per trial adds 8–16 minutes, plus 3–5 minutes for the between-block break and about one minute for practice: **52–62 minutes for the task portion** at the timeout ceiling. This is 16–18 minutes longer than the comparable 64-trial planning estimate (36–44 minutes). Faster searches shorten it. Consent/intake, voice preparation, headset fitting/calibration, final surveys, extra rests and technical pauses are additional; the full appointment has no established upper bound and needs a rehearsal. The simulation holds dropout, residual variability and learning constant; it does not model added fatigue from a longer session.

## N=60 sensitivity for comparison

| Assumptions | Reduction detectable at 80% | At 90% | Power for 10% faster | Power for 15% faster |
| --- | --- | --- | --- | --- |
| Optimistic | about 4.7% | about 5.5% | 100.0% | 99.9% |
| Middle | about 9.4% | about 10.9% | 86.0% | 99.7% |
| Pessimistic | about 18.2% | about 20.6% | 34.0% | 64.4% |

Detectable-effect values above refer to N=60 and interpolate within the simulated grid. Values outside the grid remain unavailable. The smallest scientifically meaningful effect remains a substantive study decision.

**Important limit:** these powers concern rejection by a model fitted to successful searches. Deadline censoring biases its estimate of the latent, all-assigned-search time ratio. More trials or people do not remove that bias. This rerun preserves the previous model and does not establish power for a censored survival model. The primary estimand, complementary completion/time-to-event analysis, covariance structure and multiplicity remain preregistration decisions.

## Scope and current design

**Implemented design inputs:** two voice blocks × 48 measured trials, within participant, 25-second active-search deadline, one uncounted practice trial. The simulation uses six independently shuffled eight-step groups per block: 0, +45, −45, +90, −90, +135, −135, 180 degrees. Thus each signed step appears six times and absolute-angle counts are 6/12/12/12/6. Order is exactly counterbalanced at enrollment; dropout can unbalance retained observations. There are 168 objects per trial, held constant and therefore absent from the statistical model.

Source: `Assets/ChallengeSet.cs`, v9 `readiness-target-angle-v9-signed-steps-48-one-practice`, checked against the saved production C# allocation and schedule CSV in `work_log/receipts/2026-11-01-schedule-v9/`. The simulation reproduces its angle-allocation/shuffle distribution. It does not replay .NET random-number bytes, object identities, height placements or the practice-target trajectory. None enters this candidate outcome generator. Exact pre-cue angles are **assumed** to be nominal magnitude plus uniform ±3° jitter clipped to [0,180]; this is not measured headset error. The model fits those simulated exact angles.

The Conductor thesis's `chapters/04-results.tex` Section 4.1 and `chapters/unused/planned-statistical-analysis.tex` supplied the model. The latter's 40/80 trial references are historical and are replaced here by current 48/96 counts. Earlier sensitivity/power outputs were not used as current power evidence. No thesis files, participant files or voice services were changed by the analysis. Builds and conditional deployment are recorded separately.

## Model and test actually simulated

**Protocol proposal:** for successful active-search times, fit

`log(T) ~ 1 + V + period + order + a + a² + position + (1 | participant) + (V | participant)`

with independent random intercept and slope components. Here V=−0.5 for selected/neutral and +0.5 for self-similar; period=−0.5/+0.5; order=−1/+1; a=|theta₀|/90−1; position runs linearly from −0.5 to +0.5 within each 48-trial block. The angle function is quadratic (two degrees of freedom); position is linear (one). Zero-degree trials remain included. Random intercept–slope covariance is fixed to zero to avoid estimating a weakly supported correlation at small N. The slope absorbs participant-specific voice **and block** variation; with one block per voice it cannot distinguish the two.

Each dataset gets a genuine trial-level Gaussian restricted-maximum-likelihood fit. The implementation profiles residual variance and uses exact participant cross-products, Woodbury inversion and the determinant lemma for speed. It never substitutes a regression on participant mean times. Reference checks compare its likelihood, analytic gradient, fixed coefficient, GLS covariance and variance estimates to a direct dense calculation and [statsmodels MixedLM](https://www.statsmodels.org/stable/generated/statsmodels.regression.mixed_linear_model.MixedLM.html).

Test V two-sided at α=.05 using its GLS coefficient/SE and a Student t reference with **number of participants having successful observations in both blocks minus 2** degrees of freedom. This is an explicit small-sample approximation, **not** Satterthwaite or Kenward–Roger; changing that test requires updated power calculations. Null simulations check its behavior. The nominal 95% interval uses the same t cutoff. Optimization permits exact zero variance components; a slope variance ratio <10⁻⁶ is reported as a boundary/singular slope (effectively an intercept-only fit at zero). Intercept boundaries are also recorded. There is no significance-based random-effect selection. Failed optimization gets one deterministic retry; unresolved failures count as non-rejections in headline power, with conditional-on-fit power separately available.

In the independent, centered random-effects generator without censoring, exp(βV) is both the conditional voice time ratio and the common-angle-standardized marginal arithmetic ratio: the symmetric voice coding gives both voices equal random-effect and residual retransformation factors. No voice×angle interaction is generated. That equality does not extend automatically to censored successful samples, correlated random effects, or real data. The correlated-effects stress test intentionally violates it. For the real study, define standardization and retransformation explicitly.

## What came from development data

**Observed historical evidence retained byte-for-byte from the 64-trial report (0889229), without reopening the sources:** a read-only audit of nine manifest-listed DEV_NEUTRAL headset replays from October 19–20 (runs 20–28), plus the October 23 run-30 trial summary. Source SHA-256, byte size and modification time were checked before and after reading; all ten sources were unchanged. `development-evidence.json` contains paths/hashes and scope, and `development-trials.csv` contains the extracted historical timings. No unidentified participant files were opened.

- Earlier runs: 27 started, 21 completed, 6 unfinished; median completed time **19.558 s**, raw log-time SD **0.740**, within-run-centered log SD **0.517** (15 residual df). 7 successful searches reached/exceeded 25 s; the longest was 275.924 s and was retained.
- Run 30: 5 started / 5 completed searches; median **21.486 s**, raw log-time SD **0.406** (4 residual df); 2 reached/exceeded 25 s. The next transitioning trial had no search start and is not counted as an attempted search.
- Combined: 32 started, 26 completed; raw log SD **0.683**, within-run-centered SD **0.496** (19 df); 9 of 26 completed searches reached/exceeded 25 s. These are historical exceedances, **not observed current-design timeout rates**. Within-run centering does not adjust for geometry or learning. Sparse runs do not justify a reliable geometry-adjusted variance model.

All use one development label and neutral voice, with no paired self-similar outcomes. **Neither between-participant intercept SD nor voice-slope SD is empirically estimable from this evidence.** Runs must not be treated as different participants. The recordings predate the current layout/model/deadline; run 30 identifies `gaze-directional-v26-visible-reference` and the v5 40-trial quick schedule. They are weak guidance only. No questionnaire variance or standardized effect was inferred from them.

## Synthetic assumptions and sensitivity grid

**All generator parameters below are assumptions.** The observed spread motivates exploring roughly .35–.80 residual SD; .55 is a planning choice near the descriptive within-run spread, not an estimated current-study residual SD. The 10/12/16-second reference medians are deliberately faster than the historical completed medians and must be checked in a current-design pilot. Pessimistic is a scenario label, not an empirical worst-case bound.

| Assumed value | Optimistic | Middle | Pessimistic |
| --- | --- | --- | --- |
| Residual SD, log seconds | 0.35 | 0.55 | 0.8 |
| Participant intercept SD | 0.15 | 0.3 | 0.5 |
| Participant voice-slope SD | 0.1 | 0.2 | 0.35 |
| Reference median seconds | 10 | 12 | 16 |
| Incomplete-trial probability | 2% | 5% | 10% |
| Dropout before block 2 | 5% | 10% | 20% |

Reference median is at 90°, centered position/period and V=0, with zero random effects. Shared assumed fixed effects are −.08 for period 2 versus period 1, −.10 over a block, +.30a−.08a² for angle and zero order effect. Intercept and slope are normal and independent, residuals are normal and independent in the main scenarios. Ratios are 1.00 (null), .97, .95, .93, .90, .85; every even enrollment 12–60 is simulated. Additional N=60 precision points are .925, .875, .80 and .75.

The separate **full factorial variance grid at N=60** crosses residual SD {.35,.55,.80}, intercept SD {.15,.30,.50} and slope SD {0,.10,.20,.35,.50} at all six main ratios, with the middle timing/missingness assumptions. This separates variance dimensions instead of confounding them only in three named scenarios. A slope SD of .20 means a one-SD participant contrast multiplier exp(.20)=1.22; .50 means 1.65. These are wide synthetic heterogeneity assumptions.

At N=60 with middle residual/intercept SD and missingness, changing only voice-slope SD gives:

| Voice-slope SD | Power: 5% faster | Power: 10% faster | Power: 15% faster |
| --- | --- | --- | --- |
| 0.0 | 69.8% | 99.9% | 99.8% |
| 0.1 | 54.0% | 98.7% | 99.9% |
| 0.2 | 32.4% | 85.6% | 99.2% |
| 0.35 | 15.8% | 54.0% | 87.4% |
| 0.5 | 11.5% | 32.9% | 63.2% |

## Censoring, incomplete trials and dropout

Each simulated search has a latent lognormal completion time. If an exposed, otherwise complete trial reaches 25 s it is a timeout: it has **no completion time** in the LMM, not a 25-second success. Timeout rates divide by attempted searches with known latent timeout/success status; technical/incomplete trials and post-dropout unexposed trials are excluded from that denominator and reported separately. No durations are imputed and no slow successful trials below the deadline are trimmed.

Incomplete trials are independent Bernoulli omissions at the probabilities above. Dropout is an independent participant event before block 2; block-1 data remain in the LMM. It is not wholesale deletion of that participant. Retrospective paired questionnaires are unavailable for those dropouts. Main scenarios assume missing completely at random apart from the deadline. Informative missingness is examined separately, not asserted absent in real sessions.

| Assumptions / latent ratio | Neutral timeout | Self-similar timeout | Fitted ratio | Bias in log ratio | Latent-ratio 95% CI coverage |
| --- | --- | --- | --- | --- | --- |
| optimistic / 1.00 | 1.3% | 1.3% | 1.000 | -0.000 | 94.3% |
| optimistic / 0.90 | 1.8% | 0.9% | 0.906 | +0.006 | 93.0% |
| optimistic / 0.85 | 2.1% | 0.8% | 0.858 | +0.009 | 91.4% |
| middle / 1.00 | 12.5% | 12.5% | 1.000 | -0.000 | 94.6% |
| middle / 0.90 | 14.2% | 10.9% | 0.927 | +0.029 | 78.9% |
| middle / 0.85 | 15.3% | 10.2% | 0.888 | +0.044 | 58.5% |
| pessimistic / 1.00 | 31.5% | 31.5% | 1.000 | +0.000 | 94.8% |
| pessimistic / 0.90 | 33.5% | 29.7% | 0.945 | +0.049 | 72.4% |
| pessimistic / 0.85 | 34.5% | 28.6% | 0.917 | +0.076 | 47.8% |

Ratio-dependent timeout rates are a real implication of the generator: faster self-similar trials cross the deadline less often. Voice effects are centered symmetrically around a common baseline, so changing the ratio shifts neutral times slightly upward and self-similar times downward; this is not a simulation that holds the neutral distribution fixed. The successful-only fitted contrast generally attenuates toward 1. The coverage column asks whether the nominal interval covers the **latent** log ratio; poor coverage demonstrates estimand/selection bias, not Monte Carlo error. Do not interpret an apparently precise successful-time ratio as the all-assigned-trial benefit.

The fitted-ratio column is exp(mean estimated log coefficient). Bias and coverage summarize successful model fits; unresolved failures have no estimate or interval and remain in the power denominator as non-rejections.

Stress scenarios retain middle settings except: no effective deadline; within-block AR(1)=.3; incomplete-trial log odds increase by 2×(latent log time−log 12); or intercept–slope correlation=.5. All run at N=30 and 60. They are failure-mode checks, not fitted corrections:

| Stress case at N=60 | Rejection at conditional βV=0 | Power: 10% faster | Latent coverage at ratio .90 |
| --- | --- | --- | --- |
| correlated_effects | 8.8% | 95.9% | 92.2% |
| informative_trial_loss | 5.0% | 85.9% | 79.4% |
| no_deadline | 5.9% | 92.0% | 94.7% |
| serial_rho_0.3 | 4.5% | 80.8% | 82.4% |

Serial dependence or informative missingness can change rejection/error rates. The correlated-effects case also makes the marginal arithmetic ratio differ from exp(βV); neither marginal equivalence nor a robust all-trial interpretation is claimed there. Its elevated conditional-null rejection is a warning about the combination of censoring and covariance misspecification, not evidence of extra useful power. A current-design pilot must check these assumptions before treating the middle scenario as a recruitment guarantee.

## Null behavior, boundary fits and Monte Carlo precision

N=60 null checks (5,000 datasets per main scenario):

| Assumptions | Null rejection (95% MC interval) | Slope at boundary | Failed fits |
| --- | --- | --- | --- |
| optimistic | 5.70% (5.09–6.38%) | 0.0% | 2 |
| middle | 5.40% (4.81–6.06%) | 0.0% | 4 |
| pessimistic | 5.20% (4.62–5.85%) | 0.1% | 2 |

Total **1,371,000 simulation fit attempts** (including repeated seed-prefix refinements), across 843 cells, including the 64-trial N=20 comparison. There were **515 unresolved failed fits** (counted as non-rejections) and **10,936 optimizer retries**. `results.csv` contains per-cell failure reasons, boundary rates, retained participants, missing/timeout denominators, latent bias, latent coverage, power, MC SE and 95% Wilson intervals. Boundary fits are not silently discarded. Boundary rates are among successful fits; failures have no interpretable variance estimates.

In the main curves, slope-boundary frequency reaches **12.8%** (pessimistic, N=12, ratio 0.97). Small samples therefore often cannot distinguish a small slope variance from zero. The factorial grid additionally includes a true zero-slope case; boundary estimates there are expected and are still reported, not recoded as optimizer failures.

N=20 refinement uses 10,000 replicates/cell for both designs. Main curves and factorial grid use 1,000 replicates/cell: maximum power MC SE 1.58 percentage points, about ±3.1 points for a 95% interval near 50% power. Stress cells use 2,000: maximum SE 1.12 points. N=60 refinement uses 5,000: maximum SE .71 points; around 80% power it is .57 points (about ±1.1 points at 95%). These quantify simulation noise, **not uncertainty in the assumed variances**. Headline detectable reductions interpolate linearly in log ratio between adjacent refinement points; they are approximate, not exact optimization thresholds. Brackets are saved in `detectable-effects.json`. Curves show 95% Monte Carlo intervals and may wiggle slightly; no monotonic smoothing is used. Repeated cells across runs use the same seed prefix and are **not independent replications to pool**.

## NASA-TLX and IMI interest/enjoyment

One score per participant per voice gives **one paired difference per outcome per participant**. Ninety-six trials do not create 96 questionnaire observations. Define dz as the mean paired difference divided by the SD of paired differences (not a between-person standardized effect). Exact two-sided noncentral-t power is integrated over binomial retention, assuming normal pair differences. Assumed pair retention is .90 for both blocks × .95 for a complete pair of scores = .855, or **51.3 expected usable pairs from 60 enrollees**. No empirical score SD is available to translate dz to raw scale points.

**Open multiplicity decision:** propose a two-outcome family containing NASA-TLX total and IMI interest/enjoyment. Holm tests the smaller p at .025 and, if rejected, the other at .05. The .025 power below is a conservative per-outcome guarantee/lower bound for Holm, and unadjusted .05 power an upper bound; actual Holm power depends on both true effects and their dependence. No exact joint Holm power is claimed. A larger family would require recalculation.

| Standardized paired effect dz | Unadjusted power | Conservative Holm power |
| --- | --- | --- |
| 0.2 | 29.0% | 19.9% |
| 0.3 | 55.8% | 44.2% |
| 0.4 | 80.1% | 70.8% |
| 0.5 | 93.9% | 89.3% |
| 0.6 | 98.7% | 97.3% |
| 0.8 | 100.0% | 100.0% |

At N=60, the conservative Holm bound reaches 80% at **dz≈0.442**, and 90% at **dz≈0.506**. Both outcomes have the same standardized-effect curve; these are not claims that their true effects or scale variances are equal. `secondary-power.csv` gives all even N=12–60 and dz=0,.2,.3,.4,.5,.6,.8. These are analytic probabilities with **no Monte Carlo error**. Score eligibility, complete-subscale rules and the secondary family must be fixed before collection; missing scores are not zeros.

## Reproduction and artifacts

Scripts (current v9 defaults; use commit 0889229 to reproduce the archived v8 report): `analysis/current_design_power.py`, `analysis/current_power_report.py`, `analysis/power_development_audit.py`; tests: `analysis/test_current_design_power.py`, `analysis/test_current_power_report.py`, and `analysis/test_power_development_audit.py`. Pinned dependencies are in `analysis/power/requirements-current-design.txt`. Set BLAS thread limits to avoid oversubscription. Fixed master seed **20261031** and SHA-256-derived per-cell seeds are stable across worker counts; each dataset is independently generated within its cell. Processes are CPU workers, not agent delegation.

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
$PY analysis/current_design_power.py --out "$OUT" --kind n20 --repetitions 10000 --workers 6
$PY analysis/current_design_power.py --out "$OUT/baseline64" --kind n20 --block-size 32 --repetitions 10000 --workers 6
$PY analysis/power/current-design-96-20261101/refine-enrollment.py --repo "$PWD" --out "$OUT" --workers 2
# Copy frozen development-evidence.json and development-trials.csv from the 0889229 archive.
$PY analysis/current_power_report.py --out "$OUT"
$PY -m unittest discover -s analysis -p 'test_current*power*.py' -v
$PY -m unittest discover -s analysis -p 'test_power_development_audit.py' -v
```

Raw development source paths are local and read-only; the simulation is independent of them. Without those files, copy the frozen `development-evidence.json` into the new output directory to render the report, clearly retaining its historical provenance. `*-run.json` records versions, seed, wall time and simulation-source hash. `input-provenance.json` records the runtime base commit, current schedule hashes and the inherited 0889229 input provenance. Run wall times (some jobs overlapped): curves 1377.0 s, grid 1449.1 s, stress 246.1 s, precision 337.7 s, n20 1110.4 s, enrollment 137.1 s, n20 497.5 s. No GPU, microphone or headset is needed.

`results.csv` consolidates all trial-level simulations; individual run CSVs preserve their own cells. `secondary-power.csv` contains the paired-score results. `power-curves.png` and `.pdf` are the same white-background figure. `development-evidence.json` and `development-trials.csv` keep the observed inputs distinct from synthetic outputs. Validation receipts and the local commit are recorded in the coordination DONE post and work log. Nothing was pushed or published to Overleaf.
