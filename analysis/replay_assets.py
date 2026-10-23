"""Inspect referenced replay audio locally without following links outside a package."""
import math
from pathlib import Path, PurePosixPath
import struct

MAX_AUDIO_BYTES = 128 * 1024 * 1024


def asset_path(package, relative):
    if not isinstance(relative, str) or not relative or '\\' in relative or ':' in relative:
        raise ValueError('Invalid replay asset path')
    if PurePosixPath(relative).is_absolute() or any(p in ('', '.', '..') for p in relative.split('/')):
        raise ValueError('Invalid replay asset path segment')
    root = Path(package).resolve()
    path = root
    for part in relative.split('/'):
        path = path / part
        if path.is_symlink():
            raise ValueError('Linked replay assets are not supported')
    if not path.resolve().is_relative_to(root):
        raise ValueError('Replay asset escapes its package')
    return path


def inspect_wav(path):
    """Validate the canonical PCM16 format written by TrialReplayAudio.Encode."""
    size = path.stat().st_size
    if not path.is_file() or size < 44 or size > MAX_AUDIO_BYTES + 44:
        raise ValueError('Invalid replay audio file size/type')
    with path.open('rb') as file:
        header = file.read(44)
    if len(header) != 44:
        raise ValueError('Truncated replay audio header')
    riff, riff_size, wave, fmt, fmt_size, codec, channels, rate, byte_rate, align, bits, data, data_size = struct.unpack('<4sI4s4sIHHIIHH4sI', header)
    if (riff, wave, fmt, data) != (b'RIFF', b'WAVE', b'fmt ', b'data') or fmt_size != 16 or codec != 1:
        raise ValueError('Expected canonical replay PCM16 WAV')
    if channels not in (1, 2) or not 8000 <= rate <= 192000 or bits != 16:
        raise ValueError('Unsupported replay audio format')
    if align != channels*2 or byte_rate != rate*align:
        raise ValueError('Inconsistent replay audio rates/alignment')
    if riff_size != size-8 or data_size != size-44 or data_size <= 0 or data_size % align:
        raise ValueError('Invalid replay audio data length')
    frames = data_size // align
    return dict(bytes=size, channels=channels, sample_rate_hz=rate, frames=frames, duration_seconds=frames/rate)


def inspect_audio_assets(replay_path, rows, on_file=None):
    """Return asset evidence plus structural issues and missing-evidence findings.

    Repeated uses of one clip inspect it once. No waveform/audio is sent elsewhere;
    format consistency does not establish audible playback or correct voice identity.
    """
    replay_path = Path(replay_path)
    assets, issues, incomplete, files = {}, [], [], []
    def finding(kind, code, row, detail=''):
        kind.append(dict(code=code, source=str(replay_path), trial_id=row.get('sourceTrialId') or '',
                         detail=detail, replay_sequence=row.get('sequence')))
    for row in rows:
        clip = row.get('clip')
        starts = row.get('kind') == 'audio_playback_start'
        if starts:
            volume = row.get('audioVolume')
            if volume is not None and (type(volume) not in (float,int) or not math.isfinite(volume) or not 0 <= volume <= 1):
                finding(issues, 'invalid_recorded_audio_volume', row)
        if starts and 'audio_asset_missing=' in str(row.get('detail') or ''):
            finding(incomplete, 'recorder_reported_audio_asset_failure', row)
        if clip is not None and not isinstance(clip, str):
            finding(issues, 'invalid_audio_asset_reference', row)
            continue
        if not clip:
            if starts: finding(incomplete, 'playback_without_saved_audio', row)
            continue
        if clip in assets:
            assets[clip]['playback_references'] += int(starts)
            assets[clip]['event_sequences'].append(row.get('sequence'))
            continue
        result = dict(source=str(replay_path), relative_path=clip, status='unchecked',
                      bytes=None, channels=None, sample_rate_hz=None, frames=None,
                      duration_seconds=None, playback_references=int(starts), event_sequences=[row.get('sequence')])
        assets[clip] = result
        try:
            path = asset_path(replay_path.parent, clip)
        except ValueError as error:
            result['status'] = 'invalid_reference'
            finding(issues, 'unsafe_audio_asset_reference', row, str(error))
            continue
        if not path.exists():
            result['status'] = 'missing'
            finding(incomplete, 'missing_replay_audio_file', row, clip)
            continue
        if path.is_file() and path.stat().st_size <= MAX_AUDIO_BYTES+44:
            files.append(path)
            if on_file is not None: on_file(path)  # Fingerprint before parsing; caller rechecks after the full audit.
        try:
            metadata = inspect_wav(path)
        except (OSError, ValueError, struct.error) as error:
            result['status'] = 'invalid_audio'
            finding(issues, 'invalid_replay_audio_file', row, f'{clip}: {error}')
            continue
        result.update(metadata, status='consistent_pcm16')
    return dict(assets=list(assets.values()), issues=issues, incomplete=incomplete, files=files)
