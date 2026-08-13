#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/gaze-worktree-env-test.XXXXXX")"

cleanup() {
    rm -rf "$TEST_ROOT"
}
trap cleanup EXIT INT TERM

fail() {
    printf 'FAIL: %s\n' "$*" >&2
    exit 1
}

assert_fails() {
    if "$@" >/dev/null 2>&1; then
        fail "command unexpectedly succeeded: $*"
    fi
}

MAIN_REPO="${TEST_ROOT}/main"
LINKED_WORKTREE="${TEST_ROOT}/linked"
FAKE_BIN="${TEST_ROOT}/bin"
ENV_ROOT="${TEST_ROOT}/environments"
SYNC_LOG="${TEST_ROOT}/sync.log"
mkdir -p "$MAIN_REPO/scripts" "$FAKE_BIN"
cp "${ROOT_DIR}/scripts/sync-python-env.sh" "$MAIN_REPO/scripts/"
cp "${ROOT_DIR}/pyproject.toml" "${ROOT_DIR}/uv.lock" "$MAIN_REPO/"
git -C "$MAIN_REPO" init -q
git -C "$MAIN_REPO" add .
git -C "$MAIN_REPO" -c user.name=Test -c user.email=test@example.com commit -qm init
git -C "$MAIN_REPO" worktree add -q -b linked "$LINKED_WORKTREE"

cat > "${FAKE_BIN}/uv" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ "${1:-}" == "python" && "${2:-}" == "find" ]]; then
    printf '%s\n' "$REAL_PYTHON"
elif [[ "${1:-}" == "sync" ]]; then
    [[ "$PWD" == "$EXPECTED_PROJECT_ROOT" ]]
    mkdir -p "${UV_PROJECT_ENVIRONMENT}/bin"
    ln -sf "$REAL_PYTHON" "${UV_PROJECT_ENVIRONMENT}/bin/python"
    printf '%s\n' "$PWD" >> "$SYNC_LOG"
else
    printf 'unexpected fake uv arguments: %s\n' "$*" >&2
    exit 2
fi
EOF
chmod +x "${FAKE_BIN}/uv"

REAL_PYTHON="$(command -v python3)"
export REAL_PYTHON SYNC_LOG

OUTSIDE_DIR="${TEST_ROOT}/outside"
mkdir -p "$OUTSIDE_DIR"
MAIN_ENV="$(cd "$OUTSIDE_DIR" && EXPECTED_PROJECT_ROOT="$MAIN_REPO" UV_BIN="${FAKE_BIN}/uv" UV_PROJECT_ENV_ROOT="$ENV_ROOT" "${MAIN_REPO}/scripts/sync-python-env.sh")"
LINKED_ENV="$(cd "$OUTSIDE_DIR" && EXPECTED_PROJECT_ROOT="$LINKED_WORKTREE" UV_BIN="${FAKE_BIN}/uv" UV_PROJECT_ENV_ROOT="$ENV_ROOT" "${LINKED_WORKTREE}/scripts/sync-python-env.sh" --print-only)"
[[ "$MAIN_ENV" == "$LINKED_ENV" ]] || fail "linked worktrees selected different environments"
[[ "$(wc -l < "$SYNC_LOG" | tr -d ' ')" == "1" ]] || fail "print-only unexpectedly synced dependencies"

rm "${MAIN_ENV}/.gaze-sync-complete"
assert_fails env EXPECTED_PROJECT_ROOT="$LINKED_WORKTREE" UV_BIN="${FAKE_BIN}/uv" UV_PROJECT_ENV_ROOT="$ENV_ROOT" "${LINKED_WORKTREE}/scripts/sync-python-env.sh" --print-only

if [[ "$(uname -s)" == "Darwin" ]]; then
    TEST_VOLUME="$(df -P "$TEST_ROOT" | awk 'NR == 2 { print $1 }')"
    TEST_FS="$(diskutil info -plist "$TEST_VOLUME" | plutil -extract FilesystemType raw -o - -)"
else
    TEST_FS=""
fi

if [[ "$TEST_FS" == "apfs" ]]; then
    UNITY_SOURCE="${TEST_ROOT}/unity-source"
    UNITY_TARGET="${TEST_ROOT}/unity-target"
    UNITY_LOCKED_TARGET="${TEST_ROOT}/unity-locked-target"
    mkdir -p "${UNITY_SOURCE}/Assets" "${UNITY_SOURCE}/ProjectSettings" "${UNITY_SOURCE}/Library" \
        "${UNITY_TARGET}/Assets" "${UNITY_TARGET}/ProjectSettings" \
        "${UNITY_LOCKED_TARGET}/Assets" "${UNITY_LOCKED_TARGET}/ProjectSettings"
    printf 'source-cache\n' > "${UNITY_SOURCE}/Library/cache.bin"

    "${ROOT_DIR}/scripts/seed-unity-library.sh" --source "$UNITY_SOURCE" --target "$UNITY_TARGET" >/dev/null
    [[ "$(<"${UNITY_TARGET}/Library/cache.bin")" == "source-cache" ]] || fail "Unity Library clone is incomplete"
    printf 'target-cache\n' > "${UNITY_TARGET}/Library/cache.bin"
    [[ "$(<"${UNITY_SOURCE}/Library/cache.bin")" == "source-cache" ]] || fail "Unity Library clone is not logically isolated"
    assert_fails "${ROOT_DIR}/scripts/seed-unity-library.sh" --source "$UNITY_SOURCE" --target "$UNITY_TARGET"

    mkdir -p "${UNITY_SOURCE}/Temp"
    : > "${UNITY_SOURCE}/Temp/UnityLockfile"
    assert_fails "${ROOT_DIR}/scripts/seed-unity-library.sh" --source "$UNITY_SOURCE" --target "$UNITY_LOCKED_TARGET"
fi

printf 'Worktree environment tests passed.\n'
