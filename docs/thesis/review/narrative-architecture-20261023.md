# Narrative architecture for Follow My Voice

Date: October 23, 2026
Status: Narrative plan applied to the abstract and all nine chapters on October 23, 2026. The source-paper readings below informed framing and transitions; protocol, outcome definitions, and pending-results status are preserved. Qualtrics remains on hold. This revision follows checkpoint b377dbd.

## Central question

When an immersive assistant helps someone find an object, does guidance in a voice resembling their own change performance or experience compared with a voice they already prefer?

The narrative should connect a practical activity to this unresolved design choice. Self-similar voice is the subject; guided search makes the voice consequential; gaze contingency and Jev supply the shared apparatus. A resemblance benefit is one possible outcome, not the premise.

## How related papers construct their arguments

The descriptions below are editorial readings of argument structure, not additional empirical findings.

### Kao et al.: a neglected dimension of an established phenomenon

[The Effects of a Self-Similar Avatar Voice in Educational Games](https://doi.org/10.1145/3474665) — [author full text](https://people.csail.mit.edu/dkao/pdf/kao2021chiplay1.pdf).

Introduction, PDF pages 1–3; Discussion, section 6. The paper starts with why identification with virtual representations matters, identifies a visual emphasis in earlier work, then asks what voice similarity contributes. It offers reasons resemblance might help and reasons it might disrupt the experience. The discussion returns to identification and applications rather than to the synthesis implementation.

Transfer: introduce a consequential voice-design question before describing our software. Preserve competing interpretations. Its avatar/game framing and voluntary engagement outcomes do not establish faster spatial search, and our study does not test its identification mechanism directly.

### Guo et al.: an opportunity with a meaningful tradeoff

[Collaborating with my Doppelgänger](https://doi.org/10.1145/3651288) — [author full text](https://web.ics.purdue.edu/~cmousas/papers/jour24-PACMCGIT-SelfSimiarAppearanceVoice.pdf).

Introduction, PDF pages 2–3; Discussion and Design Recommendations, sections 5–6. The authors motivate interaction with virtual counterparts, connect appearance and voice to that interaction, and justify collaborative puzzle solving as the setting. Discussion revisits the research questions; recommendations acknowledge the coexistence of favorable impressions and eeriness.

Transfer: a worthwhile thesis need not show universal improvement. Our story can distinguish efficient guidance, welcome guidance, and uncomfortable guidance. The virtual character study does not imply that our disembodied guide has the same effects.

### Binetti et al.: practical need, specific obstacle, testable response

[Using Visual and Auditory Cues to Locate Out-of-View Objects in Head-Mounted Augmented Reality](https://doi.org/10.1016/j.displa.2021.102032) — [author preprint](https://www2.inf.h-brs.de/~ekruij2m/Binetti2021.pdf).

Introduction and Problem Space, PDF pages 1–2; Discussion, section 5. Maintenance, repair, instruction, and training establish why locating objects matters. The authors narrow that broad motivation to cue–target depth discrepancy, explain why audio might help, and organize discussion around the tested questions.

Transfer: practical applications should lead to one precise question. Their depth/cue mechanism supports studying assistance, not a claim that our spoken voice comparison remedies visual-depth problems or has been validated in industrial work.

### Zhang et al.: justify the experimental task as a useful abstraction

[See or Hear?](https://doi.org/10.1109/ISMAR59233.2023.00128) — [author full text](https://arxiv.org/html/2302.01690v2).

Introduction and book-search task description. The paper links AR search to a concrete book-finding example, explaining how the example contains a target, distractors, and an environment. Its evaluations address hint modality and post-task feedback.

Transfer: explain why our geometric scene is informative rather than simply describing its objects. Our hidden-target task provides repeated encounters with instructional speech while keeping the planned difficulty allocation comparable. It differs from known-target book search and should not be called a direct replication.

## Applied narrative sequence

1. **A useful activity:** XR assistance can help people locate the object relevant to an instruction. Finding is part of doing, whether the surrounding content is physical or virtual.
2. **A design choice inside that activity:** spoken assistance carries both directional information and a recognizable voice. Choosing the voice is part of designing the interaction.
3. **The unresolved tension:** resemblance might make assistance more personally engaging, but could also be uncomfortable. Findings in games and virtual collaboration do not settle its value for repeated spatial instructions.
4. **A meaningful alternative:** compare the clone with a participant-preferred catalog voice. Ask what resemblance offers relative to an already preferred option; do not describe the comparison as isolating resemblance from every acoustic difference.
5. **A task that exposes the question:** the system knows the target, but the participant is not initially told its identity. Guidance progressively identifies the intended object. The voice therefore has a sustained task role.
6. **A fair, inspectable comparison:** the same guidance mechanism supports both assigned voices. Shared rules do not guarantee identical realized speech trajectories; actual playback and technical faults must be documented.
7. **An answer useful to design:** interpret performance and experience together. Faster search, a more welcome voice, and discomfort are different outcomes. Null or mixed results remain relevant, without implying equivalence from nonsignificance.

## Revision decisions

- Bring the voice-design tension into the first page. Keep application examples, but avoid making the reader wait through a long survey of XR search before encountering self-similarity.
- Refocus the Problem Statement on the missing voice comparison. Its current second half moves into stale audio and gaze validation too early. Retain a concise explanation that reliable guidance protects the comparison; leave detailed fault examples in the apparatus and appendix.
- Let the literature review build an argument across themes: voice resemblance, preference as the comparator, and search guidance as the task context. Avoid a sequence of disconnected paper summaries.
- Make each apparatus section explain what it controls or records for the voice study. Architecture diagrams should answer why the empirical comparison is interpretable.
- In the completed discussion, answer each research question before discussing implementation. Then compare the outcome with voice literature, explain possible mechanisms as interpretations, and state bounded design implications.
- Consolidate repeated qualifications. Keep limitations next to claims that need them, but avoid ending every paragraph with another declaration that results are pending.

## Planning example (superseded by the manuscript revision)

An immersive assistant can tell a user where to look, but choosing how that assistant should sound presents a separate design problem. A participant may prefer a familiar catalog voice, or may respond differently to guidance delivered in a synthetic version of their own voice. Object search provides a useful setting in which to examine this choice: the participant must connect spoken directions with objects distributed around them, and the voice remains part of the task until the intended object is selected.

Prior work makes self-similar voice a plausible design option without establishing that it will be a better spatial guide. Its value may lie in performance, in the experience of receiving assistance, or in neither. Follow My Voice therefore compares self-similar guidance with a participant-preferred alternative while keeping the task and guidance mechanism shared.

In a manuscript revision, attach the existing Binetti/Zhang citations to the application rationale and Kao/Guo citations to the voice rationale. Do not add an unsupported claim to be the first study.

## Discussion structure before results exist

Keep the current draft explicitly prospective. Outline how findings will bear on the question, without writing a victory narrative. After data collection: establish the delivered voice manipulation and technical coverage; answer RQ1; answer RQ2; interpret exploratory experience reports; compare with prior evidence; explain design implications and transfer limits. Do not introduce new measures or post hoc hypotheses to make the story appear cleaner.
