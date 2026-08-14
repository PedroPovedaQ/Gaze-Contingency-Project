---
name: setup-worktree-environment
description: Set up disk-efficient dependencies for a Gaze Contingency Project Git worktree by using the repository's shared uv environment and, on macOS, an isolated APFS copy-on-write Unity Library clone. Use when creating or entering a worktree, installing Python analysis dependencies, reducing duplicated virtual-environment storage, or preparing a Unity worktree without rebuilding or fully copying its Library cache.
---

# Set Up a Worktree Environment

Configure the current checkout with the repository's checked-in environment helpers. Preserve independent writable state while reusing immutable or copy-on-write storage.

## 1. Check the checkout

Resolve the repository root with `git rev-parse --show-toplevel` and run all commands from it.

Require these executable helpers:

- `scripts/sync-python-env.sh`
- `scripts/seed-unity-library.sh`
- `scripts/test-worktree-environments.sh`

Stop and report the missing helper if any is absent. Do not recreate helper logic inside the skill.

Inspect `git status --short` before setup. Generated `.venv`, `Library`, and `Temp` state is ignored, but preserve every tracked or unrelated untracked change.

## 2. Set up Python

Run:

```bash
scripts/sync-python-env.sh
```

This returns the absolute shared environment path. The helper keys the environment by `uv.lock`, Python implementation/version, operating system, architecture, and ABI under Git's common directory. Let uv own its cache and environment lock; do not copy or symlink a virtual environment between worktrees.

Verify reuse without syncing again:

```bash
scripts/sync-python-env.sh --print-only
```

Report the selected path and `du -sh` result. Use `scripts/run-analysis-pipeline.sh` for analysis because it automatically selects this environment.

## 3. Set up Unity when applicable

Treat the checkout as a Unity project only when `Assets`, `Packages`, and `ProjectSettings` exist.

If `Library` already exists, leave it unchanged and report that Unity setup was already present. Never replace, merge, delete, or symlink a writable `Library`.

If `Library` is missing:

1. Use a source path explicitly supplied by the user as the sole candidate. Otherwise inspect paths from `git worktree list --porcelain`.
2. Apply every check below to every candidate, including an explicitly supplied path. Keep only candidates that are different from the target and have:
   - a `Library` directory;
   - the same `ProjectSettings/ProjectVersion.txt`;
   - the same `Packages/packages-lock.json`;
   - no `Temp/UnityLockfile`.
3. If an explicit source fails a check, stop and name the mismatch. Otherwise use the sole compatible candidate. If several automatically discovered candidates remain, ask the user which source to use. If none remain, let Unity build a fresh target `Library` and explain why cloning was skipped.

Seed a compatible closed checkout with:

```bash
scripts/seed-unity-library.sh --source "/absolute/path/to/source-checkout"
```

The skill preflight verifies Unity version and package-lock compatibility. The helper verifies macOS, APFS, same-volume placement, Unity project shape, a closed source Editor, and an absent target `Library`. It clones through a temporary sibling and publishes only a completed copy.

Do not open two Unity Editors against the same project path. Do not automatically install or enable Unity Accelerator; mention it only as an optional shared import cache when APFS cloning is unavailable or many machines need the same imports.

## 4. Verify and report

Run the deterministic helper integration test:

```bash
scripts/test-worktree-environments.sh
```

Then report:

- the shared Python environment path and whether `--print-only` succeeded;
- whether Unity `Library` was reused, cloned, skipped, or already present;
- the selected Unity source, if any;
- verification results and any unsupported filesystem or open-Editor blocker;
- tracked working-tree changes, if setup exposed any.

Do not claim that `du` shows physical APFS savings; cloned blocks may appear in each directory's logical size. Do not prune old shared environments or caches unless the user explicitly requests cleanup and no worktree is using them.
