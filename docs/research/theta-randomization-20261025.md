# Angle-controlled randomized trials — October 25, 2026

**Implemented — October 30, 2026:** the current schedule is 64 measured trials (32 per voice), plus one practice: four per signed step, absolute-angle counts 4/8/8/8/4, and 16 above/16 below with two of each per signed step. All 24 identities occur per block; eight repeat. See [current schedule](../balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v8.

**Implemented locally; headset validation pending.** User requested random positions and target choices conditioned on matched theta, rather than mirrored trials, and approved implementation and tracking. Work is tracked in [GitHub issue #10](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/10) on [Gaze Study — Participant Readiness](https://github.com/users/PedroPovedaQ/projects/2). GitHub Projects is the task system for this repository.

## Current behavior

- Schedule v7 retains one separate practice and two 40-trial blocks. Each block has eight assignments each at 0, 45, 90, 135 and 180 degrees; each five-trial group contains one of each magnitude.
- Directions are shuffled independently per block: four left and four right at each of 45/90/135 degrees. Zero has no direction; 180 is one destination. Allocation IDs remain in `angle_pair` for compatibility and no longer promise mirrored pairs.
- Target shape/color counts remain matched across blocks, with independent order and no immediate identity repetition. Each trial has a unique layout seed.
- Heights are four above/four below the readiness cross per angle (20/20 per block). The cross is at the midpoint of the configured floor-relative range. Assigned target centers are 10–45% of that range above/below the midpoint. These bounds are an implementation choice for testing, not a validated ergonomic range. Pyramid pivots are corrected as in the existing renderer; actual pivot delta is recorded.
- The cross stays on the previous target wall, with seeded lateral jitter within 20% of usable half-width after the first measured cross; the first stays centered. The target wall/slot is scheduled in advance.
- At completion of valid readiness gaze, before opening direction, the target bearing is constructed from the observed eye origin to the readiness cross, rotated by assigned signed theta. The ray intersects the planned wall. The target is reserved first; the other objects are deterministically scattered around it with the existing 20 cm spacing. All objects are hidden during this adjustment and remain fixed throughout search.
- If the required intersection lies outside the wall, the session technically stops and asks for recentering before restart. It never substitutes an easier angle or replaces the failed trial. Test the practical tolerance of this behavior.

## Evidence and interpretation

`trial_schedule.csv` adds direction policy, target-height fraction and cross-offset fraction. `trial_events.csv` records `theta_snapshot` at each readiness completion; `trial_summary.json` retains the latest `pre_cue_theta` object per trial (null when unavailable). It contains eye origin, cross and target positions, receipt time, application time, gaze sequence, assigned/realized signed angle, absolute angle, circular assignment error and actual target-height delta. `object_manifest.csv` is now written at search onset, using finalized positions. Replay retains initial hidden geometry plus transform changes on `readiness_completed` and subsequent samples.

Exact theta is defined at the saved pre-cue instant. It is not total head/eye rotation, the gaze direction within the padded cross, or post-cue/post-reveal bearing. Movement during the opening cue and search remains an outcome. A seed alone no longer reconstructs final positions: use saved eye/cross geometry and object manifest/replay. Matching angular and height quotas does not guarantee identical local clutter, vertical difficulty or participant behavior.

## Test checklist and remaining work

- [ ] Wearer: run neutral quick test, inspect cross jitter and target visibility; all geometry must finish while hidden.
- [ ] Wearer: verify 0/45/90/135/180 transitions, wraparound, left/right, practice boundary, timeout and block break.
- [ ] Wearer: pause during readiness/opening; resumed readiness should log a fresh snapshot. No cue should reference stale target geometry.
- [ ] Wearer: lean/translate near wall feasibility limits; confirm clear technical-stop behavior and preserved partial logs.
- [ ] Export/replay: compare final target position with `pre_cue_theta`, verify circular error below 0.01 degree, and inspect replay at readiness/reveal boundary.
- [ ] Full two-block run: audit assigned and realized height/direction quotas, both voice orders and aborted sessions. Completed-only quotas need not remain balanced.
- [x] Extend the session validator and flat trial-CSV schema for v7 snapshots; exact evidence is now available in CSV, JSON, events, manifest and replay. Historical nominal theta fields remain historical.
- [ ] Reconcile thesis methodology after verification. The synchronized Overleaf manuscript still describes exact snapshots and signed-height quotas as pending in the earlier aligned release. Do not silently relabel old recordings.

The 25-second search deadline, calibration work and native gaze diagnostics predate this change. No participant study or physical 120 Hz sensor-rate validation is implied by software tests.

## Checkpoint verification

Schedule/voice checks across 50 participant IDs and both voice orders passed. Geometry checks cover every starting wall, all signed angle categories, translated eye origins, wraparound, infeasible/invalid inputs and all-pair spacing around the reserved target. Timeout and six native adapter suites passed. Unity compilation and readiness integration checks passed; the latter caught and fixed the initial cross needing to remain centered. Headset build/install evidence is recorded separately in the session handoff.

## October 25 export and viewer completion

**Implemented:** exact snapshot fields in full/per-trial CSVs, concise snapshot metrics in condensed CSV, strict malformed-value handling, v7 full/short/quick schedule validation, valid terminal-timeout checks and reset-aware pre-opening snapshot order. Finalized target positions and jittered cross geometry propagate to analysis and replay. Trial Review displays all snapshot values and raw JSON, supports exact cursor/rewind boundaries, and preserves snapshot history in selected-trial exports.

**Software verification:** Python exporter/validator regressions cover missing legacy data, exact zeros, corrupt geometry, both voice orders, quick modes, shortened runs, timeouts and readiness restart. Desktop checks exercise snapshot parsing, invalid nested values, overflow, object transforms, backwards seeks and exact export. Independent subagent reviews covered validation, viewer/export and runtime geometry; reported defects were fixed. Existing schedule and rotational geometry suites pass.

**Open wearer acceptance:** both full voice orders/blocks, neutral/clone quick modes, comfortable seated placement, pause during opening and subsequent retry, deliberate leaning/infeasible intersection, and matching saved viewer/CSV evidence. Software readiness does not replace these human/device observations or research authorization. No study outcome was invented.
