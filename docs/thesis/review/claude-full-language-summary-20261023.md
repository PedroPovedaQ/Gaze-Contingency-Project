# Full-paper Claude language review — 23 October 2026

## Scope and provenance

Three completed Claude CLI review batches used `claude-opus-5-5`, verified in each response’s modelUsage. They covered main.tex, all ten chapter source files (six compiled chapters), six appendices, and the abbreviations list. Each received the complete abstract, current project context and constraints, and its assigned complete source files. The abstract, acknowledgments and title were reference/protected material. No external papers or new evidence were added.

An initial single full-manuscript request through the older CLI did not return a result and was terminated. No changes came from that attempt. The completed batches ran sequentially using the newer CLI, with tools, MCP, hooks and session persistence disabled. The raw completed responses and accepted replacements are retained here.

## Changes

Accepted 47 of 52 suggested replacements, including ten locally synthesized replacements to preserve precision or improve readability. Four further small edits clarify scheduler terminology, correct an obsolete plural reference to methods chapters, and identify the two-second delay discussion as development evidence. Exact replacements are recorded in the three review JSON files and the synthesis JSON.

- Improved sentence flow without merging separate technical ideas indiscriminately.
- Replaced internal drafting or engineering shorthand with concrete descriptions.
- Kept self-similar versus participant-selected neutral voice as the central comparison.
- Used present tense for implemented task descriptions while preserving conditional and future language for unperformed analyses.
- Preserved the distinction between Jev deferral and network waiting, non-target selection and trigger pressing, and unhelpful guidance and merely changed state.

## Rejected suggestions and unresolved matters

Rejected changing the methodology section reference to Chapter: the label correctly resolves to section 3.2. Also retained the existing reported-policy wording, two unnecessary new cross-references, and the advisory wording about precision and bias rather than making an additional methodological commitment. Source labels prefixed ch: can correctly refer to sections after the methodology consolidation. XR is defined in the abbreviations list; Jev is defined in Methodology.

Broader repetition between Data Collection and Measures and Scoring remains a possible structural edit, outside this language pass. Readiness, sample size, final stimulus configuration, validation and consent remain study decisions, not problems that wording can resolve. The review is advisory language assistance, not scientific or institutional approval.

## Representative edits

**Before:** so that technical faults do not masquerade as effects of self-similar voice

**After:** so that technical faults are not mistaken for effects of self-similar voice

**Before:** so a sequence can contain several Jev waits without creating several independent missed confirmations.

**After:** so a sequence can include several decisions by Jev to wait without creating several independent missed confirmations.

**Before:** The unweighted mean requires all six.

**After:** The unweighted mean is calculated only when all six ratings are present.

**Before:** Together, these checks qualify the supporting apparatus, while

**After:** Together, these records describe the supporting apparatus and its current limits, while

## Verification

Build, protected-content comparison, PDF structure, and automated PDF/UA-2 results are recorded in `claude-full-language-verification-20261023.json` after the final build. This does not imply a fresh exhaustive assistive-technology usability review. No runtime or Qualtrics changes; no commit or push requested.

Final rebuilt PDF: 174 pages, no unresolved citations/references or overfull boxes, and zero automated PDF/UA-2 failures (483,477 checks passed). Source comparison confirms protected abstract, acknowledgments, citation commands, math, headings, labels/references, figures, tables, quoted strings and identifiers unchanged. Visually checked PDF pages 28, 49, 105 and 132. Delivery PDF and editable source archive refreshed.
