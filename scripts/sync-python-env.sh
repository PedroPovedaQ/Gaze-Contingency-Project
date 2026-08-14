#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOCK_FILE="${ROOT_DIR}/uv.lock"
UV_BIN="${UV_BIN:-uv}"
PYTHON_REQUEST="${PYTHON_BIN:-}"
SYNC=1

usage() {
    cat <<'EOF'
Usage: scripts/sync-python-env.sh [--print-only]

Creates or updates a content-addressed Python environment under Git's common
directory and prints its absolute path. Worktrees with the same lockfile and
Python version share this environment safely.

Environment variables:
  UV_BIN                 uv executable (default: uv)
  PYTHON_BIN             requested Python executable/version (optional)
  UV_PROJECT_ENV_ROOT    parent directory for shared environments (optional)
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --print-only)
            SYNC=0
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            printf 'ERROR: Unknown argument: %s\n' "$1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

command -v "$UV_BIN" >/dev/null 2>&1 || {
    printf 'ERROR: uv is required. Install it with: brew install uv\n' >&2
    exit 1
}

[[ -f "$LOCK_FILE" ]] || {
    printf 'ERROR: uv.lock is missing at %s\n' "$LOCK_FILE" >&2
    exit 1
}

if [[ -n "$PYTHON_REQUEST" ]]; then
    PYTHON_PATH="$(cd "$ROOT_DIR" && "$UV_BIN" python find "$PYTHON_REQUEST")"
else
    PYTHON_PATH="$(cd "$ROOT_DIR" && "$UV_BIN" python find)"
fi

PYTHON_ID="$($PYTHON_PATH -c 'import platform, sysconfig; print("-".join((platform.python_implementation().lower(), platform.python_version(), platform.system().lower(), platform.machine().lower(), sysconfig.get_config_var("SOABI") or "noabi")))')"
LOCK_DIGEST="$(shasum -a 256 "$LOCK_FILE" | awk '{print $1}')"
GIT_COMMON_DIR="$(git -C "$ROOT_DIR" rev-parse --path-format=absolute --git-common-dir)"
ENV_ROOT="${UV_PROJECT_ENV_ROOT:-${GIT_COMMON_DIR}/worktree-environments/python}"
ENV_KEY="${LOCK_DIGEST:0:20}-${PYTHON_ID}"
ENV_PATH="${ENV_ROOT}/${ENV_KEY}"
READY_MARKER="${ENV_PATH}/.gaze-sync-complete"

if [[ "$SYNC" -eq 1 ]]; then
    printf '[python-env] Syncing %s\n' "$ENV_PATH" >&2
    (
        cd "$ROOT_DIR"
        UV_PROJECT_ENVIRONMENT="$ENV_PATH" "$UV_BIN" sync --locked --python "$PYTHON_PATH" >&2
    )
    READY_MARKER_TMP="${READY_MARKER}.tmp.$$"
    printf '%s\n' "$ENV_KEY" > "$READY_MARKER_TMP"
    mv "$READY_MARKER_TMP" "$READY_MARKER"
elif [[ ! -x "${ENV_PATH}/bin/python" || ! -f "$READY_MARKER" ]] || \
     [[ "$(cat "$READY_MARKER" 2>/dev/null || true)" != "$ENV_KEY" ]]; then
    printf 'ERROR: Shared environment does not exist yet: %s\n' "$ENV_PATH" >&2
    printf 'Run scripts/sync-python-env.sh first.\n' >&2
    exit 1
fi

printf '%s\n' "$ENV_PATH"
