# Practical motivation and self-similar voice throughline — October 23, 2026

**Status: Thesis rationale revised; participant outcomes remain pending.**

## Source grounding

The introduction now distinguishes application motivation from evidence of successful deployment. No new bibliography entries are required.

- [Binetti et al., 2021](https://doi.org/10.1016/j.displa.2021.102032), [author preprint](https://www2.inf.h-brs.de/~ekruij2m/Binetti2021.pdf): Introduction, PDF pp. 1–3, frames visual search as part of maintenance, repair, instruction, and training. Used as the authors' application rationale, not evidence that our system improves those workflows.
- [Zhang et al., 2023](https://doi.org/10.1109/ISMAR59233.2023.00128), [full-text preprint](https://arxiv.org/pdf/2302.01690v2): Introduction and Sections 3.2–3.3 operationalize search with a target book, distractors, and an environment. This supports a concrete search-task precedent and the known-target/hidden-target distinction.
- [Lange et al., 2020, HiveFive](https://doi.org/10.1145/3313831.3376803), [author PDF](https://uwe-gruenefeld.de/pub/hivefive-paper.pdf): Introduction, PDF pp. 1–2, motivates attention guidance in cinematic VR, games, and collaboration. Only this motivation is newly transferred; no new result or method claim is made from this reading.
- [Kwok et al., 2022](https://doi.org/10.1145/3536221.3556612), [author-uploaded full text](https://www.researchgate.net/publication/364358586_Two-Step_Gaze_Guidance): Introduction motivates large search spaces and locating an audio-guide referent. Examples are reported as motivations, not as tested deployments.
- [Losing et al., 2014](https://doi.org/10.1145/2638728.2641687), [proceedings PDF](https://ubicomp.org/ubicomp2014/proceedings/ubicomp_adjunct/workshops/PETMEI/p1093-losing.pdf): Existing full-text synthesis supports gaze-contingent auditory feedback as a design precedent.
- [Kao et al., 2021](https://doi.org/10.1145/3474665), [author PDF](https://people.csail.mit.edu/dkao/pdf/kao2021chiplay1.pdf), and [Guo et al., 2024](https://doi.org/10.1145/3651288), [author PDF](https://web.ics.purdue.edu/~cmousas/papers/jour24-PACMCGIT-SelfSimiarAppearanceVoice.pdf): Existing full-text evidence connects voice resemblance with distinct engagement, identification, favorable, and uncomfortable experiences. The introduction and discussion now cite these connections directly; search speed is not substituted for voluntary engagement.

## Changes

Chapter 1 now develops three connected motivations: why search and attention guidance matter in AR/VR; why the voice delivering guidance is a consequential design choice; and why the controlled hidden-target task is suitable for comparing a self-similar clone with a participant-preferred catalog voice.

Chapter 8 returns to those applications with conditional implications for optional voice choice, comfort, and efficiency. It identifies transfer limits: known versus initially unnamed targets, virtual shapes versus meaningful task objects, seated orientation versus walking, and search versus complete procedural work. Its guidance section explicitly treats gaze, staged assistance, and Jev as shared apparatus rather than independent participant treatments.

Protocol parameters, questionnaire wording, scoring, and planned analysis were not changed in this pass. Qualtrics remains on hold. The commit also includes the preceding uncommitted thesis focus revision, six-paper synthesis, supporting Appendix F, and RippleVision citation addition that the user asked to save. This is a local commit; no push is requested.

## Verification

See `verification.md` for final build and layout checks. Protected-file hashes compare the method, measurement, analysis, appendices, and source snapshot before and after this pass. No new participant findings, application effectiveness claims, or causal claims about the supporting guidance system were added.
