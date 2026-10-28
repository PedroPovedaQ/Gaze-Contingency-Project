# Active adaptive assistance

**Implemented — updated 2026-10-26.** New sessions use `jev_adaptive_assistance_v4` in both voice conditions. This is the active search guidance policy. Local MiniLM (default), or Jev when explicitly selected in headset Settings, makes two sequential decisions: an action, then an exact phrase from that action. Unity enumerates spatially valid prepared alternatives and executes the model-selected phrase. The development menu can select the legacy baseline between trials. A new participant resets the default to adaptive with MiniLM. Model switching is available while paused; baseline remains development-only.

## Decision flow

**Implemented — first-wall arrival override (2026-10-27):** in Adaptive mode, the first qualified arrival at the target wall takes priority over ordinary model action selection. Unity fixes the event/action (`override_reason=first_target_wall_entry`, phrase-stage action `refine_explicit`); the selected Jev or MiniLM provider chooses exactly one of four prepared area acknowledgments, with actual round phrase history. No action-model call or `observe_1s` choice is offered for this event. This override does not retrain either model. The local phrase endpoint permits this narrowly qualified phrase-selection request during current playback; ordinary requests remain blocked while busy.

The acknowledgment is consumed only at actual playback, survives pause/resume, and resets for a new participant/run/round. Target confirmation and controller-on-target guards take precedence; after the target has been acknowledged the arrival override is no longer offered. Leaving the wall, tracking loss, pause, target/voice/provider change or stale phase invalidates the pending result. Failed model requests retry while the event remains eligible; no local phrase is substituted. Baseline keeps its existing rule-based arrival path.

The selected arrival phrase uses the existing smooth interruption path. It can replace a replaceable search instruction that began outside the target wall after 0.65 seconds, only with at least 1 second remaining and at least 2.5 seconds since the last smooth interruption. A 150 ms fade stops old speech before the replacement is revalidated and played. Setup, success, target confirmations, short endings and within-wall speech remain protected. **There is no syllable/word alignment detector:** timing is based on playback position and authored semantics, and telemetry continues to say `word_boundary_known=false`.

`HintGenerator` collects recent behavior and asks `StudyNavigationVoiceBridge` for an `assistance` decision. `JevDecisionClient.Adaptive` sends the shared typed-choice contract to the selected provider: the pinned Mac MiniLM service over ADB loopback by default, or Jev through its cloud transport. MiniLM mode does not consult Jev for separate speech advice or round feedback. See [model setup](../testing/2026-10-26-headset-model-toggle.md). The choices are:

| Choice | Result |
|---|---|
| `observe_1s` | Remain silent and reassess after one second, or earlier when qualified evidence changes. |
| `orient_brief`, `orient_explicit` | Brief direction or fuller target-location/large-turn instruction outside the target area. |
| `refine_brief`, `refine_explicit` | Direction within the area, optionally anchored by an area confirmation. |
| `correct` | Verified return instruction after passing or leaving the target or area. |
| `encourage` | Reinforce currently measured progress. |

Stage one sees capability descriptions. A speaking action starts stage two, `assistance_phrase`, with only that action’s phrase alternatives; observation skips stage two. The same selected provider makes both decisions. The second request includes `selected_action`, `parent_action_decision_id`, and the immutable behavioral snapshot. Unity never selects the winning wording. Capabilities are limited by current geometry and the prepared audio library. Explicit orientation uses a larger turn instruction only when the remaining turn warrants it. Both voice conditions use the same decision logic.

History contains up to 12 seconds of half-second samples, direction changes, wrong selections, the last eight adaptive cue onsets and their observed responses, and six previous trial summaries. The error measure is the Euclidean norm of yaw and pitch errors, not a validated workload measure or a great-circle angle. A cue response compares that measure at software playback onset with a sample approximately three seconds later. Tracking gaps, pauses, trial termination, and overlapping interventions mark unobserved responses unavailable. These are associations, not causal effects or inferred mental states.

**Implemented — round phrase history:** `behavior.phrase_history` contains each canonical phrase actually started in the current round, its playback count, last-start age, rendered text, and context. This includes locally controlled and repeated audio, not just adaptive requests. Queued/cancelled-before-start phrases are excluded. History survives pause/resume and tracking gaps but clears on a new round or participant/run. An interrupted start still counts as a start, not proof of hearing the whole phrase. Jev is instructed to discourage exact repeats; the local phrase ranker receives repetition features. Repetition is a preference, not a hard veto when only one suitable clip exists.

Other behavioral history is held in memory, scoped to the participant and run, and cleared for a new participant/run. It persists across trials and voice blocks in the same run, but not application restarts. Identity is used locally for isolation; demographics, interview recordings, names, and the local participant key are not included in the adaptive history sent to Jev.

**Implemented — reassurance (2026-10-26):** steady or slow current progress can earn `encourage`, including after earlier reassurance. Candidate qualification uses a fresh two-second window with consistent direction, at least 0.5 seconds of samples, at least 1 degree net reduction in combined yaw/pitch error, a latest reduction above 0.1 degree, and no gap above 1.25 seconds. These are engineering thresholds, not validated eye-movement event thresholds. The provider policy asks for a brief observation when the last adaptive cue was encouragement and less than four seconds of silence has elapsed; continued progress can receive reassurance again afterward. This spacing is a learned/prompted preference, not a forced runtime cadence. Stalls/reversals need directional help; invalid tracking and qualified target holds retain their hard runtime gates.

