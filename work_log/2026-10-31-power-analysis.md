# Current-design simulation-based power analysis — October 31, 2026

**Protocol proposal:** recommend 60 enrolled participants, 30 per voice order.
Under middle synthetic assumptions, the successful-time mixed-model test has
80.8% power for a 10% latent time reduction (95% Monte Carlo interval 79.7–81.8%).
Approximate reductions detectable at 80% / 90% power: middle 9.9% / 11.7%,
optimistic 4.9% / 6.1%, pessimistic 19.3% / 22.6%. These are conditional planning
results, not observed voice effects or a guaranteed all-trial estimand.

**Implemented analysis:** `analysis/current_design_power.py` fits a real
trial-level REML LMM with voice, period, order, quadratic absolute starting
angle, linear within-block position, independent participant intercept and
centered voice slope. The two-sided t contrast uses paired-participant count
minus two df, explicitly an approximation. The current v8 shuffle distribution
and 32+32 allocation are checked against a production C# schedule fixture.
Timeouts have no successful completion time. Incomplete trials and participant
dropout are simulated; first-block data from dropouts remain eligible.

966,000 datasets across 798 cells cover all even N=12–60, ratios .97/.95/.93/.90/
.85 plus nulls, three main scenarios, a full factorial variance grid at N=60,
additional effect thresholds, and no-deadline, serial-correlation,
informative-missingness and correlated-random-effects stress cases. Per-cell
fit failures, boundary estimates, bias, latent coverage, denominators and Monte
Carlo uncertainty remain visible. Unresolved fits count as non-rejections.

**Observed development evidence:** ten explicitly identified read-only inputs,
nine DEV_NEUTRAL replays and run-30 summary, hash/size/mtime verified unchanged.
26 completed searches; raw log SD .683 and within-run-centered log SD .496.
No paired self-similar observations or independent participant variance estimate.
All current-design generator SDs, reference times and losses remain synthetic
assumptions. Historical reports and raw recordings were not overwritten.

**Secondary planning:** exact paired-t probabilities over binomial retention
for NASA-TLX and IMI interest/enjoyment; 51.3 expected score pairs from 60
enrollees. The .025 power is a conservative Holm bound for a proposed two-score
family, not an invented exact joint Holm power or a raw-scale effect estimate.

**Validation:** 370 analysis tests passed, with one optional statsmodels test
skipped in the managed repository environment. All 15 new targeted tests passed
in the pinned Python 3.12 environment, including three statsmodels comparisons,
dense likelihood/gradient checks, exact schedule quotas, dropout/censoring
denominators, failure handling and paired-score calculations. Source review was
performed sequentially in the main thread; no independent peer-review claim.
Test receipts are in `work_log/receipts/2026-10-31-power-analysis/`.

**Artifacts:** `analysis/power/current-design-20261031/REPORT.md`, `results.csv`,
`secondary-power.csv`, white-background PNG/PDF power curves, run metadata,
input provenance and development evidence. Verified copies are delivered to
the thesis coordination `power-analysis/` directory. The figure was opened and
visually checked; report values were cross-checked against the saved CSVs.

**Open decisions / limitations:** the smallest meaningful effect; whether the
successful-time contrast or a censored all-trial model is primary; actual-task
variance, dropout and missingness; covariance/serial dependence; score family
membership and complete-subscale rules. Deadline selection attenuates effects
and harms latent-ratio coverage. Correlated effects yield about 9% rejection at
a zero conditional voice coefficient in the stress case. More N cannot fix
that estimand problem. Three-percent effects, and five-percent effects under
middle/pessimistic assumptions, are beyond the requested feasible sample.

Local commit only on `experiment/local-jev-20261025`. No push, Overleaf,
`docs/thesis` edits, microphone, ElevenLabs, cloning, headset commands or source
participant-data changes. The user's explicit no-headset boundary supersedes
the usual Unity Build And Run step. Existing `progress.md` edits were preserved.
Coordination CLAIM posted before work; DONE includes the local commit and
recommendation. Runtime base and thesis-plan hashes are in input provenance.
