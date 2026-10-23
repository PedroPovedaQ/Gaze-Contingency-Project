# Self-similar voice and supporting auditory guidance — October 23, 2026

**Status: Literature synthesis incorporated into the thesis draft.** This revision strengthens the rationale; it does not change the participant protocol or report new participant results. Qualtrics remains on hold.

## Sources reviewed

Full-text methods, results, discussion, and limitations were reviewed using the accessible versions below. Publisher metadata and manuscript versions should not be confused: author copies and preprints are identified explicitly. Local reading copies are under `/tmp/thesis-audio-reading` and are not redistributed in the thesis source archive.

| Citation key / paper | Publisher link | Full text reviewed | Evidence location |
|---|---|---|---|
| `kao2021selfsimilar` — The Effects of a Self-Similar Avatar Voice in Educational Games | [DOI](https://doi.org/10.1145/3474665) | [Author PDF](https://people.csail.mit.edu/dkao/pdf/kao2021chiplay1.pdf) | Study design, Results, Discussion, and limitations; especially the performance/time measures and direct motivation tests |
| `guo2024doppelganger` — Collaborating with my Doppelgänger | [DOI](https://doi.org/10.1145/3651288) | [Author PDF, Purdue](https://web.ics.purdue.edu/~cmousas/papers/jour24-PACMCGIT-SelfSimiarAppearanceVoice.pdf) | Sections 3–7: voice preparation, within-subject comparison, results, interpretation, limitations |
| `losing2014guiding` — Guiding Visual Search Tasks Using Gaze-Contingent Auditory Feedback | [DOI](https://doi.org/10.1145/2638728.2641687) | [Conference proceedings PDF](https://ubicomp.org/ubicomp2014/proceedings/ubicomp_adjunct/workshops/PETMEI/p1093-losing.pdf) | Auditory mapping, evaluation tasks and their separate results, conclusion |
| `kwok2022twostep` — Two-Step Gaze Guidance | [DOI](https://doi.org/10.1145/3536221.3556612) | [Full author-uploaded text](https://www.researchgate.net/publication/364358586_Two-Step_Gaze_Guidance) | Sections 3–4 (apparatus, stages, study), 4.3 (outcomes), 5–6 (discussion and limits) |
| `binetti2021outofview` — Using Visual and Auditory Cues to Locate Out-of-View Objects in Head-Mounted Augmented Reality | [DOI](https://doi.org/10.1016/j.displa.2021.102032) | [Author preprint PDF](https://www2.inf.h-brs.de/~ekruij2m/Binetti2021.pdf) | Sections 3–5, PDF pp. 4–9, particularly 4.1 and 5.4 |
| `zhang2023seehear` — See or Hear? | [DOI](https://doi.org/10.1109/ISMAR59233.2023.00128) | [Author preprint v2 PDF](https://arxiv.org/pdf/2302.01690v2), [HTML](https://arxiv.org/html/2302.01690v2) | Sections 3.3–4 (hint/feedback timing), 5.1–5.2 (separate sub-study results), 6.2 (limitations) |

The Kwok full text was read through the author-uploaded page; a standalone PDF download was blocked. The other five reading copies were obtained as PDFs. No claim is based solely on a search snippet. RippleVision is outside this six-paper full-text pass and retains its existing abstract-only qualification.

## Evidence-to-design decisions

- **Focal comparison:** Kao and Guo motivate investigating voice resemblance, with mixed outcome possibilities. Their assigned comparison voices are not equivalent to this study's participant-preferred alternative. Chapter 2 now makes that distinction explicit and leads its comparison table with self-similar voice.
- **Outcome meaning:** The thesis distinguishes voluntary engagement from fixed-task efficiency and does not infer a search-speed benefit from time spent in a game. Favorable experience, discomfort, and performance remain separate outcomes. Chapter 8 extends this interpretation without inventing results.
- **Auditory channel:** Losing, Kwok, Binetti, and Zhang supply different precedents: sonification, spatial audio, audiovisual cues, and an opening spoken instruction. The thesis identifies which mechanism each study tested before drawing a design connection.
- **Staged assistance:** Coarse-to-fine organization is an adaptation for the present apparatus. No cited transition angle, sampling rate, task count, or cue interval is adopted as a validated setting.
- **Feedback role:** Researcher replay and participant-facing post-task feedback are different interventions. The present revision adds no participant replay exposure or survey interruption.
- **Scope of inference:** Gaze contingency, history, Jev, and speech scheduling support the voice comparison. Without a separate manipulation, they cannot be credited with an independent participant benefit.

## Claims deliberately avoided

No general claim that self-similar voice improves motivation, all social impressions, or speed; no conversion of a nonsignificant result into equivalence; no claim that the audio studies establish speech superiority or justify more continuous talking; no confusion of target gaze with selection; no borrowed sample-size/power justification. The chapter reports conditional findings rather than adopting the broadest wording from an abstract or conclusion.

## Revision and verification scope

Edited `chapters/02-literature.tex` and `chapters/08-discussion.tex`. Existing bibliography entries suffice; no new or duplicate references were added. Method, measurement, analysis, instrument appendices, and historical source snapshot were protected against accidental changes during this pass. Build and delivery verification are recorded in `verification.md`.
