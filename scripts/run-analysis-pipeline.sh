#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ANALYSIS_DIR="${ROOT_DIR}/analysis"
PROJECT_SETTINGS="${ROOT_DIR}/ProjectSettings/ProjectSettings.asset"
VENV_DIR="${ROOT_DIR}/.venv-analysis"

DEFAULT_DATA_DIR="${ROOT_DIR}/GazeData"
DEFAULT_EDITOR_DATA_DIR_LEGACY="${HOME}/Library/Application Support/DefaultCompany/GazeContingencyProject/GazeData"
DEFAULT_EDITOR_DATA_DIR_SPACED="${HOME}/Library/Application Support/DefaultCompany/Gaze Contingency Project/GazeData"
DEFAULT_OUTPUT_DIR="${ANALYSIS_DIR}/results"
DEFAULT_PACKAGE_ID="com.DefaultCompany.MixedRealityTemplate"

DATA_DIR=""
OUTPUT_DIR="${DEFAULT_OUTPUT_DIR}"
PACKAGE_ID=""
PYTHON_BIN="${PYTHON_BIN:-python3}"
FORCE_PULL=0
DISABLE_PULL=0
SKIP_INSTALL=0
OPEN_RESULTS=0

usage() {
    cat <<'EOF'
Usage:
  scripts/run-analysis-pipeline.sh [options]

Options:
  --pull                 Force pull GazeData from headset before analysis.
  --no-pull              Never pull from headset (local data only).
  --data-dir <path>      Input data directory. Default auto-select:
                         1) ./GazeData (if present)
                         2) macOS editor persistentDataPath
                         3) ./GazeData
  --output-dir <path>    Output directory for analysis artifacts.
                         Default: ./analysis/results
  --package-id <id>      Android package id for adb pull.
                         Default: parsed from ProjectSettings, fallback
                         com.DefaultCompany.MixedRealityTemplate
  --python <exe>         Python executable to create/use venv (default: python3)
  --skip-install         Skip pip install -r requirements.txt
  --open                 Open output folder in Finder when done (macOS)
  -h, --help             Show this help

Examples:
  scripts/run-analysis-pipeline.sh
  scripts/run-analysis-pipeline.sh --pull
  scripts/run-analysis-pipeline.sh --pull --data-dir ./GazeData --output-dir ./analysis/results
  scripts/run-analysis-pipeline.sh --no-pull --data-dir "~/Library/Application Support/DefaultCompany/GazeContingencyProject/GazeData"
EOF
}

log() {
    printf '[analysis] %s\n' "$*"
}

fail() {
    printf '[analysis] ERROR: %s\n' "$*" >&2
    exit 1
}

expand_path() {
    local p="$1"
    if [[ "$p" == "~"* ]]; then
        p="${HOME}${p:1}"
    fi
    printf '%s\n' "$p"
}

infer_package_id() {
    if [[ ! -f "$PROJECT_SETTINGS" ]]; then
        printf '%s\n' "$DEFAULT_PACKAGE_ID"
        return
    fi

    local parsed
    parsed="$(awk '
        $1=="applicationIdentifier:" { in_block=1; next }
        in_block && $1=="Android:" { print $2; exit }
        in_block && $1 !~ /^[[:space:]]/ { in_block=0 }
    ' "$PROJECT_SETTINGS")"

    if [[ -n "$parsed" ]]; then
        printf '%s\n' "$parsed"
    else
        printf '%s\n' "$DEFAULT_PACKAGE_ID"
    fi
}

has_trial_summaries() {
    local dir="$1"
    if [[ ! -d "$dir" ]]; then
        return 1
    fi
    find "$dir" -type f -name "trial_summary.json" -print -quit | grep -q .
}

