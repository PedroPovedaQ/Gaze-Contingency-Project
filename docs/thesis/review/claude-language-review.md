## Thesis Review: 12 Findings

### Errors

**1. "Jev" used before any definition (Ch.1 §4)**

> The study will not isolate the benefit of gaze contingency, short-term memory, Jev, or synthetic speech in general.

First context for "Jev" appears two sections later in §6. A reader at §4 has no referent.

Replace with:
> The study will not isolate the benefit of gaze contingency, short-term memory, the cloud language-model agent, or synthetic speech in general.

---

**2. Active-completion-time symbols differ between chapters**

Methodology §3.1 defines the quantity with $(t_0, t_1, T_{\mathrm{paused}})$. Measurement §4.3 redefines it with $(t_r, t_s, \mathcal{P})$. Both represent the same measure. The Measurement version is more precise (intersection with the search interval). Adopt one notation throughout. Recommended: change Methodology to match Measurement, replacing:

> $T_{\mathrm{active}}=(t_1-t_0)-T_{\mathrm{paused}}$

with:

> $T_{\mathrm{active}}=(t_s-t_r)-\mu\!\left([t_r,t_s]\cap\mathcal{P}\right)$

and use $t_r$ (reveal) and $t_s$ (selection) consistently in the surrounding prose.

---

**3. Trial-index scope undefined in the candidate model**

The model includes $g(j)$ for "trial progression," but $j$ is never specified as within-block (1–40, resetting each period) or session-cumulative (1–80). This changes what $g$ captures and how it interacts with the period term $\beta_B B_b$.

After "The functions $f$ and $g$ represent prespecified low-complexity effects of exact starting angle and trial progression" add:

> Here $j$ indexes trials within each block from 1 to 40, so that between-block change is absorbed by the period term.

(Or state the alternative and justify it.)

---

**4. Research-question wording loses a qualifier between Introduction and Methodology**

Introduction §3 states the primary question as:

> will active search-completion time differ between self-similar and preferred catalog voices **after accounting for absolute starting angle and repeated observations within participants**?

Methodology §4 restates it as:

> whether self-similar and preferred catalog voices differ in active completion time **after accounting for absolute starting theta**.

The clustering qualifier is dropped. Replace the Methodology sentence with:

> The primary question will be whether self-similar and preferred catalog voices differ in active completion time after accounting for absolute starting theta and repeated observations within participants.

---

**5. Naming inconsistency: "trial viewer" vs. "Trial Review"**

System §3.6 uses lowercase "trial viewer" and "the viewer." The evidence-boundary table in §3.7 uses "Trial Review." Evaluation chapter uses "Trial Review." Pick one name. Recommended: replace all instances of "trial viewer" and "the viewer" (when referring to this tool) with "Trial Review."

---

**6. `\ref{app:scenarios}` references a nonexistent label**

Evaluation §6.3 says "The acceptance matrix in Appendix~\ref{app:scenarios}..." and the Organization section says appendices will preserve scenario tests, but no `\appendix` or `\label{app:scenarios}` exists. This will produce an undefined-reference warning. Add a placeholder appendix or change the reference to a forward-looking statement:

> The acceptance matrix (to appear in the scenario appendix of the completed thesis) defines evidence required to make those claims.

---

**7. SSQ is collected pre- and post-exposure but has no analysis plan**

The Methodology specifies 16-symptom SSQ collection at two timepoints with "canonical total or subscale scores." The Planned Analysis chapter never mentions the SSQ — no pre/post comparison, no threshold for stopping, and no role in screening or reporting. Add a subsection or paragraph to the analysis plan specifying at minimum descriptive reporting of pre-to-post change and the intended use (safety monitoring, sample description, or exclusion criterion).

---

**8. The planned model uses exact pre-cue $\theta_0$ without noting it does not yet exist in the data**

The candidate model includes $f(|\theta_{0,ibj}|)$ and the primary contrast section says "exact pre-cue starting theta." The Measurement chapter and Evaluation table both flag the pre-cue snapshot as an implementation gap. The Planned Analysis chapter should cross-reference that gap explicitly. After the model equation, add:

> The exact pre-cue starting angle requires a geometry snapshot not yet implemented in the study build (see Table~6.1). The confirmatory analysis cannot proceed until this snapshot is frozen and verified.

(Adjust the table reference to match the actual table number.)

---

### Optional Edits

**9. Three chapters lack `\label` commands**

System Design and Implementation, Experimental Methodology, and Planned Statistical Analysis have no `\label`, while the other four chapters do. The Organization section works around this by referring to them by name, but any future `\ref` would fail. Adding `\label{ch:system}`, `\label{ch:methodology}`, and `\label{ch:analysis}` costs nothing and enables consistent cross-referencing.

---

**10. The 20/20 vertical classification needs a near-zero rule**

The Measurement chapter correctly notes "a prespecified near-zero rule" is needed but doesn't propose one. Before the study freeze, specify the minimum $|\Delta h|$ that counts as above or below (or state that the allocation enforces a minimum separation). Without this, targets at cross height could be classified arbitrarily and the 20/20 balance could fail on realized layouts.

---

**11. No group-level pass criterion for the similarity manipulation check**

"Voice similarity will serve as a manipulation check" appears in both the Methodology and Analysis chapters, and the analysis plan says failure to recognize the clone won't exclude a participant. But there is no stated group-level test — e.g., that mean similarity must be significantly higher for the self-similar condition than the catalog condition. Without this, the manipulation check is collected but has no decision rule. Consider adding: "The manipulation check will be supported if the paired self-similar similarity rating exceeds the catalog rating with [specified] confidence."

---

**12. The Discussion raises equivalence interpretation but the analysis plan has no equivalence contingency**

The Discussion says "A nonsignificant test alone will not establish equivalence. Any equivalence analysis would require a meaningful bound set before inspecting the result." This correctly identifies a pre-registration requirement — but the Planned Analysis chapter specifies no equivalence bound or TOST procedure. If similar completion times are a plausible and interpretively important outcome, the bound should appear in the analysis plan. If equivalence testing is not planned, the Discussion should say so explicitly rather than describe it as requiring a bound that has not been set.
