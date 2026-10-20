# Jev options and replay inspector — October 12, 2026

Implemented: up to five eligible prepared navigation phrases, request/response tracing with correlation IDs, preserved provider probabilities, Jev timeline/inspector and hover help.

Validation:
- Unity SpatialNavigationChecks: 375 catalog rows, five eligible candidates throughout signed-angle/distance grid, seams/NaN/prepared filtering and local fallback.
- scripts/test-spatial-voice.sh: contract, trace-ID/credential exclusion, five-option/six-option limit, malformed/out-of-set results, PCM, deadlines and cancellation; existing 2688 encouragement fact/history cases and 8192 prepared-clip subsets.
- scripts/test-voice-isolation.sh passed.
- Trial Review Python suite: 58 tests passed.
- Desktop build runs TrialReviewJevChecks: exact request/playback joins, late/out-of-order replies, malformed/legacy records, timeouts, stale applications and cleared run state; existing reader/rendering/export checks passed.
- Four synthetic live requests through the configured Vercel Gateway: all choices within the five-option menu, all transmit decisions correct, five probabilities returned. The first cold call took 826 ms; later calls took 335–387 ms. These used a five-second diagnostic timeout and are not evidence of passing the app's 450 ms deadline or of headset latency.
- Built desktop app at `/Users/pedro.poveda/.codex/visualizations/2026/10/12/trial-review/Trial Review.app`. Visually inspected Jev lane, inspector, selected phrase and probability bars using the explicitly synthetic demo. Native pointer automation did not reliably activate Unity controls; navigation/index tests passed, but hover/click behavior still merits a human check.
- Android build-and-run succeeded; installed APK SHA-256 matched `5e80c3570ac232551ad0564d8a5b6c04528a2bcaead2b35a66934a623b31e458`. Running PID 28119 at verification, no Unity/AndroidRuntime error lines. Wi-Fi ADB required reconnects during verification.

Full traces require new recordings. Existing data is neither overwritten nor backfilled. Software playback onset is not proof of hearing; model option probability is not validated navigation correctness.
