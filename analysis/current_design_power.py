"""Synthetic current-design planning: trial-level REML, never participant results.

The sufficient-statistic likelihood below is algebraically the full trial-level
Gaussian LMM likelihood, not a regression on participant means. See companion
tests against dense covariance matrices and statsmodels MixedLM.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ProcessPoolExecutor
from dataclasses import asdict, dataclass, replace
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import time

import numpy as np
import scipy
from scipy import optimize, special, stats

VERSION = "current-design-lmm-v2-schedule-size"
SEED = 20261031
STEPS = np.array([0, 1, -1, 2, -2, 3, -3, 4])


@dataclass(frozen=True)
class Scenario:
    name: str = "middle"
    residual_sd: float = .55
    intercept_sd: float = .30
    slope_sd: float = .20
    median_seconds: float = 12.
    trial_loss: float = .05
    dropout: float = .10
    deadline: float = 25.
    ar1: float = 0.
    informative_loss: float = 0.
    re_correlation: float = 0.


MAIN = [Scenario("optimistic", .35, .15, .10, 10., .02, .05),
        Scenario(), Scenario("pessimistic", .80, .50, .35, 16., .10, .20)]


def schedule(rng, n, block_size=48):
    """Same signed-step shuffle law as ChallengeSet.Generate, not its PRNG bytes.

    Independent permutations of eight steps per block. Height/identity/layout
    draws do not enter this outcome model; practice is not a measured trial.
    """
    if block_size not in (32, 48):
        raise ValueError("Supported schedules have 32 or 48 trials per block")
    return STEPS[rng.random((n, 2, block_size // 8, 8)).argsort(axis=-1)].reshape(n, 2 * block_size) * 45


def generate(rng, n, ratio, s, block_size=48):
    if n < 12 or n % 2 or not 0 < ratio <= 1:
        raise ValueError("Even enrollment >=12 and 0 < time ratio <=1 required")
    signed = schedule(rng, n, block_size)
    total = 2 * block_size
    # Assumed exact-angle jitter, not observed calibration/geometry accuracy.
    theta = np.clip(abs(signed) + rng.uniform(-3, 3, signed.shape), 0, 180)
    a = theta / 90 - 1
    period = np.broadcast_to(np.repeat([-.5, .5], block_size), (n, total))
    order = np.tile([-1., 1.], n // 2)
    rng.shuffle(order)
    voice = period * order[:, None]  # +.5 self-similar; -.5 selected/neutral
    position = np.broadcast_to(np.tile(np.linspace(-.5, .5, block_size), 2), (n, total))
    x = np.stack([np.ones_like(a), voice, period,
                  np.broadcast_to(order[:, None], (n, total)), a, a*a, position], axis=-1)
    z = rng.normal(size=(n, 2))
    intercept = s.intercept_sd * z[:, 0]
    slope = s.slope_sd * (s.re_correlation*z[:, 0] +
                         math.sqrt(1-s.re_correlation**2)*z[:, 1])
    noise = rng.normal(0, s.residual_sd, (n, 2, block_size))
    for j in range(1, block_size):
        noise[:, :, j] = s.ar1*noise[:, :, j-1] + math.sqrt(1-s.ar1**2)*noise[:, :, j]
    beta = [math.log(s.median_seconds), math.log(ratio), -.08, 0., .30, -.08, -.10]
    y = x @ beta + intercept[:, None] + slope[:, None]*voice + noise.reshape(n, total)
    dropout = rng.random(n) < s.dropout
    exposed = np.ones((n, total), dtype=bool)
    exposed[dropout, block_size:] = False  # first-block observations remain eligible
    missing_prob = special.expit(special.logit(s.trial_loss) +
                                 s.informative_loss*(y-math.log(s.median_seconds)))
    incomplete = exposed & (rng.random(y.shape) < missing_prob)
    attempted = exposed & ~incomplete
    timeout = attempted & (y >= math.log(s.deadline))
    observed = attempted & ~timeout
    diag = dict(paired_participants=int((observed[:, :block_size].any(1) & observed[:, block_size:].any(1)).sum()),
                participants_with_data=int(observed.any(1).sum()),
                successful_trials=int(observed.sum()), dropped_participants=int(dropout.sum()),
                incomplete_trials=int(incomplete.sum()), exposed_trials=int(exposed.sum()))
    for name, mask in [("neutral", voice < 0), ("self", voice > 0)]:
        diag[f"attempted_{name}"] = int((attempted & mask).sum())
        diag[f"timeout_{name}"] = int((timeout & mask).sum())
    return x, y, observed, diag


def sufficient(x, y, observed):
    w = np.concatenate([x, y[..., None]], axis=-1) * observed[..., None]
    q = np.einsum("nij,nik->njk", w, w)
    return q, int(observed.sum())


def reml(lam, q, nobs, details=False):
    """Profile residual variance; Woodbury inverse and determinant lemma.

    V_i / sigma² = I + Z_i diag(lam) Z_i'; Z consists of X columns 0,1.
    Gradient: .5 [tr(P dV) - (m-p) y'P dV P y / y'P y].
    Nonnegative variance ratios allow exact boundary solutions.
    """
    p = q.shape[-1]-1
    a = q[:, :2, :2]
    b = q[:, :2, :]
    l0, l1 = lam
    det = (1+l0*a[:, 0, 0])*(1+l1*a[:, 1, 1])-l0*l1*a[:, 0, 1]**2
    c = np.empty_like(a)
    c[:, 0, 0] = l0*(1+l1*a[:, 1, 1])/det
    c[:, 1, 1] = l1*(1+l0*a[:, 0, 0])/det
    c[:, 0, 1] = c[:, 1, 0] = -l0*l1*a[:, 0, 1]/det
    cb = c @ b
    h = q.sum(0) - np.einsum("nki,nkj->ij", b, cb)
    hx = h[:p, :p]
    inv = np.linalg.inv(hx)
    beta = inv @ h[:p, p]
    rss = h[p, p] - h[p, :p] @ beta
    df = nobs-p
    sign, logdet = np.linalg.slogdet(hx)
    if sign != 1 or rss <= 0 or df <= 0:
        raise np.linalg.LinAlgError("Rank deficient or nonpositive residual variance")
    scale = rss/df
    objective = .5*(np.log(det).sum()+logdet+df*math.log(scale))
    wb = b-a @ cb
    r = wb[:, :, p] - wb[:, :, :p] @ beta
    trace = (a-a @ c @ a).diagonal(axis1=1, axis2=2).sum(0)
    trace -= np.einsum("nki,ij,nkj->k", wb[:, :, :p], inv, wb[:, :, :p])
    grad = .5*(trace-(r*r).sum(0)/scale)
    if details:
        return beta, inv*scale, scale, np.asarray(lam)*scale
    return objective, grad


def fit(x, y, observed, paired):
    if paired < 5:
        return dict(ok=False, error="fewer_than_five_pairs", retry=False)
    q, nobs = sufficient(x, y, observed)
    retry = False
    try:
        result = optimize.minimize(reml, [.3, .15], args=(q, nobs), jac=True,
                                   bounds=[(0, None)]*2, method="L-BFGS-B",
                                   options={"ftol": 1e-11, "gtol": 1e-5, "maxiter": 100})
        projected = np.where((result.x < 1e-8) & (result.jac > 0), 0, result.jac)
        if not result.success or np.max(abs(projected)) > .01:
            retry = True
            result = optimize.minimize(reml, [1., .5], args=(q, nobs), jac=True,
                                       bounds=[(0, None)]*2, method="L-BFGS-B",
                                       options={"ftol": 1e-13, "gtol": 1e-6, "maxiter": 200})
        projected = np.where((result.x < 1e-8) & (result.jac > 0), 0, result.jac)
        if not result.success or np.max(abs(projected)) > .01:
            return dict(ok=False, error="nonconverged", retry=retry)
        beta, cov, scale, variances = reml(result.x, q, nobs, details=True)
        se = math.sqrt(cov[1, 1])
        # Explicit small-sample approximation; NOT Satterthwaite/Kenward-Roger.
        df = paired-2
        cutoff = stats.t.isf(.025, df)
        return dict(ok=True, estimate=float(beta[1]), se=se,
                    p=float(2*stats.t.sf(abs(beta[1]/se), df)), halfwidth=cutoff*se,
                    slope_boundary=bool(result.x[1] < 1e-6),
                    intercept_boundary=bool(result.x[0] < 1e-6), retry=retry,
                    residual_sd=math.sqrt(scale), intercept_sd=math.sqrt(variances[0]),
                    slope_sd=math.sqrt(variances[1]))
    except (np.linalg.LinAlgError, ValueError, FloatingPointError) as exc:
        return dict(ok=False, error=type(exc).__name__, retry=retry)


def wilson(k, n):
    z = stats.norm.isf(.025)
    p = k/n
    center = (p+z*z/(2*n))/(1+z*z/n)
    half = z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/(1+z*z/n)
    return center-half, center+half


def run_cell(task):
    s, n, ratio, repetitions, seed, *design = task
    block_size = design[0] if design else 48
    # Stable per-cell seed; invariant to process count/task ordering.
    key = json.dumps([asdict(s), n, ratio, seed], sort_keys=True).encode()
    rng = np.random.default_rng(int.from_bytes(hashlib.sha256(key).digest()[:8], "little"))
    totals = {}
    fits, errors = [], {}
    rejects = retries = 0
    for _ in range(repetitions):
        x, y, obs, d = generate(rng, n, ratio, s, block_size)
        for k, v in d.items():
            totals[k] = totals.get(k, 0)+v
        f = fit(x, y, obs, d["paired_participants"])
        retries += int(f["retry"])
        if f["ok"]:
            fits.append(f)
            rejects += f["p"] < .05
        else:
            errors[f["error"]] = errors.get(f["error"], 0)+1
    power = rejects/repetitions  # failed fits remain non-rejections
    lo, hi = wilson(rejects, repetitions)
    out = dict(scenario=s.name, n_enrolled=n, ratio=ratio, repetitions=repetitions, trials_per_block=block_size,
               power=power, mcse=math.sqrt(power*(1-power)/repetitions),
               power_low=lo, power_high=hi, successful_fits=len(fits),
               failed_fits=repetitions-len(fits), optimizer_retries=retries,
               failures=json.dumps(errors, sort_keys=True), **asdict(s))
    out.pop("name")
    for k in totals:
        out["mean_"+k] = totals[k]/repetitions
    for name in ["neutral", "self"]:
        out["timeout_rate_"+name] = totals["timeout_"+name]/max(1, totals["attempted_"+name])
    if fits:
        est = np.array([f["estimate"] for f in fits])
        half = np.array([f["halfwidth"] for f in fits])
        truth = math.log(ratio)
        out.update(mean_log_estimate=float(est.mean()), bias_vs_latent=float(est.mean()-truth),
                   coverage_latent=float((abs(est-truth) <= half).mean()),
                   power_given_fit=rejects/len(fits),
                   slope_boundary_rate=np.mean([f["slope_boundary"] for f in fits]),
                   intercept_boundary_rate=np.mean([f["intercept_boundary"] for f in fits]),
                   mean_fitted_slope_sd=np.mean([f["slope_sd"] for f in fits]))
    return out


def tasks(kind, repetitions, seed):
    ratios = [1., .97, .95, .93, .90, .85]
    if kind == "curves":
        return [(s, n, r, repetitions, seed) for s in MAIN
                for n in range(12, 61, 2) for r in ratios]
    if kind == "n20":
        return [(s, 20, r, repetitions, seed) for s in MAIN for r in ratios]
    if kind == "grid":
        return [(Scenario(f"grid-r{r}-i{i}-v{v}", r, i, v), 60, ratio, repetitions, seed)
                for r in [.35, .55, .80] for i in [.15, .30, .50]
                for v in [0., .10, .20, .35, .50] for ratio in ratios]
    if kind == "stress":
        cases = [replace(MAIN[1], name="no_deadline", deadline=1e8),
                 replace(MAIN[1], name="serial_rho_0.3", ar1=.3),
                 replace(MAIN[1], name="informative_trial_loss", informative_loss=2.),
                 replace(MAIN[1], name="correlated_effects", re_correlation=.5)]
        return [(s, n, r, repetitions, seed) for s in cases for n in [30, 60] for r in ratios]
    if kind == "precision":
        return [(s, 60, r, repetitions, seed) for s in MAIN
                for r in [1., .97, .95, .93, .925, .90, .875, .85, .80, .75]]
    raise ValueError(kind)


def write_csv(path, rows):
    with Path(path).open("w", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--kind", choices=["curves", "grid", "stress", "precision", "n20"], default="curves")
    p.add_argument("--block-size", type=int, choices=[32, 48], default=48)
    p.add_argument("--repetitions", type=int, default=2000)
    p.add_argument("--workers", type=int, default=6)
    p.add_argument("--seed", type=int, default=SEED)
    args = p.parse_args()
    if args.repetitions < 1 or args.workers < 1:
        p.error("Positive repetitions/workers required")
    args.out.mkdir(parents=True, exist_ok=True)
    target = args.out / f"{args.kind}.csv"
    if target.exists():
        p.error(f"Refusing to overwrite {target}")
    start = time.monotonic()
    jobs = [task + (args.block_size,) for task in tasks(args.kind, args.repetitions, args.seed)]
    rows = []
    with ProcessPoolExecutor(args.workers) as pool:
        for row in pool.map(run_cell, jobs, chunksize=1):
            rows.append(row)
            if len(rows) % 6 == 0:
                write_csv(target.with_suffix(".partial.csv"), rows)
                print(f"{len(rows)}/{len(jobs)} cells; {time.monotonic()-start:.1f}s", flush=True)
    write_csv(target, rows)
    target.with_suffix(".partial.csv").unlink(missing_ok=True)
    metadata = dict(version=VERSION, kind=args.kind, seed=args.seed, repetitions=args.repetitions,
                    trials_per_block=args.block_size,
                    workers=args.workers, seconds=time.monotonic()-start, cells=len(rows),
                    python=platform.python_version(), numpy=np.__version__, scipy=scipy.__version__,
                    script_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                    scenarios=[asdict(s) for s in MAIN], thread_limit=os.environ.get("OPENBLAS_NUM_THREADS"))
    (args.out/f"{args.kind}-run.json").write_text(json.dumps(metadata, indent=2)+"\n")


if __name__ == "__main__":
    main()
