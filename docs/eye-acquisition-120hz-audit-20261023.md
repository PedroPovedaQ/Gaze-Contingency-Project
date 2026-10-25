# Eye acquisition feasibility audit — 23 October 2026

**Implemented / observed:** the experiment records EXT input-state deliveries and HTC per-eye queries through SharedGazeStream. It does not currently identify distinct physical sensor samples. No acquisition behavior changed in this audit.

## Device evidence

Read the last 5,000 complete rows of headset recording `EyeMeasurements/20261023T214253_25f786a1b7bb4009ba7e4af716cf3dde/gaze_observations.jsonl` over wireless ADB. This is a partial setup/transition recording, not a controlled acquisition benchmark or an active-trial rate estimate.

- Receipt-time span: 199.670 seconds; 2,969 changed input deliveries, across 2,030 Unity frames.
- 2,776 rows report valid gaze and both eyes valid.
- No row provides a known sensor capture timestamp.
- In receipt-time windows [125,130) and [130,135) seconds, 630 and 654 deliveries were recorded: 126.0 and 130.8 deliveries/sec. These counts cannot establish a 120 Hz physical sensor rate.
- Across the partial window, delivery interval median is 8.085 ms, with gaps up to 523.584 ms. The aggregate rate is 14.866 deliveries/sec; it mixes valid and invalid setup periods and must not be presented as the active experiment rate.

## API constraint

The installed VIVE OpenXR wrapper calls its native gaze function at predicted display time; the public convenience method drops the returned time. HTC documents XrEyeGazeDataHTC.time as the requested time, not sensor acquisition time. Repeating that call at 120 Hz, or comparing changed gaze coordinates, would not establish distinct samples. A stationary gaze can legitimately produce equal coordinates.

