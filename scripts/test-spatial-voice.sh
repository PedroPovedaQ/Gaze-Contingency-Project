#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
json_dll=$(rg --files Library/PackageCache | rg 'com.unity.nuget.newtonsoft-json[^/]*/Runtime/AOT/Newtonsoft.Json.dll$' | head -1)
cp "$json_dll" "$test_dir/"
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -r:System.Net.Http.dll -r:"$unity_mono/lib/mono/4.5/Facades/netstandard.dll" -r:"$json_dll" \
  -out:"$test_dir/checks.exe" Assets/SpatialVoice/INavigationDecisionProvider.cs Assets/SpatialVoice/JevDecisionClient.cs Assets/SpatialVoice/ElevenLabsPcmClient.cs \
  Assets/SpatialVoice/PcmStreamBuffer.cs Assets/GuidanceRequestScheduler.cs Assets/GuidanceStateSnapshot.cs Assets/SpatialVoice/SpeechPhraseMetadata.cs Assets/SpatialVoice/RoundEncouragement.cs Assets/GuidanceSpeechPolicy.cs Assets/DirectionalHintPolicy.cs scripts/tests/SpatialVoiceChecks.cs scripts/tests/RoundEncouragementChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe" "$@"
