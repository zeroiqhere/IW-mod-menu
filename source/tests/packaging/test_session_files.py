import os
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from session_files import read_json, wait_document, windows_path, write_json


class SessionFileTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def test_atomic_round_trip_has_private_permissions_and_no_partial_file(self):
        path = self.root / "state.json"
        write_json(path, {"version": 1, "generation": 3})
        self.assertEqual(read_json(path)["generation"], 3)
        self.assertEqual(path.stat().st_mode & 0o777, 0o600)
        self.assertFalse(path.with_name(path.name + ".launcher-tmp").exists())

    def test_missing_document_returns_none(self):
        self.assertIsNone(read_json(self.root / "missing.json"))

    def test_oversized_and_symlinked_documents_are_rejected(self):
        path = self.root / "large.json"
        path.write_bytes(b" " * 32769)
        with self.assertRaises(ValueError):
            read_json(path)
        linked = self.root / "linked.json"
        linked.symlink_to(path)
        with self.assertRaises(ValueError):
            read_json(linked)

    def test_wrong_protocol_version_is_rejected(self):
        path = self.root / "state.json"
        path.write_text('{"version":2}')
        with self.assertRaisesRegex(ValueError, "version"):
            read_json(path)

    def test_boolean_protocol_version_is_rejected(self):
        path = self.root / "state.json"
        path.write_text('{"version":true}')
        with self.assertRaisesRegex(ValueError, "version"):
            read_json(path)

    def test_existing_z_mapping_preserves_spaces(self):
        devices = self.root / "prefix/dosdevices"
        devices.mkdir(parents=True)
        (devices / "z:").symlink_to("/")
        file = self.root / "payload with spaces.dll"
        file.touch()
        self.assertEqual(windows_path(file, devices.parent), "Z:" + str(file).replace("/", "\\"))

    def test_wrong_z_mapping_is_rejected_without_changing_it(self):
        devices = self.root / "prefix/dosdevices"
        devices.mkdir(parents=True)
        (devices / "z:").symlink_to(self.root)
        with self.assertRaisesRegex(ValueError, "mapping"):
            windows_path(self.root, devices.parent)
        self.assertEqual((devices / "z:").resolve(), self.root)

    def test_child_failure_never_becomes_success(self):
        class FailedChild:
            def poll(self):
                return 2
        with self.assertRaisesRegex(RuntimeError, "exit 2"):
            wait_document(self.root, "state.json", FailedChild(), timeout=1)

    def test_bootstrap_error_is_reported_before_result(self):
        write_json(self.root / "bootstrap-error.json", {"version": 1, "error": "Wrong module"})
        write_json(self.root / "bootstrap-result.json", {"version": 1, "ready": True})
        with self.assertRaisesRegex(RuntimeError, "Wrong module"):
            wait_document(self.root, "bootstrap-result.json", None)

    def test_failed_backend_reports_its_specific_startup_status(self):
        class FailedChild:
            def poll(self):
                return 2
        write_json(self.root / "status.json", {"version": 1, "message": "Another RTM tool is running"})
        with self.assertRaisesRegex(RuntimeError, "Another RTM tool"):
            wait_document(self.root, "state.json", FailedChild())
