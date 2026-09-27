# Neutral voice selection

**Implemented:** setup asks for the participant's preferred agent-gender presentation, then offers two voices for that gender. This preference is not inferred from the participant's gender.

| Gender presentation | First voice (existing endpoint) | Second voice (new) |
| --- | --- | --- |
| Male | Eric | Roger |
| Female | Janet | Sarah |

Provider metadata was checked through the authenticated ElevenLabs voices API on 2026-09-27. The existing female request ID `21m00Tcm4TlvDq8ikWAM`, formerly labeled Rachel in our code, resolves to Janet (`eLDc7xhWxG2FElT3kUTj`). Its existing request ID/cache namespace is retained. Roger is `CwhRBWXzGAHq8TQ4Fs17`; Sarah is `EXAVITQu4vr4xnSDxMaL`; Eric remains `cjVigY5qzO86Huf0OWal`. These choices use American accents; this is not an accent-selection implementation.

## Interaction

1. Point at Male or Female and pull the trigger.
2. Point at one of the two named avatar cards and pull the trigger to hear it. Back returns to gender selection. Avatars are fictional illustrations, not portraits of the voice providers.
3. Use this voice is enabled only after successful sample playback. Hear the other voice or Back remain available; a failed sample cannot confirm enrollment. All setup actions use visible ray-selectable buttons with blue hover feedback and a brief pulse on the hovering controller.
4. Continue to self-similar enrollment, preparation and the existing two audio checks.

Public `SelectNeutralMale`, `SelectNeutralFemale`, `SelectNeutralVoiceOption(0 or 1)` and `ConfirmNeutralVoice` expose the same sequence for researcher/editor automation. XRI pointer clicks confirm visible controls; a press on the previous screen cannot click a newly created control. The optional Inspector default only preselects gender; it cannot skip named-voice confirmation.

**Implemented persistence:** `neutral-choice-v2.txt` stores gender and zero-based voice option under the coded participant. Legacy gender-only files map to the original voice. Explicitly confirming a new choice replaces the saved preference while preserving counterbalanced block order. Trial summaries and voice-readiness records include the voice name, request ID and option. Voice manifests retain the per-clip provider ID. Audio caches are keyed by provider voice ID; changing voice cancels prefetch and discards prepared clips before auditioning or preparing the replacement.

**Implemented audio:** self-similar speech retains first-person wording and uses the approved [soft-close v2 treatment](inner-thought-voice.md). Neutral voices remain dry. Level matching measures the speech window, excluding the self-similar room tail; manifests record the treatment and `speech_samples`. No new trials or voice-condition blocks are added.

**Open hardware validation:** select and audition all four options, go back/change gender, confirm a choice, complete preparation and verify that setup and the neutral block use that voice. Existing desktop checks cover routing/cache separation, saved-choice migration/replacement, exact option restoration, and the gender → voice → preview sequence.
