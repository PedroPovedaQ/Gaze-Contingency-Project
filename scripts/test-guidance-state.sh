#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono='/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'
test_dir=$(mktemp -d)
trap 'rm -rf "$test_dir"' EXIT
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -out:"$test_dir/checks.exe" Assets/GuidanceStateMachine.cs Assets/GuidanceRequestScheduler.cs Assets/GuidanceStateSnapshot.cs Assets/GuidanceSpeechPolicy.cs Assets/GuidanceTurnState.cs Assets/DirectionalHintPolicy.cs Assets/GazeZoneEntryGate.cs Assets/GazeReturnCueGate.cs Assets/StudyPromptDelay.cs Assets/GazeTargetHistory.cs scripts/tests/GuidanceStateMachineChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"
