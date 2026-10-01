#!/usr/bin/env bash
set -euo pipefail

# Always refocus Unity first.
osascript -e 'tell application "Unity" to activate'

# Trigger Build & Run by selecting the menu item directly to avoid shortcut conflicts.
if ! osascript <<'APPLESCRIPT'
tell application "Unity" to activate
delay 0.4
tell application "System Events"
  tell process "Unity"
    click menu item "Build And Run Android" of menu 1 of menu item "Codex" of menu 1 of menu bar item "Tools" of menu bar 1
  end tell
end tell
APPLESCRIPT
then
    echo "Unity focused, but menu dispatch failed." >&2
    echo "Check that the Mac is unlocked and the intended Unity project's Tools > Codex menu is available." >&2
    echo "If macOS reports an authorization error, check System Settings > Privacy & Security > Accessibility." >&2
    exit 1
fi

echo "Triggered Unity menu command: Tools > Codex > Build And Run Android."
