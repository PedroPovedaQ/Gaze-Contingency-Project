# Voice-study sensitivity — planning proposal

**October 1, 2026. Implemented analysis tooling; protocol proposal, not a final sample-size justification.** The selected runtime design remains 40 trials per voice, 80 measured trials per participant, with separate practice. This document advances [task #16](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/16) without choosing the primary outcome, effect of interest, enrollment target, alpha or exclusion policy for the researcher. It uses no participant data and does not fit P020 or import its earlier named-target variance.

## What this can answer

The question is how sensitivity changes when the number of independent participants, repetitions, participant-specific voice effects, serial correlation and missing data change. Forty trials improve the precision of a participant's performance estimate. They do not create forty independent voice comparisons or remove variation in how different people respond to the voices.

**Candidate estimand:** the mean within-participant difference in log active-search time, self-similar minus participant-preferred voice, adjusted for block order. Exponentiating this contrast gives a ratio of geometric mean search durations. A hypothetical ratio of 0.90 is a 10% reduction on that scale; it is not an effect observed here or the selected smallest meaningful effect. The underlying runtime interval begins at search onset after readiness/announcement and ends at the correct trigger selection; it includes wrong-selection search time but excludes readiness, explicit pauses and post-capture success feedback.

Both conditions use shared instructions and guidance policy; actual delivered guidance can differ with behavior. Voice selection, synthesis quality and the existing clone-only treatment may affect the comparison. If treatment remains condition-specific, the interpretation concerns the implemented voice experience, not isolated voice identity. A significant result would not by itself establish a mechanism involving inner speech.

**If a questionnaire becomes primary:** use one block response per voice/participant. Copying the same NASA-TLX or reliance response into 40 trial rows does not add questionnaire observations. The trial-precision simulation below is not questionnaire power.

## Reproduce the artifacts

Use the repository's shared Python environment with NumPy, SciPy and Matplotlib:

```sh
python3 analysis/voice_sensitivity.py \
  --config analysis/power/voice_sensitivity.json \
  --out /path/to/new-sensitivity-output
python3 -m unittest discover -s analysis -p 'test_voice_sensitivity.py'
```

The script refuses an existing output folder and saves the configuration, script/configuration hashes, dependency versions, analytical paired-test benchmark, variance table, simulation table, recruitment table and figure. Its default seed is 20261001 and it runs 2,000 repetitions per scenario. Each scenario has a stable derived seed, so rearranging the configuration does not change that scenario's results. Monte Carlo intervals quantify simulation uncertainty only; uncertainty about the assumed human behavior is much larger and is not covered by those intervals.

The recruitment table assumes independent participants with the same probability of providing an analyzable complete dataset. It shows both `ceil(target / retention)` (an expected-count allowance) and the smallest enrollment giving at least a 90% binomial probability of reaching the target. Retention values .80, .85 and .90 are hypothetical. The latter is still not a guarantee and cannot cure informative loss. These planning values do not change the historical IRB enrollment ceiling or authorize additional recruitment.

## Candidate model and assumptions

For each participant, two voice blocks are generated on the log-time scale:

`log(time) = baseline + participant intercept + (voice effect + participant voice slope) × voice + (period effect + participant period slope) × period + theta effect + within-block trend + residual`.

Voice is coded −0.5 for preferred and +0.5 for self-similar; period is 0 or 1. Residuals follow a stationary AR(1) process within each block and are independent across blocks. Participant intercept, voice slope and period slope are independent normal draws. Each group of five trials includes one of each absolute-theta category, randomly ordered, matching the current horizontal stratification. The simulator does not reproduce exact object identity, height, selected voice, gaze behavior or adaptive delivered-cue sequences.

For each participant with enough observations, calculate the self-similar minus preferred mean log time. Regress these participant differences on an intercept and order (+1 when self-similar is second, −1 when first). The intercept is tested two-sided with residual degrees of freedom `retained participants − 2`. This removes a common additive period effect under the stated model. It does not identify or remove condition-specific carryover, nonstationary fatigue, informative stopping or arbitrary missingness.

This is a **candidate participant-aggregate analysis**, not an implemented trial-level mixed model, survival model or frozen confirmatory analysis. Under complete balanced observations it has a known Gaussian contrast distribution, which tests compare with the noncentral-t reference. A separate conventional paired-test benchmark has `N − 1` degrees of freedom; it is labeled separately and is not the same order-adjusted test.

For `m` trials and marginal residual SD `sigma`, the variance of an AR(1) block mean is:

`sigma² / m² × [m + 2 × sum((m − k) × rho^k, k = 1 … m − 1)]`.

With complete balanced blocks, the participant contrast variance is `voice-slope variance + period-slope variance + 2 × block-mean residual variance`. This shows why increasing trials cannot remove participant-specific voice or period variability. A common participant intercept cancels in the within-participant difference.

## Hypothetical scenarios

All numbers below are illustrative sensitivity inputs, not pilot estimates or validated thresholds:

- Enrolled N: 15, 20, 30 and 48; repetitions per voice: 10 versus the selected 40. Including 10 is a comparison, not a runtime change.
- Ratios: 1.00 (null calibration), 0.95 and 0.90. Two-sided alpha .05 for simulations; paired benchmarks also show .025 to make the older protocol benchmark inspectable. No multiplicity decision is made here.
- Trial log-SD .6; participant voice-slope SD .2 or .4; within-block AR(1) correlation 0 or .5. A common period effect .1, participant period-slope SD .1 and within-block change −.1 are simulated.
- Fixed theta log-effects `[0, .1, .2, .3, .4]` and baseline 20 seconds are placeholders. The baseline does not determine log-contrast power. Neither these effects nor implied task duration are estimates of headset performance.
- Random-loss scenarios remove 10% of trial records independently and 15% of participants. The candidate model keeps a participant only when at least 80% of trials remain in each block. **This is a simulation assumption, not approval to change the current complete-run analysis or to replace lost trials.**
- An informative-loss stress case increases withdrawal probability for participants whose self-similar response is slower. This can bias the surviving contrast and inflate null rejection. Such a result is a failure of inference, not beneficial power.

The table reports both rejection probability over all generated studies (non-estimable studies count as non-rejections) and conditional on estimable studies. It also reports retained participant counts, bias, and null rejection separately. These are necessary to avoid calling a biased or non-estimable scenario successful.

## Saved synthetic results

The [saved report](../analysis/power/results-20261001/sensitivity.json), [simulation CSV](../analysis/power/results-20261001/simulation.csv) and [figure](../analysis/power/results-20261001/sensitivity.png) contain 120 scenarios × 2,000 simulated studies = 240,000 synthetic studies. All scenario values use the explicitly hypothetical configuration above.

Conventional complete-pair benchmark: smallest absolute standardized participant contrast (`d_z`, difference divided by the SD of participant differences) reaching 80% power under a normal paired t model:

| Complete participants | Two-sided alpha .05 | Two-sided alpha .025 |
|---:|---:|---:|
| 15 | 0.778 | 0.876 |
| 20 | 0.660 | 0.739 |
| 30 | 0.529 | 0.589 |
| 48 | 0.413 | 0.457 |

At the selected 40 trials per voice, hypothetical 10% shorter self-similar geometric mean times yield the following rejection rates. These are assumption-dependent, not evidence of adequate power:

| Scenario | N = 20 enrolled | N = 48 enrolled |
|---|---:|---:|
| independent | 40.6% (38.5–42.8%) | 77.5% (75.6–79.3%) |
| serial | 29.6% (27.7–31.7%) | 59.2% (57.0–61.3%) |
| heterogeneous | 16.4% (14.9–18.1%) | 31.0% (29.0–33.1%) |
| random loss | 22.6% (20.8–24.4%) | 49.8% (47.6–52.0%) |

Parentheses show 95% Monte Carlo intervals, not uncertainty about real participants. The informative-loss stress case is deliberately omitted from this power comparison: at a true ratio of 1.00, its null rejection rates reach 11.4% (N=20) and 23.3% (N=48). Selective loss makes apparently strong evidence misleading.

Recruitment example only: if 20 analyzable participants were chosen and each independently had an 85% completion probability, 24 enrollments give an expected-count allowance; 26 are needed for at least a 90% chance of reaching 20 completions. Neither 20 nor 26 is selected or authorized by this calculation. See [recruitment scenarios](../analysis/power/results-20261001/recruitment.csv).

Validation: all 123 Python tests pass, including eight sensitivity tests covering analytical inversion, covariance-sum variance, simulated versus analytical order-adjusted power, null calibration, period/intercept cancellation, informative missingness, stable seeds, binomial minimality and CLI artifacts/overwrite refusal. The saved figure was visually inspected; configuration/script hashes match the executed sources.

## Evidence basis and limits

[Lakens & Caldwell (2021), *Simulation-Based Power Analysis for Factorial Analysis of Variance Designs*](https://doi.org/10.1177/2515245920951503), [open full text](https://journals.sagepub.com/doi/10.1177/2515245920951503), explains simulation using explicit means, variances and correlations. It is methodological support for exposing assumptions; its ANOVA tool is not evidence that our proposed model or numerical settings are correct.

[Brysbaert & Stevens (2018), *Power Analysis and Effect Size in Mixed Effects Models: A Tutorial*](https://doi.org/10.5334/joc.10), [open PDF](https://journalofcognition.org/articles/10/files/submission/proof/10-1-84-1-10-20180112.pdf), examines participant and stimulus variation in repeated-measures reaction-time experiments. Its lexical-task sample recommendations are not a universal rule for this XR study. Here, generalization beyond the fixed object/voice catalog and any item-level random effects require a separate modeling decision. The benchmark uses [SciPy's noncentral-t distribution](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.nct.html).

## Researcher decisions and pilot evidence still needed

1. Select the primary outcome/estimand and meaningful effect in interpretable units; decide whether workload or performance is primary and how other tests enter multiplicity control.
2. Pilot the actual hidden-target/right-controller task to estimate completion, log-time noise, within-block serial dependence, person-specific voice differences, learning/fatigue, listening quality and total burden. Retain broad ranges; do not power solely from a small pilot's observed treatment effect.
3. Choose treatment of timeout, failed/withdrawn trials, zero/invalid timing, missing blocks and informative withdrawal. If censoring is present, implement and simulate the chosen censored/time-to-event analysis; the current tool does not do that.
4. Decide whether the aggregate contrast is adequate or a trial-level mixed model is required for geometry, repeated identities and voice-catalog generalization. Simulate the same model that will be used for confirmatory inference.
5. Select analyzable N and a separate recruitment allowance under justified retention assumptions; freeze the plan before condition-effect analysis and confirmatory enrollment. Any institutional review remains separate.

The existing 48-participant/four-cell protocol and its alpha .025 benchmark are historical. Reproducing that calculation does not transfer its justification to this two-voice task. No final power alignment, sample-size adequacy or equivalence claim is established by these artifacts.
