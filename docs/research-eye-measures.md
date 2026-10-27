# Eye measures and workload: implementation and validation status

**Implemented, 2026-10-25:** `analysis/research_measures.py` builds a reproducible four-panel development figure and a coverage figure from explicitly selected trial summaries and raw HTC eye recordings. It also exports per-eye metrics, candidate events, per-trial values, scored survey rows when supplied, and `measurement-status.json`. It does **not** certify research-grade physiological measurements.

## Definitions and current evidence

| Measure | Implemented behavior | Remaining evidence |
|---|---|---|
| Last target fixation duration | Unavailable. Historical `fixation_end` durations are retained only as `last_target_visit_s` in the legacy loader. | A validated event detector, explicit target area-of-interest (AOI) geometry and coordinate transforms, synchronized controller-selection boundary, and handling of gaps/edge-censored fixations. A target center alone is not an AOI. |
| Fixation count | Existing raw-eye I-DT candidates in valid, contiguous search segments; count retained separately per eye. Figure uses the mean of both eye counts for each resolved trial. | Independent event validation under task head motion, task-wide spatial validation, sampling evidence and a prespecified coverage/exclusion policy. Counts are over usable intervals, not extrapolated whole-trial counts. |
| Saccade frequency | Existing candidate detector uses head-relative directions and below-threshold bracketing. Rate is candidate count divided by eligible seconds per eye; figure averages the two rates. | Validate the event detector and clock; establish suitability under actual head motion. The current head-speed restriction changes the estimand to restricted intervals, not whole-search frequency. |
| Raw NASA-TLX | Explicit six-dimension unweighted scoring, performance-direction alignment and separately labelled conversion to 0–100. One response per participant/run/block. | Completed responses with documented scale anchors, form version and administration timing. Existing headset quick-test records without scale metadata are unscored; the implemented study uses the external final survey. |

**Implemented safeguards:** missing is not zero; incomplete eye pairs are not pooled; duplicate trial identities and conflicting block responses are rejected. Voice/participant/run/practice identities must agree. Repeated identical block responses collapse to one observation. Different recordings cannot supply the two eyes of one trial. Overlapping recordings for one trial require explicit reconciliation rather than silent double counting.

All eye results from this path remain `eye_research_eligible=false`. There is intentionally no command-line flag that changes this to true. Passing parser checks or setting a JSON field to `validated` does not validate a detector. The legacy loader preserves the old summary counts in `proxy_*` columns and makes physiological result columns unavailable, including for older files without provenance.

## Reproduction

Install the optional dependencies in an isolated Python environment:

```sh
uv venv /tmp/gaze-eye-analysis
uv pip install --python /tmp/gaze-eye-analysis/bin/python -r analysis/requirements-eye-outcomes.txt
/tmp/gaze-eye-analysis/bin/python analysis/research_measures.py \
  --summary /copied/GazeData/P001/run_001/trial_summary.json \
  --eye /copied/EyeMeasurements/recording/gaze_observations.jsonl \
  --survey /copied/surveys/nasa_tlx.csv \
  --out /reports/new-eye-measures
```

Repeat `--summary`, `--eye` and `--survey` for explicitly selected inputs. Omit `--survey` if none exists. Survey columns are `participant_id,run_number,block,voice_condition,response_id,mental,physical,temporal,performance,effort,frustration,scale_min,scale_max,performance_direction`. `block` is zero-based (0/1). Supported scales are 0–10 and 0–100; performance direction is `high_is_good` or `high_is_poor`. Keep instrument wording/form and administration details with the raw response. A rescaled 0–10 adaptation is not claimed equivalent in validation to the original instrument.

Output folders must be new. Sources and existing results are never overwritten. SHA-256 hashes cover inputs, available clean-close receipts and analysis code; dependency versions and detector settings are recorded. Inputs are checked again after analysis. Corrupt middle JSON records fail; an interrupted final record may be omitted with an explicit incomplete warning. `eye_research_ready=false` is expected until validation is established. Exit success means report generation succeeded, not that a study passed scientific validation.

