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

## Step 2

Header now offers the existing OpenIntake entry directly, warning-outlined provenance, dataset breadcrumb, semantic status pill, File menu, and theme icon. Existing live/detached actions retained. Added 25-file-menu.png to captures.

Built and ran captures at 1440×900 and 1280×800; inspected dark/light screenshots. Existing Unity build verification suites passed. Screenshot files: `after/step2/01-first-person.png` through `24-intake-ready.png`, plus `1280/`.

## Step 3

Compact camera segment, Layers checkbox drawer (26-layers.png), timestamped missing-pose banner, and anchored provenance caption. Verified Menlo after SF Mono failed to render; no font errors in recapture. Read thesis Step 1/2 feedback; header icon/status/menu and scrollbars will be refined with the next layout step.

Built and ran captures at 1440×900 and 1280×800; inspected dark/light screenshots. Existing Unity build verification suites passed. Screenshot files: `after/step3/01-first-person.png` through `24-intake-ready.png`, plus `1280/`.

## Step 4

Trial cards show mono number/duration, angle and semantic outcome; selected trial retains a 3px gold bar. Search placeholder respects focus with a clear control (27-trial-search.png). Applied thesis header feedback (no duplicate provenance, legible status, text theme control, outlined menu with Escape/outside dismissal) and themed 8px scrollbars. Fixed clipped theme text in a second capture pass.

Built and ran captures at 1440×900 and 1280×800; inspected dark/light screenshots. Existing Unity build verification suites passed. Screenshot files: `after/step4/01-first-person.png` through `24-intake-ready.png`, plus `1280/`.

## Step 5

Single-row Cursor/Events/Final/Jev/Eval tabs with selected underline and stable inspector width. Agent speech is above the fold; cursor cards collapse with saved preferences; numeric metrics use Menlo and unavailable values are muted italic. Final/Jev/Eval card surfaces retain legacy/missing provenance. Added 28-cursor-details.png and 29-cursor-collapsed.png. Read theta legibility feedback; proportional theta label will be included with transport typography.

Built and ran captures at 1440×900 and 1280×800; inspected dark/light screenshots. Existing Unity build verification suites passed. Screenshot files: `after/step5/01-first-person.png` through `24-intake-ready.png`, plus `1280/`.

## Step 6

Grouped neutral playback, mono clocks, speed, samples, scope, export and audio controls. Event labels offset from markers, ellipsized or omitted when crowded; boundaries and subtle selected-trial shading retain gaze gaps. Added shortcut popover (30-shortcuts.png). Actual Unity GUI handler checks pass for Space, held-key guard, arrows, text/modal guards, cross-trial drag/clamp/release. Applied theta font and compact selected Jev-row feedback; 31-jev-alternatives.png verifies probability bars and frozen-state provenance. Capture resets its renderer after input checks to isolate screenshots from exercised seek caches.

Built and ran captures at 1440×900 and 1280×800; inspected dark/light screenshots. Existing Unity build verification suites passed. Screenshot files: `after/step6/01-first-person.png` through `24-intake-ready.png`, plus `1280/`.
