# Trial demo/export side-chat handoff

## Implemented locally

The user authorized finishing sample sheets matching the meeting columns in a side
chat and reporting to the main chat. No Unity, device, voice, scene, git branch,
commit or push operations were performed here.

- `analysis/trial_csv.py`: explicit session/data-purpose/exclusion labels, optional
  `--motion`, source fingerprints before parsing and a final consistency check.
- `analysis/motion_csv.py`: streamed combined and per-trial pose CSVs, matched by
  exact accepted replay identities. Preserve invalid/paused/selection records,
  source sequence/line, nested telemetry and original JSON. Manifest links files
  and row counts; missing replay is explicit.
- `analysis/demo_trial_sheets.py`, `analysis/trial_column_dictionary.py`: reproducible
  synthetic clean, wrong-choice and paused/incomplete cases, block survey examples,
  full trial-column dictionary. No real participant measurements.
- Tests in `analysis/test_trial_csv.py`, `analysis/test_demo_trial_sheets.py`; usage
  documented in `analysis/README.md` and `docs/trial-performance-csv.md`.

## Verification

Initial verification: 162 tests passed. Follow-up verification: 178 tests passed (see below). Focused CSV suite: 46 passed; demo regression
also verifies contact timing, motion totals, missing values, survey response identity
and dictionary coverage. Two independent subagents reviewed source and output.
Resolved initial-read source-change detection and symlinked native-survey issues;
corrected two dictionary definitions. Reviewers independently reconciled all three
summaries and 156 motion records with raw sources. Workbook values were inspected,
all sheets rendered and checked, and XLSX export succeeded.

## Delivered artifact directory

`/Users/pedro.poveda/.codex/visualizations/2026/10/09/01a11e0e-0efa-77c2-9214-3a4ff9c75e91/outputs/trial-sheets/`

- `synthetic-trial-sheets.xlsx`: readable comparison, selected motion fields, full
  trial-column dictionary.
- `synthetic-trial-csv-package-v2.zip`: current one performance/subjective CSV per trial,
  combined trials, per-trial/combined motion, source fixtures, manifest and dictionary.

## Open decisions and next validation

This is an offline export against copied/stopped recordings. It does not prove
headset sampling frequency, fixation validity or 40+40 study behavior. Run a fresh
device session with clean/wrong/pause cases, pull the matching GazeData and replay
packages, and export with `--motion`; compare against observed actions/video.

The follow-up below supersedes the initial open measurement definitions. Scheduled
height balancing quotas remain a separate runtime task. Incomplete-trial pause totals
remain blank; the user removed that proposed calculation. Raw pause events survive in JSONL.
Raw JSONL/object snapshots/events/audio/scene/video must be retained for reconstruction.
Exclusion labels do not automatically filter legacy analysis loaders; those require
a separate change before using development/demo-containing folders for study analysis.

Changes are uncommitted in `codex/recovered-study-20261007`; all pre-existing Unity
and main-chat changes were preserved. Build/Run was not invoked because these are
offline exporter changes and the side chat must not disrupt main-chat device work.


## Follow-up: clarified measurement definitions (2026-10-08)

**Implemented locally**, export schema `trial-performance-subjective-v2`:

- Removed the undefined final-zone-arrival placeholder. Use final correct selection
  timing; explicitly distinguish documented controller completion from legacy dwell
  or undocumented completion methods.
- Export unique zones, stable wall transitions, revisits and observation gaps.
  Horizontal ray intersections use the recorded octagonal layout, valid tracked gaze
  and distinct observations; pilot debounce is 150 ms, maximum adjacent gap 100 ms.
  Do not infer transitions across invalid/tracking/source gaps. These are observed
  wall visits, not validated fixations; counts are lower bounds when observations gap.
- Scheduled height delta is initial target Y minus recorded readiness-cross Y
  (the plane midpoint). Missing/unsupported geometry remains explicitly unmeasured.
  This does not implement balanced above/below scheduling quotas.
- Raw NASA-TLX total is the unweighted six-dimension mean, 0–100. Require all six
  finite in-range responses plus explicit `scale_min=0`, `scale_max=100` and
  `performance_direction=high_is_good|high_is_poor`. Reverse high-is-good performance
  for scoring/plots only; preserve source responses. Missing metadata stays unscored.
  Fixed the legacy loader's 0–600 sum and aligned subscale plots before averaging.
- No new incomplete-trial pause calculation, per user decision.

New implementation: `analysis/trial_zones.py`, `analysis/tlx_scoring.py`; updates to
exporter, loader, plots, synthetic fixture, dictionary and measurement documentation.
Full analysis suite: **178 tests passed**. Independent reviewers verified source
identity gaps, legacy completion labeling, partial TLX imports, mixed performance
anchors, demo provenance and all 156 motion records. Workbook rendered and exported.

Current artifacts are `synthetic-demo-v2/`, `synthetic-trial-csv-package-v2.zip` and
`synthetic-trial-sheets.xlsx` under the directory above. Synthetic values only:
unique zones 2 each; transitions 1/1/0; signed heights +0.4/-0.4/+0.2 m; final
selection times 2/3/blank seconds; TLX means 29.16666667 with explicit demo metadata.

**Remaining runtime collection gap:** the inspected `FindObjectUI` Performance
slider lacks endpoint anchors, and `TrialDataLogger` does not write scale bounds
or performance direction. Main chat should label the actual questionnaire endpoints
and persist versioned scale metadata before future headset responses can be scored
automatically. Do not infer this metadata for historical responses. No Assets,
Unity/device, build, commit or push actions were performed by this follow-up.

Research examples: Cao et al. (2009), https://doi.org/10.3758/BRM.41.1.113;
NASA/FAA (2013), https://www.faa.gov/sites/faa.gov/files/data_research/research/med_humanfacs/oamtechreports/201317.pdf
(Table 12, PDF page 43, raw TLX and performance direction alignment).
