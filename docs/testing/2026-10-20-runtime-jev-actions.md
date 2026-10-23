# Development-only Jev action policy — 2026-10-20

**Implemented:** quick-test-only action selection; default remains `baseline_local_priority_v1`.
**Protocol proposal:** paired wearer comparison before changing participant guidance.
**Open decision:** whether extra Jev responsibility improves experienced guidance.

## Controls and ownership

In a neutral or cloned-voice quick test, open wrist settings (hold right A and double-click the right stick). Select **DEV guidance: baseline** to switch to **DEV guidance: Jev-led**, then close settings. Settings pause the search. The toggle is rejected during an unpaused search and resets for a new participant/app launch. It is unavailable in participant mode. Use one policy per run for a clean comparison.

`jev_actions_overrides_v1` offers prepared, locally eligible **orient**, **refine**, **correct**, **encourage**, plus **wait**. Typical menus have one or two phrases plus wait, not the full catalog. Encouragement requires recent measured motion in an already spoken direction; a reversal, tracking loss or stale progress withdraws it. Corrections require recorded departure/overshoot evidence. Blocked repeats are pruned before requesting the model.

Unity retains qualification, local gaze/controller confirmations, selection reminders, arrival/milestone cues, completion, interruption safeguards, cancellation, and final playback validation. Gaze confirmation still means looking; controller confirmation means pointing; completion requires correct trigger selection. Busy-audio correction/interruption handling retains its existing path. This is Jev-led ordinary action choice with local overrides, not an all-Jev controller.

A request is cancelled or discarded when its target, phase/revision, direction, voice identity, policy, validity, controller evidence or eligible phrase changes. A fresh prepared local action is the fallback on provider failure; the existing maximum-deferral and repeat rules still apply. One action request returns the action and speak/wait together. Its selected text is not sent through a second transmission request.

The state snapshot and request context record `guidancePolicy` / `guidance_policy`. Trial Review's **At cursor** panel shows Policy; its Jev panel displays `navigation_action`. `development-action-result-v1` records scheduling acceptance separately from actual `audio_playback_start`. The evaluator resolves action IDs to their offered text without inventing playback.

## Actual API probe

Production C# adapter, Mac HTTP, synthetic states, prepared-phrase metadata and the production bounded `SpeechDecisionMemory` serializer. Ten cases, two repetitions, arm order reversed on repetition: **40 requests**. This measures API completion, not Unity frame scheduling, headset response, audible latency or participant performance. p95 uses nearest rank; sample is too small for a reliable general failure-rate or tail-latency estimate.

| Policy | Valid responses | Median | p95 | Maximum |
|---|---:|---:|---:|---:|
| baseline | 20/20 | 178 ms | 379 ms | 402 ms |
| actions | 20/20 | 179 ms | 245 ms | 273 ms |

Observed action choices: 10/10 progress cases chose encourage; 4/4 verified return cases chose correct; 4/4 stationary cases chose orient/refine; 2/2 recent-repeat cases chose wait. Baseline speak/wait remained limited to the offered explicit cue and waited in all four return probes. These are scripted engineering expectations, not human-rated correctness. Current baseline runtime already has local priority overrides for returns; this API-only contrast must not be presented as a baseline wearer defect.

The raw recent-repeat adapter probe deliberately bypasses runtime pruning to test the model contract. Runtime now avoids that request when all choices are repeat-blocked.

Evidence: [final probe](../../analysis/pilots/20261020-runtime-actions/comparison-v3.json). Earlier `comparison.json` exposed an over-conservative contract (16/16 waits); `comparison-v2.json` tested the revision but used simplified history. Neither is hidden or pooled into the final figures. The final probe added two new states and the real history serializer, but most fixtures informed prompt tuning: this is not an independent efficacy evaluation.

## Verification and wearer comparison

Automated contracts cover eligible menus, participant isolation/reset, prepared audio, typed selection, out-of-set rejection, cancellation, same-direction progress, reversal, tracking loss/recovery and stale progress. Existing central-state sequence tests cover target holds, reacquisition, paused/lifecycle requests and controller priority. Evaluator regression verifies an action selection without audio remains `not_observed`.

For headset A/B, run baseline then Jev-led on the same quick-test schedule, then reverse order in a second pair. Keep voice, gaze calibration and audio level fixed. Include a long turn, stop midway, reverse, enter the wall, refine vertically, look at the target while pointing elsewhere, then point and select. Note cue helpfulness and repetition. Review recorded software playback versus first qualification, discarded stale answers, calls/trial and failures. Wearer behavior is not held constant by replay; a subjective benefit remains unproven.

Do not change the participant default based only on the API probe.

## Code review scope

Sequential source review covered the new action path, cancellation, identity/revision checks, participant isolation, missing-clip fallback, repeat pruning and telemetry. It found and addressed dispatch-time magnitude reuse, repeat-blocked model requests and empty-menu starvation. No independent review is claimed. Pure contract and sequence tests do not substitute for a sensor-to-speaker wearer test.

## Build and installation verification

Android build succeeded; installed and launched over Wi-Fi ADB on the Focus Vision. The installed base APK SHA-256 matches `Builds/CodexAndroid/codex_build.apk`; startup error-log check returned no Unity/AndroidRuntime errors for the verified process. Evidence is in `analysis/pilots/20261020-runtime-actions/deployment.json`. This verifies packaging/launch, not wearer guidance quality.

Updated Trial Review built with its bundled checks passing and is open in live mode. Wi-Fi connection and complete file synchronization were visible; the current headset status is paused/waiting for wearer signal. No live experimental action has been asserted from this idle state.
