#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/adb-record-loop-stitch.sh [options]

Records repeated ADB screenrecord chunks (default 180s each), then stitches
all pulled chunks into one MP4 when you stop with Ctrl+C.

Options:
  -o, --output FILE        Final stitched MP4 path (default: ./captures/adb_capture_<timestamp>.mp4)
  -d, --duration SECONDS   Per-chunk duration, 1-180 (default: 180)
  -b, --bitrate BPS        screenrecord bitrate in bits/sec (default: 8000000)
  -s, --size WxH           Recording size (default: 1920x1080)
      --with-mic           Capture macOS default microphone and mux into final MP4
      --mic-device NAME    Override microphone device name for ffmpeg avfoundation
      --audio-advance SEC  Shift mic audio earlier by SEC during mux (default: 1.0)
      --serial SERIAL      Target specific adb device serial
      --keep-remote        Keep remote chunk files on device
  -h, --help               Show this help

Examples:
  ./scripts/adb-record-loop-stitch.sh
  ./scripts/adb-record-loop-stitch.sh --with-mic
  ./scripts/adb-record-loop-stitch.sh -o ./captures/study_run_01.mp4 --serial FA7A12345
USAGE
}

OUTPUT=""
DURATION=180
BITRATE=8000000
SIZE="1920x1080"
SERIAL=""
KEEP_REMOTE=0
WITH_MIC=0
MIC_DEVICE=""
AUDIO_ADVANCE_SEC="1.0"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -o|--output)
      OUTPUT="$2"
      shift 2
      ;;
    -d|--duration)
      DURATION="$2"
      shift 2
      ;;
    -b|--bitrate)
      BITRATE="$2"
      shift 2
      ;;
    -s|--size)
      SIZE="$2"
      shift 2
      ;;
    --serial)
      SERIAL="$2"
      shift 2
      ;;
    --with-mic)
      WITH_MIC=1
      shift
      ;;
    --mic-device)
      MIC_DEVICE="$2"
      WITH_MIC=1
      shift 2
      ;;
    --audio-advance)
      AUDIO_ADVANCE_SEC="$2"
      shift 2
      ;;
    --keep-remote)
      KEEP_REMOTE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if ! command -v adb >/dev/null 2>&1; then
  echo "Error: adb not found in PATH." >&2
  exit 1
fi

if ! command -v ffmpeg >/dev/null 2>&1; then
  echo "Error: ffmpeg not found in PATH. Install ffmpeg to stitch chunks." >&2
  exit 1
fi

if ! command -v ffprobe >/dev/null 2>&1; then
  echo "Error: ffprobe not found in PATH. Install ffmpeg/ffprobe to validate chunks." >&2
  exit 1
fi

if ! [[ "$DURATION" =~ ^[0-9]+$ ]] || (( DURATION < 1 || DURATION > 180 )); then
  echo "Error: --duration must be an integer between 1 and 180." >&2
  exit 1
fi

if ! [[ "$BITRATE" =~ ^[0-9]+$ ]] || (( BITRATE <= 0 )); then
  echo "Error: --bitrate must be a positive integer (bits/sec)." >&2
  exit 1
fi

