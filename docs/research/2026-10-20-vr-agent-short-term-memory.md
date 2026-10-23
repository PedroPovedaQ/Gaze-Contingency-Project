# Recent XR agent memory research for the guidance state machine

Date: 2026-10-20  
Status: **Research evidence and protocol proposals**. This note does not establish that a new implementation has passed headset validation.

## Finding

**EmBARDiment (IEEE VR 2025) is the strongest direct match found:** it implements bounded, gaze-derived recent context for an XR agent. **Designing Memory-Augmented AR Agents (ISMAR-Adjunct 2025)** explicitly discusses short-term working memory for task-progress tracking, but presents a conceptual framework and evaluation plan. Neither validates our hidden-target navigation thresholds or proves that an LLM should own geometric state transitions.

Here, *short-term memory* means retained agent/task interaction evidence: recent object visits, progress, and spoken actions. It does not mean a participant's cognitive working-memory capacity, an LSTM merely appearing in related work, or a long-term personalization database.

## Priority citations

### 1. EmBARDiment: an Embodied AI Agent for Productivity in XR

Riccardo Bovo, Steven Abreu, Karan Ahuja, Eric J. Gonzalez, Li-Te Cheng, and Mar Gonzalez-Franco. **IEEE VR 2025**, pp. 708–717. Conference paper.

