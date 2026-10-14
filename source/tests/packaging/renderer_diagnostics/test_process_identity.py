import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "packaging"))
from diagnostics.process_identity import read_process_identity, require_process_identity
import diagnostics.process_identity as process_identity


class ProcessIdentityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.process = self.root / "51"
        self.process.mkdir()
        self.write_stat()
        uid = os.getuid()
        (self.process / "status").write_text(f"Uid:\t{uid}\t{uid}\t{uid}\t{uid}\n")
        (self.process / "cmdline").write_bytes(b'/wine/loader\0Z:\\game\\iw7_ship.exe\0')

    def tearDown(self):
        self.temporary.cleanup()

    def write_stat(self, ticks=9876, state="S"):
        fields = [state] + ["0"] * 18 + [str(ticks)] + ["0"] * 5
        (self.process / "stat").write_text("51 (game with (spaces)) " + " ".join(fields))

    def test_parenthesized_command_and_exact_incarnation_are_parsed(self):
        identity = read_process_identity(51, proc_root=self.root)
        self.assertEqual(identity.start_ticks, 9876)
        self.assertEqual(require_process_identity(51, 9876, proc_root=self.root), identity)

    def test_pid_reuse_crash_wrong_executable_and_owner_are_rejected(self):
        self.write_stat(ticks=9877)
        with self.assertRaises(ValueError):
            require_process_identity(51, 9876, proc_root=self.root)
        self.write_stat(state="Z")
        with self.assertRaises(ValueError):
            read_process_identity(51, proc_root=self.root)
        self.write_stat()
        (self.process / "cmdline").write_bytes(b'/game/Dx11Fixture.exe\0')
        with self.assertRaises(ValueError):
            read_process_identity(51, proc_root=self.root)
        (self.process / "cmdline").write_bytes(b'/game/iw7_ship.exe\0')
        with self.assertRaises(ValueError):
            read_process_identity(51, expected_uid=os.getuid() + 1, proc_root=self.root)

    def test_missing_and_malformed_metadata_fail_without_echoing_paths(self):
        (self.process / "stat").write_text("private invalid metadata")
        with self.assertRaises(ValueError) as failure:
            read_process_identity(51, proc_root=self.root)
        self.assertNotIn(str(self.root), str(failure.exception))
        with self.assertRaises(ValueError):
            read_process_identity(52, proc_root=self.root)

    def test_incarnation_change_between_metadata_reads_is_rejected(self):
        original_read = process_identity._bounded_read
        reads = 0
        def read_then_change(path, maximum):
            nonlocal reads
            data = original_read(path, maximum)
            if path.name == "stat":
                reads += 1
                if reads == 1:
                    self.write_stat(ticks=9877)
            return data
        with patch.object(process_identity, "_bounded_read", side_effect=read_then_change):
            with self.assertRaisesRegex(ValueError, "incarnation"):
                read_process_identity(51, proc_root=self.root)
