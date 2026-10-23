# Current literature and language memo — 23 October 2026

Purpose: improve cohesion and precise terminology, not import another paper's prose or results. Canonical thesis read: chapters 1–4 in thesis-main-sync/docs/thesis. No repository files edited.

## Recommended addition: ISMAR 2025, full text inspected

Andrea Zimmerer, Lydia Bartels, Marc Erich Latoschik. *The Impact of Performance-Specific Feedback from a Virtual Coach in a Virtual Reality Exercise Application*. ISMAR 2025, 1031–1041.
DOI: https://doi.org/10.1109/ISMAR67309.2025.00110
Author PDF: https://downloads.hci.informatik.uni-wuerzburg.de/2025-ismar-feedback-from-a-virtual-coach-in-vr-exercise.pdf
Local inspected PDF/text: /tmp/ismar2025-coach-feedback.pdf and .txt.
Crossref exact title, authors, venue, year, page range and DOI verified.

The 76-participant exercise study varies performance specificity. High specificity improved selected agent-perception measures relative to medium specificity, but did not affect motivation. This supports distinguishing feedback content from generic encouragement and separating perceived agent qualities from task outcomes. It does not imply that more speech/detail always helps, that motivation improves, or that search speed changes.

Suggested original paragraph (can adapt, not quote):

Feedback specificity offers a further distinction between adaptive assistance and generic encouragement. Zimmerer et al. examined how an embodied exercise coach's references to performance affected users' experience, finding changes in selected perception measures without a corresponding effect on motivation~\cite{zimmerer2025feedback}. For Follow My Voice, this distinction motivates describing whether a cue reports observed progress, corrects direction, or acknowledges selection. These functions remain common across voice conditions, so the study will compare voice configurations rather than estimate an independent effect of feedback specificity.

BibTeX:
```bibtex
@inproceedings{zimmerer2025feedback,
  author = {Andrea Zimmerer and Lydia Bartels and Marc Erich Latoschik},
  title = {The Impact of Performance-Specific Feedback from a Virtual Coach in a Virtual Reality Exercise Application},
  booktitle = {2025 IEEE International Symposium on Mixed and Augmented Reality (ISMAR)},
  year = {2025},
  pages = {1031--1041},
  doi = {10.1109/ISMAR67309.2025.00110},
  url = {https://downloads.hci.informatik.uni-wuerzburg.de/2025-ismar-feedback-from-a-virtual-coach-in-vr-exercise.pdf}
}
```

## IEEE VR 2026: metadata and primary abstract inspected; full text access blocked

Juliana H. Madritsch, Tomislav Duricic, Neven ElSayed, Simone Kopeinik, Eduardo Veas. *AI-Powered Conversational Assistance in Augmented Reality for Multi-Step Tasks*. IEEE VR 2026, 519–529.
DOI: https://doi.org/10.1109/VR67842.2026.00072
Author-university record: https://tugraz.elsevierpure.com/en/publications/ai-powered-conversational-assistance-in-augmented-reality-for-mul/
Full text link (403 for this audit): https://tugraz.elsevierpure.com/files/106982140/2026-IEEE-DDIA_VR_2026.pdf
Official conference program: https://ieeevr.org/2026/program/papers/
Crossref DOI metadata independently verified. University status says Submitted despite official program and published Crossref record; prefer verified publication metadata for citation.

Abstract distinguishes total task-completion time from active working time and identifies latency as a deployment limitation in an AR procedural assistant. This is useful terminology for our timing section, but their active-working definition has NOT been inspected; do not assert equivalence to our active-search clock (which retains normal assistance waiting, excluding only pauses). Do not transfer statistical effects, define our primary outcome by their clock, or add their N to our power rationale.

If a brief abstract-supported contextual citation is acceptable, suggested original paragraph:

Recent conversational AR work also makes the timing definition consequential. Madritsch et al. distinguish overall task completion from active working time in an assistant-supported procedural task~\cite{madritsch2026assistance}. Follow My Voice likewise retains multiple event boundaries, but its primary active-search measure includes ordinary waits for guidance between object reveal and correct selection; only recorded pauses are excluded. Separate decision and playback timestamps will therefore explain delivery delays without removing their task-level consequences from the primary outcome.

