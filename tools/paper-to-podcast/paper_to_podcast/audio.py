"""Speech synthesis, MP3 assembly, and codec/duration validation."""

from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from .chunking import chunk_text
from .errors import AudioValidationError, SpeechSynthesisError
from .mistral_client import VOXTRAL_MODEL, resolve_voice_id, synthesize_voxtral_wav
from .openai_client import synthesize_mp3

SPEAKER_LABELS = ("host", "cohost", "host3", "host4")


def label_transcript_for_hosts(text: str, host_count: int) -> str:
    """Label unlabeled transcript paragraphs for round-robin multi-host delivery."""
    if host_count < 1 or host_count > len(SPEAKER_LABELS):
        raise SpeechSynthesisError("Voxtral host count must be between 1 and 4")
    paragraphs = [part.strip() for part in text.split("\n\n") if part.strip()]
    if not paragraphs:
        raise SpeechSynthesisError("Transcript is empty")
    if all(part.startswith("[") and "]" in part.splitlines()[0] for part in paragraphs):
        return "\n\n".join(paragraphs)
    return "\n\n".join(
        f"[{SPEAKER_LABELS[index % host_count]}]\n{paragraph}"
        for index, paragraph in enumerate(paragraphs)
    )


def parse_speaker_segments(text: str) -> list[tuple[str, str]]:
    """Parse labeled paragraphs and bound each upstream request to 3,000 characters."""
    segments: list[tuple[str, str]] = []
    current_speaker = "host"
    for paragraph in [part.strip() for part in text.split("\n\n") if part.strip()]:
        lines = paragraph.splitlines()
        first = lines[0].strip().lower()
        if first.startswith("[") and first.endswith("]"):
            candidate = first[1:-1]
            if candidate not in SPEAKER_LABELS:
                raise SpeechSynthesisError(f"Unknown transcript speaker: {candidate}")
            current_speaker = candidate
            paragraph = "\n".join(lines[1:]).strip()
        if not paragraph:
            continue
        for part in chunk_text(paragraph, max_chars=3_000):
            segments.append((current_speaker, part))
    if not segments:
        raise SpeechSynthesisError("Transcript contains no speakable segments")
    return segments


def _run(command: list[str], error_message: str) -> None:
    try:
        subprocess.run(command, check=True, capture_output=True, text=True)
    except FileNotFoundError as error:
        raise SpeechSynthesisError(f"Command not found: {command[0]}") from error
    except subprocess.CalledProcessError as error:
        detail = (error.stderr or error.stdout or "").strip()
        raise SpeechSynthesisError(f"{error_message}: {detail}") from error


def _synthesize_macos(transcript: Path, output: Path, voice: str) -> None:
    say_command = os.environ.get("P2P_SAY", "say")
    ffmpeg = os.environ.get("P2P_FFMPEG", "ffmpeg")
    with tempfile.TemporaryDirectory(prefix="paper-to-podcast-") as temp:
        aiff = Path(temp) / "speech.aiff"
        _run(
            [say_command, "-v", voice, "-f", str(transcript), "-o", str(aiff)],
            "macOS speech synthesis failed",
        )
        _run(
            [
                ffmpeg,
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-i",
                str(aiff),
                "-codec:a",
                "libmp3lame",
                "-b:a",
                "128k",
                str(output),
            ],
            "FFmpeg MP3 conversion failed",
        )


def _synthesize_openai(text: str, output: Path, voice: str) -> None:
    parts = chunk_text(text, max_chars=3_800)
    if not parts:
        raise SpeechSynthesisError("Transcript is empty")
    ffmpeg = os.environ.get("P2P_FFMPEG", "ffmpeg")
    with tempfile.TemporaryDirectory(prefix="paper-to-podcast-") as temp:
        temp_dir = Path(temp)
        audio_parts: list[Path] = []
        for index, part in enumerate(parts):
            path = temp_dir / f"part-{index:04d}.mp3"
            path.write_bytes(synthesize_mp3(part, voice))
            audio_parts.append(path)
        if len(audio_parts) == 1:
            shutil.copy2(audio_parts[0], output)
            return
        concat = temp_dir / "concat.txt"
        concat.write_text(
            "".join(f"file '{path.as_posix()}'\n" for path in audio_parts),
            encoding="utf-8",
        )
        _run(
            [
                ffmpeg,
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-f",
                "concat",
                "-safe",
                "0",
                "-i",
                str(concat),
                "-c",
                "copy",
                str(output),
            ],
            "FFmpeg audio assembly failed",
        )


