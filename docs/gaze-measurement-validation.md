# Gaze diagnostics, validation and guidance — September 30, 2026

**Implemented locally; hardware validation pending.** This change implements the measurement tools and pilot guidance policy from the [measurement plan](plans/2026-09-28-vive-eye-gaze-measurement-plan.md). It does **not** establish native 120 Hz acquisition, task-wide eye accuracy, validated fixations/blinks/saccades, or an eye-derived workload measure.

The [October 1 clock audit](vive-eye-clock-audit.md) separates HTC requested pose time, EXT expressed pose time and physical sensor capture time. The current managed adapter cannot establish the latter; the proposed native diagnostic route is not enabled or validated.

## What changed

- `SharedGazeStream` captures the OpenXR EXT Input System eye pose and input-event timestamp. Guidance, readiness, trial intersection summaries and replay consume the same snapshot/ray. It never substitutes head gaze. Input age, tracking/pose validity, application focus and available bilateral HTC validity gate new decisions.
- `EyeGazeRayVisual` no longer flips the tracked transform in `LateUpdate`. Unity-converted EXT poses receive the tracking-parent transform once. Optional X-origin mirroring lives in the adapter, defaults off, and is recorded as a diagnostic correction. Raw observations remain separate. Native HTC positions/quaternions remain available unconverted, alongside rays converted with the vendor helper.
- HTC per-eye gaze validity, pupil diameter and eye openness are independently probed. Query receipt time is separate from EXT input delivery time. The high-level HTC getter does not expose a proven sensor timestamp/history; these calls are not guaranteed synchronized with the EXT snapshot. Unsupported/invalid values have explicit flags and export as blank measurement cells.
- Input updates and rendered-frame heartbeats are both recorded, including invalid states, transitions and pauses. `newInputUpdate` identifies a changed Input System state-event timestamp/device, **not a distinct physical sensor sample**. Identical coordinates do not imply duplicate samples. No 120 Hz timer or synthetic upsampling is introduced.
- Guidance uses shared participant-directed commands, 44 phrase variants across left/right, coarse turns, area/return, up/down and alignment. Horizontal wall membership ignores height. Near-vertical gaze is treated as azimuth-ambiguous; invalid/stale gaze suspends new guidance and cancels pending tips. Separate arrival/return gates prevent blink-induced exits.
- Target identity is removed from trial announcements, wrong-choice reminders and the readiness cross. Setup asks participants to follow guidance. Normal voice preparation shows `Loading...`. Correct choices receive a chime and varied reinforcement in the current voice; transitions wait for success feedback. The October 1 catalog now offers six choices per gender; see [voice choices](voice-choices.md).

**Protocol proposal / pilot settings:** delivery-age limit 100 ms, direction stability 150 ms, speech eligibility 1.25 s after the preceding request, within-wall horizontal tolerance 5°, vertical tolerance 3°, near-vertical horizontal direction magnitude squared below 0.04, chair-turn phrasing beyond 45°. These are configurable/code-level starting values, not validated gaze accuracy or fixation thresholds. Preserve a common policy across voices and tune against false/missed cue evidence.

## Researcher eye check

### Quick eye alignment — implemented locally, October 10

After completing HTC's calibration, select **Eye check → Quick eye alignment** in setup or while explicitly paused. Look at three world-fixed dots without moving the head substantially. The first dot fits a small angular offset; two different dots check it. Dots advance automatically, and **Next target** cannot bypass collection. **Finish check** or **Close** cancels an incomplete check. **Clear alignment** removes an accepted correction.

The fit uses the **actual usable ray source** (EXT or the derived HTC binocular fallback) in head coordinates. It takes the median yaw/pitch bias and applies the opposite bias to later directions in head coordinates, so the correction follows head turns. It never moves the eye origin, modifies the acquired EXT/per-eye data, or fits to the hidden search target. Guidance, readiness, trial logging, replay and the optional orange ray consume the resulting shared ray. This is a quick offset correction, not a replacement for HTC calibration or task-wide spatial validation.

