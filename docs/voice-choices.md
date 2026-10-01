# Neutral voice selection

**Implemented locally — October 1, 2026; headset listening pending:** setup asks for preferred agent-gender presentation, then offers six voices for that gender. The two additions meet the requested 6–8 range. Gender is explicitly selected, never inferred from the participant.

| Gender presentation | Choices (stable saved order, indices 0–5) |
| --- | --- |
| Male | Eric, Roger, Chris, Will, Brian, Liam |
| Female | Janet, Sarah, Jessica, Matilda, Bella, Laura |

All five existing indices and request IDs are preserved; Liam (`TX3LPaxmHKxFdv7VOQHJ`) and Laura (`FGY2WhTYpPnrIDTdsKH5`) are appended. Read-only authenticated ElevenLabs catalog metadata confirmed the additions on October 1, with American-English provider labels matching the existing catalog. Their live speech has not been synthesized or auditioned in this overnight run. Provider labels do not establish participant-perceived accent or gender.

The original female request ID `21m00Tcm4TlvDq8ikWAM` retains its previously verified Janet alias; it is not silently remapped. IDs are defined in `SessionConfig`. Sources: [list voices](https://elevenlabs.io/docs/api-reference/voices/search), [instant voice cloning](https://elevenlabs.io/docs/api-reference/voices/ivc/create).

**Open protocol/workflow decisions:** accent/no-preference alternatives, per-candidate ratings and ranks, exact rating endpoints/order/tie handling, and final exposure controls remain in #33/#26. Expanding the catalog does not complete those criteria. No live questionnaire or IRB submission is changed.

## Interaction

1. Point at Male or Female and pull the trigger.
2. Point at any of the six cards to hear a sample. Pull the trigger to select it; a persistent gold border marks the pending selection. Every card has a fictional cartoon portrait. The original four portraits are retained; eight additional portraits were generated with the built-in image tool. They are not likenesses of voice actors. [Asset prompts and provenance](voice-avatar-prompts-2026-10-01.json).
3. Back becomes Confirm after selection. Confirm is enabled only after the selected voice has completed successful playback and audio is idle. Pointing at another card auditions it without changing the selection; leave and reenter to retry a failed sample. All cards remain visible in a three-by-two grid with controller hover feedback and haptics.
4. Continue to self-similar enrollment, preparation and the existing two audio checks.

Public `SelectNeutralMale`, `SelectNeutralFemale`, `SelectNeutralVoiceOption(0 through 5)` and `ConfirmNeutralVoice` expose the same sequence for researcher/editor automation. XRI pointer clicks confirm visible controls; a press on the previous screen cannot click a newly created control. The optional Inspector default only preselects gender; it cannot skip named-voice confirmation.

**Implemented persistence:** `neutral-choice-v2.txt` stores gender and zero-based voice option under the coded participant. Legacy gender-only files map to the original voice. Explicitly confirming a new choice replaces the saved preference while preserving counterbalanced block order. `neutral-catalog-v1.json` snapshots all available candidates and request IDs per participant and run, with `elevenlabs-six-per-gender-v1`. It is inventory, not evidence of audition or rating. Trial summaries and voice-readiness records include the voice name, request ID and option. Voice manifests retain the per-clip provider ID. Audio caches are keyed by provider voice ID; changing voice cancels prefetch and discards prepared clips before auditioning or preparing the replacement.

**Implemented audio:** both voices use the same participant-directed imperative wording; self-similar speech uses the approved [soft-close v2 treatment](inner-thought-voice.md). Neutral voices remain dry. Level matching measures the speech window, excluding the self-similar room tail; manifests record the treatment and `speech_samples`. No new trials or voice-condition blocks are added.

**Previous desktop/API evidence:** the original ten choices returned audio on September 28. Current expansion checks exercise all twelve cache/routing identities and persistence of both new sixth options. Six-card Unity integration checks and graphical renders passed for both gender presentations. The inspected renders show twelve portraits, legible labels and an exclusive selected border; the fixture deliberately has no provider audio and leaves Confirm disabled. Metadata lookup and menu renders are not audio tests. Clone-client tests cover verification and response failures without uploading a recording.

**Open hardware validation:** select and audition all twelve options, go back/change gender, confirm a choice, complete preparation and verify that setup and the neutral block use that voice. Existing desktop checks cover routing/cache separation, saved-choice migration/replacement, exact option restoration, and the gender → voice → preview sequence.

## ElevenLabs enrollment migration

**Implemented:** new microphone recordings are submitted as multipart WAV to `POST /v1/voices/add` using the local `elevenlabs_key`. Both catalog and cloned speech use `eleven_turbo_v2_5`. Clone IDs with `requires_verification` are rejected with an actionable message; missing IDs, access errors and non-audio speech responses cannot pass readiness. No neutral fallback is used on clone failure. Natural-voice recording instructions and clone-only audio treatment are retained.

**Implemented:** clone caches use `el-clone-<voiceId>` rather than the retired `vx-` namespace. Every new session records a new clone; old Voxtral IDs/audio are not reused or uploaded. Clip manifests now record `provider=elevenlabs`, `model`, and an explicit `voice_condition` (`neutral` or `selfsimilar`). Legacy manifests remain unchanged. The secret stays in ignored `Assets/StreamingAssets/api_keys.json`; the example contains placeholders only.

**Open validation:** live microphone cloning, verification/account quota handling, listening checks and headset menu legibility still need a device run. No historical participant recording was uploaded during this migration. The older `voxtral_smoke_test.py` and `e2e_clone_test.py` scripts are historical Mistral tools, not checks of the current runtime.

**Protocol proposal / open decision:** cloud-provider disclosure and retention/deletion details in the IRB packet and participant consent must match ElevenLabs before participant use. This implementation does not establish institutional approval or delete old provider clones.
