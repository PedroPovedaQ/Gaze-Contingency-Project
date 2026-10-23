# Jev choice-set comparison

Status: **Protocol proposal — approved for engineering evaluation on October 20, 2026.** This records the evaluation decision; it does not change participant-facing guidance or establish a winning configuration.

## Decision

Compare small, medium and full-catalog choice sets on identical recorded guidance snapshots. Keep deterministic measurement and hard validity checks in Unity. Investigate whether broader conversational choice improves guidance without unacceptable delay.

The full-catalog arm is a diagnostic comparison, not permission to play geometrically invalid suggestions. Every returned selection must pass the same final local playback checks.

## Paired comparison

- Small: the current production candidate-selection behavior, retaining its actual eligible count.
- Medium: up to 20 eligible, distinct phrase IDs, broadening conversational alternatives while preserving hard validity requirements.
- Full catalog: all real catalog entries supported by the provider, including their constraints and metadata. Report both catalog-row count and distinct spoken-text count. Do not pad the catalog to 500 or silently truncate a rejected request.
- Keep the recorded state, recent interaction/speech history, endpoint/model configuration and question schema comparable across arms. Log intentional differences.
- First verify provider request/choice limits and support for the intended response format. A rejected request is a reported outcome, not evidence about wording quality.
- Interleave arm order, repeat matched cases, and separate initial/cold requests from subsequent ones. Use the same timeout policy; report late responses separately from responses usable by production.

Include turning in both directions, the rear-angle seam, progress milestones, wall arrival, vertical/diagonal refinement, overshoot, gaze acquisition, controller aim, speech already playing, invalid gaze and tracking recovery. Include cases where the observed state changes while advice is pending.

## Evidence to record

For each case and arm, retain the snapshot, eligible/returned IDs, candidate and request sizes, response latency, timeout/provider/schema errors, and acceptance or rejection by the local validity checker. Retain the reason for any rejection. Do not record credentials.

Report latency distributions, failure and invalid-choice rates, repetition, and speak/wait choices by scenario. Have human reviewers assess directional correctness, appropriateness, naturalness and whether silence was preferable, without seeing the arm label where practical.

Replay evaluation cannot establish how a participant would react to different guidance. Validate the selected configuration in a new headset pilot before treating it as a study improvement. Keep guidance policies equivalent across voice conditions.

## Open decisions

- Provider limits and whether the full annotated catalog fits.
- A useful latency budget and minimum improvement in human-rated appropriateness.
- Whether any gains come from candidate count, added metadata, or broader action types; isolate these in follow-up comparisons rather than attributing them all to catalog size.
- Advance selection/prefetch is a separate proposal and is not enabled by this decision.

## Exit criteria

Choose a configuration only after publishing paired results and representative failures. Preserve the current production setting until evidence supports a change. Even a successful larger catalog does not remove local freshness, lifecycle, geometry, prepared-audio or final playback checks.
