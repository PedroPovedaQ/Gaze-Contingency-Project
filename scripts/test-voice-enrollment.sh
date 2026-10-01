#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono="/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -out:"$test_dir/checks.exe" \
  Assets/VoiceEnrollment.cs scripts/tests/VoiceEnrollmentChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"

"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -r:System.Web.Extensions.dll -out:"$test_dir/client-checks.exe" \
  Assets/ElevenLabsVoiceClient.cs scripts/tests/ElevenLabsVoiceClientChecks.cs
"$unity_mono/bin/mono" "$test_dir/client-checks.exe"