## Runtime constraints

Unity continues to own spatial facts, target acquisition and selection confirmations, tracking validity, lifecycle transitions, audio readiness, and clip identity. It rechecks target, round, voice, participant, state revision and geometry before accepting a response and before playback. Stable direction reversal fades an obsolete search cue; it does not automatically speak a replacement.

Only one policy request is pending at a time. Evidence-triggered requests have a 0.75-second minimum separation. Each model call has a 1.5-second deadline, the combined pipeline has a three-second deadline, and its application freshness limit is 3.25 seconds. Relevance is rechecked between stages and again before playback. Failed or unavailable advice leaves search guidance silent and schedules a retry after two seconds, subject to evidence changes and audio readiness. An observation choice never enters the legacy one-second wait-to-speech fallback. Trial openings, explicit repeat requests, target confirmations, and success feedback retain their existing local responsibilities.

## Replay and verification

**Implemented — 2026-10-26 (corrected):** selection reminders are timeout feedback shared by Adaptive and Baseline. No three-second reminder is scheduled during active search. At the 25-second deadline, before state reset or target hiding, the app checks fresh valid gaze within the unique target vicinity, a qualified right-controller target hold, an acknowledged target, and a released trigger. Only that case receives “Time up. Almost! Don't forget to press the right trigger to select the object.” followed by the appropriate next-round, block-break or final-trial wording. Missing/stale evidence, looking away, another controller target or a held/unknown trigger produces ordinary timeout feedback. The trial remains a timeout; there is no extra selection time or success. The frozen evidence and timeout event record the classification. This is an interaction-evidence rule, not a claim about the participant's mental state.

Trial Viewer’s Jev tab shows both requests, action and phrase choices, probabilities and frozen round history. The phrase request explicitly links to its parent action; application and actual software playback onset are attached to that parent. Observation decisions have no playback onset. Probabilities are model scores over the offered alternatives, not calibrated success probabilities.

Run:

```sh
bash scripts/test-spatial-voice.sh
bash scripts/test-voice-isolation.sh
bash scripts/test-guidance-state.sh
python3 -m unittest discover -s tools/trial-review -p 'test_*.py'
```

The live synthetic check in `docs/testing/2026-10-25-adaptive-jev-live.json` used the production C# client with matched final geometry and different histories. It demonstrates different decisions, not participant benefit or headset latency. The preserved v4 MiniLM classifier supports only `navigation_action`. **Implemented — October 26:** the earlier adaptive-v2 checkpoint supported the nine-choice v2 contract. The current `20261026-minilm-reassurance-v8` uses seven actions and a separate learned MiniLM phrase ranker; its bootstrap training uses authored synthetic preferences, not measured participant benefit. It can now control development quick-test guidance through the Model toggle described below. See [local model comparison](../testing/2026-10-26-local-adaptive.md) for synthetic training provenance, replay checks, probabilities and timing limits.

**Open decision / validation:** evaluate assistance usefulness and frequency in pilot sessions. Freeze and record the policy version for analyzed study data; do not pool legacy-policy recordings with adaptive runs without accounting for the change. Implementation does not establish approval of a revised research protocol.

## Historical neutral pilot feedback: assistance v2

**Implemented — 2026-10-26:** v2 asks Jev to prioritize the latest 2–4 seconds of observed behavior over the net gain across the entire 12-second history. Stalled, oscillating or receding searches call for orient/refine help; verified target departure after confirmation calls for prompt correction. A historical `targetAcknowledged` flag is explicitly distinguished from current target gaze. Four-second observation is reserved for sustained current progress. Unity retains the same validity, grounding, lifecycle and playback guards; no forced-speech timer or baseline fallback was added.

Frozen development requests from neutral runs 33/34 reproduce the excessive-silence complaint. Twelve live desktop requests (six cases twice) matched the pilot expectations 6/12 under v1 and 12/12 under v2; the target-departure misses occurred in the headset record but did not recur in the v1 desktop replay. These are regression probes selected from reported failures, not a held-out effectiveness evaluation. See `docs/testing/2026-10-26-adaptive-silence-replay.json` and `scripts/tests/adaptive-device-replay.json`.

Replay using the existing production C# client (requires local credentials; never print them):

```sh
bash scripts/test-spatial-voice.sh --replay-adaptive Assets/StreamingAssets/api_keys.json scripts/tests/adaptive-device-replay.json /tmp/adaptive-replay-new-output.json
```

The output must not already exist. Replay saves results even on a mismatch and exits nonzero. Per-decision traces carry the policy version; summaries additionally record `assistance_policy`, while preserving the existing `guidance_policy` directional-geometry identifier. Do not pool v1 and v2 recordings as one unchanged intervention.


**Implemented — active provider comparison:** Settings can switch adaptive assistance between Jev and local MiniLM while paused. New sessions default to local MiniLM. See [headset model comparison](../testing/2026-10-26-headset-model-toggle.md) for connection, telemetry, shared components and verification limits.
