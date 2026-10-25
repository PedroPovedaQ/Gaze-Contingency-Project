# Review Memo — Follow My Voice Thesis Manuscript

## I. Recommended Chapter Structure

The existing five-section layout maps well onto thesis chapters. Below is a recommended structure that preserves the author's design while filling gaps a committee will expect.

### Front matter
- Title page, abstract, acknowledgments, table of contents, list of figures/tables

### Chapter 1 — Introduction
Keep the current material but add:
- **Motivation paragraph** grounding the work in a broader XR-assistance or voice-agent problem (the current opening sentence jumps straight into task mechanics).
- **Contribution summary** as a numbered or bulleted list (system, experiment design, analysis plan).
- **Thesis organization paragraph** ("Chapter 2 reviews…, Chapter 3 describes…").

### Chapter 2 — Related Work
The current four subsections are thin for a thesis chapter. Recommended sub-structure:

| Subsection | Content |
|---|---|
| 2.1 Voice agents in XR | Broader landscape; position the self-similar voice question within it |
| 2.2 Self-similar voices and agent perception | Current §2.1 (Guo et al.) plus any additional self-voice or voice-cloning literature |
| 2.3 Gaze-contingent assistance and context-dependent guidance | Merge current §2.4 with gaze-contingent interaction references; situate the EmBARDiment citation here rather than burying it in System Description |
| 2.4 Workload and motivational experience | Current §2.3 (NASA-TLX, IMI) |
| 2.5 Experimental precedents in XR | Current §2.2 (Tamboli et al.) and any other methodological models |

### Chapter 3 — System Description
Well-developed as written. Suggested reordering for readability:

1. Architecture overview (current opening + Figure 1)
2. Apparatus (§3.1)
3. Search scene and task environment (§3.2)
4. Sensing and guidance state machine (§3.3)
5. Short-term interaction memory (§3.4)
6. Agent decisions and playback control (§3.5)
7. Voice preparation and telemetry (§3.6)

Move the EmBARDiment comparison sentence to Chapter 2 and replace it with a forward reference.

### Chapter 4 — Experiment Design
Current "Experiment" section. Recommended sub-structure:

1. Task (§4.1)
2. Design and trial allocation (§4.2, including the allocation table)
3. Measures — keep the current four-level hierarchy (performance → geometry/behavior → subjective → measurement quality)
4. Research questions (§4.4)
5. Participants (§4.5 — note: enrollment target is correctly left unspecified)
6. Procedure (§4.6)
7. Pilot testing and parameter checks (§4.7)

### Chapter 5 — Planned Analysis
Keep as written. Post-data, split into:
- Chapter 5: Analysis Methods
- Chapter 6: Results

### Chapter 6 — Discussion (new)
Currently absent. At proposal stage, include a skeletal discussion covering:
- How results will be interpreted under each plausible outcome pattern (null, catalog-favored, self-similar-favored).
- Relationship to Guo et al. findings in the voice-only context.
- What the subjective measures can and cannot establish.

### Chapter 7 — Contributions, Limitations, and Future Work
Current §6 material, expanded with a future-work section.

### Back matter
- References, appendices (questionnaire full text, phrase library excerpt, consent summary, pilot-check criteria)

---

## II. Language Issues

### Awkward or unclear phrasing

| Location | Current text | Issue |
|---|---|---|
| Intro ¶1 | "controller selection" | Ambiguous; means "controller-based selection" — reword to "controller input" or "ray-and-trigger selection" |
| Intro ¶1 | "Spoken assistance can reveal the target through a sequence of directions" | "Reveal" conflicts with the later technical meaning of object reveal at trial start. The voice *guides toward* the target; it does not visually reveal it. |
| Intro ¶2 | "among eight surrounding object planes" | "Surrounding object planes" is jargon-heavy for an introduction; consider "among objects displayed on eight surrounding wall panels" |
| Related Work §2.1 | "Its embodied-collaborator setting motivates examining agent perception, but it differs from…" | Clunky. Try: "Its embodied-collaborator setting is relevant to agent perception, but differs from…" |
| Related Work §2.4 | Entire subsection | Reads as system description, not related work. Move operational detail to Ch. 3; keep only the literature-positioning sentence. |
| System §3.4 | "This bounded recent context is related to the context-management motivation of EmBARDiment" | Awkward nominalization. Simpler: "This bounded context addresses a similar concern as EmBARDiment's context management" — or move the comparison to Related Work entirely. |
| System §3.5 | "the current state, evidence, pacing, and object intersections before playback, and discards decisions that are stale or conflict with the current situation" | "current state" and "current situation" in the same sentence; redundant. |
| System §3.6 | "Voice condition is the intended manipulation; behavior-dependent wording, hint counts and synthesized acoustics need not be identical." | The semicolon sentence is doing too much. Split: state the manipulation, then separately note what is permitted to vary. |
| Contributions §6 | "Its system combines locally verified spatial evidence" | "Its system" — antecedent is unclear. "The system combines…" |
| Planned Analysis §5.1 | "Report the voice time ratio and uncertainty using a two-sided contrast." | Shifts to imperative mood; the rest of the chapter uses future tense. Change to "The analysis will report…" |

