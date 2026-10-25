# Overleaf-to-local thesis sync

Implemented October 23, 2026. Overleaf project: https://www.overleaf.com/project/6adbf46b3f96e54f5b3cc805

The Codex heartbeat automation `sync-overleaf-thesis-to-local` checks every 10 minutes using existing Overleaf Git authentication. It requires this Mac and Codex to be available, plus a working network and credential. This is periodic one-way source sync, not keystroke-level synchronization.

Destination: `/Users/pedro.poveda/.codex/worktrees/thesis-main-sync/Gaze Contingency Project/docs/thesis`.

## Manual pull

From this repository:

```sh
python3 scripts/sync-thesis-overleaf.py
```

Only main.tex, references.bib, the two UCF class/accessibility files, and document/image files inside chapters, appendices, frontmatter and figures are eligible. Review logs, build scripts, source snapshots, Unity files, and generated root PDFs are excluded. No push or automatic commit occurs. After a clean sync, the importer builds a separate snapshot with LuaLaTeX, BibTeX, and two further LuaLaTeX passes when its source fingerprint differs from the last successful build. It also rebuilds if a published PDF is missing or altered. It uses the installed TeX Live toolchain at `~/.cache/ucf-thesis-tex/TinyTeX/bin/universal-darwin`, with `LUALATEX`/`BIBTEX` overrides supported. Overleaf's preview recompiles independently.

## Conflict and recovery behavior

The original uploaded ZIP established the baseline. Local-only edits are preserved. Identical edits are accepted. If both copies diverge on a file, the entire pull stops before applying that snapshot. Remote conflict versions are saved for review. The sync never inserts merge markers or silently chooses a winner. Resolve conflicts deliberately; do not reset the baseline to bypass protection.

Status, mirror, baseline, and backups live at `~/.local/share/follow-my-voice-overleaf/`. `status.json` records the remote revision, changed files, conflicts, or error. Existing files are backed up before replacement or removal. Backups are retained. Successful builds atomically replace each of `main.pdf` and `follow-my-voice-thesis-first-pass.pdf`; previous copies are retained in `pdf-backups/`. Build failure, incomplete PDF output, unresolved references/citations, or a source edit during compilation preserves the previous PDFs. `build.json` records success or failure and the successful source fingerprint; `rebuild.log` contains compiler diagnostics. Failed builds retry on the next scheduled check even when no new Overleaf revision arrives. During compilation or after failure, the PDF can lag the sources. This compile check is not a fresh PDF/UA or thesis-format compliance certification. Concurrent source editing during the short write operation should be avoided; a second file check detects changes before each write but does not lock external editors.

Pause or delete the named automation in Codex to stop recurring pulls. This does not remove local or Overleaf files. Before making thesis edits locally, run a pull first and check for conflicts. Local revisions will need an explicit reconciliation/upload to appear in Overleaf.

## Verification

Fourteen automated tests cover incoming/local/identical/divergent edits, deletions, new-file collisions, path restrictions, symlink rejection, atomic writes, backup recovery, idempotent pulls, conflict snapshots, isolated PDF publishing, unchanged-build skipping, failed-build retries, and PDF preservation on invalid output or concurrent editing. The first authenticated pull returned `up_to_date` at remote revision `2e29c5e0860805d7df4319e9431573d66bed4b59`.
