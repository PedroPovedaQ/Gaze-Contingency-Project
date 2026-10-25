# Actual recorded development results — October 23, 2026

**Implemented analysis, not confirmatory participant results. No synthetic observations.**

Scope: every locally mirrored `DEV_NEUTRAL` headset replay dated October 19–20 (Runs 20–28). All nine source headers identify `live_headset`. This bounded date window supplies recent development examples already discussed in the thesis; it is not all historical data, a random sample, or a controlled pilot. All nine builds differ. Eight companion summaries identify neutral Eric and 168 objects; Run 21 has no companion summary. No inferred participant count or self-similar voice contrast is reported.

- `source-manifest.json`: exact paths, SHA-256 hashes, identifiers, builds, dates, terminal record and parsing coverage. Run 21 has one truncated final record; the intact prefix is retained. Raw files remain unchanged outside the repository.
- `trials.csv`: search-start to first correct selection, using the saved active-search counter; six unfinished searches retain a last observed counter, never a fabricated completion time. Only started searches enter the denominator; readiness-only attempts do not.
- `speech-delivery.csv`: 339 delivered events joined by recording/trial/decision ID with exactly one prior requested decision, audio request and readiness event, in chronological order. 11 of 350 search playback starts lack an unambiguous full join. Undelivered requests are outside this distribution. No text-based or nearest-time matching.
- `run-quality.csv`: rendered-frame active-search row counts classified using `gazeAvailable` and `gazeTracked`. These flags encode device availability and application usability (see `Assets/Replay/TrialReplayGaze.cs`). Counts include repeated source observations; they are not native samples, time-weighted coverage, accuracy, or fixations. Missing recording intervals are not silently treated as usable.
- `confirmation-example.csv`: a previously investigated Run 20 / Trial 4 sequence with two waits before gaze confirmation. The three snapshots report qualified gaze and unqualified controller hold. Only recorded event times are drawn; intervening continuous gaze is not inferred.
- `summary.json`: descriptive quantiles (linear interpolation), denominators, missingness and limits.
- `summary-crosscheck.json`: hashes of separately saved trial summaries and agreement of 20 completed durations within export rounding. Run 21's completed duration is replay-only. Both exports originate in the same app, so agreement does not establish independent timing validity.

Regenerate from the frozen source manifest:

```sh
python3 analysis/thesis_development_plots.py
```

Requires matplotlib (generated with 3.11.2). This machine uses `/Users/pedro.poveda/.cache/thesis-development-plots-env/bin/python`. A changed source hash aborts regeneration. The first invocation used `--data-root '/Users/pedro.poveda/Documents/Trial Review Live/data/GazeReplays'`.

The script validates unique recordings and decision joins, chronological records/stages, source type and development label, search boundaries, and sample denominators. It writes four PDF/PNG figure pairs in `../figures/`. Figures are generated from observed data only. No p-values, treatment effect, simulated curve, model-rated perception, acoustic latency, or claim that two-second delays have been eliminated is included.

Changes are local. Overleaf's one-way pull remains conflict-protected; no upload or overwrite of concurrent browser edits was performed.

## Distribution-figure presentation

The search and speech figures now use lettered panels, quartile boxes and individual observed points, informed by the supplied Shamblin thesis and Chiossi et al. results figures. Boxes are drawn only with at least three observations; a run with one completed search shows only that point, and zero-completion runs remain labeled `n=0`. Whiskers extend to the most extreme values within 1.5 IQR. All observed values are plotted, including beyond-whisker values. Fixed horizontal offsets improve point separation without changing measurements. Colors distinguish adjacent runs for readability, not voice conditions. Search duration and pooled stage-duration panels explicitly label logarithmic axes. Unfinished searches appear as counts rather than as fabricated durations. No violin density, confidence band, significance star or inferential comparison is introduced.
