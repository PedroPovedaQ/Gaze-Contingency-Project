#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NATIVE_DIR="$ROOT_DIR/native/gaze_timestamp"
NDK_ROOT="${GAZE_ANDROID_NDK:-/Applications/Unity/Hub/Editor/6000.3.10f1/PlaybackEngines/AndroidPlayer/NDK}"
CXX="$NDK_ROOT/toolchains/llvm/prebuilt/darwin-x86_64/bin/aarch64-linux-android32-clang++"
mkdir -p "$ROOT_DIR/Assets/Plugins/Android/arm64-v8a"
"$CXX" -std=c++17 -O2 -Wall -Wextra -Werror -fvisibility=hidden -fPIC -shared -static-libstdc++ -I "$NATIVE_DIR/include" "$NATIVE_DIR/gaze_timestamp.cpp" -o "$ROOT_DIR/Assets/Plugins/Android/arm64-v8a/libgaze_timestamp.so"
