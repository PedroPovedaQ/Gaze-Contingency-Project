# Active adaptive assistance

**Implemented — updated 2026-10-26.** New sessions use `jev_adaptive_assistance_v2` in both voice conditions. This is the active search guidance policy. Jev chooses whether to speak and the assistance strategy; Unity resolves the selected strategy to a spatially valid, prepared clip. The development menu can select the legacy baseline between trials. A new participant resets the default to adaptive.

## Decision flow

`HintGenerator` collects recent behavior and asks `StudyNavigationVoiceBridge` for an `assistance` decision. `JevDecisionClient.Adaptive` sends a typed choice request through the existing Jev transport. The choices are:

| Choice | Result |
|---|---|
| `observe_1s`, `observe_2s`, `observe_4s` | Remain silent and reassess after that interval, or earlier when qualified evidence changes. |
| `orient_brief`, `orient_explicit` | Brief direction or fuller target-location/large-turn instruction outside the target area. |
| `refine_brief`, `refine_explicit` | Direction within the area, optionally anchored by an area confirmation. |
| `correct` | Verified return instruction after passing or leaving the target or area. |
| `encourage` | Reinforce currently measured progress. |

The model sees capability descriptions, not a ranked list of candidate phrases. Capabilities are limited by current geometry and the prepared audio library. Explicit orientation uses a larger turn instruction only when the remaining turn warrants it. Both voice conditions use the same decision logic.

History contains up to 12 seconds of half-second samples, direction changes, wrong selections, the last eight adaptive cue onsets and their observed responses, and six previous trial summaries. The error measure is the Euclidean norm of yaw and pitch errors, not a validated workload measure or a great-circle angle. A cue response compares that measure at software playback onset with a sample approximately three seconds later. Tracking gaps, pauses, trial termination, and overlapping interventions mark unobserved responses unavailable. These are associations, not causal effects or inferred mental states.

History is held in memory, scoped to the participant and run, and cleared for a new participant/run. It persists across trials and voice blocks in the same run, but not application restarts. Identity is used locally for isolation; demographics, interview recordings, names, and the local participant key are not included in the adaptive history sent to Jev.

## Runtime constraints

Unity continues to own spatial facts, target acquisition and selection confirmations, tracking validity, lifecycle transitions, audio readiness, and clip identity. It rechecks target, round, voice, participant, state revision and geometry before accepting a response and before playback. Stable direction reversal fades an obsolete search cue; it does not automatically speak a replacement.

Only one policy request is pending at a time. Evidence-triggered requests have a 0.75-second minimum separation. The request deadline is 1.5 seconds; responses older than two seconds are rejected. Failed or unavailable advice leaves search guidance silent and schedules a retry after two seconds, subject to evidence changes and audio readiness. An observation choice never enters the legacy one-second wait-to-speech fallback. Trial openings, explicit repeat requests, target confirmations, and success feedback retain their existing local responsibilities.

## Replay and verification

**Implemented — 2026-10-26:** target-selection reminders are shared by Adaptive and Baseline. After target acknowledgment and approximately three seconds of continued valid target gaze, the reminder says “Almost! Don't forget to press the right trigger to select the object.” when the right-controller ray also hits the target. Otherwise it asks the participant to aim with the right controller and press the trigger. It is limited to one reminder per objective, uses the active voice's prepared audio, and rechecks current gaze/controller evidence before playback. This is a local interaction reminder, not a Jev decision or an inference that the participant forgot.

Trial Viewer’s Jev tab shows the frozen behavioral state, offered strategies, selected strategy, returned probabilities, application result, and actual software playback onset. Observation decisions have no playback onset. Probabilities are model scores over the offered alternatives, not calibrated success probabilities.

Run:

```sh
bash scripts/test-spatial-voice.sh
bash scripts/test-voice-isolation.sh
bash scripts/test-guidance-state.sh
python3 -m unittest discover -s tools/trial-review -p 'test_*.py'
```

The live synthetic check in `docs/testing/2026-10-25-adaptive-jev-live.json` used the production C# client with matched final geometry and different histories. It demonstrates different decisions, not participant benefit or headset latency. The preserved v4 MiniLM classifier supports only `navigation_action`. **Implemented — October 26:** a separate fine-tuned local checkpoint now supports the same nine-choice v2 `assistance` contract for comparison. It can now control development quick-test guidance through the Model toggle described below. See [local model comparison](../testing/2026-10-26-local-adaptive.md) for synthetic training provenance, replay checks, probabilities and timing limits.

**Open decision / validation:** evaluate assistance usefulness and frequency in pilot sessions. Freeze and record the policy version for analyzed study data; do not pool legacy-policy recordings with adaptive runs without accounting for the change. Implementation does not establish approval of a revised research protocol.

## Neutral pilot feedback: assistance v2

**Implemented — 2026-10-26:** v2 asks Jev to prioritize the latest 2–4 seconds of observed behavior over the net gain across the entire 12-second history. Stalled, oscillating or receding searches call for orient/refine help; verified target departure after confirmation calls for prompt correction. A historical `targetAcknowledged` flag is explicitly distinguished from current target gaze. Four-second observation is reserved for sustained current progress. Unity retains the same validity, grounding, lifecycle and playback guards; no forced-speech timer or baseline fallback was added.

Frozen development requests from neutral runs 33/34 reproduce the excessive-silence complaint. Twelve live desktop requests (six cases twice) matched the pilot expectations 6/12 under v1 and 12/12 under v2; the target-departure misses occurred in the headset record but did not recur in the v1 desktop replay. These are regression probes selected from reported failures, not a held-out effectiveness evaluation. See `docs/testing/2026-10-26-adaptive-silence-replay.json` and `scripts/tests/adaptive-device-replay.json`.

Replay using the existing production C# client (requires local credentials; never print them):

```sh
bash scripts/test-spatial-voice.sh --replay-adaptive Assets/StreamingAssets/api_keys.json scripts/tests/adaptive-device-replay.json /tmp/adaptive-replay-new-output.json
```

The output must not already exist. Replay saves results even on a mismatch and exits nonzero. Per-decision traces carry the policy version; summaries additionally record `assistance_policy`, while preserving the existing `guidance_policy` directional-geometry identifier. Do not pool v1 and v2 recordings as one unchanged intervention.


**Implemented — active provider comparison:** development quick-test Settings can now switch adaptive assistance between Jev and the local adaptive-v2 MiniLM. Normal study sessions remain Jev. See [headset model comparison](../testing/2026-10-26-headset-model-toggle.md) for connection, telemetry, shared components and verification limits.
