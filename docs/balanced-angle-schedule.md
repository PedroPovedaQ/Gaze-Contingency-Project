# Balanced signed-step schedule

**Implemented — November 1, 2026; wearer acceptance pending.** Two counterbalanced voice blocks contain 48 measured trials each (96 total), plus the existing single practice using the first assigned voice. Study version: `readiness-target-angle-v9-signed-steps-48-one-practice`. Development quick tests use `readiness-target-angle-v9-signed-steps-48-quick-origin0` and enable one 48-trial block without practice.

| Signed theta | Trials per block | Above cross | Below cross |
| --- | ---: | ---: | ---: |
| 0° | 6 | 3 | 3 |
| +45° | 6 | 3 | 3 |
| −45° | 6 | 3 | 3 |
| +90° | 6 | 3 | 3 |
| −90° | 6 | 3 | 3 |
| +135° | 6 | 3 | 3 |
| −135° | 6 | 3 | 3 |
| 180° | 6 | 3 | 3 |
| Total | 48 | 24 | 24 |

Absolute-angle counts are **6/12/12/12/6** at 0/45/90/135/180°. Each of six independently shuffled eight-trial groups contains all signed steps. Copy order, height side and within-group order are seeded; blocks independently order their assignments. `angle_pair` is an allocation ID (0–47), not a promise of matched geometry or mirrored directions across voices.

Each block receives two independently shuffled bags of all 24 shape/color identities. Every identity occurs exactly twice per voice block. Boundary swaps prevent immediate repeats, including at the voice change. Policies: `matched-counts-shuffled-24-twice-v1`, `three-above-three-below-per-signed-step-v1`, and `independent-blocks-six-per-signed-step-v1`.

Each trial retains 168 objects (21 per wall): one target, 39 same-color distractors, 39 same-shape distractors and 89 neutral distractors. Participant hashing, seeded pseudorandom draws, unique layout seeds, the practice seed and 25-second active-search deadline are retained. No Unity `Random.Range` drives round content. Version changes intentionally change the realized schedule for an ID; replay uses saved geometry and metadata rather than regenerating it with the current generator.

**Implemented geometry:** the first measured cross starts on the practice target wall; subsequent crosses use the previous target wall. At readiness completion, fresh eye-origin geometry pins the target to the assigned signed bearing from the cross. Above/below uses readiness-cross height, the midpoint of the configured vertical range. Heights are sampled 10–45% of that range away from the midpoint; the exact target-minus-cross height and assigned/realized pre-cue angles are recorded. These angles are not accumulated physical head rotation or validated eye fixations.

The break occurs after measured trial 48, before trial 49 (zero-based index 48). Practice remains uncounted. Debug truncation retains the complete 96-row manifest with only the enabled prefix flagged; incomplete trials are retained without replacement. Both voice orders use the same allocation contract.

**Implemented compatibility:** v8 recordings retain their 32/64 contract, four signed-step groups, 4/8/8/8/4 absolute quotas, 2/2 heights per signed step and 24 identities plus eight repeats. v7 recordings retain their 40/80 contract, five-trial groups, 8/8/8/8/8 absolute quotas and old policy strings. v5 retains its historical mirrored-pair contract. The interrupted 24/48-per-block work was never released; its draft v8 identifiers are not accepted as the final v8 contract. Trial Review and exports preserve recorded version, trial/block metadata and geometry.

**Protocol proposal — duration planning, not wearer measurements:** 96 × 25 seconds is at most 40 minutes of active search if every trial times out. An assumed 5–10 seconds per trial for readiness, opening speech and feedback adds 8–16 minutes; a 3–5 minute break and roughly 1 minute practice give about **52–62 minutes for the task portion** at the timeout ceiling. Faster searches shorten this. Intake, fitting/calibration, final questionnaires, extra rests and technical pauses are additional; full appointment length needs a pilot.

**Protocol proposal:** the 96-trial power simulation and its comparison with 64 trials are archived in `analysis/power/current-design-96-20261101/`. These are conditional estimates under synthetic assumptions, not measured voice effects.

**Open decision:** wearer comfort, spatial/height feasibility, task burden and the primary censored-time estimand require evaluation. Software allocation checks do not establish headset accuracy, behavioral validity or study approval. Historical power reports remain historical.
