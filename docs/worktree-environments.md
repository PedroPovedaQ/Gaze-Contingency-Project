# Worktree Environment Storage

This repository keeps generated Python and Unity state out of Git while minimizing physical disk duplication across worktrees.

## Python dependencies

The root `pyproject.toml` and `uv.lock` are the dependency source of truth. Run:

```bash
scripts/sync-python-env.sh
```

The script creates a content-addressed environment under the repository's Git common directory. All linked worktrees share that directory, so worktrees using the same `uv.lock` and compatible Python interpreter reuse one environment. A dependency, Python version, operating system, architecture, or ABI change selects a new environment, so incompatible worktrees remain isolated. Package files are installed from uv's global cache using copy-on-write on macOS. uv also locks the target environment while it installs, so simultaneous sync commands do not modify it concurrently.

The analysis runner syncs and uses the same environment automatically:

```bash
scripts/run-analysis-pipeline.sh
```

Useful maintenance commands:

```bash
uv cache dir
uv cache size
uv cache prune
```

`uv cache prune` cleans reusable package artifacts; it does not remove the shared project environments stored under Git's common directory. Those environments disappear when the repository is removed and can be deleted when no worktree is running Python if an old lockfile environment is no longer needed. Do not edit files inside uv's own cache manually.

## Unity dependencies and imported assets

Unity Package Manager already keeps downloaded registry and Git packages in its global per-user cache. On macOS its default root is `$HOME/Library/Unity/cache`; no worktree setup is required.

Unity's project `Library` directory is different: it contains mutable imported artifacts and editor state. Do not symlink one writable `Library` into multiple worktrees or open multiple Editors against the same `Library`.

On macOS/APFS, seed a new worktree from a closed, compatible Unity checkout with:

```bash
scripts/seed-unity-library.sh --source /absolute/path/to/source-checkout
```

The command requires the source Editor to be closed, the target worktree to have no `Library` directory, and both projects to be on the same APFS volume. It clones into a temporary directory and publishes the result only after the copy succeeds. Unchanged blocks initially share physical storage while every worktree retains its own logically separate `Library`.

Use a source checkout with the same Unity version, package lock, platform, and relevant project settings. If those inputs differ, let Unity rebuild the target `Library` instead.

For faster imports across many worktrees or machines, install Unity Accelerator and enable it under **Unity > Settings > Asset Pipeline**. Use a namespace that includes this project and Unity version, such as `GazeContingency_6000.3`. Accelerator reduces reimport work but does not make a shared writable `Library` safe.
