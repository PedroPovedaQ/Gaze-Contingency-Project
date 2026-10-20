# Central guidance and evaluation readiness

**Implemented locally — October 20, 2026.** The guidance state machine owns qualification, phase transitions, action priority and short-term spatial history. The request scheduler owns pending provider scopes and the final relevance check before prepared audio starts. Jev remains the default decision provider behind an interface.

## Automated and desktop checks

- `scripts/test-guidance-state.sh`: phase transitions, action consumption at playback, blink/reacquisition, pause/resume and lifecycle revision checks.
- `scripts/test-voice-isolation.sh`: voice-scoped preparation and cache, bounded retries, stale decisions, pacing and cancellation.
- `scripts/test-gaze-quality.sh`: freshness, validity, vertical guidance, boundary/history behavior and recovery.
- `scripts/test-spatial-voice.sh`: provider contracts, injected provider, malformed/out-of-set responses, cancellation, audio boundaries and encouragement context.
- Python Trial Review and guidance analysis tests cover export integrity, legacy evidence, timing, blinded benchmark inputs, human-rating preservation, judge schemas, cache and explicit failure states.
- The isolated Mac build runs editor checks for recorded-state markers, backward seeks, stale samples, lifecycle states, exact recording hashes and judge case joins.

These checks establish software behavior. They do not establish sensor accuracy, perceived naturalness, acoustic timing or headset comfort.

### Completed verification

All four runtime suites passed, alongside 61 viewer/export Python tests and 42 guidance-analysis/judge tests. The final isolated Mac build passed its editor checks and reconstructed 147 sampled poses across seven saved trials. The complete Run 20 Astra evaluation returned 86 validated case results across 11 successful batches; these are advisory judgments, not an accuracy score.

The final viewer rendered the recorded run and evaluation sidebar, and keyboard playback/pause worked. Automated mouse actions did not reliably activate Unity IMGUI controls, so physical click-through remains a pilot check despite passing editor seek/join tests. The older recording has an incomplete trial summary; its full CSV export correctly rejects the missing scheduled row, while its independent guidance evaluation succeeds.

Android build-only passed. APK SHA-256: `bf726e6b8cc470c85e9d1767abda7fb31ce09d80b7a869b90a534e392a294491`. The headset was disconnected, so installation and new-build wearer verification remain outstanding.

## Headset pilot checklist

Use a fresh development run after installing this build. Keep the right controller available, and confirm both eyes pass the normal calibration/validity checks.

1. **Turn and arrive.** Test a target behind you in each direction. Listen for sensible progress cues and a smooth transition from horizontal turning to vertical/diagonal refinement.
2. **Look, leave, return.** Look at the correct target vicinity, look away, then return. Confirmation qualifies after 0.1 seconds; a valid 0.15-second departure rearms it. A blink must not act like a deliberate departure.
3. **Point and select.** Hold the controller on target with qualifying gaze for one second. Keep looking afterward: the reminder should match whether the controller is already aimed. Confirm with the right trigger.
4. **Overlapping objects.** Look nearer a distractor inside overlapping vicinity rings. The target must not receive a looking confirmation solely because its ring overlaps.
5. **Tracking loss and pause.** Briefly blink, then deliberately create a longer tracking interruption. New guidance should wait for valid evidence, recover without a burst of stale phrases, and preserve correct pause/resume behavior.
6. **Change direction during speech.** Enter or leave the target area while a normal hint is pending. Listen for obsolete directions, clipped phrases, rapid repeats and awkward interruption. Record the trial/time if anything feels wrong.
7. **Finish and review.** Select the correct object, finish the development run, then open it in Trial Review. Scrub forward and backward across each transition. Compare the recorded phase, direct gaze hit, vicinity winner and controller hit with the scene.
8. **Inspect evaluation.** Open Evaluation. Check automatic evidence separately from Astra judgments. Click a historical decision to seek to its request. Missing evidence should say unavailable/unknown; a failed model call must not appear as a passing judgment.

Test the neutral path first, then repeat a short set with a self-similar voice to verify voice isolation and the shared guidance behavior. Do not treat the older run's absent state snapshots as a new-build regression.

## Separate experiments

The small/medium/full Jev choice-set comparison is documented in `docs/plans/2026-10-20-jev-choice-set-comparison.md`; no new production candidate limit or prefetch policy is enabled. Human review and a held-out participant/session split remain necessary before model judgments are used for scientific claims or training.
