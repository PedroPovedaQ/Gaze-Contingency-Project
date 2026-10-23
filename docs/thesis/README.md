# Follow My Voice — thesis first pass

Advisor-review draft revised October 23, 2026. The editable document is `main.tex`; the compiled deliverable is `follow-my-voice-thesis-first-pass.pdf`.

## October 23 milestone

The author identifies this as the October 23 format-review draft; solid participant results are not yet available. The results framework, proposed analyses, and engineering evidence must not be represented as completed empirical findings. UCF's current guidance requires a near-final draft with most actual content present for format review; acceptance of this draft has not been established. Confirm its suitability with the advisor and thesis office, and complete the current Webcourse formatting/accessibility checks before submission. Official guidance: https://graduate.ucf.edu/thesis-and-dissertation/ (checked October 23, 2026).

## What this draft contains

Nine chapters: introduction, literature, system, proposed methodology, measurement/data provenance, planned analysis, engineering verification/results framework, discussion, and conclusion. Five appendices preserve questionnaire wording, data/scoring definitions, sequence-test scenarios, study-readiness decisions, and the code-inspected decision contract. Fourteen numbered figures match the reference thesis’s figure count: three reused project figures, ten new editable vector diagrams, and one actual Trial Review screenshot of its explicitly synthetic demonstration. No figure presents invented participant results. Sixteen numbered tables cover research-question traceability, related work, allocation, system ownership, questionnaire scoring, timing, analysis scope, policy comparisons, failure categories, evidence status, data definitions, sequence checks, remaining decisions, and reproduction requirements.

The literature chapter draws on the project's bibliography. All 63 entries are retained in `references.bib`; the printed reference list includes only cited entries. Quantitative participant findings, a final sample size, a power result, and a claim of superior voice performance have not been invented. Engineering audit results are attributed to the October 20 record rather than presented as tests run while writing this draft.

## Authoritative inputs

- Overleaf project: https://www.overleaf.com/project/6a84bc097073254fbca6985b
- Retrieved revision: `f9f49e450c7b64b34c319a557114f1fa4393f003`.
- `source-snapshot/` preserves the retrieved files unchanged. The live Overleaf working tree and manuscript were not overwritten.
- The latest full protocol handoff, the subsequent practice-voice clarification, and the October 20 implementation alignment audit informed status reconciliation.
- The October 23 working-file inspection is separately identified in Appendix E and `review/implementation-contract-inspection.json`; it does not establish deployment or supersede the release audit.
- Formatting base: the user's `Template_for_University_of_Central_Florida_ETD___LauLaTex_Modernization` download.
- Presentation reference: Hoang Bao Truong, *Using Inferences from Natural Language Narratives to Improve Video Question Answering*, UCF, 2026, https://stars.library.ucf.edu/gradstudies_etd_2026/199/ . Its prose and results were not used as evidence for this experiment.

## Preserved decisions

One practice trial uses the first assigned block's voice. Two counterbalanced measured blocks contain 40 trials each, with eight trials in each nominal absolute-angle category. Target identity is hidden; opening guidance precedes reveal; the right trigger completes selection. Questionnaires refer retrospectively to each block and are administered after both. Qualtrics remains on hold.

The proposed exact pre-cue geometry snapshot and 20-above/20-below allocation remain flagged as implementation gaps in the aligned release. Left/right randomization is not mislabeled as a guaranteed four/four balance. Eye-ray contact, gaze-vicinity acknowledgment, controller acknowledgment, and trigger selection remain distinct. Native 120 Hz acquisition and perceptual timing have not been claimed as validated.

## Build

Use **LuaLaTeX**, not pdfLaTeX or Tectonic, with a current TeX Live distribution supporting `DocumentMetadata`, tagged PDF, and the supplied class. Select `main.tex` as the main document. Run:

```sh
./build.sh
```

The script runs LuaLaTeX, BibTeX, and two additional LuaLaTeX passes. `LUALATEX` and `BIBTEX` can specify executable paths. Dependencies include fontspec, unicode-math, TeX Gyre Termes/Termes Math, latex-lab/tagpdf/luamml, setspace, etoolbox, graphicx, hyperref, amsmath, booktabs, array, longtable, tabularx, xurl, and enumitem.

The delivered PDF was compiled with LuaHBTeX 1.24.0 / TeX Live 2026. The built-in editor compiler was attempted but its older LaTeX kernel could not process `DocumentMetadata`; the successful build used a separate local LuaLaTeX toolchain. No system-wide TeX replacement was performed.

Two compatibility changes were made only to the copied `UCF_ETD.cls`: the obsolete `sec-add-grouping` key is used only if present; chapter star/optional-argument dispatch avoids the new latex-lab `secdef` interception, preserving UCF headings and front-matter numbering. Original template files are available in the user's Downloads. TOC depth is limited to subsections. Uppercase chapter entries, indented section levels, dotted leaders, Roman front-matter numbering, Arabic body numbering, and separate lists of figures and tables follow the supplied class and reference presentation. Figure-list entries use concise titles; appendix longtable entries receive the same Table prefix as ordinary tables. Widow/orphan controls are set in `main.tex`.

The PDF is tagged, but successful compilation and its PDF/UA metadata are **not** an accessibility or UCF-format certification. Final heading structure, reading order, alternative text, tables, and institutional requirements still need formal validation.

## Review and remaining work

Claude performed separate language and methods reviews. A further verified Opus 5.5 pass produced 99 synthesized paragraph revisions and three source-grounded literature additions; see `review/opus55-language-synthesis-20261023.md`. The revised PDF contains 148 pages, 14 figures, and 16 tables. `review/review-resolution.md` records applied changes, rejected false positives, and open design decisions. The raw review reports are included. `review/truong-gap-audit.md` records the reference-comparison additions and elements that remain author-dependent or require empirical evidence. The reviews are assistance, not author or committee approval.

Before submission: confirm author/degree/term/advisor metadata; finish power and analysis preregistration; reconcile consent with actual cloud services; validate hardware, timing, scoring, and the complete study flow; collect and analyze participant data; replace the results framework with observed findings; perform a complete source-to-claim citation audit; finalize AI disclosure and ETD/accessibility review. All pending items remain visible in the text.
