# Eye measures: implementation and validation boundary (2026-10-23)

## Implemented

`analysis/eye_outcomes.py` processes a **stable copied** `gaze_observations.jsonl`.
It writes per-trial/per-eye `eye_metrics.csv`, individual candidate event intervals,
a hash/version/configuration report, optional three-variant threshold sensitivity,
and four real-data plot panels. It does not change live guidance or infer workload.

```
python3 -m venv /tmp/eye-analysis
/tmp/eye-analysis/bin/pip install -r analysis/requirements-eye-outcomes.txt
/tmp/eye-analysis/bin/python analysis/eye_outcomes.py /path/to/gaze_observations.jsonl --out /path/to/analysis --plot --sensitivity
```

- Query receipts are deduplicated globally. They identify distinct software queries,
  **not necessarily distinct sensor observations**. No 120 Hz claim is made.
- Readiness, pauses, invalid shared gaze, source trial/device changes, backwards
  query time, and gaps over 25 ms break segments. The logger timestamps the enclosing
  observation before its HTC query may complete, so small negative observation-minus-
  query intervals are allowed within the same 25 ms plausibility bound.
- Per-eye direction validity is required. Invalid samples and observed closed eyes
  exclude nearby 200 ms intervals; global tracking loss also pads adjoining segments.
  Missing tracking is not classified as a blink. World-space azimuth is unwrapped.
- Fixation candidates use time-aware I-DT: horizontal plus vertical range <=1.8 deg
  for >=83 ms. These transferred Chiossi parameters are **pilot settings**, not device
  validation. Counts exclude segments shorter than the required hold. Direction
  duration and fixation-eligible duration are both exported.
- Saccade candidates require eye-in-head angular velocity >=60 deg/s, at least two
  intervals, duration 20–100 ms, observed below-threshold bracketing, and head speed
  <=20 deg/s. Frequency divides by retained low-head-speed time, not full trial time.
  These engineering thresholds intentionally exclude much head-turn activity and
  cannot be described as total saccade frequency. Head pose is software-time matched,
  not synchronized to a known eye sensor capture time.
- IPA candidates use each eye separately: finite valid 1–10 mm pupil values,
  segments >=2 s, median query rate >=60 Hz, p95 interval deviation <=50% of median,
  >=124 resampled points, and no interpolation across rejected gaps or invalid samples.
  Within accepted segments, interpolate to their median query interval. Apply sym16
  level-2 periodized wavelet detail /2, modulus maxima, universal hard threshold,
  and count retained extrema per retained second. Aggregate extrema and duration,
  rather than averaging segment rates. Missing IPA stays blank.
- IPA implementation uses the text's nearest-neighbour maxima definition at the
  penultimate sample, rather than the apparent indexing typo in the paper's Listing 2.
  It suppresses numerical residue <=1e-10*max(1, max(abs(pupil))). These differences,
  interpolation, boundary effects from periodization, and all quality thresholds
  require sensitivity review before using IPA as a study outcome.
- Counts and rates are exploratory candidates; no microsaccades, final target
  fixation duration, acoustic timing or cognitive-load estimate is claimed.
- Missing Target: `trial_csv.py` exports timeout=1, successful selection=0,
  technical/incomplete=unknown. `missed_targets.csv` uses nonpractice completed +
  timed-out attempts with a recorded positive deadline as its denominator, grouped
  by run/block/voice/deadline. It retains excluded/unknown counts and development
  labels. Older unconstrained recordings are not retrospectively timed out at 15 s.

## Computational verification completed

Unit tests exercise elapsed-time minimums, azimuth wrap, two known fixation segments,
query deduplication, stale pre-reveal evidence, pauses, trial boundaries, tracking loss,
invalid pupil padding, missing head pose, head-turn suppression, bracketed events,
constant-pupil zero counts, and an independent scalar implementation of wavelet peak
counting. Artificial signals are unit fixtures only; thesis plots use saved headset data.

`python -m unittest discover -s analysis -p 'test_eye*.py'`: 36 tests passed.
`python -m unittest discover -s analysis -p test_trial_csv.py`: 41 tests passed.
This is software verification, **not physiological validation**.

