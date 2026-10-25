#!/bin/bash
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
test_dir="$(mktemp -d "${TMPDIR:-/tmp}/native-gaze-tests.XXXXXX")"
trap 'rm -rf "$test_dir"' EXIT
# Address/undefined-behavior sanitizers test the host adapter with a fake runtime.
# Override CXX for an alternate host compiler; no Unity or Android SDK is required.
"${CXX:-clang++}" -std=c++17 -O1 -g -Wall -Wextra -Werror \
    -fsanitize=address,undefined -fno-omit-frame-pointer -pthread \
    -I "$repo_dir/native/gaze_timestamp/include" \
    "$repo_dir/native/gaze_timestamp/tests.cpp" -o "$test_dir/native-gaze-tests"
"$test_dir/native-gaze-tests"
