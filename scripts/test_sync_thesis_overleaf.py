import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import json

spec = importlib.util.spec_from_file_location("sync", Path(__file__).with_name("sync-thesis-overleaf.py"))
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)


class SyncTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.file = self.root / "main.tex"
        self.file.write_bytes(b"original")
        self.base = {"main.tex": sync.digest(b"original")}

    def test_remote_edit(self):
        self.assertEqual(sync.plan(self.root, self.base, {"main.tex": b"remote"}), ({"main.tex": b"remote"}, []))

    def test_local_only_edit_preserved(self):
        self.file.write_bytes(b"local")
        self.assertEqual(sync.plan(self.root, self.base, {"main.tex": b"original"}), ({}, []))

    def test_divergent_edit_blocks(self):
        self.file.write_bytes(b"local")
        self.assertEqual(sync.plan(self.root, self.base, {"main.tex": b"remote"}), ({}, ["main.tex"]))

    def test_identical_concurrent_edit(self):
        self.file.write_bytes(b"same")
        self.assertEqual(sync.plan(self.root, self.base, {"main.tex": b"same"}), ({}, []))

    def test_remote_delete(self):
        self.assertEqual(sync.plan(self.root, self.base, {}), ({"main.tex": None}, []))
        self.file.write_bytes(b"local")
        self.assertEqual(sync.plan(self.root, self.base, {}), ({}, ["main.tex"]))

    def test_local_delete_conflicts_with_remote_edit(self):
        self.file.unlink()
        self.assertEqual(sync.plan(self.root, self.base, {"main.tex": b"remote"}), ({}, ["main.tex"]))

    def test_new_file_collision(self):
        self.assertEqual(sync.plan(self.root, {}, {"main.tex": b"remote"}), ({}, ["main.tex"]))

    def test_path_scope(self):
        for name in ("../main.tex", "/main.tex", "chapters/../../secrets.tex", "build.sh", "review/result.json", "figures/x.py"):
            self.assertFalse(sync.permitted(name), name)
        for name in ("main.tex", "chapters/new.tex", "figures/new.png"):
            self.assertTrue(sync.permitted(name), name)

    def test_symlink_rejected(self):
        self.file.unlink()
        self.file.symlink_to(self.root / "outside")
        with self.assertRaises(ValueError):
            sync.plan(self.root, self.base, {"main.tex": b"remote"})

    def test_atomic_write(self):
        sync.atomic_write(self.file, b"updated")
        self.assertEqual(self.file.read_bytes(), b"updated")

    def test_full_sync_backup_and_conflict(self):
        state = self.root / "state"
        state.mkdir()
        (state / "overleaf.git").mkdir()
        (self.root / "references.bib").write_bytes(b"refs")
        sync.save_json(state / "baseline.json", {**self.base, "references.bib": sync.digest(b"refs")})
        remote = {"1": b"remote edit", "2": b"refs"}

        def fake_git(*args):
            if "rev-parse" in args:
                return b"revision\n"
            if "ls-tree" in args:
                return b"100644 blob 1\tmain.tex\0" + b"100644 blob 2\treferences.bib\0"
            if "cat-file" in args:
                return remote[args[-1]]
            return b""

        with patch.object(sync, "git", side_effect=fake_git):
            report = sync.synchronize(self.root, state)
            self.assertEqual(report["status"], "updated")
            self.assertEqual(self.file.read_bytes(), b"remote edit")
            self.assertEqual(next((state / "backups").rglob("main.tex")).read_bytes(), b"original")
            self.assertEqual(sync.synchronize(self.root, state)["status"], "up_to_date")
            self.file.write_bytes(b"local edit")
            remote["1"] = b"new remote edit"
            baseline = (state / "baseline.json").read_bytes()
            self.assertEqual(sync.synchronize(self.root, state)["status"], "conflict")
            self.assertEqual(self.file.read_bytes(), b"local edit")
            self.assertEqual((state / "baseline.json").read_bytes(), baseline)
            self.assertEqual((state / "conflicts/revision/main.tex").read_bytes(), b"new remote edit")

    def test_build_publish_skip_retry_and_backup(self):
        state = self.root / "state"
        state.mkdir()
        old_pdf = b"%PDF-1.7\nold\n%%EOF"
        new_pdf = b"%PDF-1.7\nnew\n%%EOF"
        for name in sync.PDF_NAMES:
            (self.root / name).write_bytes(old_pdf)
        report = {"status": "up_to_date", "revision": "rev"}

        def compile_ok(directory, log):
            (directory / "main.pdf").write_bytes(new_pdf)

        with patch.object(sync, "compile_snapshot", side_effect=RuntimeError("bad LaTeX")):
            self.assertEqual(sync.rebuild_pdf(self.root, state, report)["status"], "failed")
        for name in sync.PDF_NAMES:
            self.assertEqual((self.root / name).read_bytes(), old_pdf)
        with patch.object(sync, "compile_snapshot", side_effect=compile_ok) as compiler:
            self.assertEqual(sync.rebuild_pdf(self.root, state, report)["status"], "success")
            self.assertEqual(sync.rebuild_pdf(self.root, state, report)["status"], "up_to_date")
            self.assertEqual(compiler.call_count, 1)
            self.file.write_bytes(b"incoming edit")
            self.assertEqual(sync.rebuild_pdf(self.root, state, report)["status"], "success")
            self.assertEqual(compiler.call_count, 2)
        self.assertEqual(next((state / "pdf-backups").rglob("main.pdf")).read_bytes() in (old_pdf, new_pdf), True)
        for name in sync.PDF_NAMES:
            self.assertEqual((self.root / name).read_bytes(), new_pdf)

    def test_build_skips_conflicts(self):
        with patch.object(sync, "compile_snapshot") as compiler:
            self.assertEqual(sync.rebuild_pdf(self.root, self.root, {"status": "conflict"})["status"], "skipped")
            compiler.assert_not_called()

    def test_build_rejects_changed_source_and_incomplete_pdf(self):
        state = self.root / "state"
        state.mkdir()
        pdf = self.root / sync.PDF_NAMES[1]
        pdf.write_bytes(b"previous PDF")

        def changed_source(directory, log):
            (directory / "main.pdf").write_bytes(b"%PDF-1.7\n%%EOF")
            self.file.write_bytes(b"edited during compile")

        def incomplete_pdf(directory, log):
            (directory / "main.pdf").write_bytes(b"%PDF-partial")

        for compiler in (changed_source, incomplete_pdf):
            with patch.object(sync, "compile_snapshot", side_effect=compiler):
                self.assertEqual(sync.rebuild_pdf(self.root, state, {"status": "updated"})["status"], "failed")
            self.assertEqual(pdf.read_bytes(), b"previous PDF")


if __name__ == "__main__":
    unittest.main()
