#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOOKS_DIR="$ROOT_DIR/.git/hooks"
SOURCE_HOOK="$ROOT_DIR/.githooks/pre-commit"
TARGET_HOOK="$HOOKS_DIR/pre-commit"

if [[ ! -d "$HOOKS_DIR" ]]; then
  echo "Not a git repository: $ROOT_DIR" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_HOOK" ]]; then
  echo "Missing source hook: $SOURCE_HOOK" >&2
  exit 1
fi

mkdir -p "$HOOKS_DIR"
cp "$SOURCE_HOOK" "$TARGET_HOOK"
chmod +x "$TARGET_HOOK"

echo "Installed git pre-commit hook at $TARGET_HOOK"
echo "Set SKIP_UNITY_LINT=1 to bypass for an emergency commit."
