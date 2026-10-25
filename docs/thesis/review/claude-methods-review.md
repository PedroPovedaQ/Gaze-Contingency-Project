Here are 12 substantive findings, ordered errors-first.

---

## Errors

**1. Active-completion-time notation is inconsistent across chapters.**
Methodology defines reveal as $t_0$ and selection as $t_1$; Measurement redefines them as $t_r$ and $t_s$. Pick one pair and use it everywhere. Suggested: adopt $t_r$/$t_s$ in the Methodology formula, since the Measurement chapter's version is the more precise definition.

**Replace** (Methodology §Measures, Active completion time):
> from object reveal ($t_0$) to the correct right-trigger selection ($t_1$), excluding the union of recorded pause intervals within that interval:
> $T_{\mathrm{active}}=(t_1-t_0)-T_{\mathrm{paused}}.$

**with:**
> from object reveal ($t_r$) to the correct right-trigger selection ($t_s$), excluding the total duration of recorded pause intervals that overlap the search interval:
> $T_{\mathrm{active}}=(t_s-t_r)-\mu\!\bigl([t_r,t_s]\cap\mathcal{P}\bigr),$
> where $\mu$ denotes elapsed duration and $\mathcal{P}$ is the union of valid pause intervals.

This also fixes error 2 below.

**2. The Methodology formula is mathematically imprecise.**
"$T_{\mathrm{paused}}$" is described as "the union of recorded pause intervals," but a union of intervals is a set, not a duration. The Measurement chapter correctly applies $\mu(\cdot)$ to denote elapsed duration of the set intersection. The replacement in finding 1 resolves this.

**3. Allocation table presents 20/20 vertical balance as a design parameter without noting it is unenforced.**
The Measurement chapter explicitly states "floor-to-height scattering did not enforce that balance," and the Evaluation evidence table lists it as a gap. The Methodology table should match.

**Replace** the allocation table row:
> Vertical allocation | 20 targets above and 20 below readiness-cross height per block

**with:**
> Vertical allocation | 20 targets above and 20 below readiness-cross height per block (not yet enforced; implementation required before study freeze)

**4. Exact pre-cue theta is used in the Methodology as a planned covariate without flagging that the snapshot is unimplemented.**
The Measurement chapter calls this "a required addition to the frozen study data contract" and says existing fields "must not be renamed or silently interpreted as exact pre-cue measurements." The Methodology's measures subsection on starting geometry should note this gap.

**Add** after the Methodology paragraph defining $\theta_0$ and $|\theta_0|$:
> The exact pre-cue snapshot that produces these values has not yet been implemented. Existing signed and absolute theta fields in the current data contract reflect nominal wall geometry relative to the previous target wall and are not substitutes. This snapshot is a required addition before the study release is frozen.

**5. The voice-cloning recording passage is never specified.**
The System chapter says "each participant records the same standard passage" and the Procedure says participants "record a standard passage in their natural voice for synthesis," but nowhere is the passage identified, its language stated, or its duration bounded. For a replicable method this must be specified.

**Add** to the Procedure section, after "record a standard passage in their natural voice for synthesis":
> The passage, its language, and its approximate duration will be specified in the frozen protocol and reproduced in Appendix~[ref].

**6. Left/right balance within bilateral angle categories is unspecified.**
The allocation says "Eight trials each at 0, 45, 90, 135 and 180 degrees" with "left/right assignments will be randomized." For the three bilateral categories (45°, 90°, 135°), it is unclear whether left and right are balanced (4 each) or only randomized without constraint. This affects whether the design guarantees directional symmetry within each block.

**Replace:**
> trial order and left/right assignments will be randomized subject to those quotas.

**with:**
> trial order will be randomized. For each bilateral absolute-angle category (45, 90, 135 degrees), four trials will be assigned to the left wall and four to the right within each block.

(If equal balance is not intended, state the actual rule.)

**7. Consent materials do not cover cloud processing of voice recordings.**
The Discussion explicitly flags this: "Older local-only descriptions are inconsistent with current ElevenLabs voice preparation and Jev decisions and require reconciliation before participant use." The Procedure section should state that consent includes cloud voice processing rather than leaving this solely to the Discussion's limitations.

**Add** to the Procedure, after "After consent":
> Consent will describe that voice recordings are transmitted to ElevenLabs for synthesis and that agent decisions use a cloud language-model service, identifying the providers, processing locations, and retention terms.

**8. Appendix reference `\ref{app:scenarios}` has no corresponding appendix in the draft.**
The Evaluation chapter cites "the acceptance matrix in Appendix~\ref{app:scenarios}" but the Organization section lists appendices for "instrument wording, data contract, sequence tests, and study-readiness decisions" without naming a scenarios appendix. Either add a placeholder appendix with `\label{app:scenarios}` or change the reference to match an existing appendix label.

---

## Optional edits (would improve clarity but not correctness)

**9. Chapter cross-references are inconsistent in the Organization section.**
Three chapters use `\ref{}` (literature, measurement, evaluation) while three others are named descriptively ("The system chapter," "The methodology chapter," "the planned-analysis chapter"). Use `\ref{}` for all, or confirm the latter three have `\label` tags to reference.

**10. The candidate model's trial-progression term $g(j)$ does not specify indexing scope.**
If $j$ runs 1–40 within each block, $g(j)$ captures within-block learning that resets. If $j$ runs 1–80 across both blocks, it captures cumulative learning that may absorb part of the period effect. The planned-analysis chapter should state which is intended or mark this as an open decision.

**Suggested addition** after "represent prespecified low-complexity effects of exact starting angle and trial progression":
> Here $j$ indexes trial position within a block (1--40). A cumulative cross-block counter will be considered as an alternative parameterization during simulation.

**11. "Model judge" appears in the Analysis chapter without prior definition.**
The phrase "model-judge ratings will remain advisory engineering evidence" in §Guidance and qualitative evaluation assumes the reader knows this concept, which is only introduced in the Evaluation chapter. Add a minimal forward reference.

**Replace:**
> Automatic checks and model-judge ratings will remain advisory engineering evidence

**with:**
> Automatic checks and model-judge ratings (Section~\ref{ch:evaluation}) will remain advisory engineering evidence

**12. The Methodology's procedure does not mention the per-trial pre-cue geometry snapshot as a logged step.**
The Measurement chapter requires "origin, cross, target, timestamp, frame, and coordinate-frame identifier before the opening cue" and calls it "a required addition." The Procedure's trial description says the trial begins with "the participant looking at a readiness cross" and then an opening cue plays, but does not mention logging the geometry snapshot between readiness and the cue. This is related to finding 4 but concerns the procedure flow rather than the measures section. Adding one sentence to the trial-flow description would close the gap.

**Add** to the Procedure's trial description, after "After readiness is established":
> the system will record the pre-cue geometry snapshot (origin, cross position, target position, timestamp, and coordinate frame).
