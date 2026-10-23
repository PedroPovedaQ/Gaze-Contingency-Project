#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono='/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'
test_dir=$(mktemp -d)
trap 'rm -rf "$test_dir"' EXIT
json_dll=$(rg --files Library/PackageCache | rg 'com.unity.nuget.newtonsoft-json[^/]*/Runtime/AOT/Newtonsoft.Json.dll$' | head -1)
cp "$json_dll" "$test_dir/"
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -r:"$unity_mono/lib/mono/4.5/Facades/netstandard.dll" -r:"$json_dll" -out:"$test_dir/bridge.exe" \
 Assets/GuidanceStateMachine.cs Assets/GuidanceStateSnapshot.cs Assets/GuidanceSpeechPolicy.cs \
 Assets/GuidanceTurnState.cs Assets/DirectionalHintPolicy.cs Assets/GazeZoneEntryGate.cs Assets/GazeReturnCueGate.cs \
 Assets/StudyPromptDelay.cs Assets/GazeTargetHistory.cs Assets/SpatialVoice/SpeechDecisionMemory.cs scripts/tests/GuidanceSequenceBridge.cs >&2
"$unity_mono/bin/mono" "$test_dir/bridge.exe"
