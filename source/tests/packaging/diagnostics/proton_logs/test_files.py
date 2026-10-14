from contextlib import redirect_stderr, redirect_stdout
import hashlib
import io
import json
import os
from pathlib import Path
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[4] / "packaging"))
from diagnostics.proton_logs.cli import main
from diagnostics.proton_logs.files import analyze_file, write_new_report
import diagnostics.proton_logs.files as files

FATAL = b"1.001:0134:0264:err:seh:NtRaiseException Unhandled exception code c0000409 flags 1 addr 0x1412ba464\n"


class ProtonLogFileTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.source = self.root / "saved.log"
        self.source.write_bytes(FATAL)

    def tearDown(self):
        self.temporary.cleanup()

    def test_stable_source_is_hashed_and_remains_unchanged(self):
        before = self.source.stat()
        report = analyze_file(self.source, 308, session_label="supplied-session")
        self.assertEqual(self.source.read_bytes(), FATAL)
        self.assertEqual(self.source.stat().st_mtime_ns, before.st_mtime_ns)
        self.assertEqual(report["source"]["sha256"], hashlib.sha256(FATAL).hexdigest())
        self.assertTrue(report["source"]["stable_metadata_check_passed"])
        self.assertEqual(report["selection"]["supplied_session_label"], "supplied-session")
        self.assertFalse(report["selection"]["process_or_session_identity_verified"])

    def test_changed_source_after_real_scan_is_rejected(self):
        original = files.analyze_stream
        def scan_then_change(stream, pid, **kwargs):
            report = original(stream, pid, **kwargs)
            self.source.write_bytes(FATAL + b"changed\n")
            return report
        # The real parser reads the real file; only the write timing is controlled
        with patch.object(files, "analyze_stream", side_effect=scan_then_change):
            with self.assertRaisesRegex(ValueError, "changed"):
                analyze_file(self.source, 308)

    def test_replaced_source_path_after_real_scan_is_rejected(self):
        original = files.analyze_stream
        def scan_then_replace(stream, pid, **kwargs):
            report = original(stream, pid, **kwargs)
            replacement = self.root / "replacement.log"
            replacement.write_bytes(FATAL)
            replacement.replace(self.source)
            return report
        with patch.object(files, "analyze_stream", side_effect=scan_then_replace):
            with self.assertRaisesRegex(ValueError, "changed"):
                analyze_file(self.source, 308)

    def test_symlink_missing_directory_and_fifo_sources_are_rejected(self):
        linked = self.root / "linked.log"
        linked.symlink_to(self.source)
        fifo = self.root / "pipe.log"
        os.mkfifo(fifo)
        for path in [linked, self.root / "missing.log", self.root, fifo]:
            with self.subTest(path=path.name), self.assertRaises((OSError, ValueError)):
                analyze_file(path, 308)

    def test_invalid_session_labels_cannot_supply_proven_identity(self):
        for label in [True, "", "x" * 129, "name\nnext"]:
            with self.subTest(label=label), self.assertRaises(ValueError):
                analyze_file(self.source, 308, session_label=label)

    def test_report_has_exact_private_permissions_and_never_overwrites(self):
        report = analyze_file(self.source, 308)
        directory = self.root / "new-report"
        path = write_new_report(report, directory)
        self.assertEqual(stat.S_IMODE(directory.stat().st_mode), 0o700)
        self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)
        self.assertEqual(json.loads(path.read_text()), report)
        contents = path.read_bytes()
        with self.assertRaises(FileExistsError):
            write_new_report(report, directory)
        self.assertEqual(path.read_bytes(), contents)
        with self.assertRaises(FileExistsError):
            write_new_report(report, self.source)
        self.assertEqual(self.source.read_bytes(), FATAL)

    def test_failed_json_write_removes_only_its_new_partial_report(self):
        directory = self.root / "failed-report"
        with self.assertRaises(ValueError):
            write_new_report({"invalid": float("nan")}, directory)
        self.assertFalse((directory / "report.json").exists())
        self.assertEqual(self.source.read_bytes(), FATAL)

    def test_cli_writes_a_new_report_and_rejects_reuse_without_touching_source(self):
        directory = self.root / "cli-report"
        arguments = [str(self.source), "--windows-pid", "308", "--output-dir", str(directory)]
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            self.assertEqual(main(arguments), 0)
            contents = (directory / "report.json").read_bytes()
            self.assertEqual(main(arguments), 1)
        self.assertEqual((directory / "report.json").read_bytes(), contents)
        self.assertEqual(self.source.read_bytes(), FATAL)

    def test_cli_analysis_failure_creates_no_output(self):
        for source, pid in [(self.root / "missing.log", "308"), (self.source, "0")]:
            directory = self.root / "unused-report"
            with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                self.assertEqual(main([str(source), "--windows-pid", pid, "--output-dir", str(directory)]), 1)
            self.assertFalse(directory.exists())


if __name__ == "__main__":
    unittest.main()
