# Reference comparison and applied gaps — October 23, 2026

Reference: Hoang Bao Truong, *Using Inferences from Natural Language Narratives to Improve Video Question Answering* (UCF, 2026), [repository record](https://stars.library.ucf.edu/gradstudies_etd_2026/199/), [PDF](https://stars.library.ucf.edu/cgi/viewcontent.cgi?article=1033&context=gradstudies_etd_2026).

Reviewed the supplied local PDF (`816b2027-2604-406c-adee-07cf38b030e5.pdf`), including its contents, experimental setup, diagnostic analysis, conclusion, and appendices. Page references below are printed thesis pages, not PDF viewer indices. This is a structural comparison, not a source of evidence about our participants or headset.

| Reference practice | Gap in the preceding first pass | Applied change |
|---|---|---|
| RQs introduced in Chapter 1 and explicitly revisited in Chapter 4 and conclusion (pp. 5–6, 54 onward, 87 onward) | Questions and analysis existed, but lacked stable identifiers and a compact evidence map. | Added RQ1–RQ3 and separate engineering question EQ1; a traceability table; aligned method/results/conclusion references. No directional hypothesis was added. |
| Comparative literature tables in Chapter 2 | Narrative review existed but did not make transferred methods and nontransferable conclusions easy to compare. | Added five-strand comparison table using the already cited literature and explicit transfer limitations. |
| Dataset characterization, gold subset, and explicit separation of development cases from evaluation (pp. 24–31, 51–53) | Sequence scenarios existed; an untouched evaluation set and judge calibration procedure were not specified. | Added a proposed whole-session/participant split, exclusion of already inspected cases from held-out claims, human-reviewed rubric calibration, and reporting of unresolved judgments. No dataset size or agreement score is invented. |
| Baseline/advanced architecture and controlled comparisons (pp. 45–53) | Current architecture and future pilots existed; candidate-set size and decision autonomy were not clearly separated. | Added three prospective comparison roles and held-constant inputs, repeated requests, execution-order controls, and exact candidate-count logging. Existing 2–5-candidate wrapper limit is explicit. |
| Operational conditions and retained prompt templates (pp. 51–53; appendices beginning p. 97) | Diagrams existed, but the actual decision contract and reproduction requirements were only summarized. | Added Appendix E with inspected phrase instructions, state/history fields, constructed response, local admission pseudocode, and a release-manifest table. Three inspected working files are hashed; this is not a deployment claim. |
| Error taxonomy and illustrative mechanisms (pp. 64–69) | Failure discussion lacked a compact classification linking causes to required evidence. | Added six categories and three explicitly constructed sequence diagnoses. Proposed versus played speech and gaze versus controller confirmation stay distinct. |
| RQ-specific conclusions and future directions (pp. 87 onward) | Conclusion summarized the system but did not report the status of each question. | Added question-by-question status and the next empirical step, with no fabricated answers. |
| Dedication and acknowledgments (pp. iv–v) | Personal text is absent. | Left for author input rather than inventing gratitude, advisor names, or relationships. |
| Quantitative dataset distributions, baseline scores, error rates, and statistical findings | Our confirmatory participant dataset and frozen comparative pilot evidence are not supplied. | Kept result requirements and reporting structure; did not manufacture charts, scores, participant quotations, or achieved effects. |

## Already present and retained

UCF-style front matter and contents hierarchy, separate lists of figures/tables, fourteen numbered figures, detailed apparatus/methods, math and event definitions, planned clustered analysis, instrument appendix, readiness decisions, ethical/data handling discussion, limitations, and future work. The Trial Review capture is an actual application screenshot of its synthetic demonstration, not participant evidence.

## Limits of transfer

The benchmark thesis evaluates a different task and dataset. Its model assumptions, conclusions, prompt content, and numeric results were not copied into this experiment. Inference-only requests do not eliminate stochastic or service-load variation in our proposed comparison. Existing research citations support the manuscript's methodological connections; a complete source-to-claim citation audit remains a submission requirement.

Live Overleaf, its Section 3 edits, the experimenter run sheet, runtime source, and Qualtrics were not changed by this document pass. The October 20 alignment audit and October 23 working-file inspection remain separate evidence snapshots.
