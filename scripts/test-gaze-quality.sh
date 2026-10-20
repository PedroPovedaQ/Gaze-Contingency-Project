#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -out:"$test_dir/checks.exe" Assets/GuidanceSpeechPolicy.cs Assets/GuidanceTurnState.cs Assets/StudyPromptDelay.cs Assets/GazeQualityPolicy.cs Assets/GazeTrackingFeedbackState.cs Assets/DirectionalHintPolicy.cs Assets/GazeTargetHistory.cs Assets/GazeZoneEntryGate.cs Assets/GazeReturnCueGate.cs scripts/tests/GazeQualityChecks.cs scripts/tests/GazeTargetHistoryChecks.cs scripts/tests/GazeTrackingFeedbackChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"