### General style notes
- Heavy semicolon use throughout creates long compound sentences that are hard to parse on first read. Many could be split into two sentences with no loss of precision.
- The Measures section (§4.3) is admirably precise but approaches specification-document density. For thesis readability, consider moving the most granular validity/gap-handling rules to an appendix and keeping the chapter text at the level of "what is measured and why."

---

## III. Claims That Must Remain Proposed

These statements currently read as established facts or results. They should retain explicit future/conditional framing throughout all drafts until data exist.

1. **System effectiveness.** Sentences like "This division keeps task-critical decisions deterministic and auditable while allowing the agent to adapt…" (System §3.0) and "A local speech scheduler therefore revalidates each decision…and discards decisions that are stale" (System §3.5) describe *intended* behavior of built software. The system exists, but its effectiveness at producing correct, timely guidance for real participants is unvalidated. Add a qualifier such as "is designed to" or "is intended to" where the sentence implies successful operation during participant sessions.

2. **Perceptual equivalence of voices.** The manuscript correctly notes that "automated checks do not establish perceptual equivalence" (System §3.6), but no other sentence should imply that the two voice conditions are perceptually matched on intelligibility, loudness, or naturalness. This remains a pilot-verification item.

3. **Self-similar voice recognition.** The manuscript assumes participants will perceive the cloned voice as self-similar. The manipulation check (voice-similarity rating) is correctly planned, but any framing that presupposes recognition must stay conditional: "if participants perceive the self-similar voice as resembling their own…"

4. **Contribution claims.** "The study will connect a personalized voice comparison to a measurable gaze-guided search task" (Contributions §6) is appropriate future tense. Ensure this never migrates to "The study connects…" or "We have shown…" before data collection.

5. **Analysis model specification.** "The model's random-effects structure, angle function and smallest meaningful effect will be finalized with simulation before confirmatory recruitment" (Planned Analysis §5.1). This is correctly marked as incomplete. Any later draft that presents model output must confirm this step was actually completed and report the simulation parameters.

6. **Questionnaire discrimination.** "Its burden and ability to distinguish the two blocks will be checked during pilots" (Contributions §6). Until pilot data show that participants can reliably provide separate retrospective ratings for two blocks, the claim that 130 condition-specific items will yield usable per-block scores is a design intention, not a validated property.

7. **Gaze-derived measures.** Multiple careful disclaimers already appear (ray intersections ≠ fixations, render frames ≠ native sampling). Maintain these throughout; do not let any results section later label gaze hits as "fixations" or report "fixation duration" without the validated classifier mentioned in §4.3.4.

8. **Memory as a system property.** §2.4 correctly states that memory use "will be described as a system property…rather than as a demonstrated source of engagement." This framing must persist — no results section should claim that bounded memory *caused* improved guidance quality without an independent manipulation.

---

## IV. Minor Structural Notes

- **No participant N is specified**, which is correct for the proposal stage. The power-analysis commitment (§4.5) is the right placeholder.
- **No Qualtrics or survey-platform detail** appears anywhere, which is consistent with the author's design scope.
- **Practice trial count** (one, in first-assigned voice) is correctly and consistently stated in §4.2 and §4.6. Ensure no other section contradicts this.
- The **allocation table** (§4.2) is clear and should be preserved as-is in the thesis.
- **Figure placeholders** with fallback text boxes are fine for proposal stage but should be flagged for replacement before submission.
