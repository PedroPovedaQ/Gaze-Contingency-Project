# Follow My Voice: Assisted Gaze-Contingent Search with a Self-Similar Agent in Virtual Reality

Advisor-review draft revised October 23, 2026. The editable document is `main.tex`; the compiled deliverable is `follow-my-voice-thesis-first-pass.pdf`.

## Overleaf visual editing copy

The October 23 thesis was imported into [the full-thesis Overleaf project](https://www.overleaf.com/project/6adbf46b3f96e54f5b3cc805) for prose editing in Visual Editor. It uses `main.tex`, LuaLaTeX, TeX Live 2026, and automatic compilation. The original protocol project below is unchanged. The active Codex automation **Sync Overleaf thesis to local** checks every 10 minutes and pulls changes into this checkout. It updates sources and figures, then automatically rebuilds the local PDF when the source content changes. A failed compile preserves the last good PDF and is retried on the next check. It does not push local changes to Overleaf or commit/push GitHub changes. See [sync workflow](overleaf-sync.md) for conflict handling and a manual pull.

## October 23 milestone

The author identifies this as the October 23 format-review draft; solid participant results are not yet available. The results framework, proposed analyses, and engineering evidence must not be represented as completed empirical findings. UCF's current guidance requires a near-final draft with most actual content present for format review; acceptance of this draft has not been established. Confirm its suitability with the advisor and thesis office, and complete the current Webcourse formatting/accessibility checks before submission. Official guidance: https://graduate.ucf.edu/thesis-and-dissertation/ (checked October 23, 2026).

## What this draft contains

Six chapters: Introduction, Literature Review, Methodology, Results, Discussion, and Conclusion. Methodology contains application design, experimental design, procedure, data collection, measures/scoring, and planned statistical analysis. Source files 04-method, 05-measurement, 03-measures-scoring and 06-analysis are included sections of Chapter 3, not standalone chapters. The six appendices preserve detailed instruments, definitions, tests, readiness decisions and implementation evidence. Existing figures and tables are retained, with the recorded scene caption shortened and a condensed-CSV table added. Participant results remain pending; development recordings are not participant-study results.

The literature chapter draws on the project's bibliography. All 69 entries are retained in `references.bib`; the printed reference list includes only cited entries. Quantitative participant findings, a final sample size, a power result, and a claim of superior voice performance have not been invented. Engineering audit results are attributed to the October 20 record rather than presented as tests run while writing this draft.

## Research focus

Self-similar synthetic voice versus a participant-selected neutral voice is the focal empirical comparison. Neutral is the condition label; participants choose that voice from a supplied catalog, and the label does not establish affective neutrality. Gaze guidance, Jev, memory and Trial Review are supporting apparatus and validity checks, not additional participant factors or coequal outcome claims. The supplied Yang et al. AR-navigation preprint adds a bounded interface-versus-mechanism comparison; it does not supply evidence about self-similar voice.

The introduction and discussion now carry the practical AR/VR search rationale through to the self-similar voice comparison and its transfer limits. See [motivation and source notes](review/practical-motivation-and-voice-throughline-20261023.md).

The six-paper full-text synthesis strengthens the self-similar voice rationale and distinguishes sonification, spatial audio, and spoken guidance. See [reading and design notes](review/audio-guidance-and-self-voice-20261023.md). No result from those papers is presented as an observed outcome of this study.

RippleVision is included as abstract-supported visual-guidance context; full-text review remains pending. See `../literature/ripplevision-2026.md` for verified metadata and transfer boundaries.

## Authoritative inputs

- Overleaf project: https://www.overleaf.com/project/6a84bc097073254fbca6985b
- Retrieved revision: `f9f49e450c7b64b34c319a557114f1fa4393f003`.
- `source-snapshot/` preserves the retrieved files unchanged. The live Overleaf working tree and manuscript were not overwritten.
- The latest full protocol handoff, the subsequent practice-voice clarification, and the October 20 implementation alignment audit informed status reconciliation.
- The October 23 working-file inspection is retained in `review/implementation-contract-inspection.json`; it does not establish deployment or supersede the release audit.
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

The PDF is tagged. The October 23 accessibility pass uses veraPDF 1.30.3 against PDF/UA-2, structural inspection, and sampled visual/reading-order checks; see `review/accessibility-validation-20261023.md` for the exact artifact and limitations. An automated pass is not UCF-format approval or a claim that every assistive-technology workflow has been tested.

## Review and remaining work

Claude performed separate language and methods reviews. A further verified Opus 5.5 pass produced 99 synthesized paragraph revisions and three source-grounded literature additions; see `review/opus55-language-synthesis-20261023.md`. That language-review snapshot contained 148 pages. The subsequent voice-focus revision reorganized figures and tables and moved engineering material to a supporting appendix; Appendices D–F are now excluded from the manuscript at the author’s request; see `review/voice-focus-and-navigation-context-20261023.md` and the current verification record. `review/review-resolution.md` records applied changes, rejected false positives, and open design decisions. The raw review reports are included. `review/truong-gap-audit.md` records the reference-comparison additions and elements that remain author-dependent or require empirical evidence. The reviews are assistance, not author or committee approval.

Before submission: confirm author/degree/term/advisor metadata; finish power and analysis preregistration; reconcile consent with actual cloud services; validate hardware, timing, scoring, and the complete study flow; collect and analyze participant data; replace the results framework with observed findings; perform a complete source-to-claim citation audit; finalize AI disclosure and ETD/accessibility review. All pending items remain visible in the text.

The [narrative revision](review/narrative-architecture-20261023.md) connects practical search assistance to the self-similar versus neutral-voice question throughout the abstract and chapters.

The [Claude narrative review](review/claude-narrative-review-20261023.md) records 18 applied suggestions, structural synthesis, and remaining methods questions, with exact before/after edits.

## Current terminology and checklist review

The condition labels are **neutral voice** and **self-similar voice**. Participants select the neutral voice from the supplied catalog. This label does not imply flat emotional delivery or no individual preference. Historical Overleaf snapshots retain their original wording.

The supplied UCF Manuscript Submission Checklist was included in review. Confirmed table-header, chapter-heading and longtable-caption structure issues were fixed, and the bibliography count was reconciled. See `review/pdf-structure-check-20261023.json`, `review/accessibility-validation-20261023.md`, and `review/verification.md`. The independent full CE review and formal automated PDF/UA validation are recorded separately from institutional acceptance.

## Latest full-paper language pass — October 23, 2026

[Claude Opus 5.5 review](review/claude-full-language-summary-20261023.md) covers all current chapters and appendices against the approved abstract. The 174-page PDF includes the accepted wording improvements and passes the recorded build, preservation, structural and automated PDF/UA-2 checks. [Exact verification](review/claude-full-language-verification-20261023.json) identifies this artifact.

## Recorded development results

The Results chapter now includes four plots from actual headset Runs 20–28: search durations, software speech delivery, a recorded delayed gaze-confirmation sequence, and application-reported gaze usability. These development observations remain separate from the pending self-similar/neutral voice comparison. [Data provenance and reproduction](development-results/README.md).