**Pilot acceptance settings, not research-validated thresholds:** each point requires a 350 ms settling window followed by at least 1.5 seconds and 30 distinct software input updates of continuous valid gaze/head tracking. Invalid data or delivery gaps over 100 ms reset collection; head rotation over 5° resets the window. Each point times out after 20 seconds. Fit/validation offset scatter p90 must be at most 1.5°; corrections above 5° are rejected. Each held-out point must have corrected median error at most 2° and p90 at most 3°, with no worsening greater than 0.1° median or 0.25° p90. Nontrivial corrections require at least 0.25° average median improvement across both checks; fitted magnitudes below 0.25° use zero correction. These settings need wearer testing and protocol review before research collection.

Acceptance is session-local and source/participant/device-specific. A new alignment starts uncorrected; failure does not restore an old offset. Source, diagnostic-origin correction, or participant changes clear the profile. App focus loss/pause clears it too; manually clear/repeat after a headset adjustment that does not cause focus loss. Missing head tracking suppresses corrected guidance. Profiles are not reloaded after app restart. No mandatory block-start gate is added.

Each finished attempt saves `quick_alignment_<id>.json` alongside the eye JSONL, including method/version, source, participant, fit/check metrics, sample sequence ranges and pilot limits. Result-save failure disables alignment. Each observation retains `raw*` EXT and native eye fields, adds `unalignedWorldOrigin/Direction` for the selected pre-alignment ray, and records the final used ray plus `alignmentId`, `alignmentStatus`, `alignmentApplied`, and angular corrections. The flattened raw CSV includes these fields. Replay retains its existing exact link to the eye observation. The general offline EXT quality report still evaluates original EXT data; quick-alignment before/after validation is in the separate result file and includes native fallback when that was the fitted source.

New replay records also include `gazeRayVersion`, source, alignment ID/status/applied flag and angles, plus the pre-alignment ray in recording coordinates. **Recorded gaze** reproduces the ray used live, with its actual eye origin; **Before alignment** displays the saved pre-correction ray. Playback does not apply the angles again. The Unity replay inspector shows the alignment metadata; the standalone viewer offers both rays. Legacy P020 recovery remains separate and older recordings are not retroactively calibrated.

