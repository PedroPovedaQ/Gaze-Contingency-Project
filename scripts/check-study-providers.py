#!/usr/bin/env python3
"""Offline source guard against retired study voice-provider integrations.

Scan tracked and untracked, non-ignored study source/config files. Historical
provider labels remain valid. Never read api_keys.json, recordings or binaries.
"""
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STUDY_PATHS = ('Assets', 'tools/trial-review', 'tools/local-jev', 'scripts', 'analysis')
SOURCE_SUFFIXES = {
    '.cs', '.py', '.sh', '.bash', '.zsh', '.js', '.mjs', '.ts', '.json',
    '.toml', '.yaml', '.yml', '.ini', '.cfg', '.txt', '.lock', '.asmdef',
    '.unity', '.prefab', '.asset', '.xml', '.plist',
}
GUARD_FILES = {'scripts/check-study-providers.py', 'scripts/tests/test_study_providers.py'}
FORBIDDEN = re.compile(r'api\.mistral\.ai|mistral[\W_]*(?:api[\W_]*)?key|voxtral|\bmistralai\b|\b(?:from|import)\s+mistral', re.I)


def violations(root, paths):
    found = []
    for relative in paths:
        path = Path(relative)
        if path.name == 'api_keys.json' or path.as_posix() in GUARD_FILES:
            continue
        if path.suffix.lower() not in SOURCE_SUFFIXES:
            continue
        source = root / path
        if not source.is_file():
            continue  # tracked deletions
        with source.open('rb') as stream:
            if b'\0' in stream.read(4096):
                continue  # Unity also uses .asset for binary serialized data.
        with source.open(encoding='utf-8', errors='replace') as stream:
            for line, text in enumerate(stream, 1):
                if FORBIDDEN.search(text):
                    # Report locations only; config values never enter the output.
                    found.append(f'{path.as_posix()}:{line}')
    return found


def check(root=ROOT):
    paths = subprocess.check_output(
        ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard', '--', *STUDY_PATHS],
        cwd=root,
    ).decode().split('\0')
    return violations(root, sorted(set(filter(None, paths))))


if __name__ == '__main__':
    found = check()
    if found:
        raise SystemExit('Retired study provider integration found at:\n' + '\n'.join(found))
    print('PASS: study source contains no retired provider endpoint, key lookup, import or Voxtral call.')
