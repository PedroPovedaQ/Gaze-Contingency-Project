#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono='/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'
test_dir=$(mktemp -d)
trap 'rm -rf "$test_dir"' EXIT
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -r:System.Web.Extensions.dll -r:Microsoft.CSharp.dll -out:"$test_dir/checks.exe" Assets/EyeValidationProtocol.cs Assets/ParticipantSessionAssignment.cs Assets/GuidanceStateSnapshot.cs Assets/HeadsetPreparationStatus.cs scripts/tests/HeadsetPreparationChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"
