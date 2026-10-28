# Neutral quick-test preparation cache — 2026-10-26

## Implemented

Raw MP3 downloads were already persistent, but each quick test destroyed in-memory clips and repeated 566 sequential MP3 decodes, neutral no-op worker scheduling, normalization and replay export. Neutral quick test always uses Eric, so a rotating identity was not the cause. No previous full loading-screen timing was available; this diagnosis identifies repeated work, not a measured before/after headset latency.

Added a disposable persistent float-PCM decode sidecar bound to the exact provider-source SHA-256 and its own payload hash. It skips MP3 decoding on warm neutral loads; malformed/partial/mismatched sidecars fall back to the source. No transcript/wording validation was reintroduced. Final normalization still runs using the current library and replay exports exactly the final headset waveform. Clone DSP remains on its existing worker.

Preparation batches cached phrases and replay exports with a 4 ms yield budget rather than forcing an extra frame per clip. Individual decode/copy/normalization operations are not preemptible. Total preparation seconds are logged by VoiceSynthesizer.

Trial Viewer Wi-Fi settings exposes **Prepare neutral test audio**. A separate Mac worker reads the installed runtime catalog (including quick-test introduction without mutating participant configuration), imports existing headset source recordings, synthesizes only missing Eric lines, creates versioned decode sidecars, and publishes changed files using same-filesystem renames. Existing headset MP3s are never overwritten. Three workers, one global process lock, atomic status, bounded transport timeouts, and retry only for transient provider HTTP failures. Current scope is Eric neutral quick tests, not every participant neutral voice. Mac cache remains available after viewer exits; a running preparation finishes independently. Trials do not rely on the Mac cache once transferred.

On-device verification found a separate prior model-toggle regression: GetComponentInParent omitted inactive parents after hiding its button. Fixed all provider-button lookups to include inactive parents; this caused recurring StudyRayControls.LateUpdate null-reference exceptions during setup.

## Verification

- Voice-isolation suite passes, including production warm-load path without asynchronous decode/network yield, corrupt/stale/truncated cache fallback, clone worker cancellation and level matching.
- Trial Viewer Python suite: 94 tests pass. New tests cover catalog identity/path restrictions, source reuse without keys/provider access, active-session rejection, binary format, transfer publication and a second pass with zero decode/transfer.
- Actual Eric catalog: 566 clips; first Mac pass 39.08 s end-to-end, generated 0, decoded 566, transferred 566. Second pass 4.76 s, generated/decoded/transferred all 0.
- Production C# cache reader validated all 566 server-generated clips in 2.222 desktop seconds. This is cache read/validation, not headset loading-screen timing.
- Viewer builds passed; latest app `/Users/pedro.poveda/Applications/Trial Review Neutral Cache.app`, opened in Live. UI visually shows the button and ready 566/566.
- Initial Android cache build passed in 75.1 s and installed at 22:06:48. Final settings-fix Android build passed in 46.2 s; installed at 22:09:10, app PID 20011. MiniLM v4 service/ADB tunnel restored after installation.

## Remaining wearer verification

Start Neutral quick test twice and compare the preparation log elapsed time and perceived wait. Confirm all speech plays, no network waits occur during trials, Settings opens/reopens without error, and the model toggle works. Full headset loading time still includes Unity clip allocation, level matching and replay export; no claim of instant loading or measured headset speedup yet.