**Validation remaining:** headset UI readability, timing/comfort, same-source stability, before/after error across the full study layout, different head poses/distances, and between-block drift. Software tests prove math/state/data handling, not physical accuracy or native 120 Hz sampling. Independent quality measurement is required; see [Niehorster et al. (2026), open access](https://doi.org/10.3758/s13428-026-03039-4) and [HTC calibration instructions](https://www.vive.com/us/support/focusvision/category_howto/eye-tracking.html).

### Full diagnostic sets

In setup or an explicitly paused study, point the right controller at **Eye check** and press the trigger. It opens a panel with raw ray, validity/reason, delivery age, head pose, HTC per-eye state, input-delivery/row rates and error to a **separate visible validation dot**. The panel is hidden during search and does not use the hidden experimental target as its validation reference.

Choose a full diagnostic check, turn toward the dot, hold gaze for at least two seconds, then press **Next target**. The dot and control panel remain world-fixed. Default analysis discards the first 250 ms of each target; inspect the recording and increase the settling interval if the participant has not yet reached the target. These full diagnostic sets do not automatically pass/fail or apply an offset; only the separate quick-alignment flow above does.

- **Validation:** 54 targets: six bearings (including a seam) × three low/middle/high elevations × three distances (1.2/2.4/3.2 m). Height and distance vary independently; head orientation is logged.
- **Offset fit:** local 3×3 grid at 2 m for estimating a candidate constant angular bias.
- **Held-out:** different bearings, elevations and distances; a different validation ID prevents fitting and evaluating on the same set.
- **Drift:** five center/side/high/low positions. Repeat between blocks and after refitting; compare to the preceding check with the same fit/reference. Recenter changes must be identified from logged tracking-origin pose and treated separately from physiological drift.

Repeat left/center/right and up/down while facing different walls, then after recentering. Compare raw and mirrored-origin errors. Leave correction off unless independent held-out results support a specific correction. Changing X position alone cannot repair a directional sign/rotation error. Vendor calibration is still performed through the headset; the app does not claim to invoke it.

## Files and analysis

Continuous data is written under:

`Application.persistentDataPath/EyeMeasurements/<UTC>_<recording_id>/`

- `gaze_observations.jsonl`: raw/used rays, head/tracking-origin poses, validity/gaps, input-delivery times, independent target context and optional HTC signals.
- `capabilities.json`: build/device/OS, source and coordinate/clock limitations.
- `end_status.json`: accepted row count and clean-close/error receipt when the component closes normally; absent receipt means completeness is unverified.

Replay samples carry the observation sequence, source, timestamps and validity reason. The replay header links the eye file when acquisition has started. Trial IDs retain the existing participant/run/round convention. Unknown sensor sequence/capture timestamps remain explicitly unknown. The bounded existing replay writer is reused; write errors/overflow stop the eye recording visibly rather than silently dropping evidence. Measure on-device CPU, disk and memory overhead under voice/video load.

Pull the eye folder from the actual installed app's persistent-data directory, then run:

```sh
python3 analysis/eye_quality.py /path/to/gaze_observations.jsonl --out /path/to/new-eye-report
```

Output: a flattened raw CSV, per-target spatial validation CSV, `trial_eye_quality.csv`, candidate eye-event CSV and `quality_report.json`. Existing output directories are refused to protect earlier evidence. Preserve the JSONL source and metadata.

The report separates polling rows from input delivery rate, shows interval median/p90, valid-time coverage and gaps, target median/p90 error, and within-target RMS successive-sample angular displacement excluding gaps. It always reports sensor rate **not established** for this adapter. Compare normal versus reduced rendering rates on the headset; evidence of 120 deliveries/s still does not establish independent sensor observations.

To evaluate an offset on independent data:

```sh
python3 analysis/eye_quality.py recording.jsonl --out heldout-report \
  --fit-id FIT_VALIDATION_ID --heldout-id DIFFERENT_HELDOUT_VALIDATION_ID
```

The constant world yaw/pitch diagnostic is estimated only on `fit` records and evaluated only on `heldout` records. It reports worsened regions and never alters live gaze. A candidate improvement still needs task-wide checks, repeat fits and a predefined acceptance policy.

## Eye events and workload gate

**Implemented analysis, not validated events:** a basic world-ray velocity detector emits `candidate_fixation` and `candidate_saccade`; bilateral, separately valid openness can emit `candidate_blink`. Loss of tracking alone is never a blink. Events do not bridge gaps, trial boundaries or validation-target changes. Default velocity/duration/openness cutoffs are pilot parameters, not transferred validation claims.

Supply an independently annotated CSV (`kind,start,end,trial_id,validation_id`, input-event clock, kinds `fixation`, `saccade`, `blink`) with `--annotations`. The report computes one-to-one matching within a 50 ms onset/end tolerance, precision/recall and onset disagreement. Inspect by event class and head-motion condition; do not use pooled performance as approval. A reviewer-approved held-out comparison against a head-aware method is still required. Video alone does not provide true foveal ground truth.

Legacy trial fields called fixation/saccade are explicitly labeled ray-intersection proxies; live blink totals remain unavailable pending validation. Pupil values are retained for later quality checks but no workload score is generated. Only after validating signal availability, lighting/baselines, artifact handling and events should exploratory measures be compared with block-scoped NASA-TLX. Repeated block ratings are not independent trial outcomes.

## Verification and remaining gates

- Local C# freshness/vertical/zone-gap checks, rotational layout tests, voice isolation tests and Python quality/CSV tests run separately from headset validation.
- `Tools > Codex > Check Gaze Measurement` supplies Unity coordinate/hidden-target/shared-language checks; running this is not a sensor accuracy test.
- After the researcher authorized restoring the three missing dependencies, a fresh canonical Unity batch compile and the full offline `StudyVerificationChecks.Run` suite passed. This includes actual shared-stream CSV valid/invalid rows, append safety, coordinate transforms, physics filtering, controller gates, readiness, boundaries and replay. These checks establish desktop behavior, not hardware accuracy or sensor rate.
- No Vive was listed by ADB during this work. Native-rate evidence, actual HTC signal availability, UI legibility, false cue rates, fit/recenter behavior and the headset Build And Run remain unverified. Do not mark #14/#35 or event validation complete from the desktop checks.

## Compatibility gaze CSV — September 30 overnight repair

**Implemented locally, hardware verification pending.** `gaze_log.csv` now samples the shared stream once per frame, including invalid and transition rows. Its appended fields identify the raw observation sequence, delivery/receipt times, validity and raw-file link. It is a compatibility table, not proof of the sensor sampling rate. Blink/fixation fields remain empty pending validated event analysis; legacy public blink access reports unavailable. Re-enabling the component appends to the same compatible file, and an incompatible existing header causes an explicit recording error without overwriting data. The full input-delivery/heartbeat record is still `EyeMeasurements/.../gaze_observations.jsonl`. Non-finite vendor values retain an invalid row with finite unavailable placeholders; CSV validity flags must be respected.

Interrupted final JSON records are recoverable as explicitly incomplete prefixes; corrupt middle records, mixed recording IDs and unordered sequences are rejected. A clean-close receipt is checked against the accepted row count and cannot override a recovered/truncated file.

**Implemented — October 1 trial linkage repair:** raw gaze and compatibility rows use the manager’s presented-trial context, preserving round and voice while success feedback plays after the next-objective counter advances. Practice remains explicitly separate. Unity regression checks cover practice, the 39→40 block boundary and final completion; historical recordings are not relabeled.

**Implemented — October 1 pause durability:** eye/replay application-pause callbacks enqueue an ordered background flush after their final marker/observation. The file-level regression verifies visibility before disposal/periodic flush and continued recording after resume, including a stalled worker. This best-effort flush is not fsync or protection against immediate OS termination; forced-interruption testing remains a hardware gate.

### October 1 audit/report checks

**Implemented locally; physical validation pending.** Per-trial quality now separates search windows from pauses/practice and retains explicit unknown calibration/exclusion status. Per-target reports distinguish mean-direction offset, signed bias, sample error, pooled within-contiguous-segment angular spread and time/update missingness. See `analysis/README.md` for exact definitions. Invalid heartbeats break precision even between otherwise close input updates. All 72 Python tests passed, including CLI output verification.

`guidance-decision-v1` links ordinary, repeated, area-entry and return requests to immutable raw observation identities. Revalidated commitments, abandoned corrections and playback/cancellation are distinguishable. Voice fixtures and the full Unity integration suite passed; the tests do not establish physical false-cue rate, native rate or acoustic timing. See the gaze guide for the event contract.

**Implemented — offline decision audit:** `analysis/guidance_audit.py --eye <raw.jsonl> --replay <recording.jsonl> --out <new-directory>` checks exact source observations, validity/freshness claims, trial context, revalidation and request/playback/cancellation provenance. `consistent` is only recording consistency; incomplete-file warnings and missing hardware validation remain separate. See `analysis/README.md` before interpreting its software latency.

**Implemented — timing/capability exports:** the eye-quality command additionally produces phase delivery tables, exact input interval rows, signal-availability/clock dictionaries and PNG/SVG timing plots. Missing hardware capture time, native counts and drops remain unknown. HTC availability counts use distinct host-query times so copied fields cannot imply a higher update rate. See `analysis/README.md` for denominator and missingness definitions. The plots were inspected using a labeled synthetic fixture; actual Focus Vision capability/rate evidence remains outstanding.

**Diagnostic layout repair:** while a validation dot is active, the opaque readout moves below the local target grid. The prior idle position intersected the view rays to the right (+15-degree) fit dots. An editor geometry check projects fit/drift/held-out directions against the actual canvas dimensions, scale and active offset. This addresses panel occlusion, not headset optical accuracy or comfort.
