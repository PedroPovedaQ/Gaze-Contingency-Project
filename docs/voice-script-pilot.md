# Voice perspective pilot scripts

**Implemented (local issue #24):** two selectable script modes use identical wording across generic and self-similar voices. **Open decision:** select and freeze the final study perspective after listening QA and pilot assessment; neither has been completed.

The runtime wording API is `VoicePromptText.Format(text, perspective)` in `Assets/VoicePromptText.cs`, with `VoicePerspective.Collaborative` and `VoicePerspective.External`. The current wording set is identified as `VoicePromptText.Version` (`perspective-v1`). Both perspectives preserve the same task instruction, gaze selection rule, spatial hint, and completion action.

## Candidate scripts

| Moment | Collaborative candidate | External candidate |
| --- | --- | --- |
| Welcome | “Let’s begin the surrounding search. Let’s stay seated at the center. We’ll see objects on eight planes around us. Let’s turn to find the target by its color and shape. Let’s hold our gaze on the matching object to select it. Our first two rounds are practice.” | “Welcome to the surrounding search. Stay seated at the center. Objects will appear on eight planes around you. Turn to search for the target by its color and shape. Hold your gaze on the matching object to select it. Your first two rounds are practice.” |
| Practice | “Let’s try a practice round. This one does not count toward our study rounds. Let’s find the [color] [shape].” | “This is a practice round. It does not count toward the study. Locate the [color] [shape].” |
| Round instruction | “Let’s find the [color] [shape].” | “Locate the [color] [shape].” |
| Helpful hint | “We’re getting closer. Let’s keep searching this direction.” | “You’re getting closer. Keep searching this direction.” |
| Redirecting hint | “Let’s switch areas and try again.” | “Switch areas and try again.” |
| Success | “We did it!” | “Nice!” |
| Completion | “Excellent! We’ve found all the objects. Let’s complete the NASA T L X questionnaire now.” | “Excellent! You located all the objects. Please complete the NASA T L X questionnaire now.” |
| Final | “We’ve finished the experiment. Let’s remove the headset now and enjoy the rest of our day.” | “Thank you for your participation in this experiment, please remove the headset now and have a great day.” |

The bracketed tokens are substituted by the deterministic round target. Hint candidates are the gaze-contingent temperature phrases already in `HintGenerator.AllPhrases()`; the pilot should sample the full set rather than introduce new directional guidance.

## Structured pilot rubric

**Protocol proposal:** assess both script modes in both voices using the dimension-specific anchors below. Add comments for unclear or unnatural lines and high odd-wording scores. These are exploratory pilot items, not a validated scale.

| Dimension | Question for the listener |
| --- | --- |
| Clarity | Is the instruction immediately understandable when heard once? (1 = low, 5 = high) |
| Naturalness | Does the sentence sound like fluent spoken guidance? (1 = low, 5 = high) |
| Usefulness | Does it help the listener decide what to do next? (1 = low, 5 = high) |
| Internal dialogue | How much does the wording feel like shared or internal thinking? (1 = not at all, 5 = very much) |
| Odd wording | How much does any phrase sound awkward, repetitive, overly familiar, or confusing? (1 = not at all, 5 = very much) |

Record the perspective, actual voice/profile, script version, clip identifier, listener identifier, five dimension scores, and comment. Review clarity/naturalness/usefulness lows and odd-wording highs by phrase and by moment (welcome, practice, hint, success, completion, final). Preserve the same meaning and action across candidates when revising wording. Selection of a final perspective or phrase set remains an open decision until the pilot evidence is reviewed.

## Unity setup and QA

**Implemented:** `Start setup` → `Script pilot`: Trigger / keyboard **1** selects Collaborative; A / X / keyboard **2** selects External. Then choose the male or female **neutral** profile; record the participant sample and check both voices. The selected script is locked when enrollment starts and remains the same through both voice blocks and retries. Start a fresh session to compare the other script; no mid-session switch is provided. Inspector default: `m_DefaultPerspective`; public selector methods also support automation after the start gate.

Setup announcements and the recording passage remain fixed and outside the script-mode manipulation. Eight starter clips (four per voice) load first, followed by the background library. Cache keys include perspective, phrase version, provider/model, voice ID and text. Manifests contain exact text, mode and version; playback events record the spoken text and script metadata; run summaries and `session-perspective-v1.txt` preserve the selection.

**Protocol proposal:** listen to both modes in both voices using equivalent targets, practice and hints; vary comparison order across pilot listeners. Record the rubric in a separate form. A randomized clip-comparison interface is not implemented. Keep issue #24 open until listening assessment and final script selection are recorded.
