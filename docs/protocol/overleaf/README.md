# Overleaf run sheet

**Protocol proposal / annotated review edition, September 22, 2026.** This is a formatted edition of the [annotated source](../experimenter-run-sheet.md). It preserves the source's spoken lines, actions and meeting notes; it does not resolve the methodological conflicts documented in the [reconciliation](../../meeting-reconciliation-2026-09-22.md).

- [Overleaf project](https://www.overleaf.com/project/6a84bc097073254fbca6985b): open `experimenter-run-sheet.tex` and recompile it as a standalone document. The manuscript remains a separate file.
- [Editable standalone LaTeX](experimenter-run-sheet.tex): all styling and the seated 360-degree TikZ diagram are embedded; no external fonts, figures or bibliography are needed.
- [Rendered review PDF](../../../output/pdf/experimenter-run-sheet.pdf): eight pages, with dark spoken text, italic muted-gold actions, highlighted meeting notes, session fields and page numbers.

Regenerate from the Markdown source:

```sh
python3 scripts/format-overleaf-run-sheet.py
tectonic docs/protocol/overleaf/experimenter-run-sheet.tex --outdir output/pdf
```

Overleaf's eight-page output was verified after recompiling from scratch, which removed the manuscript's stale bibliography-cache errors. The local verification used Tectonic because `pdflatex` was unavailable. Local text coverage and every rendered page were checked; no overfull boxes were reported. The source's generic/dwell wording remains visible with a clear cover note describing the newer preferred-voice/controller requirements. #27 still owns the reconciled operator protocol and synchronized final editions.
