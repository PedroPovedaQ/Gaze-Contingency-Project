#!/usr/bin/env python3
"""Pull the thesis from Overleaf; never push, commit, or overwrite divergent edits."""
import argparse
import fcntl
import hashlib
import json
import os
import shutil
from pathlib import Path, PurePosixPath
import subprocess
import tempfile
import time
import zipfile

PROJECT = "6adbf46b3f96e54f5b3cc805"
REMOTE = f"https://git@git.overleaf.com/{PROJECT}"
ROOT = Path(__file__).resolve().parents[1] / "docs/thesis"
STATE = Path.home() / ".local/share/follow-my-voice-overleaf"
TEX_BIN = Path.home() / ".cache/ucf-thesis-tex/TinyTeX/bin/universal-darwin"
PDF_NAMES = ("main.pdf", "follow-my-voice-thesis-first-pass.pdf")


def permitted(name):
    p = PurePosixPath(name)
    if p.is_absolute() or ".." in p.parts or str(p) != name:
        return False
    if name in {"main.tex", "references.bib", "UCF_ETD.cls", "UCF_ETD_accessibility.tex"}:
        return True
    return len(p.parts) > 1 and p.parts[0] in {"chapters", "appendices", "frontmatter", "figures"} and p.suffix.lower() in {".tex", ".bib", ".pdf", ".png", ".jpg", ".jpeg", ".svg"}


def digest(data):
    return None if data is None else hashlib.sha256(data).hexdigest()


def local_bytes(root, name):
    if not permitted(name):
        raise ValueError(f"Out-of-scope source path: {name}")
    root = root.resolve()
    path = root / name
    parents = path.relative_to(root).parts[:-1]
    if path.is_symlink() or any(root.joinpath(*parents[:i]).is_symlink() for i in range(1, len(parents) + 1)):
        raise ValueError(f"Refusing symlink destination: {name}")
    return path.read_bytes() if path.exists() else None


def plan(root, baseline, incoming):
    changes, conflicts = {}, []
    for name in sorted(set(baseline) | set(incoming)):
        current = local_bytes(root, name)
        remote = incoming.get(name)
        previous = baseline.get(name)
        if digest(current) == digest(remote) or digest(remote) == previous:
            continue
        if digest(current) == previous:
            changes[name] = remote
        else:
            conflicts.append(name)
    return changes, conflicts


def atomic_write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def save_json(path, value):
    atomic_write(path, (json.dumps(value, indent=2) + "\n").encode())


def git(*args):
    env = {k: v for k, v in os.environ.items() if not k.startswith("GIT_")}
    env.update(GIT_TERMINAL_PROMPT="0", GIT_ASKPASS="/usr/bin/false")
    result = subprocess.run(["git", "-c", "core.hooksPath=/dev/null", *args], env=env,
                            capture_output=True, timeout=120, check=True)
    return result.stdout


def synchronize(root, state):
    baseline_path = state / "baseline.json"
    if not baseline_path.exists():
        raise ValueError("Missing upload baseline; initialize with --seed-zip before syncing.")
    baseline = json.loads(baseline_path.read_text())
    mirror = state / "overleaf.git"
    if not mirror.exists():
        git("clone", "--bare", REMOTE, str(mirror))
    git("--git-dir", str(mirror), "fetch", REMOTE, "refs/heads/main")
    revision = git("--git-dir", str(mirror), "rev-parse", "FETCH_HEAD").decode().strip()
    entries = git("--git-dir", str(mirror), "ls-tree", "-rz", revision).split(b"\0")
    incoming, ignored = {}, []
    for entry in filter(None, entries):
        metadata, raw_name = entry.split(b"\t", 1)
        mode, kind, oid = metadata.decode().split()
        name = raw_name.decode()
        if not permitted(name):
            ignored.append(name)
            continue
        if mode not in {"100644", "100755"} or kind != "blob":
            raise ValueError(f"Unsupported source entry: {name}")
        incoming[name] = git("--git-dir", str(mirror), "cat-file", "blob", oid)
    if "main.tex" not in incoming or "references.bib" not in incoming:
        raise ValueError("Incomplete thesis snapshot; no local files changed.")
    changes, conflicts = plan(root, baseline, incoming)
    report = {"time": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "project": PROJECT,
              "revision": revision, "status": "conflict" if conflicts else "up_to_date",
              "changed": [], "conflicts": conflicts, "ignored": ignored}
    if conflicts:
        for name in conflicts:
            if name in incoming:
                atomic_write(state / "conflicts" / revision / name, incoming[name])
        save_json(state / "status.json", report)
        return report
    backup = state / "backups" / f"{time.time_ns()}-{revision[:8]}"
    # Check again before writing, and retain every prior local file for recovery.
    before = {name: local_bytes(root, name) for name in changes}
    for name, content in before.items():
        if content is not None:
            atomic_write(backup / name, content)
    for name, data in changes.items():
        if local_bytes(root, name) != before[name]:
            raise ValueError(f"Local file changed during sync: {name}; rerun after editing stops.")
        if data is None:
            (root / name).unlink()
        else:
            atomic_write(root / name, data)
    save_json(baseline_path, {name: digest(data) for name, data in incoming.items()})
    report.update(status="updated" if changes else "up_to_date", changed=list(changes))
    save_json(state / "status.json", report)
    return report