if ! [[ "$AUDIO_ADVANCE_SEC" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
  echo "Error: --audio-advance must be a non-negative number of seconds." >&2
  exit 1
fi

default_mic_from_system_profiler() {
  system_profiler SPAudioDataType 2>/dev/null | awk '
    /^[[:space:]]{8}[^:].*:$/ {
      dev = $0
      sub(/^[[:space:]]+/, "", dev)
      sub(/:$/, "", dev)
    }
    /Default Input Device: Yes/ {
      print dev
      exit
    }
  '
}

adb_cmd() {
  if [[ -n "$SERIAL" ]]; then
    adb -s "$SERIAL" "$@"
  else
    adb "$@"
  fi
}

is_valid_video_file() {
  local file_path="$1"
  local duration packets
  ffprobe -v error -select_streams v:0 -show_entries stream=codec_type \
    -of default=noprint_wrappers=1:nokey=1 "$file_path" >/dev/null 2>&1 || return 1
  duration="$(ffprobe -v error -show_entries format=duration \
    -of default=noprint_wrappers=1:nokey=1 "$file_path" 2>/dev/null || true)"
  if [[ -z "$duration" ]]; then
    return 1
  fi
  if ! awk -v d="$duration" 'BEGIN { exit !(d >= 0.5) }'; then
    return 1
  fi
  packets="$(ffprobe -v error -count_packets -select_streams v:0 \
    -show_entries stream=nb_read_packets \
    -of default=noprint_wrappers=1:nokey=1 "$file_path" 2>/dev/null || true)"
  if [[ -z "$packets" ]]; then
    return 1
  fi
  awk -v n="$packets" 'BEGIN { exit !(n >= 2) }'
}

if ! adb_cmd get-state >/dev/null 2>&1; then
  echo "Error: no adb device in 'device' state. Run 'adb devices' and connect one device." >&2
  exit 1
fi

timestamp="$(date +%Y%m%d_%H%M%S)"
if [[ -z "$OUTPUT" ]]; then
  OUTPUT="./captures/adb_capture_${timestamp}.mp4"
fi

OUTPUT_DIR="$(dirname "$OUTPUT")"
mkdir -p "$OUTPUT_DIR"

SESSION_DIR="${OUTPUT_DIR}/.adb_record_session_${timestamp}"
CHUNKS_DIR="${SESSION_DIR}/chunks"
mkdir -p "$CHUNKS_DIR"

AUDIO_FILE="${SESSION_DIR}/mic_audio.m4a"
audio_pid=""
audio_started=0

start_mic_capture() {
  if (( WITH_MIC == 0 )); then
    return 0
  fi

  if [[ -z "$MIC_DEVICE" ]]; then
    MIC_DEVICE="$(default_mic_from_system_profiler)"
  fi

  if [[ -z "$MIC_DEVICE" ]]; then
    echo "Error: could not detect default macOS input device. Use --mic-device NAME." >&2
    exit 1
  fi

  echo "Starting microphone capture from: ${MIC_DEVICE}"
  ffmpeg -hide_banner -loglevel error -nostdin -y \
    -f avfoundation -i ":${MIC_DEVICE}" \
    -c:a aac -b:a 192k \
    "$AUDIO_FILE" >/dev/null 2>&1 &
  audio_pid=$!
  sleep 0.4

  if ! kill -0 "$audio_pid" >/dev/null 2>&1; then
    echo "Error: microphone capture failed to start for device '${MIC_DEVICE}'." >&2
    echo "Tip: grant Microphone permission to Terminal and retry, or pass --mic-device." >&2
    exit 1
  fi

  audio_started=1
}

stop_mic_capture() {
  if (( audio_started == 0 )); then
    return 0
  fi

  if kill -0 "$audio_pid" >/dev/null 2>&1; then
    kill -INT "$audio_pid" >/dev/null 2>&1 || true
    wait "$audio_pid" >/dev/null 2>&1 || true
  fi
}

stop_requested=0
chunk_idx=1

on_interrupt() {
  stop_requested=1
  echo
  echo "Stop requested. Finishing current chunk..."
}
trap on_interrupt INT TERM
trap stop_mic_capture EXIT

echo "Recording started. Press Ctrl+C to stop and stitch."
echo "Chunks: ${DURATION}s, size: ${SIZE}, bitrate: ${BITRATE}, output: ${OUTPUT}"
if (( WITH_MIC == 1 )); then
  echo "Mic audio: enabled (default input device unless --mic-device is set), advance: ${AUDIO_ADVANCE_SEC}s"
fi

record_one_chunk() {
  local idx="$1"
  local remote_path local_path rec_exit rec_pid

  remote_path="/sdcard/Download/adb_chunk_$(printf '%04d' "$idx").mp4"
  local_path="${CHUNKS_DIR}/chunk_$(printf '%04d' "$idx").mp4"

  if (( idx == 1 )) && (( WITH_MIC == 1 )) && (( audio_started == 0 )); then
    start_mic_capture
  fi

  echo "[chunk $(printf '%04d' "$idx")] recording..."
  set +e
  (
    trap '' INT TERM
    adb_cmd shell screenrecord \
      --time-limit "$DURATION" \
      --bit-rate "$BITRATE" \
      --size "$SIZE" \
      "$remote_path"
  ) &
  rec_pid=$!
  while true; do
    wait "$rec_pid"
    rec_exit=$?
    if (( rec_exit > 128 )) && kill -0 "$rec_pid" >/dev/null 2>&1; then
      continue
    fi
    break
  done
  set -e

  if (( rec_exit != 0 )) && (( stop_requested == 0 )); then
    echo "Warning: screenrecord exited with code ${rec_exit}." >&2
  fi

  set +e
  adb_cmd pull "$remote_path" "$local_path" >/dev/null
  local pull_exit=$?
  set -e

  if (( pull_exit != 0 )); then
    echo "Warning: failed to pull $remote_path (chunk may be empty)." >&2
    return 1
  fi

  if (( KEEP_REMOTE == 0 )); then
    adb_cmd shell rm -f "$remote_path" >/dev/null 2>&1 || true
  fi

  local size_bytes
  size_bytes=$(wc -c < "$local_path" | tr -d ' ')
  if (( size_bytes < 1024 )); then
    echo "Warning: pulled chunk is very small (${size_bytes} bytes), skipping." >&2
    rm -f "$local_path"
    return 1
  fi

  if ! is_valid_video_file "$local_path"; then
    echo "Warning: chunk $(printf '%04d' "$idx") is not a valid MP4 (likely interrupted), skipping." >&2
    rm -f "$local_path"
    return 1
  fi

  echo "[chunk $(printf '%04d' "$idx")] saved (${size_bytes} bytes)"
  return 0
}

while true; do
  record_one_chunk "$chunk_idx" || true

  if (( stop_requested == 1 )); then
    break
  fi

  ((chunk_idx++))
done

valid_chunk_count=0
CONCAT_LIST="${SESSION_DIR}/concat.txt"
: > "$CONCAT_LIST"
while IFS= read -r f; do
  [[ -n "$f" ]] || continue
  if is_valid_video_file "$f"; then
    ((valid_chunk_count++))
    printf "file '%s'\n" "$(cd "$(dirname "$f")" && pwd)/$(basename "$f")" >> "$CONCAT_LIST"
  else
    echo "Warning: skipping corrupt chunk file $(basename "$f")." >&2
  fi
done < <(find "$CHUNKS_DIR" -maxdepth 1 -type f -name 'chunk_*.mp4' | sort)

if (( valid_chunk_count == 0 )); then
  echo "No valid chunks captured. If you stop very quickly, the current chunk can be incomplete." >&2
  echo "Tip: wait for at least one full chunk, or use a shorter chunk with -d (e.g. -d 30)." >&2
  exit 1
fi

echo "Stitching ${valid_chunk_count} valid chunk(s)..."

VIDEO_STITCHED="${OUTPUT}"
if (( WITH_MIC == 1 )); then
  VIDEO_STITCHED="${SESSION_DIR}/stitched_video_only.mp4"
fi

set +e
ffmpeg -y -f concat -safe 0 -dn -i "$CONCAT_LIST" -map 0:v:0 -c copy "$VIDEO_STITCHED"
concat_copy_exit=$?
set -e

if (( concat_copy_exit != 0 )); then
  echo "Fast concat failed; retrying with re-encode..."
  ffmpeg -y -f concat -safe 0 -dn -i "$CONCAT_LIST" \
    -map 0:v:0 \
    -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \
    "$VIDEO_STITCHED"
fi

if (( WITH_MIC == 1 )); then
  stop_mic_capture
  audio_started=0

  if [[ -s "$AUDIO_FILE" ]]; then
    echo "Muxing stitched video with microphone audio..."
    ffmpeg -y -i "$VIDEO_STITCHED" -itsoffset "-${AUDIO_ADVANCE_SEC}" -i "$AUDIO_FILE" \
      -map 0:v:0 -map 1:a:0 -dn \
      -c:v copy -c:a aac -shortest \
      "$OUTPUT"
  else
    echo "Warning: no microphone audio captured. Final output will be video-only." >&2
    cp -f "$VIDEO_STITCHED" "$OUTPUT"
  fi
fi

echo "Done: $OUTPUT"
echo "Chunks kept in: $CHUNKS_DIR"
