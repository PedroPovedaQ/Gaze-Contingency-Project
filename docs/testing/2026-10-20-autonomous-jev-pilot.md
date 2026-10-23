# Autonomous Jev action pilot — October 20, 2026

**Implemented: offline experiment only.** Checkpoint `26c8b96` preserves the tested headset build. No action-choice policy or unprepared pilot speech has been enabled on the headset. No new build or device installation is needed for this Python-only experiment.

## What was compared

- Baseline: the current idle **speak/wait question**, with a fixed locally proposed phrase.
- Experimental: a single typed **action choice** among eligible `orient`, `encourage`, `correct`, `refine`, and `wait`. Selecting an action resolves to an explicit phrase in the pilot menu. This is broader than wording selection, but never bypasses geometric/validity gating.
- Same sanitized state and history for each paired request, alternating arm order and reversing it on repetition. Two repeats. Output selection is not fed back into recorded user behavior or subsequent history.
- The experimental Python eligibility adapter is **not production Unity's state machine**. It shows a policy worth testing; it does not establish runtime integration correctness. The baseline uses the production transmit wording with the same added context, not an exact replay of the entire Unity scheduler/network pipeline.
- Local checks retain control of gaze/controller confirmations, completion, invalid tracking, pause, and existing speech. Such cases are resolved locally and excluded from network latency. No audio is played.

## Pilot sizes and outcome

Initial: 14 scripted states plus 16 Run 25 request snapshots, two repetitions, **96 real network requests** and 24 local gate checks. Initial autonomous wording never selected encouragement and deferred a valid recorded return cue twice; it was not an improvement.

Revised: added explicit instructions to prefer one encouragement when progress is measured and to treat verified departure after acknowledgment as a new corrective event. Added five mirrored scenarios before running the revision. These are simple engineering generalization checks, not an independent human-validation set. **116 real network requests**, plus 24 local gate checks; 35 distinct states, each evaluated twice per arm.

| Revised paired batch | Current speak/wait prompt | Autonomous actions v2 |
|---|---:|---:|
| Network requests | 58 | 58 |
| Median HTTP decision time | 235 ms | 236 ms |
| p95 HTTP decision time | 366 ms | 672 ms |
| Maximum | 525 ms | 775 ms |
| Valid typed responses | 58/58 | 58/58 |
| Responses over the runtime 450 ms budget | 2/58 | 3/58 |
| Network/model decisions matching the broad scripted rubric | 28/28 | 28/28 |
| Local gate checks (including the recorded controller-hit case) | 12/12 | 12/12 |
| Chose encouragement in the ten measured-progress opportunities | 0/10 | 10/10 |

The baseline cannot choose another action; 0/10 is a capability difference, not ten baseline errors. Both orient/refine and encourage were accepted by the broad scripted rubric. Valid responses arriving after450ms would not be usable within the present runtime deadline. The local gate checks must not be presented as Jev accuracy.

Measurements are **Mac-to-Vercel HTTP decision times**, using Python urllib, not the Unity pooled HttpClient, headset timing, software playback, or acoustic latency. This small interleaved sample does not establish a stable latency or failure rate. No statistical superiority claim is made.

## Representative differences

- At 90° remaining during a scripted right turn with measured progress, baseline proposes “Turn way right”; revised Jev chooses **“Keep going right.”** The mirrored left-turn case also selects encouragement.
- During measured downward refinement, baseline proposes “A little lower”; revised Jev chooses **“Keep going down.”** The upward mirror behaves similarly.
- Run 25 request3235 (~58.724s): controller is already on target, so **both policies stay silent locally**, regardless of the proposed “Go back down.” No false qualified controller confirmation is generated.
- Request3266 (~59.211s): controller is no longer on target, target departure is verified, previous speech is “You're on it!”, and playback is idle. Initial autonomous policy waited2/2. Revised policy chooses **correct2/2**; paired baseline chooses correct1/2 and wait1/2. This is a request-level difference, not measured saved participant time.
- Requests3472–3632 are **one repeated-direction episode**, spanning about2.56 seconds of request observations. Both revised arms wait throughout, while recorded silence is still under3 seconds. Those seven requests are not seven missed confirmations or seven failures.
- Request397, immediately after the opening “The object is to the right”: revised Jev waits2/2 rather than immediately repeating “Turn way right.” Whether that is more natural requires a wearer trial.

## Recorded sequence probe (frozen history)

Times are original recording request times. Actions are new counterfactual responses, not speech that actually played. The first row is locally suppressed controller evidence; later rows do not inherit counterfactual speech.

| Repetition | Recording seconds | Case | Baseline | Revised |
|---|---:|---|---|---|
| 1 | 58.724 | recorded_3235 | wait | wait |
| 1 | 59.211 | recorded_3266 | correct | correct |
| 1 | 62.871 | recorded_3472 | wait | wait |
| 1 | 63.379 | recorded_3503 | wait | wait |
| 1 | 63.778 | recorded_3529 | wait | wait |
| 1 | 64.194 | recorded_3556 | wait | wait |
| 1 | 64.623 | recorded_3581 | wait | wait |
| 1 | 65.003 | recorded_3606 | wait | wait |
| 1 | 65.432 | recorded_3632 | wait | wait |
| 2 | 58.724 | recorded_3235 | wait | wait |
| 2 | 59.211 | recorded_3266 | wait | correct |
| 2 | 62.871 | recorded_3472 | wait | wait |
| 2 | 63.379 | recorded_3503 | wait | wait |
| 2 | 63.778 | recorded_3529 | wait | wait |
| 2 | 64.194 | recorded_3556 | wait | wait |
| 2 | 64.623 | recorded_3581 | wait | wait |
| 2 | 65.003 | recorded_3606 | wait | wait |
| 2 | 65.432 | recorded_3632 | wait | wait |

## Decision and remaining work

**Do not replace the participant policy yet.** The richer action vocabulary produces the requested progress encouragement and fixes an initial over-deferral in this pilot, but tail latency is worse in this small revised batch. It has not demonstrated better perceived guidance, reduced actual confirmation delays, or reduced end-to-end silence.

Next integration should be an explicit development-only action-provider flag: Unity exposes multiple valid actions, Jev selects one, and the existing scheduler revalidates the action and state revision at actual playback. Prepared and verified audio must exist for every enabled phrase. Retain the immediate local confirmation paths. Run a fresh paired headset pilot before adoption. This policy must remain equivalent across voice conditions.

No Astra or human rating was performed for these new action probes. Scripted expectations are an engineering rubric, not human-perceived correctness. The existing sequence evaluator can judge new headset recordings after the integration, including actual speech timing; it cannot manufacture playback evidence from this decision-only experiment.

## Artifacts and reproduction

- `analysis/guidance_action_pilot.py`: opt-in real endpoint runner; reads the existing `jev_key` without printing it. Refuses to reuse an output folder.
- `analysis/test_guidance_action_pilot.py`: 11 tests covering gates, schema validation, history isolation, mirrored eligibility, and payload immutability.
- `analysis/pilots/20261020-actions/comparison.json`: both batches, per-request outcomes, late-response flags and summaries.
- Full input states and reports remain in Documents / Trial Review Evaluations / action-pilot-20261020-v1 and action-pilot-20261020-v2. No participant identity or credentials are sent in the pilot payload.

Run tests: `python3 -m unittest discover -s analysis -p test_guidance_action_pilot.py`.
Network run: `python3 analysis/guidance_action_pilot.py --keys <api_keys.json> --benchmark <guidance-benchmark.jsonl> --output <new-folder> --policy v2 --holdout`.
