# Vive Focus Vision: gaze accuracy and measurement plan

Date: 2026-09-28. **Protocol proposal / implementation plan**, grounded in the current checkout and primary sources. No new gaze runtime implementation or headset validation was performed. Existing uncommitted voice/guidance work is preserved.

## Recommendation

Start with a device capability/coordinate diagnostic and task-specific validation. Improve agent gaze decisions using a shared, validity-aware sample stream; validate physiological measures separately. Higher sampling frequency does not correct spatial bias, and smoother interaction does not establish scientific accuracy.

## Reading list and method transfer

These are a targeted set of methods papers and the relevant earlier study references, not an exhaustive eye-tracking bibliography. Full-text links are included where available. Parameters from other devices are not validated thresholds for Focus Vision.

| Reference | Evidence and use here | Transfer limit |
|---|---|---|
| Niehorster, Nyström, Hessels, Benjamins, Andersson & Hooge (2026), **The fundamentals of eye tracking, Part 7: Determining data quality**. [DOI](https://doi.org/10.3758/s13428-026-03039-4), [open full text](https://link.springer.com/article/10.3758/s13428-026-03039-4) | Operational definitions of accuracy, precision and data loss; distinguish RMS sample-to-sample fluctuation from spatial spread. Use for our validation report. | Quality must be evaluated at known targets, not inferred from arbitrary exploratory gaze dispersion. |
| Schuetz & Fiehler (2022), **Eye Tracking in Virtual Reality: Vive Pro Eye Spatial Accuracy, Precision, and Calibration Reliability**. [DOI](https://doi.org/10.16910/jemr.15.3.3), [full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC10136368/) | Systematic spatial validation across eccentricities and depths; practical HMD calibration reliability. Use as the closest HTC validation template. | Vive Pro Eye with Vizard, not standalone Focus Vision/OpenXR; do not promise its measured accuracy. |
| Lamb, Brundin, Perez Luque & Billing (2022), **Eye-Tracking Beyond Peripersonal Space in Virtual Reality: Validation and Best Practices**. [DOI](https://doi.org/10.3389/frvir.2022.864653), [open full text](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2022.864653/full) | Shows why validation should cover the actual stimulus-distance range. Relevant to tall walls and targets at different eye-to-object distances. | Gaze-ray intersection with a known wall/object does not establish accurate physiological vergence depth. |
| Dunn et al., **Minimal reporting guideline for research involving eye tracking (2023 edition)**, online 2023, journal volume 2024. [DOI](https://doi.org/10.3758/s13428-023-02187-1), [open full text](https://link.springer.com/article/10.3758/s13428-023-02187-1) | Reporting checklist for apparatus, calibration, acquisition, preprocessing, data quality and exclusions. Use for the session manifest and thesis methods. | A reporting checklist does not validate a device or algorithm. |
| Andersson, Larsson, Holmqvist, Stridh & Nyström (2017), **One algorithm to rule them all? An evaluation and discussion of ten eye movement event-detection algorithms**. [DOI](https://doi.org/10.3758/s13428-016-0738-9), [publisher text](https://link.springer.com/article/10.3758/s13428-016-0738-9) | Comparison against human ratings motivates explicit detector selection and validation. Use when replacing hover-derived “fixations” and “saccades.” | Different apparatus/tasks; no universal velocity or duration threshold should be copied. |
| Agtzidis, Startsev & Dorr (2019), **360-degree Video Gaze Behaviour: A Ground-Truth Data Set and a Classification Algorithm for Eye Movements**. [DOI](https://doi.org/10.1145/3343031.3350947), [author preprint, earlier title](https://arxiv.org/abs/1903.06474), [full text](https://arxiv.org/html/1903.06474) | Head-free annotation and classification account for eye/head coordination, pursuit and vestibulo-ocular reflex. Useful for our rotating-chair analysis and timeline annotations. | Watching 360-degree video differs from active object search. Pilot classifiers on our recordings. |
| Drewes, Feder & Einhäuser (2021), **Gaze During Locomotion in Virtual Reality and the Real World**. [DOI](https://doi.org/10.3389/fnins.2021.656913), [open full text](https://www.frontiersin.org/journals/neuroscience/articles/10.3389/fnins.2021.656913/full) | Context for gaze behavior during real versus virtual locomotion and why movement context matters. | Not a calibration recipe or workload validator; our seated turning differs from walking. |
| Zagermann, Pfeil & Reiterer (2016), **Measuring Cognitive Load using Eye Tracking Technology in Visual Computing**. [DOI](https://doi.org/10.1145/2993901.2993908), [full PDF](https://d-nb.info/1128594803/34) | Position paper organizing fixation, saccade, blink and pupil measures and interaction confounds. Use to motivate exploratory workload outcomes. | It does not supply a validated workload score or prove that any one gaze metric measures workload in our task. |
| Mathôt & Vilotijević (2023; online 2022), **Methods in cognitive pupillometry: Design, preprocessing, and statistical analysis**. [DOI](https://doi.org/10.3758/s13428-022-01957-7), [open full text](https://link.springer.com/article/10.3758/s13428-022-01957-7) | Practical pupil baselines, artifact handling, experimental control and analysis. Use only after valid diameter acquisition is demonstrated. | Passthrough brightness, gaze angle, colors, viewing distance, voice/audio and movement can affect pupil response. |
| Chiossi et al. (2024), **Searching Across Realities: Investigating ERPs and Eye-Tracking Correlates of Visual Search in Mixed Reality**. [DOI](https://doi.org/10.1109/TVCG.2024.3456172), [author PDF](https://www.mmi.informatik.uni-muenchen.de/pubdb/publications/pub/chiossi2024searchingacross/chiossi2024searchingacross.pdf) | Closely related search paradigm; documents 120 Hz Vive Pro Eye acquisition using SRanipal/LSL and luminance checks. | PC acquisition and its preprocessing rules do not transfer automatically to standalone Android. Do not label all missing samples as blinks. |

The supplied PeriSpike screenshot remains contextual evidence only: its Quest Pro 90 Hz and normalized-viewport settings are not Focus Vision settings. A complete identifiable paper is needed before adopting additional methods from that excerpt.

## Implemented baseline and concrete gaps

Audit paths are relative to this repository.

| Area | Current evidence | Missing mechanism |
|---|---|---|
| Device configuration | `Assets/XR/Settings/OpenXRPackageSettings.asset`: Android `XR_EXT_eye_gaze_interaction` and `XR_HTC_eye_tracker` enabled. | Enabled settings are not proof that fields work on the connected firmware. |
| Acquisition | `Assets/GazeDataLogger.cs`: `Update`, `Time.time`, interactor pose; transition periods skipped. Optional Unity eyes/openness and facial-tracking data. | Native sample identity/time, freshness and per-field validity; continuous phase-tagged raw stream; pupil diameter. |
| Coordinates | `Assets/EyeGazeRayVisual.cs` flips local position X in `LateUpdate`; `Assets/Scenes/GazeContingencyStudyScene.unity` explicitly sets `m_FlipX: 1`. CSV reads in `Update`, replay in `LateUpdate`. | Known-target evidence for the flip and a single immutable coordinate-conversion result. Potential consumer inconsistency, not a demonstrated cause of inaccuracy. |
| Replay | `Assets/Replay/TrialReplayRecorder.cs`: head/gaze/controller context and device `isTracked`, once per rendered frame. Unknown tracking can still accompany an interaction proxy. | Timestamp-matched native eye/head observations; consistent treatment of unknown validity. |
| Guidance | `Assets/HintGenerator.cs` checks device tracking; `FindObjectGameManager.IsGazeInSearchSector` deliberately ignores vertical scanning. Entry/return gates already debounce and preserve state through tracking loss. | Per-sample freshness and shared validity; tested boundary uncertainty and vertical-localization stage. Preserve working horizontal-sector behavior. |
| Eye events | `Assets/TrialDataLogger.cs` and `Assets/GazeCoverageTracker.cs` derive “fixation” episodes from hovered objects; logger counts transitions as “saccades.” | Physiological event detection and human-reviewed validation. Rename derived proxies without rewriting historical data. |
| Calibration | `docs/experiment-procedure.md` proposes vendor calibration, nine-direction validation and provisional numeric gates. | Executable validation, saved accuracy/precision maps, drift checks and object-size-aware acceptance criteria. |

## What can run at 120 Hz?

**Reported specification:** HTC lists binocular gaze output at 120 Hz (~8.33 ms per observation). This is separate from display refresh. [Official Focus Vision specifications](https://www.vive.com/sea/product/vive-focus-vision/specs/).

**Reported API capability:** HTC documents standalone Focus Vision support from VIVE OpenXR 2.5.0, with per-eye gaze validity, pupil diameter validity and eye geometry. Our installed package is 2.5.1. [Official eye-tracker API tutorial](https://developer.vive.com/resources/openxr/unity/tutorials/face-data/getting-the-data-of-eye-tracker/).

**Source-audited limitation:** installed `ViveEyeTracker.cs` lines 558–591 requests `predictedDisplayTime` and initializes the output time to that value. Do not treat that field as verified capture time. Neither the tutorial's `Update()` polling nor this wrapper demonstrates a native sample queue. Do not patch `Library/PackageCache` as the permanent solution.

**Open decision:** genuine 120 Hz capture depends on a supported way to retrieve distinct source observations and establish timestamp semantics. A faster timer, 120 FPS, repeated rows, interpolation, or changing predicted gaze values cannot prove it. If the current API only exposes a frame-delivered pose, record the achieved rate and limitation. Do not switch SDK, streaming mode or device without an explicit design decision.

## Sequenced implementation

### 1. Capability and coordinate diagnostic — P0

Owner: [#14 calibration/quality](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/14), [#35 120 Hz evidence](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/35).

- Record build, headset/firmware/runtime, mode, SDK/extensions, IPD/fit and correction state.
- Probe combined/per-eye gaze, pupils and eye openness independently; record supported/unsupported, validity, reference frames and units.
- Log host monotonic receipt time separately from requested pose time, returned runtime time and documented native capture time/sequence if available. Unknown remains unknown.
- Inspect the X-flip with a controlled left/center/right and up/down target sequence, then repeat with head turns and recentering. Check transform parenting and vendor coordinate conversion. Apply any necessary correction exactly once in the shared adapter, never in a visual component that mutates measurement state.
- Run at normal frame rate and deliberately reduced rendering rate. Seek documented callback/history/polling support and native thread-safety before moving acquisition off the main thread.

Deliverable: capability CSV, clock/frame dictionary, plot of achieved intervals and unique observations when identifiable, and explicit 120 Hz feasible/not-established decision. Never deduplicate by equal coordinates alone.

### 2. Shared raw samples and loss-aware recording — P0

Owners: [#22 raw telemetry](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/22), [#8 data contract](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/8).

Create one acquisition adapter in the root `Assembly-CSharp` area and reuse the existing log/replay writers. Do not create another competing trial logger. Only use a native callback or buffer if step 1 establishes support. Otherwise preserve the truthful application-delivered stream.

Minimum sample fields: participant/session/block/trial IDs, schema/source, source sequence/time when available, receipt/requested-pose times, frame index, per-eye validity, gaze origin/direction, coordinate-frame ID, pupil millimeters and validity when supported, openness/source/validity, calibration ID, phase and tracking-loss reason. Maintain head/controller clocks and explicit synchronization mapping; record transforms/recenters. Unsupported values are null, never invented zeros.

Retain raw samples during preparation, transitions, search and pauses; derive analysis windows from markers. Buffer writes with overflow/drop counters. Join object manifests, controller hits, selection and audio events by IDs/time. Expose a separate filtered live estimate; retain raw values unchanged. Do not silently substitute head direction for unavailable eyes.

Acceptance: invalid/unknown/stale gaze cannot count as valid dwell or issue a fresh guidance transition; gaps remain explicit; no unexplained sample loss under audio, video/replay recording and file flush load. Latency claims distinguish software timing from end-to-end sensor latency, which needs independent reference equipment.

### 3. Calibration, validation and drift — P0

Owner: #14. Adapt sources 1–4 rather than copying their numeric thresholds.

Perform vendor calibration after fit/IPD adjustment. Then use a separate validation sequence covering center and eccentric eye positions, representative low/mid/high targets, actual distances and wall seams. Have participants turn toward world sectors before local eye validation; do not require an impossible gaze angle behind the head. A forward target duplicates one wall-center direction in the current nine-direction wording; label repeats rather than pretending these are nine unique positions.

**Proposed pilot design:** a local 3×3 grid plus selected seam/depth targets, repeated across representative head/chair orientations, followed by a brief center/side/high/low drift check between blocks and after refitting. Freeze dwell/discard windows and target order after the pilot; do not impose a long grid every trial.

For target position p, gaze origin o, measured unit gaze g: expected direction v = normalize(p-o); angular target error = acos(clamp(dot(g,v), -1, 1)) in degrees. Report per-target mean-direction offset and sample-error median/percentiles distinctly. Report within-target precision (angular SD and RMS-S2S), missingness and systematic yaw/pitch bias. Never compute precision across target changes, blinks or gaps.

A single dot can test or estimate a local constant offset, not task-wide accuracy. Fit any optional correction on a calibration set, evaluate it on independent targets/repetitions, retain both raw and corrected rays and correction version, and reject a correction that worsens held-out regions. Do not fit to the hidden target during experimental trials.

**Acceptance:** derive limits from measured angular target size/separation and wall geometry. Existing 3° median/6° p90/70%-valid thresholds are provisional and may be too permissive for small objects. Report usability for wall assignment separately from object fixation. Review/freeze recalibration and exclusion policy before collection rather than silently changing protocol thresholds.

### 4. Reliable horizontal then vertical assistance — P1

Owners: #14 and [#24 guidance](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/24).

Consume the same valid/fresh sample snapshot as measurement code. Stabilize horizontal wall membership with measured uncertainty, hysteresis and the existing entry/return gates. Vertical movement inside the wall must not reannounce area entry. Once wall membership is stable, compare target elevation with gaze elevation in a documented common reference; introduce up/down cues with a central tolerance, cooldown and reversible state transitions. Suspend new directional decisions when gaze is stale/invalid or nearly vertical and azimuth is poorly defined.

Keep controller-ray hit and trigger selection independent. “In correct wall,” “aligned vertically,” “ray intersects target,” and “validated fixation on target” are distinct states. Log the sample/time, decision state, uncertainty/reason, phrase category and audible onset; do not expose the hidden target to the participant.

Acceptance scenarios: wall-edge jitter, looking upward within the wall, vertical-ray ambiguity, overshoot and return, blink, headset shift, loss/recovery and recenter. Measure false cues, missed transitions, repeat cues and response latency against scripted known-target sequences.

### 5. Defensible eye events and trial measures — P1

Owners: [#15 quality reports](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/15), [#20 trial CSV](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/20), #8.

Version names for legacy hover durations/transitions as interaction proxies. Compare simple angular-velocity/dispersion detectors with a head-aware approach on our measured cadence; use sources 5–6 to guide validation, not to select thresholds by authority. Annotate a held-out set with two reviewers where feasible; report event precision/recall, duration/onset disagreement, noise sensitivity and head-motion failure cases. Screen video alone cannot establish the participant's true foveal gaze; known-target validation remains necessary.

Define and export separately:

- First valid target ray intersection: seconds from authoritative search onset; no hit = missing/censored, never zero.
- First validated target fixation: same clock, with detector/version and onset-versus-confirmation rule recorded.
- Target-looking duration: sum accepted intervals, excluding gaps; also retain raw hover-duration proxy.
- First right-controller target contact and final correct trigger selection: separate event times from controller telemetry.
- Quality per trial: valid observation coverage, gap duration, calibration ID, achieved rate, event algorithm version and exclusion reason.
- Blink rate: verified bilateral blink events per minute of usable observation, with tracking outages separate and source-specific openness validity.

Reproduce summaries from raw streams, manifests and events. Validate sample-clock alignment and pause handling against the timeline without fabricating sensor samples at video frame times.

### 6. Workload measures only after measurement validation — P2

Owner: [#16 outcomes/analysis](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/16), #15. Sources 8–10 support motivation and controls, not an automatic workload estimator.

Verify pupil diameter availability and its own sampling/validity. Prespecify pupil baselines and artifact/gap handling; never interpolate a long outage or saccade as real data. Keep scene brightness, passthrough/room conditions, color distribution and display settings controlled or measured. Log head/gaze angle, speech/audio and scene transitions. First analyze gaze/pupil outcomes as exploratory alongside NASA-TLX; block ratings repeated in trial rows remain block-level measurements, not independent trial labels. Avoid claiming workload solely from fixation duration, blink rate or pupil size.

## Delivery order and test gate

First implementation slice: diagnostic overlay + coordinate check + continuous validity-aware raw record + known-target validation report. This can improve reliability and disclose limitations even if full native 120 Hz access is blocked.

Then integrate validated live gaze, followed by horizontal/vertical policy tests and offline event extraction. A small development rehearsal can expose failures; it does not establish population accuracy or statistical power. Test multiple headset fits, vision correction conditions and representative users before freezing protocol limits. Preserve current 40-trial-per-block decision separately; this plan does not change the scheduler or power analysis.

Finish with a reproducible headset dataset and report: spatial error map, precision, missingness, timing/rate histogram, head-motion effects, false-guidance cases, calibration/drift history and overhead. Freeze build/firmware and algorithm versions. This is planning only; no GitHub issue state, IRB submission or runtime behavior was changed by this document.
