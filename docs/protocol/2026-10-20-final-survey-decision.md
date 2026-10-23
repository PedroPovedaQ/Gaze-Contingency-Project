# Final-survey timing and protocol reconciliation

**Protocol proposal / user decision — October 20, 2026.** One practice trial precedes two counterbalanced 40-trial voice blocks. Retain a between-block rest break and immediate comfort/tracking checks. Remove the between-block questionnaire; administer the condition-specific questionnaires at the final survey after both blocks. Baseline demographics/SSQ and setup voice auditions remain before the task. **Qualtrics synchronization is on hold.** Runtime verification is recorded separately below; this is not study approval.

## Administration and scoring

After headset removal, collect the post-exposure SSQ/current-symptom check first. Then collect two separately labelled retrospective sets in Block 1 / Block 2 order, identifying the actual voice condition for each. Keep within-set item wording/order identical. Finish with comparative preference and open-first debrief. Condition scope is preserved; moving administration to the end does not merge ratings across voices.

| Instrument per referenced block | Numeric responses | Scoring |
|---|---:|---|
| NASA-TLX | 6 | Documented 0–10 adaptation; performance Perfect → Failure; individual dimensions and complete unweighted mean; no pairwise weighting |
| IMI | 24 | Interest/enjoyment 7, effort/importance 5, pressure/tension 5, value/usefulness 7; separate complete-subscale means with specified reverse keys |
| Guo agent perception | 21 | Q1–6 co-presence; Q13–18 intelligence; Q20–22 appealing/eerie/familiar individually; Q37–42 anthropomorphism |
| Custom ratings | 14 | Twelve separate 1–7 agreement items, liking 0–100 and guidance-use percentage 0–100; no composite |

This preserves **65 numeric responses per condition, 130 across both final sets**, plus technical checks, optional text, final SSQ and comparison/debrief. Pilot final-survey burden and whether participants distinguish the two blocks. Retrospective ratings may reflect recall/recency; do not describe them as immediate post-block measurements.

Source items/scoring are preserved from the side-thread documents, including [Guo et al. (2024), Appendix A Table A1](https://doi.org/10.1145/3651288). Four accent questions remain removed (own, preferred, perceived and similarity). Preferred/perceived gender and voice similarity remain. Keep setup liking separate from final condition-specific liking.

## Data provenance and implementation gaps

- Save `administration_phase=final`, actual survey timestamp, referenced block, actual voice ID/condition, condition order and instrument version separately. Baseline/final SSQ are session measures; setup ratings are audition measures. Missing stays missing.
- Repeating block-reference ratings in trial rows does not create independent per-trial observations. Do not silently mix raw 0–10 TLX with older 0–100 exports. **Implemented:** scoring retains native raw scale/mean and writes a separately labelled 0–100 rescaling. Six complete valid dimensions and explicit scale/performance direction are required. Missing metadata remains unscored.
- **Implemented locally (October 20):** one practice uses the first assigned voice (Order A preferred, Order B self-similar). The first measured trial starts from that practice target wall. The study schedule version is now `previous-wall-angle-v6-stratified-40-trials-one-practice`. The between-block survey gate is removed; the break remains. Study completion hands off to external final questionnaires. Development quick tests retain their legacy 0–100 in-headset TLX form, clearly outside study data. Wearer acceptance remains pending.
- Primary outcome: reveal-to-correct-trigger active time excluding logged pauses, adjusted for absolute exact pre-cue cross-to-target theta. Preserve signed theta; do not divide completion time by theta. Legacy nominal/post-onset theta is not equivalent.
- Planned angle allocation remains eight **trials** at each absolute nominal wall-angle category per block, not eight simultaneously visible objects per wall. The illustrative PNG retains the user's requested “8 objects at each θ” shorthand; use the protocol table for operational allocation.
- Planned vertical quota: 20 above / 20 below readiness-cross height per block. Realized quotas remain to be verified; do not claim existing randomized heights satisfy them.
- Gaze/controller contact, vicinity qualification, spoken confirmation and trigger completion are distinct. Motion totals must not bridge invalid tracking or pauses. Sample-size/power details, calibration acceptance, practice proficiency, timeout and stop thresholds remain unresolved.
- **Verified against code:** Section 3 now distinguishes four recent speech records and four transition records (15-second snapshot window) from the three-second gaze-bearing history. Unsupported object-memory/blink placeholders were removed, the citation wired, and the absolute wrong-direction guarantee qualified. Claude’s surrounding prose was preserved.

## Current sources and historical material

The current illustrated overview is [figure v9](../figures/experiment-procedure-overview-2026-10-20-v9.png), with [figure notes](../figures/README.md). Its typography, stimulus illustration and legend-padding refinements do not change the protocol.

The live [Overleaf project](https://www.overleaf.com/project/6a84bc097073254fbca6985b) was read before editing. Current local counterparts are in [overleaf](overleaf/): run sheet, questionnaire companion, scoring guide, study protocol and manuscript sections. The vector procedure figure is synchronized too.

Earlier annotated run sheets, themed PDFs, IRB submission/project exports and previous figure versions are historical drafts. They must not be used as current participant materials or regenerated over the reconciled sources. No submitted/approved packet is being amended here. Qualtrics was neither opened nor changed.
