# Focus Vision eye acquisition: clock and capability audit

October 1, 2026. **Implemented:** source audit and application-delivery reporting. **Open decision:** access to independent native observations. **Hardware verification pending:** every rate and spatial-accuracy claim below.

## Current conclusion

Keep `sourceCaptureTimeKnown` and `sourceSequenceKnown` false. The current adapter establishes host delivery/freshness and saves raw observations; it does not establish physical sensor sample identity. More polling, changing pose coordinates and different JSONL row numbers cannot change that conclusion. See the [implemented measurement tools](gaze-measurement-validation.md) and [research plan](plans/2026-09-28-vive-eye-gaze-measurement-plan.md).

## Primary-source audit

| Route | Documented meaning | Consequence for this project |
|---|---|---|
| HTC `XrEyeGazeDataHTC.time` | HTC describes this as the requested gaze-information time. [HTC structure reference](https://hub.vive.com/apidoc/api/VIVE.OpenXR.EyeTracker.XrEyeGazeDataHTC.html) | A populated or advancing field is not evidence of capture time. |
| EXT `XrEyeGazeSampleTimeEXT.time` | When chained to the `XrSpaceLocation` result, this describes the clamped, predicted or interpolated gaze-pose time. Zero means unavailable; predicted times may be in the future. [Khronos specification](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrEyeGazeSampleTimeEXT.html) | This is a candidate diagnostic, not automatic proof of a distinct sensor observation. Establish this runtime's semantics before interpreting differences as sampling intervals. |
| `XrTime` versus host time | Runtime time requires the applicable platform/runtime clock conversion. [Khronos XrTime reference](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrTime.html) | Do not subtract raw runtime ticks from Unity seconds or fit one undocumented offset and call it capture latency. |

**Local source evidence:** VIVE OpenXR 2.5.1's `ViveEyeTracker.GetEyeGazeData(out ...)` initializes the gaze request and output time with `m_frameState.predictedDisplayTime`, then returns only the per-eye array. `m_frameState` comes from the `xrWaitFrame` hook. The low-level overload retains a result structure but does not establish a buffered sensor-history contract. Audited files: `Runtime/Features/EyeTracker/ViveEyeTracker.cs` and `ViveEyeTrackerHelper.cs` under installed package revision `e099ad12dbab`. Preserve upstream packages; do not patch `Library/PackageCache`.

Unity OpenXR 1.16.1's managed `EyeGazeInteraction.EyeGazeDevice` exposes a pose control, not `XrEyeGazeSampleTimeEXT`. The enum/header appearing elsewhere in the package is not evidence that the Input System forwards it. Our `SharedGazeStream` reads this managed pose and separately queries HTC fields; its host query time does not synchronize those sources physically.

## Remaining implementation gate — tasks #35 / #22

**Protocol proposal:** first investigate a supported native diagnostic feature that can own or identify the eye action space, request `xrLocateSpace` with the EXT timing structure, and save requested time, returned pose time, reference space, validity, host receipt and source clock mapping separately. This feature is not implemented or enabled in the study build. It must remain diagnostic until its lifecycle, native memory, session restart and coordinate behavior are tested.

Ask the vendor/runtime documentation to establish whether returned poses are raw, filtered, predicted or interpolated; whether a source sequence/callback/history exists; what happens when requests arrive faster than rendering; and which calls are thread-safe. Do not move the existing Unity feature calls to a worker thread without that contract. A new timestamp field must not automatically set `sourceCaptureTimeKnown` true.

If only the most recent frame-delivered pose is available, report that limitation and achieved delivery rate. Another SDK, PC-streaming mode or headset would be a separate study decision. Native binocular output, API observations, persisted rows and valid observations need distinct denominators; one row containing two eyes is not two temporal samples.

## Connected-device evidence to collect

**Protocol proposal; no numeric pass threshold selected:** save a separate short diagnostic recording for each condition below, recording actual app/render timing and all configuration changes. Do not silently cap `Application.targetFrameRate` and assume the XR compositor honored it.

1. Stable known target with normal study rendering, then intentional eye movements and head/chair turns.
2. Blinks, brief tracking loss, recovery and a repeat of the same target; retain invalid rows.
3. Representative study load with speech, selection, replay capture and file flushes.
4. A documented rendering slowdown using a supported mechanism. Verify the achieved frame intervals before comparing acquisition. Restore the ordinary configuration afterward.
5. Repeat independent spatial targets and drift checks after refitting/recentering.

Use the same firmware/runtime/build and record any changes. Compare phase delivery intervals, validity and gaps using `analysis/eye_quality.py`; use exact linked source observations in `guidance_audit.py`. If no native identity is exposed, leave native counts/drops and sensor Hz unknown. A truthful lower-rate result is an acceptable engineering finding, not evidence that task-level measurement is validated.

Acceptance of spatial thresholds, event algorithms and workload outcomes remains separate from acquisition rate. Task #35 stays open; #14 owns spatial validation, #15 event/quality evaluation and #22 the shared raw contract.