def _synthesize_voxtral(
    text: str, output: Path, voice_aliases: list[str]
) -> dict[str, object]:
    segments = parse_speaker_segments(text)
    if not voice_aliases:
        raise SpeechSynthesisError("At least one Voxtral voice is required")
    voice_by_speaker = {
        speaker: voice_aliases[index % len(voice_aliases)]
        for index, speaker in enumerate(SPEAKER_LABELS)
    }
    ffmpeg = os.environ.get("P2P_FFMPEG", "ffmpeg")
    input_characters = 0
    used_speakers: set[str] = set()
    with tempfile.TemporaryDirectory(prefix="paper-to-podcast-voxtral-") as temp:
        temp_dir = Path(temp)
        wav_parts: list[Path] = []
        for index, (speaker, segment_text) in enumerate(segments):
            alias = voice_by_speaker[speaker]
            wav = synthesize_voxtral_wav(segment_text, alias)
            path = temp_dir / f"part-{index:04d}.wav"
            path.write_bytes(wav)
            wav_parts.append(path)
            input_characters += len(segment_text)
            used_speakers.add(speaker)
        concat = temp_dir / "concat.txt"
        concat.write_text(
            "".join(f"file '{path.as_posix()}'\n" for path in wav_parts),
            encoding="utf-8",
        )
        _run(
            [
                ffmpeg,
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-f",
                "concat",
                "-safe",
                "0",
                "-i",
                str(concat),
                "-codec:a",
                "libmp3lame",
                "-b:a",
                "128k",
                str(output),
            ],
            "FFmpeg Voxtral assembly failed",
        )
    used_voice_aliases = [
        voice_by_speaker[speaker]
        for speaker in SPEAKER_LABELS
        if speaker in used_speakers
    ]
    return {
        "provider": "mistral-voxtral",
        "model": os.environ.get("P2P_VOXTRAL_MODEL", VOXTRAL_MODEL),
        "endpoint": "/v1/audio/speech",
        "response_format": "wav",
        "segment_requests": len(segments),
        "input_characters": input_characters,
        "speaker_voice_aliases": {
            speaker: voice_by_speaker[speaker]
            for speaker in SPEAKER_LABELS
            if speaker in used_speakers
        },
        "resolved_voice_ids": {
            speaker: resolve_voice_id(voice_by_speaker[speaker])
            for speaker in SPEAKER_LABELS
            if speaker in used_speakers
        },
        "voices": used_voice_aliases,
        "system_voice_fallback_used": False,
    }


def synthesize(
    transcript_path: Path,
    output_path: Path,
    *,
    provider: str = "auto",
    allow_fallback: bool = True,
    voxtral_voices: list[str] | None = None,
) -> tuple[str, str | None, dict[str, object]]:
    selected = provider.lower()
    if selected == "auto":
        if os.environ.get("MISTRAL_API_KEY"):
            selected = "voxtral"
        else:
            selected = "openai" if os.environ.get("OPENAI_API_KEY") else "macos"
    voice = (
        os.environ.get("P2P_OPENAI_VOICE", "alloy")
        if selected == "openai"
        else os.environ.get("P2P_MACOS_VOICE", "Samantha")
    )
    text = transcript_path.read_text(encoding="utf-8").strip()
    fallback_reason: str | None = None
    details: dict[str, object] = {}
    try:
        if selected == "voxtral":
            voices = voxtral_voices or [
                part.strip()
                for part in os.environ.get(
                    "P2P_VOXTRAL_VOICES", "alloy,echo"
                ).split(",")
                if part.strip()
            ]
            details = _synthesize_voxtral(text, output_path, voices)
            voice = ",".join(voices)
        elif selected == "openai":
            _synthesize_openai(text, output_path, voice)
            details = {
                "provider": "openai",
                "voice": voice,
                "system_voice_fallback_used": False,
            }
        elif selected == "macos":
            _synthesize_macos(transcript_path, output_path, voice)
            details = {
                "provider": "macos-system-voice",
                "voice": voice,
                "system_voice_fallback_used": True,
            }
        else:
            raise SpeechSynthesisError(f"Unknown TTS provider: {provider}")
    except SpeechSynthesisError as error:
        if not allow_fallback or selected in {"macos", "voxtral"}:
            raise
        fallback_reason = str(error)
        selected = "macos"
        voice = os.environ.get("P2P_MACOS_VOICE", "Samantha")
        _synthesize_macos(transcript_path, output_path, voice)
        details = {
            "provider": "macos-system-voice",
            "voice": voice,
            "system_voice_fallback_used": True,
        }
    return f"{selected}:{voice}", fallback_reason, details


def validate_mp3(path: Path) -> dict[str, object]:
    if not path.is_file() or path.stat().st_size < 1024:
        raise AudioValidationError(f"Audio is missing or too small: {path}")
    ffprobe = os.environ.get("P2P_FFPROBE", "ffprobe")
    try:
        completed = subprocess.run(
            [
                ffprobe,
                "-v",
                "error",
                "-select_streams",
                "a:0",
                "-show_entries",
                "stream=codec_name,codec_type,sample_rate,channels,bit_rate,duration",
                "-show_entries",
                "format=format_name,duration,size,bit_rate",
                "-of",
                "json",
                str(path),
            ],
            check=True,
            capture_output=True,
            text=True,
        )
        data = json.loads(completed.stdout)
    except (FileNotFoundError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        raise AudioValidationError(f"ffprobe could not validate {path}: {error}") from error
    streams = data.get("streams", [])
    if not streams or streams[0].get("codec_name") != "mp3":
        raise AudioValidationError(f"Expected an MP3 audio stream in {path}")
    duration = float(data.get("format", {}).get("duration") or 0)
    if duration <= 1:
        raise AudioValidationError(f"Audio duration is invalid: {duration} seconds")
    return {
        "codec": streams[0].get("codec_name"),
        "sample_rate_hz": int(streams[0].get("sample_rate") or 0),
        "channels": int(streams[0].get("channels") or 0),
        "duration_seconds": duration,
        "size_bytes": path.stat().st_size,
        "format": data.get("format", {}).get("format_name"),
        "bit_rate_bps": int(data.get("format", {}).get("bit_rate") or 0),
    }
