# Balanced signed-step schedule

**Implemented — October 30, 2026; wearer acceptance pending.** Two counterbalanced voice blocks contain 32 measured trials each (64 total), plus the existing single practice using the first assigned voice. Study version: `readiness-target-angle-v8-signed-steps-32-one-practice`. Development quick tests use `readiness-target-angle-v8-signed-steps-32-quick-origin0` and enable one 32-trial block without practice.

| Signed theta | Trials per block | Above cross | Below cross |
| --- | ---: | ---: | ---: |
| 0° | 4 | 2 | 2 |
| +45° | 4 | 2 | 2 |
| −45° | 4 | 2 | 2 |
| +90° | 4 | 2 | 2 |
| −90° | 4 | 2 | 2 |
| +135° | 4 | 2 | 2 |
| −135° | 4 | 2 | 2 |
| 180° | 4 | 2 | 2 |
| Total | 32 | 16 | 16 |

Absolute-angle counts are **4/8/8/8/4** at 0/45/90/135/180°. Each of four independently shuffled eight-trial groups contains all signed steps. Copy order, height side and within-group order are seeded; blocks independently order their assignments. `angle_pair` is an allocation ID (0–31), not a promise of matched geometry or mirrored directions across voices.

Each block receives a shuffled bag of all 24 shape/color identities followed by a shuffled bag of the same participant-seeded subset of eight. Thus 16 identities occur once and eight twice in each block, with identical identity counts across voices. Boundary swaps prevent immediate repeats. Policies: `matched-counts-shuffled-24-plus-8-v1`, `two-above-two-below-per-signed-step-v1`, and `independent-blocks-four-per-signed-step-v1`.

Each trial retains 168 objects (21 per wall): one target, 39 same-color distractors, 39 same-shape distractors and 89 neutral distractors. Participant hashing, seeded pseudorandom draws, unique layout seeds, the practice seed and 25-second active-search deadline are retained. No Unity `Random.Range` drives round content. Version changes intentionally change the realized schedule for an ID; replay uses saved geometry and metadata rather than regenerating it with the current generator.

**Implemented geometry:** the first measured cross starts on the practice target wall; subsequent crosses use the previous target wall. At readiness completion, fresh eye-origin geometry pins the target to the assigned signed bearing from the cross. Above/below uses readiness-cross height, the midpoint of the configured vertical range. Heights are sampled 10–45% of that range away from the midpoint; the exact target-minus-cross height and assigned/realized pre-cue angles are recorded. These angles are not accumulated physical head rotation or validated eye fixations.

The break occurs after measured trial 32, before trial 33 (zero-based index 32). Practice remains uncounted. Debug truncation retains the complete 64-row manifest with only the enabled prefix flagged; incomplete trials are retained without replacement. Both voice orders use the same allocation contract.

**Implemented compatibility:** v7 recordings retain their 40/80 contract, five-trial groups, 8/8/8/8/8 absolute quotas and old policy strings. v5 retains its historical mirrored-pair contract. The interrupted 24/48-per-block work was never released; its draft v8 identifiers are not accepted as the final v8 contract. Trial Review and exports preserve recorded version, trial/block metadata and geometry.

**Protocol proposal — duration planning, not wearer measurements:** 64 × 25 seconds is at most 26.7 minutes of active search if every trial times out. Allowing an assumed 5–10 seconds per trial for readiness, opening speech and feedback adds 5.3–10.7 minutes; a 3–5 minute break and roughly 1 minute practice give about **36–44 minutes for the task portion** at the timeout ceiling. Faster searches shorten this. Intake, fitting/calibration, final questionnaires, extra rests and technical pauses are additional and unbounded by the search deadline; full appointment length needs a pilot.

**Open decision:** wearer comfort, spatial/height feasibility, task burden and statistical power under 32 trials per voice require fresh evaluation. Software allocation checks do not establish headset accuracy, behavioral validity or study approval. Historical 40-trial sensitivity results remain historical and must not justify v8 enrollment.
