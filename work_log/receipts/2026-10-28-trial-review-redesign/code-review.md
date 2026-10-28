# Trial Viewer Phase 2 review

Scope: `650c202` to the final working tree in `tools/trial-review/`, including the uncommitted Step 8. Explicit intent: implement DESIGN-PHASE2 Steps 0-8 while preserving replay, intake gates, data/provenance and live behavior.

## Coverage

The ce-code-review workflow ran sequentially in the main session as required by the project AGENTS tool mapping. No subagents or independent cross-model review ran; these are attributed same-session checks, not independent corroboration. Checked correctness, capture isolation/security, worker lifecycle, test fidelity, maintainability, project constraints and the agent-facing capture command. No external review submission or publishing.

Inspected the full desktop diff and its touched methods. All original Python backend/receiver code and replay renderer/timeline/audio implementations are unchanged. `invariant-methods.json` records the method-body comparison for intake workers, automatic clone/cache polling, live initialization/polling, keyboard and drag handlers. UI gate expressions and extracted action bodies were compared manually. New capture guards return before filesystem/process/microphone work; GUI input is suppressed in capture, evaluation is disabled, intake/catalog fixtures have no persistent path, audio output is silent.

The capture harness invokes the real keyboard and timeline handlers with synthetic Unity GUI events, and editor checks exercise capture job/poll guards and phase mapping. These do not establish physical mouse interaction, real audio/clone quality or headset operation, which were intentionally not exercised. The screenshot set covers both resolutions, dark/light, missing pose, empty/loading, all inspector tabs, state variants and all intake phases.

## Findings and resolutions

- P2: Layers was clipped when Wi-Fi settings reduced viewport height (`TrialReviewInterface.cs`, DrawLayers). Added an inner scroll view, and a new synthetic settings-open/layers-scrolled capture. No live connection is made.
- P2: The new settings-open capture can reach audio-status polling from OnGUI (`TrialReviewNeutralAudio.cs`, NeutralAudioControls). Added a capture guard and an editor assertion exercising that entry point before any polling/UI.
- Minor selected-card active text inherited black-on-dark styling. Set all selected text states to the theme body color.
- Removed obsolete UI fields and simplified the phase mapping without changing its meaning.

## Requirements completeness

Steps 0-8 are implemented; before and per-step after captures, isolated new app, local commits, preservation backup and final installation are recorded in the work log. Existing limitation/provenance text remains in its relevant cards, captions or tooltips. No new research or participant-data behavior was introduced.

## Verdict

Ready for the requested local desktop installation after the final build/capture checks. No unresolved code findings. Withdrawal confirmation suggested on the board was not added because the binding task prohibits new gates; retained the existing action and improved separation/styling. Pedro may decide on a separate behavior change later.

## Actionable Findings

None remaining. The two concrete review findings above are fixed and verified by the final build and capture checks.
