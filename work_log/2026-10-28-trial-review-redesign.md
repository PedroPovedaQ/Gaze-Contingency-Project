# Trial Viewer Phase 2 — development UX verification

Spec: thesis coordination `trial-viewer-shots/DESIGN-PHASE2.md` (2026-10-28). Native sequential implementation, existing feature branch, local commits only. Existing progress.md work preserved and excluded from commits. No study protocol or headset behavior changes.

## Step 0

Added opt-in `--capture <directory>` with 24 fixed synthetic/development views. Capture startup ignores live/recording/intake flags, skips catalog/export/evaluation/polling workers, uses silent replay output, blocks input, and prevents intake/audio/live jobs even when their entry points are called directly. Intake fixtures have no folder; catalog fixture cannot save. Screenshots are the only task output of capture mode. Standard Unity process logs remain.

Baseline: `before/01-first-person.png` through `before/24-intake-ready.png`. After: `after/step0/` with the same names, and `1280/` for small-window captures. Baseline revealed an existing Unity shutdown disposal-order exception; capture now disposes owned render objects before quit. No live receiver processes were touched.

Existing 121 Python tests passed. New Unity capture isolation check passed alongside all existing build verification suites. Screenshots replace unit tests for styling/layout. Synthetic speech exists at 1.2s, not 3s, so the subtitles capture uses that actual saved interval without altering the fixture recording.

Local Vikunja service unavailable (connection refused); coordination board is the task progress record.

## Step 1

Rounded 9-slice controls, semantic colors, muted section labels, 14px body/20px titles and shared monospace styles. Capture evaluation toggle now visibly follows no-auto-evaluation semantics as well as bypassing workers.

Built and ran captures at 1440×900 and 1280×800; inspected dark/light screenshots. Existing Unity build verification suites passed. Screenshot files: `after/step1/01-first-person.png` through `24-intake-ready.png`, plus `1280/`.
