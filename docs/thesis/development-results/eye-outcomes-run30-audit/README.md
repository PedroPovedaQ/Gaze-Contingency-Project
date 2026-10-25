# Pupil retention and eye-metric readiness — October 23, 2026

## Implemented: diagnosis from actual development Run 30

Source SHA-256: `6f1ea9237b082c5ed1451676bff8ad7015e0d8daa1808ab19d68d50defc3d9f9`.
Five searches in neutral-male mode; not a participant-condition comparison.
All 15,543 search rows report valid pupils in both eyes. These include 5,181 cached
query rows; deduplication leaves 10,362 query rows. Among 10,357 positive within-search
query intervals, 86 exceed 25 ms. Median is 9.93 ms, p95 15.47 ms, maximum 697.71 ms.
No rate derived from these receipts establishes sensor acquisition frequency.

For each eye, the first failed check partitions screened pupil segments:

| Result | Segments | Segment duration |
|---|---:|---:|
| Shorter than 2 s | 75 | 68.74 s |
| Irregular query intervals | 8 | 22.82 s |
| Accepted | 6 | 13.96 s |

Resampling retains 13.93 s. These totals are NOT the whole search-time denominator:
intersegment gaps, invalid padding and singleton samples are outside them. Validity
flags alone do not prove accurate pupil measurements or independence of samples.

**Root cause established for retention:** the current continuity and regularity
screens discard much of an otherwise SDK-valid query stream. Whether the uneven
query spacing reflects frame scheduling, input delivery, runtime caching or the
sensor cannot be attributed from receipt timestamps alone. Do not describe the
excluded 91.56 seconds as pupil tracking failure.

## Implemented: more auditable processing

`eye_outcomes.py` now writes `pupil_segments.csv` with all failed checks plus an
ordered first-failure field, `pupil_quality.json`, and a first-failure duration plot.
`--review-traces` writes unlabeled per-trial eye-in-head/pupil traces and blank
annotation/window CSV templates. No detector classifications become ground truth.
Metrics remain exactly equal to the previous Run 30 output. Graph labels now use
Fixation Count, Saccade Frequency, Index of Pupillary Activity and Usable Pupil
Duration, retaining candidate/exploratory qualifications and the restricted rate
denominator in titles/captions.

## Protocol proposal: sensitivity, not a deployed recovery policy

A replay of four configurations produced these retained left-eye totals (right-eye
retention is identical):

| Analysis configuration | Retained pupil duration |
|---|---:|
| Existing 25 ms gap / 50% relative-jitter gate | 13.93 s |
| 25 ms gap / 100% relative-jitter gate | 36.71 s |
| 50 ms gap / 50% relative-jitter gate | 5.06 s |
| 50 ms gap / 100% relative-jitter gate | 103.02 s |

These engineering variants are **not validated** or selected for participant data.
Longer segments can fail regularity even when their fragments pass. Increased
retention alone cannot justify interpolation or a changed IPA outcome. The gap
parameter also participates in receipt plausibility, so these are full analysis
configuration comparisons, not an isolated causal estimate of missing-sample repair.
No raw samples were fabricated or interpolated across explicitly invalid tracking.
The participant/runtime acquisition policy is unchanged.

## Open decision / required validation

1. Record the existing Eye check **Validation** / **Held-out** target sequence after
   headset calibration. Keep the correction fixed; do not fit and evaluate on the
   same points. Include low/middle/high positions, different distances and head turns.
2. Record stationary gaze and instructed target jumps, deliberate blinks and recovery,
   plus a stable-brightness pupil segment. Use recorded target-transition markers.
   Keep ordinary trial data as well; a static exercise cannot establish search coverage.
3. Independently annotate reviewed trace windows (two reviewers where possible),
   preserve ambiguous windows, and evaluate held-out events. Target jumps describe
   intended behavior, not physiological onset ground truth.
4. Before outcome collection, agree on event precision/recall and timing-error
   acceptance limits, spatial-error bounds appropriate to actual target separation,
   and minimum pupil coverage per trial/condition. Set these before judging held-out
   results; no universal validated thresholds are asserted here.
5. Confirm pupil timestamp semantics and benchmark an independent published IPA
   workflow. Compare any bounded gap-repair policy to intact segments with held-out
   masking; report changed IPA, not only recovered duration. Keep luminance and
   viewing geometry controlled and compare against workload questionnaires separately.

Existing Run 30 known-target records are quick-alignment data only. A separate
per-eye HTC diagnostic on those records reports raw median target errors roughly
4.3–8.4 degrees across attempts; target presentation does not establish that the
wearer maintained fixation. These are not held-out accuracy measurements and do not
certify the corrected guidance ray. The legacy spatial report only evaluates EXT
rays, whereas this recording uses HTC binocular fallback; do not call its lack of
eligible EXT samples total eye-tracking failure. Native pose timestamps have repeats,
regressions and unavailable values; sensor capture frequency remains unestablished.
The app was reachable wirelessly and running during this audit; no wearer validation
was conducted by this automated turn.

## Comparison with Searching Across Realities

Reference: https://doi.org/10.1109/TVCG.2024.3456172
Full text: https://www.mmi.ifi.lmu.de/pubdb/publications/pub/chiossi2024searchingacross/chiossi2024searchingacross.pdf

| Paper panel | Our label / status |
|---|---|
| Accuracy | Correct Selection Rate (%) or explicitly defined Selection Accuracy (%); derive from terminal outcomes/errors. Do not silently substitute first-attempt accuracy. |
| Missed Targets | Missed Targets (%); implemented for explicit 15-second deadlines; needs new deadline-bearing recordings. |
| Reaction Time | Active Search Completion Time (s); existing development graph, successful searches only; keep the actual event definition. |
| Index of Pupillary Activity | Index of Pupillary Activity (exploratory); plotted from Run 30, validation pending. |
| Last Fixation Duration | Not yet supported as a validated final target fixation. Join validated events to target AOIs and selection; do not substitute last ray contact. The source's onset-to-final-fixation wording also needs clarification before claiming exact replication. |
| Fixation Count | Fixation Count (candidate); plotted; independent validation pending. |
| Saccade Frequency | Saccade Frequency (restricted); plotted per retained low-head-speed second, unlike their per-trial denominator and microsaccade detector. |
| Perceived Workload | Perceived Workload (Raw NASA-TLX); requires actual completed questionnaires, not inferred pupil scores. |
| ERP waveform and PD amplitudes | Unavailable: require synchronized EEG acquisition, electrodes, preprocessing and event-locked analysis. |

Participant comparison figures use **Neutral Voice** and **Self-Similar Voice** on
X, with participant-level paired summaries where appropriate. A separate angle plot
uses **Nominal Target Angle |θ| (°)** at 0, 45, 90, 135 and 180. Exact measured bearing
remains a separate variable. Do not label our factors AR/AV or Physical/Virtual,
copy significance stars, or invent paired conditions from this neutral-only run.

## Verification

36 eye-analysis tests, 7 pupil-diagnostic tests and 17 native timing tests pass.
Run 30 metric CSV values are exactly unchanged. Android build succeeded in 14.8 s;
wireless package update confirmed at 22:54:28 local time and process running.
This confirms software deployment, not wearer or physiological validation.
