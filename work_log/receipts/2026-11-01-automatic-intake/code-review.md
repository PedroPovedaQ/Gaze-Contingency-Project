# Automatic intake review

Review scope: current intake changes against a6adf39, excluding inherited progress/readiness work. Applied ce-simplify-code and ce-code-review lenses sequentially in the main thread as required by the repository's Task mapping. No subagents, external peer or independent-review claim.

## Intent

Implement the exact ten fixed questions, automatic consent/prompt/tone-gated capture, local silence/no-speech/cap handling, continuous researcher controls, reviewed eligibility and unchanged clone thresholds. Preserve full transcript provenance, retry/correction/export, withdrawal and old protocol reads; prohibit Mistral follow-ups.

## Actionable Findings

Resolved before delivery:
- Withdrawal racing an ASR result must not reopen the interview. The answer merge preserves withdrawal; state patches preserve the withdrawn phase; consent is rechecked before uploads. Synthetic withdrawal-during-ASR and late phase-patch checks pass.
- Pause/Repeat during prompt loading or tone must invalidate pending capture. Epoch checks guard loaded audio and microphone onset; consent/pause/withdrawal coroutine guards run before requesting microphone authorization.
- Skip during an answer save must not skip the following question. Queued controls retain the original question index.
- New participant eligibility selections must start Unclear. Clear previous selections when reservation completes.
- Unsaved transcript edits must not be lost by saving eligibility or switching questions. Disable both until the correction is saved.
- Capture-receipt write errors must still stop the microphone. StopAnswer runs in finally; failure pauses and retains any successfully saved audio.
- Legacy sessions must not display the new ten-question capture controls. They retain read-only text/protocol.
- Silence processing allocated a PCM buffer every frame. Reuse the per-capture buffer (behavior-preserving simplification).
- Capture fixtures must match the real listening state and exact v3 wording. Updated labels, ten questions and controls; final screenshots are synthetic only.

## Coverage

Correctness, consent/privacy boundaries, background job and coroutine races, persisted schema/export compatibility, synthetic test sufficiency, repository standards and maintainability were reviewed in context. All ten supplied prompts were compared with the protocol constant and guide. Headset validator accepts explicit v2/v3 versions and rejects unknown protocols. No automatic eligibility pass; fallback and 30 s / 50 word gate retained. No Mistral URL/request remains in intake; direct followup calls return empty without loading keys.

Tests cover ASR failure and retry, machine-text preservation after correction, P###/session export linkage and changed export identity, withdrawal export refusal, cached prompt replay, no-speech attempt retention, legacy read-only behavior, 3/5 s silence, a 4 s open-answer pause, noise/DC/impulse rejection, 10 s no speech, one repeat, 120 s cap and multichannel input. UI capture bypasses microphone, providers, participant folders and service initialization.

## Verdict

Ready for local delivery. Final Python, Unity, installed-desktop capture and APK deployment receipts passed. No unresolved implementation finding. Hardware microphone behavior, quiet speech/background noise, real provider latency, voice similarity and wearer comfort are not established by synthetic checks. Name flags are conservative heuristics and require researcher review. Historical participant data and thesis files were not used or edited.
