"""Observe mirrored Jev traces on the Mac; never send commands or audio to the headset."""
import argparse
from collections import Counter
import fcntl
import hashlib
import json
import math
import os
from pathlib import Path
import sqlite3
import statistics
import time

from data import blocked, unpack
import adaptive_data

MAX_LINE = 1024 * 1024


def detail(record):
    raw = record.get('detail', '')
    if not isinstance(raw, str) or '{' not in raw: return None
    try:
        value = json.loads(raw[raw.index('{'):])
        return value if isinstance(value, dict) else None
    except ValueError: return None


def latency(values):
    values = sorted(v for v in values if type(v) in (float, int) and math.isfinite(v) and v >= 0)
    return dict(n=len(values), median=statistics.median(values) if values else None,
                p95=values[max(0, math.ceil(.95*len(values))-1)] if values else None)


class Shadow:
    def __init__(self, source, output, policy, fingerprint, from_start=False):
        self.source, self.output = Path(source).resolve(), Path(output).resolve()
        if not self.source.is_dir(): raise ValueError('Replay source directory does not exist')
        if self.output == self.source or self.source in self.output.parents:
            raise ValueError('Shadow output must be outside the read-only replay source')
        self.output.mkdir(parents=True, exist_ok=True, mode=0o700)
        self.policy = policy
        self.scope = getattr(policy, 'scope', 'navigation_action')
        self.unpack = adaptive_data.unpack if self.scope == 'assistance' else unpack
        self.blocked = adaptive_data.blocked if self.scope == 'assistance' else blocked
        self.silence = 'observe_1s' if self.scope == 'assistance' else 'wait'
        self.db = sqlite3.connect(self.output/'shadow.sqlite3')
        os.chmod(self.output/'shadow.sqlite3', 0o600)
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT);
            CREATE TABLE IF NOT EXISTS files (path TEXT PRIMARY KEY, offset INTEGER, skip_partial INTEGER);
            CREATE TABLE IF NOT EXISTS decisions (recording TEXT, id TEXT, value TEXT, PRIMARY KEY(recording,id));
        ''')
        identity = json.dumps(dict(schema=1, source=str(self.source), model=fingerprint,
                                   device=getattr(policy, 'device', 'test')), sort_keys=True)
        old = self.db.execute("SELECT value FROM meta WHERE key='identity'").fetchone()
        if old and old[0] != identity:
            self.close()
            raise ValueError('Output belongs to a different model/source; choose a new output folder')
        if not old:
            with self.db:
                self.db.execute('INSERT INTO meta VALUES (?,?)', ('identity', identity))
                self.db.execute('INSERT INTO meta VALUES (?,?)', ('mode', 'backfill' if from_start else 'live'))
                if not from_start:
                    for path in self.paths():
                        with path.open('rb') as f:
                            f.seek(0, 2); size = f.tell()
                            if size: f.seek(-1, 2)
                            partial = bool(size and f.read(1) != b'\n')
                        self.db.execute('INSERT INTO files VALUES (?,?,?)', (str(path.relative_to(self.source)), size, partial))

    def paths(self):
        return sorted((self.source/'GazeReplays').glob('*/recording.jsonl'))

    def close(self): self.db.close()

    def observe(self, recording, record):
        if record.get('kind') not in ('jev_trace', 'navigation_status', 'audio_playback_start'): return
        value = detail(record)
        if record['kind'] == 'jev_trace':
            if not value or value.get('schema') != 'jev-trace-v1': return
            # Active MiniLM traces are not cloud Jev comparison ground truth.
            if value.get('provider', 'jev') != 'jev': return
            request_id = value.get('request_id')
        else:
            request_id = (value or {}).get('decision_id') or record.get('guidanceDecisionId')
        if not isinstance(request_id, str) or not request_id or len(request_id) > 128: return
        old = self.db.execute('SELECT value FROM decisions WHERE recording=? AND id=?', (recording, request_id)).fetchone()
        item = json.loads(old[0]) if old else dict(recording=recording, request_id=request_id)
        if record['kind'] == 'navigation_status':
            if value and value.get('schema') == 'development-action-result-v1':
                item['scheduling'] = {key: value.get(key) for key in ('action', 'source', 'accepted_for_scheduling')}
        elif record['kind'] == 'audio_playback_start':
            item['playback_observed'] = True
        elif value.get('stage') == 'request' and 'payload' not in item:
            payload = value.get('payload')
            item.update(payload=payload, trial_id=record.get('trialId'), request_time=record.get('time'),
                        observed_utc=time.time(), deadline_ms=value.get('deadline_ms'))
            if (not isinstance(payload, dict) or set(payload.get('questions') or {}) != {self.scope} or
                    (self.scope == 'assistance' and (payload.get('state') or {}).get('policy') != adaptive_data.POLICY)):
                item['local'] = dict(status='unsupported')
            else:
                start = time.perf_counter()
                try:
                    choices = self.unpack(payload)[-1]
                    result = self.policy.predict(payload, guard=False)
                    answer = result['answers'][self.scope]
                    choice = answer['choice']
                    if answer.get('type') != 'choice' or choice not in choices:
                        raise ValueError('Unoffered local choice')
                    gate_closed = self.blocked(payload)
                    item['local'] = dict(status='ok', raw_choice=result.get('raw_choice', choice),
                                         masked_choice=choice, gated_choice=self.silence if gate_closed else choice,
                                         probabilities=answer.get('probabilities'),
                                         gate_closed=gate_closed, compute_ms=(time.perf_counter()-start)*1000)
                except Exception as error:
                    item['local'] = dict(status='error', error_type=type(error).__name__)
        elif value.get('stage') == 'response' and 'jev' not in item:
            item['jev'] = {key: value.get(key) for key in ('status', 'answers', 'elapsed_ms', 'error_type')}
        elif value.get('stage') == 'application':
            item['application'] = value.get('outcome')
        self.db.execute('INSERT OR REPLACE INTO decisions VALUES (?,?,?)', (recording, request_id, json.dumps(item)))

    def poll(self):
        consumed = 0
        with self.db:
            for path in self.paths():
                name = str(path.relative_to(self.source))
                row = self.db.execute('SELECT offset,skip_partial FROM files WHERE path=?', (name,)).fetchone()
                offset, skip = row or (0, False)
                if path.stat().st_size < offset:
                    raise ValueError('Replay source shrank; preserving evidence: '+name)
                with path.open('rb') as f:
                    f.seek(offset)
                    # Bound catch-up per file, without dropping records or blocking the receiver.
                    while f.tell()-offset < 2*MAX_LINE:
                        before = f.tell(); line = f.readline(MAX_LINE+1)
                        if len(line) > MAX_LINE: raise ValueError('Oversized replay record: '+name)
                        if not line.endswith(b'\n'): f.seek(before); break
                        consumed += len(line)
                        if skip: skip = False; continue
                        try: record = json.loads(line)
                        except (ValueError, UnicodeError):
                            self.db.execute("INSERT INTO meta VALUES ('malformed','1') ON CONFLICT(key) DO UPDATE SET value=CAST(value AS INTEGER)+1")
                            continue
                        if isinstance(record, dict): self.observe(name, record)
                    self.db.execute('INSERT OR REPLACE INTO files VALUES (?,?,?)', (name, f.tell(), skip))
        return consumed

    def decisions(self):
        return [json.loads(row[0]) for row in self.db.execute('SELECT value FROM decisions ORDER BY recording,id')]

    def report(self, decisions=None):
        decisions = self.decisions() if decisions is None else decisions
        items = [r for r in decisions if 'payload' in r]
        supported = [r for r in items if r.get('local', {}).get('status') != 'unsupported']
        paired = []
        for row in supported:
            answer = (row.get('jev', {}).get('answers') or {}).get(self.scope) or {}
            if (row.get('local', {}).get('status') == 'ok' and row.get('jev', {}).get('status') == 'ok'
                    and answer.get('type') == 'choice' and answer.get('choice') in row['payload']['questions'][self.scope]['criteria']):
                paired.append((row, answer['choice']))
        result = dict(schema='local-jev-shadow-v1', scope=self.scope, updated_utc=time.time(),
                      identity=json.loads(self.db.execute("SELECT value FROM meta WHERE key='identity'").fetchone()[0]),
                      mode=self.db.execute("SELECT value FROM meta WHERE key='mode'").fetchone()[0],
                      requests=len(items), supported=len(supported), unsupported=len(items)-len(supported),
                      orphan_responses=sum('payload' not in r and 'jev' in r for r in decisions),
                      local_errors=sum(r.get('local', {}).get('status') == 'error' for r in supported),
                      jev_statuses=dict(Counter(r.get('jev', {}).get('status') or 'pending' for r in supported)),
                      comparable=len(paired),
                      local_compute_ms=latency([r['local']['compute_ms'] for r, _ in paired]),
                      jev_latency_ms=latency([r['jev'].get('elapsed_ms') for r, _ in paired]),
                      discarded=sum(str(r.get('application', '')).startswith('discarded') for r in supported),
                      playback_observed=sum(r.get('playback_observed', False) for r in supported),
                      interpretation='Agreement with Jev, not accuracy. Mac compute excludes replay-transfer delay; Jev elapsed includes headset/network. Neither measures acoustic onset. No playback authority.')
        for kind in ('raw', 'masked', 'gated'):
            result[kind+'_matches'] = sum(r['local'][kind+'_choice'] == choice for r, choice in paired)
        return result

    def export(self, status):
        decisions = self.decisions()
        report = self.report(decisions); report['status'] = status
        report['runner_pid'] = os.getpid()
        for name, value in [('summary.json', report), ('decisions.json', decisions)]:
            pending = self.output/(name+'.tmp')
            with pending.open('w') as f:
                os.chmod(pending, 0o600)
                json.dump(value, f, indent=2, allow_nan=False)
                f.flush(); os.fsync(f.fileno())
            pending.replace(self.output/name)
        return report


def fingerprint(path):
    digest = hashlib.sha256()
    # Feature extraction and guards are part of the model's inference identity.
    for name in ('model.py', 'adaptive_data.py', 'data.py', 'continuation_data.py', 'shadow.py'):
        digest.update((Path(__file__).parent/name).read_bytes())
    for file in sorted(Path(path).iterdir()):
        if file.is_file():
            digest.update(file.name.encode())
            with file.open('rb') as f:
                for block in iter(lambda: f.read(1024*1024), b''): digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=Path.home()/'Documents/Trial Review Live/data')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--device', choices=['cpu', 'mps'], default='cpu')
    parser.add_argument('--from-start', action='store_true', help='Include existing records; default starts at current archive ends')
    parser.add_argument('--once', action='store_true', help='Catch up then exit')
    args = parser.parse_args()
    os.umask(0o077)
    args.output.mkdir(parents=True, exist_ok=True)
    with (args.output/'shadow.lock').open('w') as lock:
        try: fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError: raise SystemExit('A shadow observer already owns this output')
        from model import Policy
        policy = Policy(args.model, args.device)
        shadow = Shadow(args.source, args.output, policy, fingerprint(args.model), args.from_start)
        print(json.dumps(dict(status='observing', pid=os.getpid(), output=str(args.output), device=args.device)), flush=True)
        try:
            shadow.export('observing')
            while not (args.output/'stop-request').exists():
                consumed = shadow.poll()
                if consumed: shadow.export('observing')
                if args.once:
                    if consumed: continue
                    break
                time.sleep(.5)
            shadow.export('stopped')
        except KeyboardInterrupt: shadow.export('stopped')
        except Exception:
            shadow.export('error')
            raise
        finally: shadow.close()


if __name__ == '__main__': main()