def source_snapshot(root):
    return {p.relative_to(root).as_posix(): local_bytes(root, p.relative_to(root).as_posix())
            for p in sorted(root.rglob("*"))
            if permitted(p.relative_to(root).as_posix()) and (p.is_file() or p.is_symlink())}


def snapshot_hash(snapshot):
    return digest(json.dumps({name: digest(data) for name, data in snapshot.items()}, sort_keys=True).encode())


def compile_snapshot(directory, log):
    env = os.environ.copy()
    env["PATH"] = str(TEX_BIN) + os.pathsep + env.get("PATH", "")
    latex = env.get("LUALATEX") or shutil.which("lualatex", path=env["PATH"])
    bibtex = env.get("BIBTEX") or shutil.which("bibtex", path=env["PATH"])
    if not latex or not bibtex:
        raise RuntimeError("LuaLaTeX/BibTeX unavailable; last good PDF preserved.")
    latex_command = [latex, "-no-shell-escape", "-interaction=nonstopmode", "-halt-on-error", "main.tex"]
    with log.open("wb") as output:
        for command in (latex_command, [bibtex, "main"], latex_command, latex_command):
            subprocess.run(command, cwd=directory, env=env, stdout=output, stderr=subprocess.STDOUT,
                           check=True, timeout=300)
    final_log = (directory / "main.log").read_text(errors="replace")
    if "There were undefined references" in final_log or "Citation" in final_log and "undefined on input line" in final_log:
        raise RuntimeError("Unresolved references/citations; inspect rebuild.log. Last good PDF preserved.")


def rebuild_pdf(root, state, report):
    """Compile an isolated snapshot; publish only successful, still-current output."""
    if report["status"] not in {"updated", "up_to_date"}:
        return {"status": "skipped", "reason": "Source sync did not complete cleanly."}
    build_state = state / "build.json"
    log = state / "rebuild.log"
    started = time.monotonic()
    try:
        snapshot = source_snapshot(root)
        fingerprint = snapshot_hash(snapshot)
        previous = json.loads(build_state.read_text()) if build_state.exists() else {}
        if (previous.get("status") == "success" and previous.get("source_hash") == fingerprint
                and all((root / name).is_file() and digest((root / name).read_bytes()) == previous.get("pdf_hash")
                        for name in PDF_NAMES)):
            return {"status": "up_to_date", "source_hash": fingerprint, "pdf": str(root / PDF_NAMES[1])}
        with tempfile.TemporaryDirectory(prefix="build-", dir=state) as temporary:
            directory = Path(temporary)
            for name, data in snapshot.items():
                atomic_write(directory / name, data)
            compile_snapshot(directory, log)
            pdf = (directory / "main.pdf").read_bytes()
            if not pdf.startswith(b"%PDF-") or b"%%EOF" not in pdf[-1024:]:
                raise RuntimeError("Compiler did not produce a complete PDF.")
            if snapshot_hash(source_snapshot(root)) != fingerprint:
                raise RuntimeError("Sources changed during compilation; retry on the next sync.")
            backup = state / "pdf-backups" / str(time.time_ns())
            for name in PDF_NAMES:
                if (root / name).exists():
                    atomic_write(backup / name, (root / name).read_bytes())
            for name in PDF_NAMES:
                atomic_write(root / name, pdf)
        result = {"status": "success", "source_hash": fingerprint, "pdf_hash": digest(pdf),
                  "pdf": str(root / PDF_NAMES[1]), "log": str(log),
                  "seconds": round(time.monotonic() - started, 2), "revision": report.get("revision")}
    except Exception as exc:
        result = {"status": "failed", "error": str(exc), "log": str(log),
                  "seconds": round(time.monotonic() - started, 2)}
    save_json(build_state, result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed-zip", type=Path, help="Original uploaded source ZIP; one-time baseline")
    args = parser.parse_args()
    STATE.mkdir(parents=True, exist_ok=True, mode=0o700)
    with (STATE / "sync.lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if args.seed_zip:
            if (STATE / "baseline.json").exists():
                raise ValueError("Baseline already exists; refusing to reset conflict protection.")
            with zipfile.ZipFile(args.seed_zip) as archive:
                baseline = {name: digest(archive.read(name)) for name in archive.namelist() if permitted(name)}
            if "main.tex" not in baseline:
                raise ValueError("Upload archive does not contain main.tex.")
            save_json(STATE / "baseline.json", baseline)
        try:
            report = synchronize(ROOT, STATE)
            report["build"] = rebuild_pdf(ROOT, STATE, report)
            save_json(STATE / "status.json", report)
        except Exception as exc:
            report = {"status": "error", "error": str(exc), "time": time.strftime("%Y-%m-%dT%H:%M:%S%z")}
            save_json(STATE / "status.json", report)
            print(json.dumps(report, indent=2))
            return 1
        print(json.dumps(report, indent=2))
        return 2 if report["conflicts"] else 1 if report["build"]["status"] == "failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())
