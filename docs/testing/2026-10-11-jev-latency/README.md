# Jev guidance latency — 2026-10-11

## Implemented

Coarse catalog guidance now requests `navigation_phrase` and `transmit` in one POST, with the same candidate whitelist, prepared-clip filter, live geometry and voice/session revalidation. A supplied answer bypasses the second transmission API call but still uses the shared wait cooldown, three-second deferral bound and final local checks. One-candidate local selection retains the ordinary transmission advisor. Fine guidance and interruption decisions retain their separate paths/deadlines.

After the prepared voice library becomes available, one synthetic, non-blocking request warms the shared HTTP client/model path (1.5-second cap, canceled on disable). It contains no participant data and cannot authorize speech. Warm-up failures are diagnostic only. The real combined decision keeps the existing 450 ms catalog deadline; the separate transmission and interruption deadlines remain 400 ms. No deadline inflation or stale-response caching.

## Attribution

Latest pre-change archived headset run 019: 72 transmission snapshots. The interval between context capture and playback capture had median 0.206 ms, p95 0.962 ms, and maximum 98.576 ms. This covers only that part of snapshot construction, not total Unity frame time or all JSON/network work. Existing successful remote decision averages were approximately 261–268 ms. Coarse guidance serialized two remote decisions before playback.

## Measured experiments

`paired-desktop*.json` contain synthetic live gateway tests, alternating old/new order across 12 pairs per iteration. Two candidates initially, then four candidates matching normal catalog count. Scenarios: fresh corrective direction, recent redundant direction, invalid gaze, and already-speaking audio. These are desktop Mono networking measurements, not Android results.

- Initial combined prompt was rejected after incorrect invalid-gaze/already-speaking answers. Retained raw results for traceability.
- Revised prompt made gaze validity and idle playback explicit hard preconditions. All completed joint replies in subsequent iterations matched expected answers.
- Four-candidate cold comparison: one first-request timeout in 12 joint decisions, no other joint failures. This motivated connection warm-up.
- Final warmed comparison: median 223.749 ms joint versus 453.905 ms sequential (50.7% reduction); maximum joint 323.420 ms. Zero deadline/errors in either set of 12 decisions; all 12 joint answers matched expectations. Warm-up itself took 397 ms outside the timed guidance path.
- Baseline returned speak for three invalid-gaze synthetic cases. Unity's existing validity gate prevents those requests/playbacks in production. They are explicitly reported as raw advisory mismatches, not silent test passes or hardware safety regressions.

This small sample demonstrates lower request-path latency, not a statistically established reduction from the prior headset failure rate of 5.8%. A fresh headset run must confirm Android frame/network latency, cold/idle reconnections and perceived pacing.

## Verification

Spatial API contracts and voice-isolation checks pass: exact choice parsing, missing/out-of-set answers, no duplicate transmission request, wait silence, bounded deferral, cooldown, stale evidence discard and existing cancellation/voice isolation. Unity SpatialNavigationChecks passes, including offline joint eligibility. Android Build And Run passed in 112.8 seconds; installed/launched at 14:38:53 with local/device APK hash equality (`e689a973639cb7ebde190d2e9835ebe42bebc65b13b4e8741b2d6086ab0ac47f`). Initial process error log was empty. Fresh headset trial latency and listening acceptance remain unmeasured.

## Reproduce

```sh
bash scripts/test-spatial-voice.sh --benchmark-guidance Assets/StreamingAssets/api_keys.json /tmp/jev-paired.json
bash scripts/test-voice-isolation.sh
```

The benchmark reads credentials locally and writes only synthetic state outcomes and timings.
