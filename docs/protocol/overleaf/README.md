# Current protocol and survey documents

**Implemented / October 30 local schedule alignment:** v8 uses 32 trials per block, 64 total, four per signed step, two above/two below per step, and all 24 identities plus eight repeats. Local source counts were updated; existing PDFs/figures and earlier narrative remain historical snapshots pending separate packet reconciliation. Nothing was published to Overleaf. See [current allocation](../../balanced-angle-schedule.md).

**Protocol proposal — October 20, 2026. Qualtrics on hold.** Sources were reconciled from the live [Overleaf project](https://www.overleaf.com/project/6a84bc097073254fbca6985b), preserving collaborator prose. See [decision and implementation gaps](../2026-10-20-final-survey-decision.md).

- `experimenter-run-sheet.tex`: one practice trial in the first assigned voice, two 32-trial blocks, rest between blocks, final-only condition-specific questionnaires.
- `survey-questions-by-block.tex`: participant items; “block” means the referenced block, not administration timing. Final survey has two retrospective sets, 65 numeric responses per set.
- `survey-instruments.tex`: scoring, raw ranges, source attribution and missingness.
- `study-protocol.tex`, `section-04-experiment.tex`, `section-05-planned-analysis.tex`, `section-06-contributions-limitations.tex`, `rough_draft.tex`: current method, analysis and interpretation.
- `study-procedure-current.svg` / `.pdf`: vector procedure figure with one practice and final-only questionnaires.

Section 3 and its aggregate copies use `references.bib` with plain BibTeX; the printable `bib.tex` wrapper uses BibLaTeX/Biber. The library contains 62 unique entries. Each TeX file is standalone. Documents using the procedure figure require the neighboring PDF when compiled. The old Markdown formatter and older themed/Word/PDF editions are historical; do not run them over these reconciled sources. The runtime practice/survey gates now follow this sequence locally; wearer acceptance remains pending. See the alignment report for remaining geometry and analysis gaps. No Qualtrics synchronization or approval/deployment claim is implied.
