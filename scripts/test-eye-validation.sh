#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
unity_mono='/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'
test_dir=$(mktemp -d)
trap 'rm -rf "$test_dir"' EXIT
"$unity_mono/bin/mono" "$unity_mono/lib/mono/4.5/csc.exe" -nologo -out:"$test_dir/checks.exe" Assets/ParticipantSessionAssignment.cs Assets/EyeValidationProtocol.cs scripts/tests/EyeValidationProtocolChecks.cs
"$unity_mono/bin/mono" "$test_dir/checks.exe"
python3 -m unittest discover -s tools/trial-review -p 'test_validation_control.py'
python3 -m unittest discover -s analysis -p 'test_validation_session.py'
