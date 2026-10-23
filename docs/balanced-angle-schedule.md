# Balanced target-wall angles

**Implemented locally — September 30, 2026; headset verification pending:** 40 measured trials per voice block, 80 total, plus two separate practice trials. This replaces the 10+10 beta. Schedule version: `previous-wall-angle-v5-stratified-40-trials`.

| Absolute theta | Trials in each block |
| --- | --- |
| 0° | 8 |
| 45° | 8 |
| 90° | 8 |
| 135° | 8 |
| 180° | 8 |

Angles run from the readiness cross on the previous target wall to the new target wall center. Starting at 315° and targeting 0° gives signed +45° and absolute 45°. Fixed-front wall azimuth is recorded separately. Zero has no horizontal direction; 180° has equivalent shortest directions and is stored as +180°.

Each block contains eight five-trial groups, each with one of every absolute angle. The generator independently shuffles the eight copies of each magnitude across groups, then shuffles each group. The first block randomly chooses left/right; the second mirrors each `angle_pair` and independently orders the pairs. This is constrained randomization, not an unrestricted shuffle. A five-trial group can still end with a small angle followed by another small angle in the next group.

There are only 24 shape/color identities. Each block uses a shuffled bag containing all 24, followed by a shuffled bag of the same participant-seeded 16 identities. Thus sixteen identities occur twice and eight once in **each** voice block. Bags are independently ordered and boundary swaps prevent immediate repeats, including across the voice break. `target_identity_policy` records `matched-counts-shuffled-24-plus-16-v1`. Identity counts match between voices; exact target identities and object coordinates are not matched by angle pair. This policy should be reviewed for learning effects during pilot analysis.

Every trial has 168 objects, including exactly one target and 167 distractors; repeated identities refer to different trials. A separate layout seed scatters within-wall positions and floor-relative heights with existing spacing constraints. The schedule is deterministic per participant ID and version. Debug truncation preserves the complete 80-row manifest and flags only the enabled prefix; aborted trials are not replaced to repair completed quotas.

**Open decision / not implemented:** 20 positive and 20 negative target-height deltas per block, proposed four of each sign per angle. Issue #7 still needs the vertical reference, zero/deadband and feasible magnitude range. `height_assignment_policy` explicitly records `unbalanced-floor-scatter-pending-reference-v1` in the schedule, summary and replay; current random heights must not be described as balanced. Full 40+40 power/sensitivity and burden justification remain in #16.

## Readiness cross

**Implemented — pilot:** before each search, the cross is fixed in world space at the center of the just-completed target wall, at the midpoint of the configured floor-relative height range. The first practice uses the front wall. The second practice starts from the first practice target; measured trial 1 starts from the second practice target. The block boundary continues from the previous measured target. Generation chains those exact starts, and a runtime mismatch stops the session rather than silently changing the angle allocation.

Gaze must remain within the full 0.4 m cross panel plus 0.15 m padding on each edge for one continuous second (configurable). The panel fills left to right. Looking away, unavailable/untracked eyes, a pause or a frame gap over 0.2 s resets charge. This is a readiness interaction threshold, not a validated fixation definition. Charge and trial-introduction completion are both required; timing starts only when search objects become visible. There is no timed bypass, extra countdown or random blank delay after charging. Pausing during readiness resets charge and replays the generic trial introduction upon resume.

**Open verification:** comfortable cross placement and eye calibration, gaze validity support and readiness usability on the Focus Vision. Search objects still require controller ray + trigger; gaze dwell only opens the between-trial readiness gate.

## Recorded evidence

- `trial_schedule.csv`: written before measured trials begin; includes participant ID, version/seed, zero-based trial and block indices, voice, angle pair, start wall/azimuth, target wall/slot, target wall azimuth, signed/absolute theta, layout seed, target identity, and whether the trial is enabled by any debug limit.
- `trial_summary.json`: includes schedule version/seed, whether rotational balancing applies, theta reference, and the planned angle/pair/layout seed for each trial, including incomplete outcomes.
- `object_manifest.csv`: retains actual world coordinates, wall/slot, seated origin, forward yaw and layout seed for every spawned object. Target height and exact target bearing can be derived from these coordinates.

**Interpretation:** these are wall-angle categories, not measured head rotation or the object's exact bearing. Random horizontal offsets mean exact object bearings vary within a wall. Each trial begins from the previous target wall. The world-fixed readiness cross establishes that direction before objects appear. Wall-center angles remain distinct from exact eye/head movement. Gross movement and head rotation remain separate measurements. Aborted runs and debug-truncated runs may have unequal completed counts; do not silently replace trials to repair them.

**Confirmed decision:** forty measured trials per block, eight at each absolute-angle category. Exact-wall balancing instead of absolute-angle balancing would require a different allocation.

**Implemented telemetry:** readiness start/charge/reset/completion events go to `trial_events.csv` (measured trials) and replay (practice and measured); replay frame samples retain separate readiness progress, wall, world pose, valid-inside and announcement state. The legacy object `dwell` field stays zero. See [replay](trial-replay.md).

## Recording cue

**Implemented:** existing spoken recording instructions lead directly to the short start tone, after microphone permission/device checks. The visual 3–2–1 countdown and its three-second wait have been removed. Microphone capture still starts after the tone finishes.

## Verification

The regression first failed because the old schedule omitted an absolute-angle category. Updated tests cover one of every angle in each five-trial group, per-block quotas, actual target slots, mirrored pair assignments, full schedule export, participant restart reproducibility, changed seeds across participants, matched target-identity counts, no immediate target repeats, both voice orders, debug-truncated manifests, and unique per-trial layout seeds for 50 participant IDs. Existing distractor and randomized-height/spacing tests remain in use. Headset timing, listening, and initial-facing acceptance still require device testing.
