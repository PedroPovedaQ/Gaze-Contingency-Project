# Voice-condition panels — recorded development evidence

Implemented descriptive plots: `analysis/thesis_voice_condition_plots.py`.
The four panels adapt the *presentation* of Chiossi et al. to the actual voice factor:
Neutral voice and Self-similar voice. They do not reproduce the reference data.

Frozen scope is development Run 30, the latest local completed summary with the
corresponding audited eye-analysis CSV. All five started searches are neutral;
`voice_order=selfsimilar_then_neutral` is counterbalance configuration, not evidence
of self-similar playback. Scheduled/unstarted objectives do not enter denominators.
Other local summaries are older neutral development runs and are not pooled here.

- First-selection accuracy: four of five completed searches have no incorrect trigger
  selections before completion (80%). One run-level point, not a participant distribution.
- Missed targets: unavailable, because this recording has no explicit 25-second deadline.
  Searches exceeding 25 seconds are not retrospectively relabeled as timeouts.
- Completion time: five recorded active search exposure values for successful searches.
  Seconds, not the comparison paper's brief response-time measure.
- IPA: exploratory candidate rate, mean of available per-eye rates per trial. Four
  trial points from eight eye rows; fifth trial has no eligible pupil segment and
  stays missing. This mean is a descriptive display choice, not a validated workload
  score or a confirmatory analysis. See eye-outcomes-run30-audit for screening,
  raw-source hash, and detector limitations.

Both conditions appear on all axes. Self-similar values remain explicitly absent;
no synthetic values, zero imputation, significance stars, or treatment effect claims.
Boxes appear only for at least three trials (quartiles, median, 1.5-IQR whiskers).
All observations are visible. Trial points are repeated development observations,
not independent participants. Future study comparisons require participant-level
paired summaries with the same measure definitions and eligibility rules.

`sources.json` locks the summary and derived eye CSV hashes. `plotted-trials.csv`
contains every plotted value and missingness. `summary.json` contains denominators.
Regenerate with `/Users/pedro.poveda/.cache/thesis-development-plots-env/bin/python analysis/thesis_voice_condition_plots.py`.
Tests use isolated fixtures solely to exercise conditions, denominators, missing
values and exact eye/trial joins. No fixture enters the figure.
