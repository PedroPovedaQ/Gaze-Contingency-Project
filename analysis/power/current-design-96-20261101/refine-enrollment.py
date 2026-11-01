"""Reproduce the 96-trial middle-scenario enrollment crossing refinement."""
import argparse
from concurrent.futures import ProcessPoolExecutor
from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import platform
import sys
import time


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--repo', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--workers', type=int, default=2)
    args = p.parse_args()
    sys.path.insert(0, str(args.repo.resolve()/'analysis'))
    import current_design_power as power
    target = args.out/'enrollment.csv'
    if target.exists():
        p.error(f'Refusing to overwrite {target}')
    start = time.monotonic()
    jobs = [(power.MAIN[1], n, .90, 5000, power.SEED, 48) for n in range(44, 61, 2)]
    with ProcessPoolExecutor(args.workers) as pool:
        rows = list(pool.map(power.run_cell, jobs))
    power.write_csv(target, rows)
    metadata = dict(version=power.VERSION, kind='enrollment', seed=power.SEED, repetitions=5000,
        trials_per_block=48, workers=args.workers, cells=len(rows), seconds=time.monotonic()-start,
        python=platform.python_version(), numpy=power.np.__version__, scipy=power.scipy.__version__,
        script_sha256=hashlib.sha256(Path(power.__file__).read_bytes()).hexdigest(),
        driver_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), scenarios=[asdict(power.MAIN[1])])
    target.with_name('enrollment-run.json').write_text(json.dumps(metadata, indent=2)+'\n')
    print([(r['n_enrolled'], r['power']) for r in rows], flush=True)


if __name__ == '__main__':
    main()
