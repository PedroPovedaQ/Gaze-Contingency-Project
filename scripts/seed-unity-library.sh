#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_TARGET="$(cd "${SCRIPT_DIR}/.." && pwd)"
SOURCE_PROJECT=""
TARGET_PROJECT="$DEFAULT_TARGET"

usage() {
    cat <<'EOF'
Usage:
  scripts/seed-unity-library.sh --source <unity-project> [--target <unity-project>]

Seeds a worktree's missing Library directory with an APFS copy-on-write clone.
The source Unity Editor must be closed. The target Library must not exist.

This creates an isolated Library: later writes consume space only for changed
blocks. It never symlinks or shares a writable Library between Unity Editors.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --source)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            SOURCE_PROJECT="$2"
            shift 2
            ;;
        --target)
            [[ $# -ge 2 ]] || { usage >&2; exit 2; }
            TARGET_PROJECT="$2"
            shift 2
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

[[ "$(uname -s)" == "Darwin" ]] || {
    printf 'ERROR: APFS copy-on-write seeding is supported by this script only on macOS.\n' >&2
    exit 1
}
[[ -n "$SOURCE_PROJECT" ]] || {
    printf 'ERROR: --source is required.\n' >&2
    usage >&2
    exit 2
}

SOURCE_PROJECT="$(cd "$SOURCE_PROJECT" && pwd -P)"
TARGET_PROJECT="$(cd "$TARGET_PROJECT" && pwd -P)"

[[ "$SOURCE_PROJECT" != "$TARGET_PROJECT" ]] || {
    printf 'ERROR: Source and target projects must be different.\n' >&2
    exit 1
}
[[ -d "${SOURCE_PROJECT}/Library" ]] || {
    printf 'ERROR: Source Library does not exist: %s\n' "${SOURCE_PROJECT}/Library" >&2
    exit 1
}
[[ -d "${SOURCE_PROJECT}/Assets" && -d "${SOURCE_PROJECT}/ProjectSettings" ]] || {
    printf 'ERROR: Source is not a Unity project: %s\n' "$SOURCE_PROJECT" >&2
    exit 1
}
[[ -d "${TARGET_PROJECT}/Assets" && -d "${TARGET_PROJECT}/ProjectSettings" ]] || {
    printf 'ERROR: Target is not a Unity project: %s\n' "$TARGET_PROJECT" >&2
    exit 1
}
[[ ! -e "${TARGET_PROJECT}/Library" ]] || {
    printf 'ERROR: Target Library already exists: %s\n' "${TARGET_PROJECT}/Library" >&2
    exit 1
}
[[ ! -e "${SOURCE_PROJECT}/Temp/UnityLockfile" ]] || {
    printf 'ERROR: The source project appears open in Unity. Close it before cloning Library.\n' >&2
    exit 1
}

SOURCE_VOLUME="$(df -P "${SOURCE_PROJECT}/Library" | awk 'NR == 2 { print $1 }')"
TARGET_VOLUME="$(df -P "$TARGET_PROJECT" | awk 'NR == 2 { print $1 }')"
SOURCE_FS="$(diskutil info -plist "$SOURCE_VOLUME" | plutil -extract FilesystemType raw -o - -)"
TARGET_FS="$(diskutil info -plist "$TARGET_VOLUME" | plutil -extract FilesystemType raw -o - -)"
SOURCE_DEVICE="$(stat -f '%d' "${SOURCE_PROJECT}/Library")"
TARGET_DEVICE="$(stat -f '%d' "$TARGET_PROJECT")"

[[ "$SOURCE_FS" == "apfs" && "$TARGET_FS" == "apfs" ]] || {
    printf 'ERROR: Source and target must both be on APFS (source: %s, target: %s).\n' "$SOURCE_FS" "$TARGET_FS" >&2
    exit 1
}
[[ "$SOURCE_DEVICE" == "$TARGET_DEVICE" ]] || {
    printf 'ERROR: Source and target must be on the same APFS volume for copy-on-write cloning.\n' >&2
    exit 1
}

STAGING_DIR="$(mktemp -d "${TARGET_PROJECT}/.unity-library-seed.XXXXXX")"
cleanup_staging() {
    rm -rf "$STAGING_DIR"
}
trap cleanup_staging EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

printf '[unity-cache] Cloning %s -> %s\n' "${SOURCE_PROJECT}/Library" "${TARGET_PROJECT}/Library"
cp -cR "${SOURCE_PROJECT}/Library" "${STAGING_DIR}/Library"
mv "${STAGING_DIR}/Library" "${TARGET_PROJECT}/Library"
rmdir "$STAGING_DIR"
trap - EXIT INT TERM
printf '[unity-cache] Done. The target Library is isolated and copy-on-write.\n'
