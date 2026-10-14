import os
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "packaging"))
from diagnostics.panel import DiagnosticPanel
from diagnostics.session import DiagnosticFileError, DiagnosticSession
from session_files import read_json


class SessionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.session = DiagnosticSession(self.root)

    def tearDown(self):
        self.temporary.cleanup()

    def test_heartbeat_commits_privately_and_advances_only_after_replacement(self):
        panel = DiagnosticPanel(42)
        self.session.publish(panel)
        self.session.publish(panel)
        state = read_json(self.root / "state.json")
        self.assertEqual(state["generation"], 2)
        self.assertFalse(state["isOpen"])
        self.assertEqual((self.root / "state.json").stat().st_mode & 0o777, 0o600)
        (self.root / "state.json.launcher-tmp").mkdir()
        with self.assertRaises(DiagnosticFileError):
            self.session.publish(panel)
        self.assertEqual(panel.generation, 2)
        self.assertEqual(read_json(self.root / "state.json")["generation"], 2)

    def test_unsafe_read_write_size_and_existing_state_are_rejected(self):
        linked = self.root / "command.json"
        linked.symlink_to(self.root / "private-target")
        with self.assertRaises(DiagnosticFileError):
            self.session.read_bytes("command.json", 4096)
        linked.unlink()
        linked.write_bytes(b' ' * 4097)
        with self.assertRaises(DiagnosticFileError):
            self.session.read_bytes("command.json", 4096)
        (self.root / "state.json").symlink_to(self.root / "private-target")
        with self.assertRaises(DiagnosticFileError):
            self.session.publish(DiagnosticPanel(42))
        with self.assertRaises(ValueError):
            DiagnosticSession(self.root)

    def test_private_directory_mode_and_identity_changes_are_rejected(self):
        os.chmod(self.root, 0o755)
        with self.assertRaises(DiagnosticFileError):
            self.session.verify()
        with self.assertRaises(ValueError):
            DiagnosticSession(self.root)
        os.chmod(self.root, 0o700)
        moved = self.root / "retained"
        sub = self.root / "session"
        sub.mkdir(mode=0o700)
        retained = DiagnosticSession(sub)
        sub.rename(moved)
        sub.mkdir(mode=0o700)
        with self.assertRaises(DiagnosticFileError):
            retained.verify()

    def test_fixed_filenames_and_bounds_prevent_path_traversal(self):
        with self.assertRaises(DiagnosticFileError):
            self.session.write("../escaped.json", {"version": 1})
        with self.assertRaises(DiagnosticFileError):
            self.session.read_bytes("../escaped.json", 4096)
        with self.assertRaises(DiagnosticFileError):
            self.session.read_bytes("command.json", 32768)

    def test_stop_file_duplicates_foreign_identity_and_boolean_version_cannot_stop(self):
        path = self.root / "stop-request.json"
        for body in ['{"version":1,"version":1,"processId":42}', '{"version":true,"processId":42}',
                     '{"version":1,"processId":43}', '{"version":2,"processId":42}', '{']:
            path.write_text(body)
            self.assertFalse(self.session.stop_requested(42))
        path.write_text('{"version":1,"processId":42}')
        self.assertTrue(self.session.stop_requested(42))