run_adb_pull() {
    local data_dir="$1"
    local package_id="$2"
    local device_data_dir="/sdcard/Android/data/${package_id}/files/GazeData"

    command -v adb >/dev/null 2>&1 || fail "adb not found. Install Android platform-tools or rerun with --no-pull."

    local adb_state
    adb_state="$(adb get-state 2>/dev/null || true)"
    if [[ "$adb_state" != "device" ]]; then
        fail "No adb device connected. Connect headset and run 'adb devices', or rerun with --no-pull."
    fi

    mkdir -p "$data_dir"
    log "Pulling from headset: ${device_data_dir} -> ${data_dir}"
    adb pull "${device_data_dir}/." "${data_dir}/"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --pull)
            FORCE_PULL=1
            shift
            ;;
        --no-pull)
            DISABLE_PULL=1
            shift
            ;;
        --data-dir)
            [[ $# -ge 2 ]] || fail "--data-dir requires a value"
            DATA_DIR="$2"
            shift 2
            ;;
        --output-dir)
            [[ $# -ge 2 ]] || fail "--output-dir requires a value"
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --package-id)
            [[ $# -ge 2 ]] || fail "--package-id requires a value"
            PACKAGE_ID="$2"
            shift 2
            ;;
        --python)
            [[ $# -ge 2 ]] || fail "--python requires a value"
            PYTHON_BIN="$2"
            shift 2
            ;;
        --skip-install)
            SKIP_INSTALL=1
            shift
            ;;
        --open)
            OPEN_RESULTS=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "Unknown argument: $1 (use --help)"
            ;;
    esac
done

[[ -d "$ANALYSIS_DIR" ]] || fail "analysis folder not found at ${ANALYSIS_DIR}"

if [[ -z "$DATA_DIR" ]]; then
    if [[ -d "$DEFAULT_DATA_DIR" ]]; then
        DATA_DIR="$DEFAULT_DATA_DIR"
    elif [[ -d "$DEFAULT_EDITOR_DATA_DIR_LEGACY" ]]; then
        DATA_DIR="$DEFAULT_EDITOR_DATA_DIR_LEGACY"
    elif [[ -d "$DEFAULT_EDITOR_DATA_DIR_SPACED" ]]; then
        DATA_DIR="$DEFAULT_EDITOR_DATA_DIR_SPACED"
    else
        DATA_DIR="$DEFAULT_DATA_DIR"
    fi
fi

DATA_DIR="$(expand_path "$DATA_DIR")"
OUTPUT_DIR="$(expand_path "$OUTPUT_DIR")"
PACKAGE_ID="${PACKAGE_ID:-$(infer_package_id)}"

log "Data directory: ${DATA_DIR}"
log "Output directory: ${OUTPUT_DIR}"
log "Package id: ${PACKAGE_ID}"

SHOULD_PULL=0
if [[ "$DISABLE_PULL" -eq 1 ]]; then
    SHOULD_PULL=0
elif [[ "$FORCE_PULL" -eq 1 ]]; then
    SHOULD_PULL=1
elif ! has_trial_summaries "$DATA_DIR"; then
    SHOULD_PULL=1
fi

if [[ "$SHOULD_PULL" -eq 1 ]]; then
    run_adb_pull "$DATA_DIR" "$PACKAGE_ID"
fi

if ! has_trial_summaries "$DATA_DIR"; then
    fail "No trial_summary.json found under ${DATA_DIR}. Record a run first, or use --pull with a connected headset."
fi

RUN_PY="$PYTHON_BIN"
if [[ "$SKIP_INSTALL" -eq 0 ]]; then
    command -v "$PYTHON_BIN" >/dev/null 2>&1 || fail "Python executable not found: ${PYTHON_BIN}"
    if [[ ! -x "${VENV_DIR}/bin/python" ]]; then
        log "Creating analysis virtualenv at ${VENV_DIR}"
        "$PYTHON_BIN" -m venv "$VENV_DIR"
    fi
    RUN_PY="${VENV_DIR}/bin/python"

    log "Installing analysis dependencies"
    "$RUN_PY" -m pip install --upgrade pip >/dev/null
    "$RUN_PY" -m pip install -r "${ANALYSIS_DIR}/requirements.txt"
fi

mkdir -p "$OUTPUT_DIR"
log "Running analysis pipeline"
"$RUN_PY" "${ANALYSIS_DIR}/run_analysis.py" "$DATA_DIR" "$OUTPUT_DIR"

log "Generated artifacts:"
find "$OUTPUT_DIR" -maxdepth 1 -type f \
    \( -name "*.png" -o -name "*.csv" -o -name "*.txt" \) \
    -print | sort

if [[ "$OPEN_RESULTS" -eq 1 ]] && command -v open >/dev/null 2>&1; then
    open "$OUTPUT_DIR"
fi

log "Done."
