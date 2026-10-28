# Eye measurement readiness — 26 October 2026

**Implemented:** the capture and analysis changes below. **Open decision:** approval of measurement acceptance criteria and study use. A successful build or artificial fixture is not evidence of physiological accuracy. The application is prepared for wearer validation; the eye outcomes are not yet certified study outcomes.

## Capture contract

- Each raw observation retains independent HTC gaze, pupil and geometric query stamps in `htcTiming`. Every stamp includes the OpenXR instance/tracker generation, tracker, session, base space where applicable, requested and returned XrTime, native query bracket and result. Nanoseconds remain signed integers; Python subtracts integers before converting intervals. CSV consumers must import them as text or integers, not floating-point spreadsheet cells.
- Gaze time is a requested/returned **pose-expression time**. Pupil/geometric time is the runtime-reported capture time. Those streams are never interchangeable. The native adapter clears the SDK's prefilled output timestamp before the runtime call, so an unwritten field remains unavailable. It does not manufacture timestamps or increase acquisition cadence.
- The native hook observes the existing HTC calls on their calling thread. A query from another thread cannot be attached to the current observation. Failed queries never reuse a previous timestamp. Epoch/session/tracker changes, backwards timestamps, invalid samples and gaps break continuity.
- A matching head pose is requested from the runtime's identity-offset VIEW space at the gaze query's requested XrTime and base space. The raw native pose/flags and converted `htcHeadWorldRotation` are retained. This matches pose-expression times; it does not prove simultaneous hardware sensor capture. New timestamped recordings cannot substitute a receipt-time camera pose when the matching pose is missing. Older recordings retain explicitly exploratory legacy analysis.
- `taskTargetId`, target position/bounds, and raw left/right ray object IDs support the event-to-target join. **AOI definition:** closest Layer 8 interaction collider, including the task's gaze-hitbox expansion. These are not visual mesh intersections. Headset alignment corrections are not applied to these raw per-eye rays. This AOI definition needs prospective approval/validation before a last-target-fixation outcome is used.
- Raw head receipt time, eye-dot state, focus, source validity and existing trial identity remain recorded. Frame polling is still frame limited. Distinct predicted gaze timestamps do not establish 120 Hz sensor acquisition.

## Analysis and graphs

**Implemented:** `analysis/eye_timing.py` counts actual observed query brackets, repeated/missing/backwards timestamps, failed queries, clock changes and reported timestamp cadence separately for gaze, pupil and geometry. It never labels those rates as verified sensor cadence.

`analysis/eye_outcomes.py` deduplicates each stream independently. Pupil wavelet timing uses integer differences in reported pupil capture time when available; historical recordings retain labeled query time. Gaze candidates retain query-receipt durations, with native times used for continuity/deduplication. Saccade candidates use the matching head pose for new recordings and require the existing low-head-motion/bracketing checks. IPA is still exploratory; it is not automatically a workload score.

A last-target **candidate** must be independently detected by I-DT, have every sample intersect the same target AOI, and have observed samples on either side of its segment boundaries. A final target event touching a boundary is reported as censored, not replaced with an earlier event or zero. Both eyes need an uncensored candidate for the descriptive trial mean. Selection-adjacent events can therefore be unavailable. The validated `last_target_fixation_s` field stays empty. Graphs use the explicitly named candidate column; historical files cannot gain missing AOI information retrospectively.

`analysis/eye_quality.py` exports two additional precision measures alongside the existing world-ray measures:

- `target_error_sd_population_deg = sqrt(mean((error - mean(error))²))`.
- `target_error_rms_s2s_deg = sqrt(mean((error[i+1] - error[i])²))`, within contiguous valid segments only.

These measure variation in **error magnitude**. Existing `rms_s2s_deg` measures angles between consecutive gaze directions. Looking alternately one degree left and right of a target gives a 2° direction RMS but a 0° error-magnitude RMS. Neither is silently renamed or substituted for the other. See the [reference audit](vive-eye-measurement-audit-20261026.md) and [Schuetz & Fiehler's paper](https://doi.org/10.16910/jemr.15.3.3) ([full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC10136368/)).

`research_measures.py` now reports capture-evidence checks and per-stream timing in `measurement-status.json`. These are presence checks, not acceptance thresholds. Legacy ray-visit/transition counters have explicit `proxy_*` aliases; old columns remain for compatibility. Trial Viewer shows actual current timestamp/head-pose availability and tells the researcher that measurement validation is pending.

## Recording and validation exercise

**Implemented:** independent validation, fit, held-out and drift dots require at least two seconds of target exposure before manual Next. Finishing or cancelling creates a receipt containing the validation ID, expected/completed target count, recording ID and completion state. Completion explicitly does not approve accuracy. Finishing these checks and completing a run close the eye recording and write its completion receipt; subsequent observations start a new segment. Crashes and failed writes remain incomplete.

**Protocol proposal:** complete HTC calibration, then the independent held-out check in the actual seated task area and a drift check before/after a test run. Look at the dot, allow time to find it and settle, then advance. The two-second minimum is an exposure floor, not proof that the wearer fixated it. Current analysis excludes the first 0.25 seconds; approve this procedure before collecting study data.

## Remaining evidence required before study use

1. **Open decision:** predeclare acceptable spatial error/precision, valid-time coverage, minimum cadence and exclusions. Choose these from task object size/separation and the intended outcomes; no paper's threshold is automatically validated for Focus Vision.
2. **Physical validation:** record independent held-out dots and drift on the actual headset, across the task's angles/distances and representative wearers. Confirm left/right orientation and the target AOI against the scene. A quick alignment pass is not this validation.
3. **Physical timing validation:** inspect fresh raw stamps and flags after deployment, including invalid tracking, pause/resume, repeated readings, head turns and session restart. Confirm matching VIEW-space availability and measure added query cost. Neither pupil time nor predicted eye time proves sensor sampling independence.
4. **Independent event validation:** use the existing reviewed-window annotation comparison and threshold-sensitivity outputs; report agreement on separate recordings rather than treating synthetic tests as ground truth. Review the collider AOI and censored selection-adjacent events before prescribing a last-fixation metric.
5. **Pupil protocol:** control/report lighting, visible task luminance, gaze angle and eye-dot use; collect separate light/blink/head-turn checks. The current IPA implementation has numerical safeguards but still needs reference-method and physiological validation. Do not substitute IPA for reported NASA-TLX.
6. **Surveys:** collect all six Raw TLX responses per intended block. Missing responses remain missing.

No approval file, significance result or participant data was fabricated. The older development recordings remain development evidence.
