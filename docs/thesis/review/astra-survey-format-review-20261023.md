# Survey format and NASA-TLX direction review - 2026-10-23

Status: **Protocol proposal**. Local thesis source update only; Qualtrics remains on hold. No live Overleaf changes, commits, or participant data were made by this review.

## Reference and scope

Visually inspected the user-supplied `/Users/pedro.poveda/Downloads/theimpact_thesis_ms_ucf.pdf`, PDF page 54 (printed page 42), Appendix A.2. It uses open circular response marks alongside questions. Its example states a 0-20 range but displays five circles and omits Effort. That example is used solely for visual style, not as a scoring or instrument specification.

The current protocol decisions (`docs/protocol/2026-10-20-final-survey-decision.md`, `docs/protocol/2026-10-20-side-thread-handoff.md`) and the thesis scoring before this change specified six 0-10 workload dimensions with Perfect-to-Failure Performance anchors. The user subsequently clarified that the current thesis must instead display Failure-to-Perfect; that explicit choice supersedes the initial orientation-preserving edit. The prior question's negative wording, "How unsuccessful...", is the wording corrected here.

## Changes

- `docs/thesis/appendices/a-instruments.tex`: adds open response circles for categorical choices and every discrete numeric option. Workload has all eleven 0-10 choices; each IMI, agent-perception and custom agreement question has all seven 1-7 choices; each baseline/final SSQ symptom has all four 0-3 choices. Numeric rows stay together with their questions. Repeated anchors retain the original meanings, including IMI's Somewhat true midpoint and the distinct agent/custom agreement anchors.
- The 0-100 liking and guidance-percentage ratings remain numeric write-ins with separate unable-to-answer circles. They are not reduced to a coarser bubble scale. Optional text and study code have write-in lines. Multi-select gender wording remains explicit.
- Performance now reads: "How successful were you in accomplishing what you were asked to do?" Its row now runs from 0 = Failure through 10 = Perfect, following the user's explicit clarification. Higher raw ratings therefore mean better perceived performance. This is deliberately opposite the NASA source orientation, and is disclosed as a local adaptation.
- `docs/thesis/chapters/03-measures-scoring.tex` and `docs/thesis/appendices/b-data-dictionary.tex`: preserve the favorable-direction raw Performance rating and reverse only Performance exactly once as `10 - r` before calculating the adverse-direction workload mean. This supersedes the earlier no-reversal instruction, which applied to the earlier opposite anchors. The complete six-dimension unweighted mean, missingness policy, and documented local adaptation remain unchanged.
- No other instrument item wording, number of response options, item order, or reverse keys changed; IMI keys remain unchanged. Existing IMI task adaptations and the locally adapted NASA-TLX form are not claimed to be newly validated instruments.

## NASA source check

1. [NASA short rating form](https://human-factors.arc.nasa.gov/groups/TLX/downloads/TLXScale.pdf): indexed official source gives the positive success question and Perfect-to-Failure anchors. Direct retrieval of this endpoint timed out during review.
2. [NASA-TLX version 1.0 manual and forms](https://www.nasa.gov/wp-content/uploads/2026/03/nasa-tlx-v1-0-searchable-text-and-forms.pdf): downloaded successfully and text inspected; the Performance definition asks about success. Appendix C, PDF page 19 (printed page 17), was rendered and visually inspected: Good is on the left and Poor on the right. This independently confirms favorable-left/adverse-right direction.
3. [Hart and Staveland, 1988](https://doi.org/10.1016/S0166-4115(08)62386-9) remains the thesis instrument attribution.

## Scoring implications and integration

For the final proposed form, record the selected Performance number directly: 0 means Failure and 10 means Perfect. Preserve it as raw perceived performance. Create a separate workload-oriented value using `10 - r`, exactly once, and average that with the five unchanged demand/effort/frustration ratings only when all six responses are present. Thus raw Performance 0 contributes 10 to workload, and raw Performance 10 contributes 0. Do not apply this reversal automatically to legacy Perfect-to-Failure responses. A separately named 0-100 representation may multiply by ten with bounds and direction documented; it is not the original response. Earlier exports with unknown scale/direction remain unscored. IMI reversal remains `8 - r` at its established reverse-key positions.

Historical/source snapshots and `docs/protocol/overleaf/survey-questions-by-block.tex` / `docs/irb/source/questionnaires.md` still contain the previous negative wording. They were not silently rewritten during this thesis-only task. Reconcile any future participant-facing release against this corrected form and version it; no Qualtrics synchronization was performed.

Static checks: six workload rows, 58 seven-point rows (24 IMI + 21 agent + 12 custom + one baseline familiarity), and 32 SSQ rows. Modified source files pass scoped `git diff --check`. Shared thesis compilation and final rendered-page inspection are delegated to the main agent to avoid concurrent builds. No standalone PDF was produced by this review.

First shared build succeeded. Numeric response rows passed the main agent's visual inspection. Corrected the baseline gender-choice overflow by placing the longer alternatives on a separate line, and selected the defined OMS/cmsy circle glyph explicitly to avoid the textcircled font-substitution warning. Final rebuilt-page check remains with the main agent.

User clarification supersedes the first pass: "should be failure to perfect, so reverse that scale". Current thesis wording now consistently distinguishes favorable-direction raw Performance from its once-reversed workload contribution. Historical/source snapshots, protocol records, Qualtrics, and live Overleaf were left unchanged. Current thesis search found no contradictory direction outside the three owned TeX files after this correction. Shared rebuild and visual confirmation remain with the main agent.

Main-thread integration verification: rebuilt the 190-page local thesis after the user's Failure-to-Perfect clarification. Final-pass compilation has no overfull boxes, undefined references or missing glyphs; numeric scales and categorical wrapping were visually inspected. Current artifact/build hashes are recorded in `development-results-20261023/build.json`. No full new PDF/UA conformance audit is claimed.