## Protocol proposal: independent validation before outcome use

1. Record sustained fixed-target gaze at low/middle/high heights, then explicit target
   jumps with the head held steady; repeat while deliberately turning the head.
2. Include blinks, headset tracking loss, pauses and target reacquisition. Keep raw
   query records and exact target-event markers. Run a separate pupil session with
   stable lighting, scene brightness and viewing geometry; include controlled task
   difficulty instead of guessing workload from normal search.
3. Have reviewers label fixation/saccade onset/offset from traces with task markers,
   independently of the candidate output. Preserve ambiguous intervals separately.
   Validate timestamps against a sensor-timed reference where feasible; the instructed
   jump itself does not prove the physiological event onset.
4. Supply CSV `recording_id,eye,kind,start,end,trial_id` (query-clock seconds) through
   `--annotations`, accompanied by `--annotation-windows` with columns
   `recording_id,eye,trial_id,start,end`. Each window must have exhaustive labels;
   events outside it or straddling its boundary are unscored, not false positives. The report provides precision/recall and onset error at the stated
   50 ms matching tolerance. Comparisons do not automatically grant validation.
5. Freeze acceptance criteria, calibration-error bounds and thresholds before using
   participant outcomes. Keep validation recordings separate from threshold tuning.
   Assess event precision/recall, event duration error, binocular agreement and
   head-motion sensitivity. Do not accept a detector solely because two algorithms agree.
6. For IPA, establish pupil coverage, regularity, luminance/gaze-angle robustness,
   blink handling and agreement with a separately implemented published workflow.
   Compare with NASA-TLX only after those checks; correlation is not validation of
   workload causation or a self-similar-voice effect.

## Verified methodological sources

- Chiossi et al., *Searching Across Realities* (2024):
  https://doi.org/10.1109/TVCG.2024.3456172 ; full text
  https://www.mmi.ifi.lmu.de/pubdb/publications/pub/chiossi2024searchingacross/chiossi2024searchingacross.pdf
  Supplies the visual-search measurement model and I-DT pilot parameters (83 ms,
  1.8 deg). Uses Vive Pro Eye/SRanipal, not our Focus Vision query interface.
- Salvucci & Goldberg (2000): https://doi.org/10.1145/355017.355028 .
  Basis for dispersion/velocity detector definitions.
- Duchowski et al. (2018): https://doi.org/10.1145/3173574.3173856 ; full text
  https://par.nsf.gov/servlets/purl/10096514 . IPA algorithm, not a validated workload
  calibration for our task; original arithmetic/fixed-view conditions differ.

## Search for the unidentified UCF paper — open identity

The user does not remember its title. Do not assert that one candidate is the intended
paper or that the experiment originally copied an unidentified method.

1. **Pfeil et al. (2018), A Comparison of Eye-Head Coordination Between Virtual and
   Physical Realities.** https://doi.org/10.1145/3225153.3225157 ; UCF full text:
   https://www.eecs.ucf.edu/isuelab/publications/pubs/SAP2018.pdf .
   Supports keeping eye and head motion separate in immersive measurements. This
   verified UCF connection is used on that narrow basis in the methodology.
2. **Wanyea Barbel (2024), NeuroGaze in Virtual Reality: Assessing an EEG and Eye
   Tracking Interface against Traditional Virtual Reality Input Devices.**
   https://stars.library.ucf.edu/etd2023/184/ . Compares selection interfaces with
   completion time, accuracy, NASA-TLX and preference; plausible remembered thesis,
   but not established as our fixation/IPA validation source.
3. **Krueger et al. (2019), Microsaccades Distinguish Looking From Seeing.**
   https://doi.org/10.16910/jemr.12.6.2 ; full text
   https://pmc.ncbi.nlm.nih.gov/articles/PMC7962679/ . Includes UCF researchers and
   students; visual-search/mental-arithmetic study. Its high-speed microsaccade
   methods are not automatically supported by this headset.

The supplied Shamblin movement-imitation thesis and Truong video-question-answering
thesis are presentation references, not eye-event measurement authorities.