This is a conceptual connection supported by the authors' abstract, not a full-text methods comparison. Omit if the current review requires full-text inspection for every new citation.

Verified BibTeX (citation-ready metadata, not a claim of full-text inspection):
```bibtex
@inproceedings{madritsch2026assistance,
  author = {Juliana H. Madritsch and Tomislav Duricic and Neven ElSayed and Simone Kopeinik and Eduardo Veas},
  title = {AI-Powered Conversational Assistance in Augmented Reality for Multi-Step Tasks},
  booktitle = {2026 IEEE Conference on Virtual Reality and 3D User Interfaces (VR)},
  year = {2026},
  pages = {519--529},
  doi = {10.1109/VR67842.2026.00072},
  url = {https://tugraz.elsevierpure.com/en/publications/ai-powered-conversational-assistance-in-augmented-reality-for-mul/}
}
```

## Existing citation worth using more substantively

Bovo et al., *EmBARDiment: an Embodied AI Agent for Productivity in XR*, IEEE VR 2025. Existing key bovo2025embardiment.
DOI: https://doi.org/10.1109/VR59515.2025.00093
Author PDF inspected: https://spice-lab.org/papers/EmBARDiment.pdf
Author project: https://spice-lab.org/projects/EmBARDiment/

Terms: gaze-informed context, contextual memory, implicit input, contextually grounded interaction. Use to explain what context represents and why it is selected. Our system's 15-second/four-entry history and separate three-second spatial history are implementation choices; not EmBARDiment's thresholds or evidence for a human memory model. Our voice agent lacks a visible body; 'embodied agent' describes that cited system, not automatically ours.

## Latest ISMAR 2026 leads: official abstracts only; do not imply full-text review

Official primary program https://www.ieeeismar.net/2026/papers/ (PS19, 8 October). Program publication date checked; full text/DOI not verified for these leads.
- Yi Li, Yixin Dai, Chen Li, Peter H. F. Ng, Qing Li: *VAGENT: A Vision-Language Embodied Agent for Attention Awareness and Guidance in Virtual Reality*. Terms: attention awareness versus attention guidance; multimodal cues; spatial context. Useful distinction between sensing user behavior and directing it. Avoid claiming gaze equals attention, or that our voice-only agent inherits embodied multimodal findings.
- Hyeongil Nam, Jeewoo Kim, Kangsoo Kim: *Self-Resemblance and Activity-Aware Responses Shape Relational Openness and Reflection in XR Companion Interaction*. Terms: identity-related cues versus behavioral responsiveness. Useful to explain why voice condition is varied while responsive policy stays shared. Do not import claims about self-disclosure, relational closeness, or recognition into search outcomes.
- Siqi Guo, Fengze Zhang, Christos Mousas: *Doppelgänger with Character: Effects of Self-similarity and Persona Valence on VR Conversation*. Scope: multimodal appearance/voice self-similarity and persona valence in embodied conversation, not isolated voice resemblance. This looks important for a later full-text update but should not displace the verified 2024 Guo source for current questionnaire provenance.

Treat these as follow-up reading leads; no new numerical findings or detailed study claims recommended for the manuscript until the papers are inspected.

## Style synthesis for Opus

1. Link sentences through the causal/methodological relationship: observation -> interpretation -> consequence. Short boundary statements can be integrated into the sentence that motivates them rather than repeated as 'This does not prove...' paragraphs.
2. Use precise terms when they add meaning: spatially grounded guidance, bounded interaction history, performance-specific feedback, cue delivery, response latency, perceived responsiveness, voice resemblance, acoustic similarity, explicit confirmation.
3. Preserve all key distinctions: gaze hit / vicinity / controller hit / trigger; nominal theta / exact bearing; software playback / acoustic latency; synthetic demonstration / participant recording; planned study / implemented system.
4. Avoid 'sentience', 'humanlike understanding', 'seamless', 'real-time' as unmeasured guarantees, 'natural interaction' as an established outcome, and treating subjective agent ratings as objective intelligence.
5. Our agent's phrases should stay brief; this request is to improve thesis prose, not make the in-headset cues verbose.
6. Existing system chapter grammar to fix: 'Both memory types ... is copied' and 'the analysis will report ... and do not label'.