A stored `command` array records the invocation. To reproduce, use the hashed inputs and recorded dependency versions with a **new** output directory. Missing raw recordings cannot be replaced with a nearby run. Analysis does not repair sources or rewrite old thesis figures.

## Recorded October 25 development data

**Implemented report:** [four panels](reports/eye-measures-20261025-final/research-measures.png), [PDF](reports/eye-measures-20261025-final/research-measures.pdf), [coverage](reports/eye-measures-20261025-final/measurement-coverage.png), [machine-readable status](reports/eye-measures-20261025-final/measurement-status.json).

Explicitly selected: DEV_NEUTRAL run 32, DEV_CLONE runs 1 and 2, with matching raw recordings from October 25. These contain 22 resolved searches (completed or timed out), not 22 independent participants or complete paired study sessions. Later copied eye files contained no search trials. The three source recordings lack verified clean closes; two have recovered incomplete final rows. No target-validation rows and no NASA-TLX responses are present in these selected files. The plot therefore leaves last target fixation and workload unavailable.

Neutral trial fixation-eligible coverage ranges approximately 47–97%; self-similar coverage approximately 94–97% (minimum across eyes). Saccade coverage is lower because it also excludes high-head-motion intervals. These unequal windows prevent interpreting the boxplot differences as a voice effect. No significance tests or causal claims are produced.

## Physical validation required before study outcomes

**Protocol proposal:** use the existing [measurement validation procedure](gaze-measurement-validation.md) to collect independent fit and held-out validation sets on the Focus Vision. Add checks across the actual task directions, elevations and distances, including natural head turns, before and after blocks and after fit/recenter changes. Record device/software versions, per-eye error, dispersion, loss and timing. Inspect accuracy relative to actual object angular size and separation; a central quick-alignment result alone cannot approve target-level classification.

**Open decisions:** the study team must set acceptance thresholds, invalid-time coverage limits, gap handling, eye-combination policy, final-fixation censoring rules and participant/trial exclusions before confirmatory analysis. Do not tune these to maximize a voice difference. The existing detector cutoffs are development parameters, not validated thresholds transferred from another paper.

**Protocol proposal:** compare raw-eye candidate events with independently reviewed held-out traces (including head-motion strata) using `analysis/eye_outcomes.py --annotations ... --annotation-windows ...`. Preserve labels, reviewed windows, annotator procedure and precision/recall/timing disagreement. Agreement with labels drawn from the same noisy signal is evidence about annotation agreement, not independent optical ground truth. The native timing audit must establish what timestamps represent; software query cadence alone does not establish 120 Hz sensor capture.

**Protocol proposal:** implement and verify the target-AOI/time join only after the coordinate and spatial validation checks pass. Define last fixation as the last qualifying target fixation before the final controller selection, preserve onset/end bounds, and flag fixations cut by selection, gaps or trial boundaries instead of silently assigning their observed prefix as a full duration. Report trials with no qualifying fixation separately from missing measurements.

**Implemented scoring; protocol decision still required:** workload uses block responses, not copied per-trial rows. Keep retrospective administration distinguishable from immediate post-block administration. The current external final-survey protocol must be described as retrospective. Final voice comparisons must account for participant pairing/order and repeated trials; this development report deliberately does not perform that inference.

## Method sources

The measurement/provenance checklist follows the reporting topics in Dunn et al., *Minimal reporting guideline for research involving eye tracking (2023 edition)*: [DOI / open-access article](https://doi.org/10.3758/s13428-023-02187-1), [full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC11225961/). It calls for reporting sampling, processing and data quality; it does not certify our headset or prescribe task-specific passing thresholds.

NASA documents the six workload dimensions and their anchors in the [NASA-TLX manual](https://humansystems.arc.nasa.gov/groups/tlx/downloads/TLX.pdf). Our raw/unweighted score and any 0–10 adaptation must be identified explicitly, rather than described as the original weighted procedure.
