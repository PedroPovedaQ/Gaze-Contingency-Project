# Passive OpenXR gaze timestamp diagnostics

This native library observes existing eye-action `xrLocateSpace` calls. It does not acquire a new sensor stream, increase polling cadence or modify the ray used by the experiment.

The diagnostic feature reuses Unity's **Eye Gaze Interaction** extension. Do not declare that extension again in the custom feature: the installed VIVE/OpenXR validation rejects duplicate extension providers.

## Build and test

From the repository root:

```sh
./scripts/test-native-gaze.sh
./scripts/build-native-gaze.sh
python3 -m unittest discover -s analysis/tests -p 'test_native_gaze_timing.py'
```

The native build uses Unity 6000.3.10f1's bundled Android NDK and outputs the ARM64 plugin under `Assets/Plugins/Android/arm64-v8a/`. Override `GAZE_ANDROID_NDK` for a different compatible NDK. Rebuild the library whenever native source or headers change; importing C# alone does not rebuild it.

In Unity, use **Tools > Codex > Enable Native Gaze Timestamp Diagnostics**, then **Check Native Gaze Timestamp Diagnostics**. Keep Eye Gaze Interaction enabled. Disable just the diagnostic feature to leave normal eye input operational without native interception. The recorder will label the native feature unavailable.

## Design boundaries

- OpenXR calls execute through the original function pointer. The mutex protects maps and a bounded 8192-record ring, and is not held while invoking the runtime.
- Suggestions and action-space creation may arrive in either order. A re-suggestion replaces that interaction profile's candidates; explicit action, space, action-set and session destruction clean the maps.
- Existing output chains are retained. An existing sample-time structure is reused and cleared before each query; otherwise a temporary structure is inserted and removed before returning.
- Success codes and validity flags are separate. Failed outputs are unspecified and not copied into records. Zero time means unavailable.
- Sequence and epoch are adapter bookkeeping, **not hardware sample identifiers**. Overflow counts, missing sequences and incomplete files must remain visible.
- A successful build does not prove that a runtime uses this entry point or binds the candidate action to the physical sensor. Verify `eyeActions`, `locateCalls`, records and runtime status on device.
- ABI sizes are 136 bytes per record and 56 bytes per status for ARM64; Unity verifies record size before draining and Editor tests check both.
- Runtime pose timestamps may be interpolated/predicted. Raw sensor sampling rate remains unknown.

See [the acquisition audit](../../docs/eye-acquisition-120hz-audit-20261023.md) for clock semantics, source links and the remaining wearer/vendor validation gate.
