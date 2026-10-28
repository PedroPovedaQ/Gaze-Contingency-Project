# Vive eye-measurement audit — 2026-10-26

**Implemented / observed:** the latest installed study app was brought to the foreground on the wireless Focus Vision (`192.168.1.208:5555`), using its resolved `UnityPlayerGameActivity`. The local MiniLM v8 helper reported ready. This was a launch, not a new participant session or a physical eye-tracker validation.

**Conclusion:** the software preserves useful raw observations and explicitly restricts scientific claims, but the current eye outcomes are not yet validated research measurements. Our device is **Focus Vision with VIVE OpenXR**, whereas the references below used **Vive Pro Eye with PC-based APIs**. Methodological comparisons are useful; their hardware accuracy, sample rates, or thresholds do not certify ours.

## Primary papers and inspected author code

1. **Schuetz & Fiehler (2022), Eye Tracking in Virtual Reality: Vive Pro Eye Spatial Accuracy, Precision, and Calibration Reliability.** [DOI/publisher](https://doi.org/10.16910/jemr.15.3.3), [full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC10136368/), [code/data](https://github.com/ischtz/jemr2022-vr-eyetracking), [OSF archive](https://doi.org/10.17605/OSF.IO/GAHCP). Relevant for target-based accuracy, precision, repeated calibration, eccentricity and vision correction. Their repository documents engine-sampled 90 Hz observations; it is not evidence that every Pro Eye study used independent 120 Hz acquisition. Inspected commit `0e4eb8ddcc587bb78df10b529430728dc3892af1`, particularly `analysis/vexptoolbox/data.py` and `stats.py`.
2. **Lamb et al. (2022), Eye-Tracking Beyond Peripersonal Space in Virtual Reality: Validation and Best Practices.** [DOI](https://doi.org/10.3389/frvir.2022.864653), [open full text](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2022.864653/full), [author collection code](https://github.com/Pretendprof/EyeTrackingDataCollectionExample), [archived software](https://doi.org/10.5281/zenodo.6368107). Relevant for independent acquisition, head–eye timing, coordinate frames and validation across distances. They collected outside Unity's frame loop and investigated head–eye lag. Do not transfer their fixed lag compensation to Focus Vision. Inspected commit `cccc3498e20e35490d9e159fd98cb0d2598beab7`; `ViveEyeDevice.Worker` polls on a worker and deduplicates vendor `frame_sequence`. `ViveEyeController` provides the associated collection pipeline. BSD-2-Clause repository.
3. **Imaoka, Flury & de Bruin (2020), Assessing Saccadic Eye Movements With Head-Mounted Display Virtual Reality Technology.** [DOI](https://doi.org/10.3389/fpsyt.2020.572938), [open full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC7527608/), [author code](https://github.com/MotorControlLearning/SaccadeVR-mobile). Relevant for instructed saccade protocols and retaining vendor sequence, timestamp, validity and per-eye data. Their paper reports timestamp problems and a task-specific detection procedure; it is not a universal detector for free-head visual search. Inspected `Saccade_measure_rev1.cs` at commit `fd7f29c609cf9a851f6175e237941ac4ca6a1a08`. Its callback/clock handling is a comparison, not code to copy blindly. GPL-3.0 repository.

Reference repositories were read as data. No external collection code was installed into the app or executed against the headset. The precision comparison below executes only the inspected standalone statistics functions.

## Implemented: measurement trace and findings

| Measure / layer | Actual implementation | Finding and remaining evidence |
|---|---|---|
| Raw observations | `Assets/SharedGazeStream.cs`, `GazeObservation.cs`: raw versus corrected rays, per-eye directions/origins, pupil validity, openness, head pose and context | Good separation of observed data and runtime correction. Preserve it. No hardware-accuracy conclusion follows from field availability. |
| Target angular error | `SharedGazeStream.cs:243`; `analysis/eye_quality.py:angle`; per-eye diagnostic in `research_measures.py` | Uses the actual 3D angle between gaze direction and eye-to-target direction. This agrees in geometry with the reference accuracy calculation. Guidance's yaw/pitch Euclidean norm is a different quantity. |
| Acquisition timing | `SharedGazeStream.cs:210–214,305`: native queries triggered by new input deliveries, otherwise cached; one query timestamp assigned before gaze, pupil and geometry calls | Distinct software queries do not prove distinct sensor samples. The field named query receipt is set before the native calls. Head pose is read separately; simultaneous eye/head capture is unproven. |
| Vendor time metadata | Installed `ViveEyeTracker.cs:558–636`, `ViveEyeTrackerHelper.cs:194,278` | Array-returning helpers used by our logger discard enclosing native time fields. Gaze is requested for predicted display time. Pupil structure documents a capture-time field. Preserve and verify these independently before choosing a sensor clock. |
| Native timestamp recorder | `NativeGazeRecorder.cs`, `NativeGazeTimestampFeature.cs` | Passive `xrLocateSpace` instrumentation preserves runtime timing evidence, but is not an independent sensor acquisition loop. It cannot establish 120 Hz from query count. |
| Accuracy / calibration | `GazeQuickAlignment.cs`, validation targets; `eye_quality.py`, `research_measures.py` | Code supports checks and retained raw errors. Held-out full-layout validation and drift acceptance are not established in the reviewed report. Alignment targets cannot also be the sole validation set. |
| Precision | `eye_quality.py:spatial_bias,summarize` | Radial sample SD around a segment mean and RMS successive world-ray angles. These are not identical to the inspected author's SD/RMS of absolute target errors. Head motion and geometry remain contributors. |
| Fixation count | `eye_outcomes.py:idt` | Per-eye candidate I-DT, minimum 83 ms, summed angular ranges ≤1.8°. These are pilot thresholds. The report averages eye counts; this is not a fused binocular fixation count. |
| Saccade frequency | `eye_outcomes.py:analyze` | Candidate eye-in-head speed ≥60°/s, duration 20–100 ms, below-threshold boundaries, retained head speed ≤20°/s. Denominator is eligible low-head-speed time, not total trial duration. Head–eye synchronization and detector validation remain open. |
| Last target fixation | `research_measures.py` | Explicitly unavailable: independently detected events are not yet joined to a validated, time-resolved target area of interest (AOI) before selection. |
| Legacy fixation/saccade fields | `TrialDataLogger.cs:410–431` | Based on object-hit transitions and dwell. They are interaction proxies, despite legacy names, and must not substitute for physiological events. |
| Pupil / IPA | `eye_outcomes.py:ipa,analyze` | Validity/blink padding, segment length/rate/regularity screening, per-eye wavelet candidates and retained-duration aggregation exist. Physical sample timing, independent numerical benchmark, viewing-angle/luminance sensitivity and task validity remain unestablished. Do not call IPA a validated workload score. |
| Blinks | Openness and validity masks in raw stream / candidate preprocessing | Closed-eye samples can screen artifacts; tracking loss alone does not establish a blink or validated blink rate. |
| RAW TLX | Separate questionnaire scoring, `tlx_scoring.py` | Not an eye-tracker measure. The reviewed graph report contains zero scored blocks; missing values stay missing. |

HTC's [pupil-structure documentation](https://hub.vive.com/apidoc/api/VIVE.OpenXR.EyeTracker.XrEyePupilDataHTC.html) explicitly describes its time field as capture time. **Open decision:** inspect actual device values, duplicates/regressions and cross-clock mapping before trusting this field for analysis. Do not subtract `XrTime` from Unity seconds or assume gaze/pupil timestamps mean the same thing.

For IPA, the existing implementation declares departures from the printed listing (nearest-neighbour endpoint handling and a numerical floor). Its method reference is **Duchowski et al. (2018)**, [DOI](https://doi.org/10.1145/3173574.3173856), [full text](https://par.nsf.gov/servlets/purl/10096514). That paper's full text could not be freshly retrieved in this audit; no new independent IPA equivalence claim is made. See the existing [implementation boundary](eye-measures-validation-20261023.md) and [pupil-readiness audit](pupil-readiness-20261023.md).

## Implemented: executable reference comparison

Saved inputs, pinned author commit, source hashes and results: [comparison JSON](vive-eye-reference-comparison-20261026.json).

Three contiguous synthetic traces were sent through current `eye_quality.summarize` and the inspected author's `mean`, `sd`, `rmsi` functions on the absolute target-error sequence used by their analysis code. This is a definition comparison, not replication of the complete paper or validation on humans.

| Synthetic yaw sequence (degrees) | Reference RMS change in error | Our RMS change in direction | Reference population SD of error | Our radial sample SD |
|---|---:|---:|---:|---:|
| −1, +1, −1, +1 | 0.000° | 2.000° | 0.000° | 1.155° |
| +1, +2, +3, +4 | 1.000° | 1.000° | 1.118° | 1.291° |
| +2, +2, +2, +2 | 0.000° | 0.000° | 0.000° | 0.000° |

Equal target distance can conceal a change in direction. Different SD denominators also matter. Neither matching names nor matching one trace establishes metric equivalence. Keep explicit definitions and, if paper-comparison columns are added, retain both definitions rather than silently replacing existing outputs. Accuracy summaries also differ: our report uses median/p90; the reference has mean and median. These are separate aggregations of the same geometric error.

## Observed saved-data limitations

The checked `docs/reports/eye-measures-20261025-final/measurement-status.json` covers 22 resolved development trials, not an audit of every later recording. It reports:

- `eye_research_ready: false`, zero spatial diagnostic rows and no validation target sets.
- All three input recordings lack established clean completion; two have a recovered truncated final line. This does not prove every retained row is bad, but prevents treating the files as certified complete.
- Independent detector validation, task-wide accuracy/drift acceptance, sensor clock/sampling independence, and prespecified coverage/exclusion are all unestablished.
- Last target fixation unavailable; no scored TLX blocks.

## Protocol proposal: order of validation work

### Focus Vision-specific references added during the audit

- **Chernysh et al. (2026), What Mean Error Misses: Device-Clock Diagnostics for Evaluating Eye Tracking across Three Virtual Reality Headsets.** [Preprint](https://arxiv.org/abs/2610.32915), [full text and supplement](https://arxiv.org/html/2610.32915v1), [code archive linked by authors](https://hdl.handle.net/10877/25343), [data archive](https://hdl.handle.net/10877/25342). Posted October 23; submitted to ETRA 2027, not established as peer-reviewed. Particularly relevant: standalone Focus Vision, VIVE OpenXR 2.5.1, and pupil-timestamp deduplication. The authors explicitly cannot verify that each distinct pupil timestamp represents a new gaze estimate. Their analysis also addresses filtering, duplicate timestamps and missing samples. The archives returned HTTP 403 here; source-code inspection/reproduction remains pending, not completed.
- **Yilmaz et al. (2026), Disentangling Cognitive Load From Visual Reflexes: An Iso-Luminant Framework for Virtual Reality (VR)-Based Pupillometry.** [DOI](https://doi.org/10.7759/cureus.101950), [open full-text PDF](https://assets.cureus.com/uploads/technical_report/pdf/456413/20260121-172186-310w1k.pdf). Published Focus Vision/OpenXR technical report with 22 volunteers. Useful for stable-lighting pupil validation; not fixation/saccade or IPA certification. Programmed RGB was used without absolute photometric calibration, so its illumination control should not be assumed equivalent to our passthrough scene. No public implementation link was found in the inspected PDF; data are offered on request. Different filtering paths also limit interpreting its response-speed comparison as an independent sensor validation.
- **Xue et al. (2026), Application of Deep Multi-Scale Representation Learning Based on Eye-Tracking and Facial Expression Data in Cognitive Decline Assessment.** [DOI and full text](https://doi.org/10.3390/s26092600), [PMC full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC13165876/). An example of Focus Vision use, not an independent hardware-accuracy benchmark. Its published methods describe an empirical openness threshold and interpolation; these should not be copied as universal blink rules. Publisher full-page retrieval was rate-limited, so only accessible indexed primary-source methods were screened and public-code availability remains unverified.

**Additional public code, not established here as a peer-reviewed validation paper:** [Eye-LEAN](https://github.com/kubasuchy/Eye-LEAN) explicitly targets Focus Vision and includes Unity collection/calibration, Python classification and metrics, and replay. Inspected its [velocity classifier at commit 75f19696](https://github.com/kubasuchy/Eye-LEAN/blob/75f19696ee738b6b4ef30b099bfe662309b62361/eyelean_analysis/classification/velocity_classifier.py): the default path derives a single rate from median timestamp differences before velocity computation, and warns/falls back to 120 Hz when that estimate fails. Our candidate saccade path uses actual adjacent time differences and rejects bad intervals. These are material differences on irregular recordings; importing the other classifier would not automatically improve accuracy. No end-to-end equivalence test was run. Treat it as a comparison candidate, not a gold-standard detector; toolkit calibration labels and prescribed tasks are not independent physiological ground truth.

### Meaning of the pupil time field

`XrEyePupilDataHTC.time` is the enclosing pupil result's OpenXR timestamp, documented by HTC as the time of capture. It is separate from the diameter in millimetres, gaze pose time, Unity query time and eventual log-write time. Our array convenience getter returns pupil values but drops that enclosing timestamp. Two later application reads can return the same measurement: retaining the native timestamp allows us to test for that, subject to device behavior. Conversely, a new pupil time does not prove independent gaze information. Store the raw integer `XrTime` with its source and units, plus application query start/end; assess clock mapping, duplicates and regressions rather than converting clocks by assumption. This is a proposed instrumentation improvement, not a change deployed by this audit.

### Proposed implementation and validation sequence

1. **Record usable timing evidence first.** Preserve native gaze/pupil/geometry time fields plus query start/end, result codes and base-space identity. Measure actual timestamp semantics/rates on Focus Vision. Determine supported independent acquisition without assuming old SRanipal threading applies to OpenXR. Retain render/input timing separately.
2. **Validate geometry over the actual task.** Run known held-out targets after vendor calibration with correction frozen; cover shelf edges/heights/distances, both eyes, glasses, stationary and moving head. Repeat drift checks after blocks/refits. Set spatial acceptance from object size/separation before scoring held-out data.
3. **Establish events separately.** Record stationary gaze, instructed jumps, blinks, smooth head turns with stable fixation and ordinary search. Target jumps are intended-behavior markers, not physiological onset truth. Independently label reviewed traces, preserve ambiguity and evaluate held-out event precision/recall and timing error. Strong sensor/latency claims need a suitable independent reference.
4. **Finish target-AOI joining.** Preserve target identity/geometry and selection/reveal/pause times in the chosen clock. Distinguish last fixation before selection from fixation ending due to selection; report censoring and uncertainty at object boundaries.
5. **Freeze outcome definitions and exclusions.** Separate binocular/per-eye counts, qualified saccade rates and legacy hover proxies. Set coverage and clean-close rules prospectively. Benchmark IPA on intact/synthetic reference signals and review masking, segment-edge effects, gaze angle and luminance before workload interpretation.

No new detector thresholds or runtime measurement definitions were deployed during this audit. Research readiness must come from these observations and validation results; an LLM reviewer cannot certify it.

## Software verification

Ran the existing Python tests in `/tmp/gaze-eye-measures-20261025/bin/python`: 36 `test_eye*.py` tests and 6 `test_research_measures.py` tests passed. The three reference-comparison fixtures completed and the equal-radius disagreement was asserted. These check code behavior, not headset accuracy. No commit, push or new APK build was required for this documentation/analysis audit.
