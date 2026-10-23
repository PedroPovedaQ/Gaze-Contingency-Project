#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -out:"$test_dir/checks.exe" \
  Assets/VoiceEnrollment.cs scripts/tests/VoiceEnrollmentChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"

json_dll=$(rg --files Library/PackageCache | rg 'com.unity.nuget.newtonsoft-json[^/]*/Runtime/AOT/Newtonsoft.Json.dll$' | head -1)
cp "$json_dll" "$test_dir/"
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -r:"$unity_mono/lib/mono/4.5/Facades/netstandard.dll" -r:"$json_dll" -nologo -r:System.Web.Extensions.dll -out:"$test_dir/client-checks.exe" \
  Assets/ElevenLabsVoiceClient.cs Assets/VoiceSynthesisRequest.cs scripts/tests/ElevenLabsVoiceClientChecks.cs
"$unity_mono/bin/mono" "$test_dir/client-checks.exe"
