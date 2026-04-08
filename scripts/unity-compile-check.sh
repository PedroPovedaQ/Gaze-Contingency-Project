#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_VERSION_FILE="$ROOT_DIR/ProjectSettings/ProjectVersion.txt"
LOG_FILE="${UNITY_LINT_LOG_FILE:-$ROOT_DIR/Temp/unity_compile_check.log}"
EDITOR_LOG_MAC="${UNITY_EDITOR_LOG:-$HOME/Library/Logs/Unity/Editor.log}"

print_recent_editor_errors() {
  local editor_log="$1"
  local error_pattern='error CS[0-9]+|scripts have compile errors|Compilation failed'

  if [[ ! -f "$editor_log" ]]; then
    echo "Unity appears to be open, but no Editor.log was found at $editor_log" >&2
    return 2
  fi

  local start_line
  start_line="$(rg -n "AssetDatabase: script compilation time" "$editor_log" | tail -n 2 | head -n 1 | cut -d: -f1 || true)"
  if [[ -z "$start_line" ]]; then
    start_line=1
  fi

  local recent_log
  recent_log="$(mktemp)"
  sed -n "${start_line},\$p" "$editor_log" > "$recent_log"

  if rg -n "$error_pattern" "$recent_log" >/dev/null 2>&1; then
    echo
    echo "Unity compile errors detected (live Editor.log):"
    rg -n "$error_pattern" "$recent_log" || true
    echo
    echo "If you just fixed code, return focus to Unity and wait for recompilation,"
    echo "then run this check again."
    rm -f "$recent_log"
    return 1
  fi

  rm -f "$recent_log"
  return 0
}

resolve_unity_bin() {
  if [[ -n "${UNITY_BIN:-}" && -x "${UNITY_BIN}" ]]; then
    echo "${UNITY_BIN}"
    return 0
  fi

  if [[ -f "$PROJECT_VERSION_FILE" ]]; then
    local version
    version="$(awk '/m_EditorVersion:/ { print $2 }' "$PROJECT_VERSION_FILE")"
    if [[ -n "$version" ]]; then
      local mac_path="/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity"
      if [[ -x "$mac_path" ]]; then
        echo "$mac_path"
        return 0
      fi
    fi
  fi

  if command -v unity >/dev/null 2>&1; then
    command -v unity
    return 0
  fi

  return 1
}

UNITY_BIN_PATH="$(resolve_unity_bin || true)"
if [[ -z "$UNITY_BIN_PATH" ]]; then
  echo "Unity executable not found." >&2
  echo "Set UNITY_BIN or install Unity Hub editor for this project version." >&2
  exit 2
fi

# If the editor is already open for this project, batchmode cannot acquire the lock.
# Fall back to parsing the most recent compiler output from the live Editor.log.
if [[ -f "$ROOT_DIR/Temp/UnityLockfile" ]]; then
  echo "Unity lockfile detected. Checking live Editor.log instead of batchmode..."
  if print_recent_editor_errors "$EDITOR_LOG_MAC"; then
    echo "Unity compile check passed (Editor.log)."
    exit 0
  else
    exit $?
  fi
fi

mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$LOG_FILE"

echo "Running Unity compile check..."
echo "Unity: $UNITY_BIN_PATH"
echo "Log:   $LOG_FILE"

set +e
"$UNITY_BIN_PATH" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$ROOT_DIR" \
  -logFile "$LOG_FILE"
UNITY_EXIT=$?
set -e

ERROR_PATTERN='error CS[0-9]+|scripts have compile errors|Compilation failed'
if rg -n "$ERROR_PATTERN" "$LOG_FILE" >/dev/null 2>&1; then
  echo
  echo "Unity compile errors detected:"
  rg -n "$ERROR_PATTERN" "$LOG_FILE" || true
  exit 1
fi

if [[ $UNITY_EXIT -ne 0 ]]; then
  echo
  echo "Unity exited with code $UNITY_EXIT. Check $LOG_FILE for details." >&2
  exit $UNITY_EXIT
fi

echo "Unity compile check passed."
