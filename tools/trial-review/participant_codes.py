"""Local-only participant numbering. Reservation ledger is append-only by convention."""
import json
from pathlib import Path
import re

DEV_BASE = 900_000_000_000
MAX_CODE = 999_999_999_999


def number(code):
    return int(code[1:]) if isinstance(code, str) and re.fullmatch(r'P[0-9]{3,12}', code) else None


def known_codes(intake_root, catalog=None, archives=(), known=()):
    used, development, study = set(), set(), set()

    def add(value, dev=None):
        if number(value) is not None:
            used.add(value)
            if dev: development.add(value)
            if dev is False: study.add(value)

    def read(path):
        # An unreadable inventory must not silently reset numbering to P001.
        try: return json.loads(path.read_text())
        except (OSError, ValueError) as exc: raise ValueError('Cannot read participant inventory: ' + str(path)) from exc

    root = Path(intake_root)
    for code in known: add(code)
    ledger = root / 'participant-codes.json'
    if ledger.exists():
        saved = read(ledger)
        for code in saved.get('used', []): add(code)
        development.update(saved.get('development', []))
    for path in root.glob('*/session.json'):
        state = read(path); add(state.get('participantCode'), state.get('development') is True)
    if catalog and Path(catalog).is_file():
        data = read(Path(catalog))
        for participant in data.get('participants', []): add(participant.get('id'))
        for dataset in data.get('datasets', []):
            add(dataset.get('participantId')); add(dataset.get('sourceParticipantId'))
            for old in dataset.get('assignments', []):
                add(old.get('previousId')); add(old.get('participantId'))
            if dataset.get('studyDataPath'): archives = (*archives, str(Path(dataset['studyDataPath']).parent))
    for archive in set(str(a) for a in archives if a):
        path = Path(archive)
        if not path.is_dir(): continue
        add(path.name)
        # Only directory names and minimal assignment metadata; never read audio/transcripts.
        for directory in path.rglob('*'):
            if directory.is_dir(): add(directory.name)
            elif directory.name in ('intake-assignment-v2.json', 'session.json'):
                data = read(directory); add(data.get('participantCode'), data.get('development') is True)
    return used, development - study


def suggest(used, development=(), rehearsal=False, headset=None):
    values = [number(code) for code in used if code not in development or rehearsal]
    low, high = (DEV_BASE, MAX_CODE) if rehearsal else (0, DEV_BASE - 1)
    greatest = max([low] + [v for v in values if v is not None and low <= v <= high])
    candidate = greatest + 1
    hint = number(headset)
    # Headset's suggestion is an UNUSED candidate, not a used participant.
    if hint is not None and low < hint <= high: candidate = max(candidate, hint)
    while any(number(code) == candidate for code in used): candidate += 1
    if candidate > high: raise ValueError('Participant code sequence exhausted.')
    return f'P{candidate:03d}'


def duplicate(code, used):
    value = number(code)
    return value is not None and any(number(existing) == value for existing in used)
