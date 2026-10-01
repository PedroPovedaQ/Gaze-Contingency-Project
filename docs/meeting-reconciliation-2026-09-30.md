# Meeting reconciliation — September 30, 2026

**Source:** researcher notes in this chat. **Status: Protocol proposal — current researcher decisions and implementation backlog**, except items explicitly marked **Open decision**. These notes do not establish runtime completion, headset validation, a voice-type effect or sufficient statistical power. They supersede conflicting earlier first-person clone wording, five-voice catalog targets and loading-copy requirements.

## Current direction

The study will compare participant-preferred and self-similar voices in two counterbalanced blocks of **40 measured trials each** (80 total), with practice separate. Only the gaze-contingent agent will know the target identity and location. The participant will rely on its guidance to discover the object and select it with the **right controller ray and trigger**. No target color/shape announcement or participant-facing identity display should reveal the answer.

Guidance will first bring the participant to the correct vertical wall/plane using horizontal directions, then guide up/down within that wall. More conversational, engaging and less silent assistance is intended to make the agent useful throughout the search and provide an opportunity to study voice-type effects; this is a rationale to test, not an established effect.

Both voices will use the same participant-directed imperative style: “turn to your left,” “look right,” “look slightly up.” The researcher's “3PP” label is recorded operationally through these examples; grammatically they are commands addressed to the participant. This replaces earlier clone-only first-person scripts. A separate linguistic-perspective manipulation remains deferred.

## Speech and setup requirements

- **Protocol proposal — selected direction:** use short contextual fragments and more variation, rather than a fixed long monologue. Example: “turn to your left” → “a little more right” → “just a little” → “slightly up” → “right there.” Corrections such as “not that much” or “go back” must reflect actual overshoot. Guidance should remain responsive to current valid gaze, cancel obsolete speech, and avoid repeated arrival announcements when looking up/down within the same wall.
- **Open decision:** tune cadence, stability and interruption thresholds in a pilot. Less silence does not mean continuous speech regardless of tracking validity or participant progress. Keep the cue policy comparable across voices and log actual delivered speech/silence.
- **Protocol proposal — selected direction:** offer **6–8 preferred-voice candidates per gender**, after explicit gender preference. **Implemented baseline documented September 28:** five choices per gender; expansion is pending. Final count and new identities are open. Preserve hover preview, selection border, right-trigger confirmation and exact saved voice identity.
- **Protocol proposal — selected direction:** normal visual processing states will say **`Loading...`**, replacing “cloning voice…” and the older `Loading…….` spelling. Preserve actionable error/retry messages, recording instructions and consent disclosures. This does not independently change the spoken processing announcement.
- **Protocol proposal — selected direction:** correct selection will receive a pleasant chime plus varied positive reinforcement in the active voice. Candidate phrases include “Nice work,” “Well done,” and “You got it.” Avoid repeating the identical phrase every trial. Success audio is separate from search timing/instruction counts and must not carry into the next trial.
- **Protocol proposal / Open decision:** audition more personality, pitch variation, tone or expressiveness in synthesized self-similar speech. No pitch shift, DSP preset or expressive model setting is approved by this note. Retain natural-voice recording instructions; check perceived self-similarity, naturalness, intelligibility and loudness. Existing clone-only treatment and any new asymmetry must be documented: otherwise voice identity and presentation style change together. Decide how to match or report treatment before interpreting a voice-type effect.

## Data products and naming

**Protocol proposal — reaffirmed:** keep one performance/subjective CSV per logical trial and separate high-frequency motion/reconstruction files. Retain the existing post-session exporter work; this note does not claim new device-side recording. An optional combined trial table is useful; one mega CSV must not replace the separate deliverables/raw sources.

Trial rows will retain coded participant/session/block/trial identities, condition and actual voice identity; nominal and realized angles, signed height delta/reference and Euclidean distance; first valid gaze and right-controller target-contact times and final correct selection/overall completion time; delivered instruction/error counts, cumulative gross motion and survey values with source/scope. Missing values remain missing. Block survey responses may repeat in trial rows for joins but remain single block observations.

Motion/reconstruction data will retain synchronized valid gaze, head/XR poses, right-controller pose/ray/trigger, scene/object state and event/audio references. Motion alone cannot reconstruct the full run. Preserve raw sources and replay records, and link synchronized video where available.

**Protocol proposal — selected direction:** output filenames need not contain “gaze contingency.” Use descriptive names and stable coded identifiers, for example `trial_<trial_id>_performance_subjective.csv`. **Open decision:** exact motion naming and whether the request also covers document/app titles. Do not bulk-rename historical files; preserve joins through a versioned manifest/mapping.

## Existing task ownership

All items remain implementation/validation work unless separately supported by evidence. Updating requirements does not close tasks.

| Task | September 30 update |
|---|---|
| [#7](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/7) | Study contract |
| [#8](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/8) | Output contract and naming |
| [#11](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/11) | Conversational spatial guidance |
| [#12](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/12) | Voice preparation and loading copy |
| [#16](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/16) | Voice-effect interpretation |
| [#19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19) | Meeting overview |
| [#20](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/20) | Trial performance and subjective export |
| [#21](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/21) | Delivered audio evidence |
| [#22](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/22) | Separate motion and reconstruction output |
| [#23](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/23) | Responsive, less silent speech |
| [#24](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/24) | Shared conversational phrase inventory and success feedback |
| [#25](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/25) | Enrollment wording and optional prosody |
| [#27](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/27) | Operator and participant wording |
| [#29](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/29) | Shared pilot language; perspective factor remains deferred |
| [#33](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/33) | Expand preferred-voice candidates |

[September 28 meeting and data definitions](meeting-reconciliation-2026-09-28.md) · [Current implemented voice baseline](voice-choices.md) · [Study hub](STUDY-HUB.md).