- [HTC gaze result structure](https://hub.vive.com/apidoc/api/VIVE.OpenXR.EyeTracker.XrEyeGazeDataHTC.html)
- [Focus Vision specifications](https://www.vive.com/us/product/vive-focus-vision/specs/)
- [Wave eye API](https://hub.vive.com/storage/docs/en-us/WVR_GetEyeTracking.html) exposes a timestamp but requires main-thread calls; compatibility with this OpenXR application and timestamp semantics are unverified.

## Open decision / next implementation gate

Probe whether the standalone runtime returns XrEyeGazeSampleTimeEXT with a genuine source timestamp through the native OpenXR gaze-space location path. This requires a native acquisition adapter beyond the current Unity EyeGazeDevice interface. Keep it diagnostic until its timestamps, thread/lifecycle requirements and coordinate conventions are verified. If unavailable, investigate a supported HTC native acquisition or buffered-sample API; do not assume Wave can be mixed into this OpenXR app.

A successful 120 Hz claim requires distinct source timestamps or sequence IDs, approximately 8.33 ms spacing, explicit duplicate/gap/invalid counts, and a sustained wearer run during the actual experiment. Rendering can remain independent. Preserve raw gaze and existing calibration/guidance behavior. The current integration does not yet meet this gate.

## Native timestamp adapter (implemented after the audit)

**Implemented:** an Android ARM64 diagnostic OpenXR feature now intercepts the existing `xrLocateSpace` calls for action spaces with a suggested EXT eye-gaze binding. It requests `XrEyeGazeSampleTimeEXT` through the output chain and preserves existing output structures, poses, result codes and the application's shared gaze pipeline. It records raw OpenXR coordinates with the base-space handle and action-space offset. These are not directly Unity world-space coordinates.

**Important correction to the original next-step wording:** OpenXR defines this timestamp as the time at which the returned pose is expressed. The runtime can clamp, interpolate or predict it. It is therefore **not guaranteed to be a physical sensor acquisition timestamp**, and changing its value on every call does not establish independent observations. A zero value means unavailable. See the [Khronos definition of XrEyeGazeSampleTimeEXT](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrEyeGazeSampleTimeEXT.html).

The feature does not start a 120 Hz polling thread. It passively measures the application's existing query path. It reuses the enabled Eye Gaze Interaction extension, rather than declaring a second implementation of that extension. Missing support leaves the experiment's existing gaze path in use. `sourceCaptureTimeKnown` remains false in the shared stream. Suggested bindings identify a candidate action; they do not independently certify its current physical sensor source.

### Files and interpretation

Each `EyeMeasurements/<session>/` directory gains:

- `native_gaze_capabilities.json`: runtime/build identity, coordinate conventions, timestamp limitations.
- `native_gaze_observations.jsonl`: bounded native batches with epoch/sequence, result and validity flags, requested and returned OpenXR times, native receipt time, raw pose, space handles, buffer drops, Unity drain-time clock bracket and focus status.
- `native_gaze_end_status.json`: orderly writer shutdown and coverage through the last observed native counter. Missing end status means the live/aborted recording is incomplete or unknown; it is not silently treated as complete. This flag does not certify sensor coverage.

`requestedTime` and `runtimePoseTime` use the OpenXR clock. `receiptMonotonicNs` uses a native steady clock. Unity drain times are a third clock. Do not subtract across those clocks to invent capture latency. The clock bracket documents software drain timing only. Focus is observed at drain time, not individually by the sensor.

Analyze a copied session with:

```sh
python3 analysis/native_gaze_timing.py /path/to/EyeMeasurements/session --output report.json
```

The report separates software query rate from distinct reported pose-time cadence, counts zero/repeated/regressing timestamps, validity, gaps and native drops, and groups epochs and spaces independently. A default 60-second valid/focused software-query interval uses a 50 ms gap cutoff as an engineering diagnostic; neither threshold validates the sensor. Missing global sequence numbers break sustained intervals, while legitimate interleaving of spaces does not. Sensor rate remains unknown even if reported pose times happen to advance at 120 Hz.

### Verification and remaining acquisition gate

- Native fake-runtime tests (`scripts/test-native-gaze.sh`) exercise binding order, output chains, success/failure results, handle lifecycle, overflow, concurrency and restart, under AddressSanitizer and UndefinedBehaviorSanitizer.
- Analyzer tests exercise partial recordings, clock separation, validity, repeated/predicted times, reordered and missing sequences, and inconsistent end counters.
- Unity's native diagnostic checks verify ABI sizes, integer nanosecond serialization, file writing and enabled Android settings. Existing shared-gaze and entry-alignment checks must also pass.

**Open validation:** obtain vendor-supported raw capture timestamps/sequence IDs or a documented buffered native sample stream before calling this physical 120 Hz acquisition. A sustained wearer run is also required. Run at least two minutes of normal search, with stationary gaze, left/right and up/down movement, deliberate blinks, and a pause/resume. Verify normal calibration, cross charging and guidance; then inspect records, gaps, flags and rates. The adapter alone cannot establish sensor cadence, eye accuracy, fixation validity or acoustic latency.

## Device smoke-test result (23 October, updated build)

**Observed:** Android IL2CPP build succeeded in 104.1 seconds. Wireless installation and launch succeeded on the connected Focus Vision. The APK's stripped ARM64 library matches the locally built native library. The runtime reports `VIVE WAVE`, version `0.0.1`; this is the runtime's own reported string, not a headset firmware version. Device initialization identified one candidate eye action, and the managed/native record ABI matched.

Recording `20261023T231821_7a137d30122c4d70a133b5b60e3a6453`, copied during this unworn setup check:

| Diagnostic | Observed |
|---|---:|
| Complete native query records in copied snapshot | 3,024 |
| Native receipt-time span | 64.178 s |
| Failed queries | 0 |
| Native buffer drops / missing sequences | 0 / 0 |
| Valid position-and-orientation queries | 0 |
| Returned pose time equal to requested time | 3,024 / 3,024 |
| Malformed complete snapshot lines | 0 |

The app remains running, so there is no final end-status file. The analyzer correctly labels the recording **unknown or incomplete** rather than declaring a complete session. An earlier copy caught a partially flushed final line; the later snapshot contains no malformed lines. Local raw files and generated report are under ignored `analysis/results/native-gaze-20261023/`.

This establishes that the installed hook, ABI and recorder operate on the device. It does **not** establish valid tracking, normal wearer guidance, a sustained physical sensor rate, or 120 Hz acquisition. In this unworn sample the runtime returned requested pose times, not independently evidenced sensor capture times. Sleep/wake and relaunch commands were exercised and the app was returned to the foreground; wearer pause/recovery still requires confirmation.
