#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
json_dll=$(rg --files Library/PackageCache | rg 'com.unity.nuget.newtonsoft-json[^/]*/Runtime/AOT/Newtonsoft.Json.dll$' | head -1)
cp "$json_dll" "$test_dir/"
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -r:"$unity_mono/lib/mono/4.5/Facades/netstandard.dll" -r:"$json_dll" -nologo -out:"$test_dir/checks.exe" \
  Assets/SessionConfig.cs Assets/ChallengeSet.cs Assets/StudyTrialClock.cs Assets/VoicePromptText.cs Assets/DirectionalHintPolicy.cs Assets/GuidanceSpeechPolicy.cs Assets/InnerThoughtVoice.cs Assets/VoiceSynthesizer.cs Assets/VoiceSynthesizer.Interruption.cs Assets/SpatialVoice/SpeechPhraseMetadata.cs scripts/tests/VoiceIsolationChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"