- [DOI / publisher](https://doi.org/10.1109/VR59515.2025.00093)
- [Open full text, author manuscript v2](https://arxiv.org/html/2408.08158v2) · [PDF](https://arxiv.org/pdf/2408.08158)
- [Author project page](https://spice-lab.org/projects/EmBARDiment/) · [Google implementation repository](https://github.com/google/embardiment)
- Inspected: §§2.2–2.3, 3.2, 5.1, 5.3, and 6 of the open manuscript. Publication metadata also checked against IEEE's Crossref deposit.

**Method/evidence:** An XR reading assistant retains gaze-selected text in reading order, caps this contextual buffer at 250 words, discards older content, and clears it after each request. Chat history is retained separately. The reading implementation uses a 120 ms word-bounding-box threshold; buffer capacity was chosen in pilots. A 15-participant study compares no context, full text context, and gaze-selected context. The authors report fewer question reformulations and better perceived helpfulness with gaze-selected context. This is a comparison of contextual-input strategies, not an isolated test of memory duration.

**Transfer proposal:** Give Jev a small, structured recent-event summary alongside current state rather than the entire replay or phrase catalog. Keep separate records of observations and speech actually delivered. Define explicit expiry/reset rules.

**Limit:** Reading words differs from searching scattered 3D objects. Do not copy 120 ms or 250 words as validated settings for our task. Our target is intentionally unknown to the participant, so retained gaze should inform guidance without being equated with recognition.

### 2. Designing Memory-Augmented AR Agents for Spatiotemporal Reasoning in Personalized Task Assistance

Dongwook Choi, Taeyoon Kwon, Dongil Yang, Hyojun Kim, and Jinyoung Yeo. **ISMAR-Adjunct 2025**, pp. 113–119. Adjunct proceedings; conceptual framework, not a completed efficacy study.

- [DOI / publisher](https://doi.org/10.1109/ISMAR-Adjunct68609.2025.00028)
- [Open full text](https://arxiv.org/html/2508.08774v1) · [PDF](https://arxiv.org/pdf/2508.08774)
- Inspected: PDF §§4.1, 4.4, 4.5.2, and 5.2. The HTML rendering numbers the corresponding sections 3.1, 3.4, 3.5.2, and 4.2. Venue/year/pages/DOI checked against IEEE's Crossref deposit.

**Method/evidence:** The proposed architecture separates perception, memory, spatiotemporal reasoning, and actuation. Stored experiences are sequences of scene graphs. Its task-progress component explicitly proposes working memory of short-term behavior, aligned with stored procedural experience, to avoid treating brief off-task behavior as task failure. Implementation and evaluation remain planned; the paper does not report a measured memory benefit.

**Transfer proposal:** Separate a timestamped observation layer, bounded task memory, deterministic state transitions, and speech execution. Record why a transition occurred and distinguish a sensor gap from a measured departure.

**Limit:** Much of the paper concerns persistent personal experiences. Our within-trial memory need not implement a scene-graph retrieval database or learn participant preferences. Cite it as an architectural proposal, not empirical proof of more accurate or faster guidance.

### 3. GENLARP: Enabling Immersive Live Action Role-Play through LLM-Generated Worlds and Characters

Yichen Yu, Yifan Jiang, Mandy Lui, and Qiao Jin. **ISMAR-Adjunct 2025**, pp. 763–764. Two-page system description.

- [DOI / publisher](https://doi.org/10.1109/ISMAR-Adjunct68609.2025.00178)
- [Open publisher-version PDF hosted by the authors' lab](https://extendedhorizonlab.github.io/papers/genlarp-ismar2025.pdf)
- Inspected: p. 764, §§2.2–2.3 and 4; publisher DOI and venue are printed on p. 763.

**Method/evidence:** The described role-playing system uses per-character internal state, memories, behavior-path recording, and interaction history to maintain continuity across interactions. It describes a Unity/GPT implementation. The short paper does not specify a bounded-memory algorithm or report a controlled user evaluation; persistent long-term coherence is discussed as future work.

**Transfer proposal:** It is a secondary citation for explicit state/history in embodied-agent architectures. Our event ledger could similarly make continuity inspectable in the trial viewer.

**Limit:** Narrative consistency is not evidence for correct gaze-contingent directional assistance. This paper is less useful than EmBARDiment for justifying our concrete memory design.

## Adjacent evidence, not direct short-term agent-memory validation

### 4. Toward User-Aware Interactive Virtual Agents: Generative Multi-Modal Agent Behaviors in VR

Bhasura S. Gunawardhana, Yunxiang Zhang, Qi Sun, and Zhigang Deng. **ISMAR 2024**, pp. 1068–1077. Conference paper.

- [DOI / publisher](https://doi.org/10.1109/ISMAR62088.2024.00123)
- [Open author PDF](https://graphics.cs.uh.edu/wp-content/papers/2024/2024-ISMAR-AgentBehaviorGeneration.pdf) · [NSF public-access copy](https://par.nsf.gov/servlets/purl/10637053)
- Inspected: PDF pp. 3–4, §§3 and 5.3–5.5; publication metadata verified against IEEE's Crossref deposit.

**Method/evidence:** A conditional variational autoencoder uses tracked user motion to generate responsive agent gaze/body behavior alongside an LLM dialogue pipeline. Its implementation describes sequence length 32. This is temporal conditioning of behavior generation; it is not a specified short-term task-event memory policy. The authors evaluate the broader multimodal agent against an animation-based baseline.

**Transfer proposal:** Relevant to making an embodied agent responsive to several synchronized signals rather than one isolated gaze hit. Keep it out of claims that our overshoot or duplicate-speech memory is empirically validated. An LSTM mention in its related work is not evidence that its own system uses an LSTM memory controller.

### 5. Supplement outside the requested venues: Satori

Chenyi Li, Guande Wu, Gromit Yeuk-Yin Chan, Dishita G. Turakhia, Sonia Castelo Quispe, Dong Li, Leslie Welch, Cláudio T. Silva, and Jing Qian. *Satori: Towards Proactive AR Assistant with Belief-Desire-Intention User Modeling*. **ACM CHI 2025 — not IEEE VR or ISMAR.**

- [DOI / publisher](https://doi.org/10.1145/3706598.3714188)
- [Open full text](https://arxiv.org/html/2410.16668v3) · [Author PDF](https://gromitchan.com/assets/papers/li-chi2025.pdf)
- [Author project page](https://www.chenyi-li.com/satori/)
- Inspected: §§6.1.2 and 6.2; publication venue/DOI are present in the open manuscript.

**Method/evidence:** Satori's action and assistance history includes previous guidance and progress to reduce repeated instructions. It combines a belief–desire–intention model with proactive AR assistance. Its user evaluation concerns the complete system, not a memory-only ablation.

**Transfer proposal:** This is a particularly direct citation for remembering already-delivered instructions. Track semantic action plus actual playback, rather than merely phrase text or requests that were canceled. Do not describe this CHI result as an ISMAR finding.

## Application to this experiment

**Implemented baseline at review time:** Existing guidance already has separate target-visit/overshoot history, turn-progress tracking, debounce gates, and speech repetition rules. These can be found in `HintGenerator`, `GazeTargetHistory`, `GuidanceTurnState`, `GazeZoneEntryGate`, and the voice scheduler. This note does not certify completion of the concurrent central-state-machine refactor.

**Protocol / engineering proposal:** Consolidate the following into inspectable, trial-scoped memory:

| Retained evidence | Purpose | Guardrail |
|---|---|---|
| Last qualified target-vicinity visit, object ID, and observation time | Support “go back” after a genuine departure | A tracking gap is not a departure; overlap is not proof of recognition |
| Recent signed yaw/pitch errors and previous navigation stage | Distinguish progress, stalling, and overshoot | Never infer a crossing across invalid samples |
| Last acknowledged turn milestone | Avoid repeated “keep going” for the same progress | Consume only on committed speech; reset for a new target |
| Recent spoken intent, phrase, start/end time, and outcome | Avoid repeating directions and interrupting unfinished feedback | Requested/canceled audio is not delivered audio |
| Current direct gaze hit, unique vicinity winner, and controller hit | Explain which evidence drove an action | Keep all three distinct; attach freshness/validity |
| State transition reason, action ticket, and evidence sequence | Reproduce decisions in the trial viewer | Legacy missing state is “not recorded,” not a fabricated state |

Jev would receive current eligible actions plus a compact summary of this memory. Unity would retain authority over geometry, validity, transitions, cancellation, and playback revalidation. This is our engineering interpretation of the literature, not an architecture proven superior by these papers.

**Open decision / validation:** Memory duration, evidence expiry, and trend-window length should be selected in a pilot and frozen before the voice comparison. Existing 0.1-second target qualification, 0.15-second valid departure, 45-degree milestones, and repetition timing are implementation choices. These papers do not validate them as fixation thresholds or as optimal values for the Vive Focus Vision.

A separate counterbalanced pilot can compare the current and consolidated-memory policies using one voice and matched layouts. Measure confirmation latency, stale/wrong hints, repeated semantic instructions, overshoots, angular travel, selection errors, task time, and perceived naturalness/workload. Replay tests assess reproducibility and stale decisions; new participants are required to assess behavioral consequences. In the main study, use identical policy and phrase eligibility across neutral and self-similar voices. Changing memory only for one voice would confound the intended voice-type effect. Log actual hint exposure; do not force equal counts when behavior differs.

## Search coverage and limits

Targeted search conducted on 2026-10-20 for IEEE VR and ISMAR publications primarily in 2022–2026, using combinations of “short-term memory,” “contextual memory,” “agent,” “interaction history,” “task progress,” and “conversation history.” Sources inspected included official IEEE VR 2024/2025 program listings, author/lab pages, open manuscripts, author-hosted publisher PDFs, and publisher-deposited Crossref metadata. DOI resolution through the browser was unreliable for several IEEE entries, so venue/year/DOI verification used the publisher deposits and/or the printed publisher PDF.

The strongest verified matches are from 2024–2025. No closer 2022, 2023, or 2026 IEEE VR/ISMAR implementation was verified in this targeted pass. That is a search limitation, not proof that none exists. This is an annotated shortlist, not a systematic review.

Excluded from direct supporting evidence: studies measuring participants' working-memory capacity; autobiographical-memory reconstruction; LSTM architectures mentioned only in related work; and long-term agent-memory papers without a verified requested-venue publication. Environment-aware agents without an inspected memory method were not counted as direct memory evidence. General CHI guidance from Amershi/Horvitz remains useful background, but is not relabeled as IEEE VR/ISMAR research.

**Citation-ready synthesis:** Recent XR work has implemented bounded gaze-derived contextual memory for an embodied assistant (EmBARDiment), while ISMAR-Adjunct work has proposed explicit working memory for task-progress reasoning (Choi et al.). These motivate a compact, inspectable interaction history for our guidance system; its navigation policy and timing remain subject to task-specific validation.
