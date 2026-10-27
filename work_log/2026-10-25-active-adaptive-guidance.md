# Active adaptive guidance — 2026-10-25

User scope: fully implement the new active policy, not a shadow rollout.

Implemented in `HintGenerator.Actions`, `AdaptiveGuidancePolicy`, `JevDecisionClient.Adaptive`, and `StudyNavigationVoiceBridge.Adaptive`. New participant sessions default to the active policy in both voice blocks. Jev controls observation and assistance strategy; local geometry, prepared audio identity, acquisition confirmations and playback relevance remain authoritative. No model training or Overleaf edits were performed.

## Review and corrections

The project AGENTS mapping requires subagent work to run sequentially in the main thread. I applied correctness, reliability, API-contract, standards, testing, maintainability and adversarial checks locally. No independent reviewer or cross-model review occurred.

Code review: skipped (ce-code-review unavailable) — its required independent finish contexts conflict with that project instruction; the attempted skill returned failed. A manual diff review and regression checks followed.

Corrections from that review:

- Explicit orientation below 45 degrees previously resolved to the same clip as brief orientation. It now uses the fuller prepared location statement; larger angles retain grounded larger-turn instructions.
- Bearing-return advice must use the verified crossing axis and check `StillBeyond`, even when the general direction is inside its aligned hysteresis. Added positive, wrong-side and visible-reference suppression regressions.
- Overlapping cue response windows and unfinished responses at trial end are unavailable, never presented as isolated observed improvements.
- Stable direction reversals fade obsolete search instructions without generating a replacement outside the model policy.
- Removed the unused legacy action bridge and its redundant progress tracking from HintGenerator. Historical action request helpers remain for the existing MiniLM tools and tests.

## Verification

- `bash scripts/test-spatial-voice.sh`: pass, including contract/parser, timing, history isolation, observed outcomes, transport cancellation and prepared-phrase grounding.
- `bash scripts/test-voice-isolation.sh`: pass, including adaptive playback onset, cancellation after queueing, default mode reset and voice isolation.
- `bash scripts/test-guidance-state.sh`: pass.
- `python3 -m unittest discover -s tools/trial-review -p 'test_*.py'`: 88 passed.
- Trial Viewer build and its Unity editor checks: pass. Output: `/Users/pedro.poveda/.codex/visualizations/2026/10/23/01a166b4-103c-79b3-af49-7009456006d4/Trial Review Adaptive.app`. Build log: `/private/var/folders/nx/rshb8w7x3fb5svqjk0xhchrm0000gn/T/trial-review-unity-p0gksuif/build.log`.
- Four live synthetic Jev requests: progress chose `observe_4s` twice; stalled-after-brief chose `orient_explicit` twice. Recorded latencies approximately 143–540 ms; sample too small for a performance claim. Evidence: `docs/testing/2026-10-25-adaptive-jev-live.json`.
- Android build and wireless install/launch succeeded. Final correction rebuild passed in 117.6 seconds; install returned `Success`, launch returned the Unity activity, and PID 12596 was observed on Wi-Fi device `192.168.1.208:5555`. Evidence is in `~/Library/Logs/Unity/Editor.log`. Final compile check and `git diff --check` passed. The new viewer was opened and its rendered window inspected.

No participant-worn end-to-end session was conducted. Pilot usefulness, frequency and study analysis implications remain validation work, not claims of this implementation. MiniLM's older classifier does not implement the new `assistance` contract.

Changes remain uncommitted in the existing working tree because participant-intake work is interleaved in shared files. No push or automatic publication was performed.
