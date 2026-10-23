# PDF accessibility verification — October 23, 2026

## Scope and tools

This record covers the current thesis PDF, not institutional acceptance or the completeness of the empirical study. veraPDF 1.30.3 uses the **PDF/UA-2 + Tagged PDF** profile (ISO 14289-2:2024). The validator was downloaded from the official software.verapdf.org release service and run locally with Unity's bundled OpenJDK 17. No manuscript upload was needed. [Validator documentation](https://docs.verapdf.org/cli/validation/).

The PDF was opened in Acrobat and its document title, selectable text, and chapter/bookmark navigation were inspected. Acrobat's automated Accessibility Checker could not be operated reliably through the available UI automation; no Acrobat-checker pass is claimed. veraPDF supplies the formal automated results below.

## Defects resolved

- Added semantic chapter headings and column headers in all tables.
- Moved longtable captions outside table grids, removing merged-header representations; caption links resolve to their actual pages.
- Made five implicit CID-to-glyph identity maps explicit in three vector diagram PDFs. The PDF default interpretation is unchanged, but PDF/UA-2 requires the map entry.
- Preserved imported PDFs' original transparency groups rather than allowing LuaTeX's graphics driver to append a second Group dictionary.
- Darkened gold text/essential geometry from #c89416 to #8a630a in starting-geometry, height-reference and latency-definition. Calculated sRGB contrast is 5.43:1 on white and 4.79:1 on the pale-gold fill, compared with 2.73:1 and 2.41:1 previously.

The font-only repairs rendered pixel-identically at 120 dpi in all three affected figures. Re-running the normalizer over all twelve vector figure PDFs changed zero files and preserved their SHA-256 hashes. A full 163-page before/after render comparison showed only figure-region raster differences after removal of duplicate transparency groups; page layout and text remained unchanged. The contrast revision intentionally changes three diagrams' accent color.

## Manual and structural checks

- Document title, en-US language, DisplayDocTitle, marked content, named destinations and bookmarks inspected.
- All 14 figures have meaningful alternatives; surrounding prose and captions describe their role and the limits of schematic/synthetic evidence.
- All 17 table structures have headers: 16 numbered tables and one unnumbered administration table. No merged cells; captions are separate Caption structures.
- Chapter/section hierarchy, front matter, prose, representative figure pages and all five longtable opening/caption pages were visually inspected. Reading-order spot checks use extracted text and the tag hierarchy for prose and table rows; this is not a complete screen-reader user study.
- All fonts embedded; no unresolved references/citations, LaTeX warnings or overfull boxes in the final build.
- Semantic colors also have text, shape, or positional descriptions. Body text is black on white; the identified pale gold labels were corrected. This is a sampled visual/contrast audit, not an exhaustive WCAG certification.

## Exact final artifact

Final validator receipt, artifact hash and build metrics are recorded in `accessibility-validation-20261023.json` and `verapdf-ua2-final-20261023.json`. The pre-fix report is retained separately. Do not transfer this pass to a later rebuilt or edited PDF without rerunning validation.

## Reproduction

Build with `./build.sh` in the thesis directory using current LuaLaTeX. Run `verapdf --flavour ua2 --format json follow-my-voice-thesis-first-pass.pdf`. Figure regeneration additionally requires CairoSVG 2.9.1 and pypdf 6.21.2; see figures/README.md. A validator pass establishes its machine-checkable profile requirements only. Author metadata, study results, complete citation auditing, final assistive-technology review, and UCF approval remain separate responsibilities.
