# Balanced target-wall angles

**Current decision / protocol proposal — September 28:** the researcher selected 40 measured trials per voice, eight at each of the five absolute-angle categories, and 20 positive/20 negative target-height deltas per block. See the [meeting record](meeting-reconciliation-2026-09-28.md) and #10. The vertical reference/deadband and cross-balancing details remain open. The ten-trial implementation described below has not yet been generalized; current target-identity uniqueness and two-group scheduling assumptions need changes before an 80-trial run.

**Implemented — beta, 2026-09-27:** both ten-trial voice blocks have the same absolute target-wall angle counts. Angles are measured from the readiness cross on the previous target wall to the next target wall center. For example, starting at wall 315° and targeting wall 0° gives signed theta +45° and absolute theta 45°. Wall azimuth remains relative to the seated fixed front.

| Absolute theta | Trials in each block |
| --- | --- |
| 0° | 2 |
| 45° | 2 |
| 90° | 2 |
| 135° | 2 |
| 180° | 2 |

The first block randomly assigns a left or right turn for each magnitude. The second block mirrors those assignments. Each block independently shuffles two five-trial halves. Each half contains exactly one 0°, 45°, 90°, 135° and 180° assignment; the copy assigned to each half is also randomized. This stratified shuffle reduces early small-angle clustering while preserving the two-per-category block quota. `angle_pair` links mirrored assignments, not matching object identities. The existing saved voice order determines which voice receives each block; angle scheduling does not override that assignment.

Every measured trial randomly selects a slot on its assigned wall and has a separate layout seed. All 168 objects retain random horizontal offsets and floor-relative heights with the existing spacing constraints. Target placement swaps object identities to put exactly one target into the planned slot without changing the distractor counts. Practice remains separate from the measured quotas.

Randomization is deterministic per coded participant ID and schedule version `previous-wall-angle-v4-stratified-10-trials`, so a restart reproduces that participant's plan. It is intentionally not a fresh unrecorded draw on every retry. Different participants receive different seeded plans. The current schedule is ten measured trials per block, twenty total, plus two separate practice trials. This supersedes the earlier seven-trial beta and proposed 30-trial block counts.

## Readiness cross

**Implemented — pilot:** before each search, the cross is fixed in world space at the center of the just-completed target wall, at the midpoint of the configured floor-relative height range. The first practice uses the front wall. The second practice starts from the first practice target; measured trial 1 starts from the second practice target. The block boundary continues from the previous measured target. Generation chains those exact starts, and a runtime mismatch stops the session rather than silently changing the angle allocation.

Gaze must remain within the full 0.4 m cross panel plus 0.15 m padding on each edge for one continuous second (configurable). The panel fills left to right. Looking away, unavailable/untracked eyes, a pause or a frame gap over 0.2 s resets charge. This is a readiness interaction threshold, not a validated fixation definition. Charge and target-announcement completion are both required; timing starts only when search objects become visible. There is no timed bypass, extra countdown or random blank delay after charging. Pausing during readiness resets charge and replays the target announcement upon resume.

**Open verification:** comfortable cross placement and eye calibration, gaze validity support and readiness usability on the Focus Vision. Search objects still require controller ray + trigger; gaze dwell only opens the between-trial readiness gate.

## Recorded evidence

- `trial_schedule.csv`: written before measured trials begin; includes participant ID, version/seed, zero-based trial and block indices, voice, angle pair, start wall/azimuth, target wall/slot, target wall azimuth, signed/absolute theta, layout seed, target identity, and whether the trial is enabled by any debug limit.
- `trial_summary.json`: includes schedule version/seed, whether rotational balancing applies, theta reference, and the planned angle/pair/layout seed for each trial, including incomplete outcomes.
- `object_manifest.csv`: retains actual world coordinates, wall/slot, seated origin, forward yaw and layout seed for every spawned object. Target height and exact target bearing can be derived from these coordinates.

**Interpretation:** these are wall-angle categories, not measured head rotation or the object's exact bearing. Random horizontal offsets mean exact object bearings vary within a wall. Each trial begins from the previous target wall. The world-fixed readiness cross establishes that direction before objects appear. Wall-center angles remain distinct from exact eye/head movement. Gross movement and head rotation remain separate measurements. Aborted runs and debug-truncated runs may have unequal completed counts; do not silently replace trials to repair them.

**Confirmed decision:** ten measured trials per block, two at each absolute-angle category. Exact-wall balancing instead of absolute-angle balancing would require a different allocation.

**Implemented telemetry:** readiness start/charge/reset/completion events go to `trial_events.csv` (measured trials) and replay (practice and measured); replay frame samples retain separate readiness progress, wall, world pose, valid-inside and announcement state. The legacy object `dwell` field stays zero. See [replay](trial-replay.md).

## Recording cue

**Implemented:** existing spoken recording instructions lead directly to the short start tone, after microphone permission/device checks. The visual 3–2–1 countdown and its three-second wait have been removed. Microphone capture still starts after the tone finishes.

## Verification

The regression first failed because the old schedule omitted an absolute-angle category. Updated tests cover one of every angle in each five-trial half, per-block quotas, actual target slots, mirrored pair assignments, full schedule export, participant restart reproducibility, changed seeds across participants, and unique per-trial layout seeds for 50 participant IDs. Existing distractor and randomized-height/spacing tests remain in use. Headset timing, listening, and initial-facing acceptance still require device testing.
