import ast
import argparse
import contextlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "packaging"))
from diagnostics.controller import bounded_integer, main, run
from diagnostics.process_identity import ProcessIdentity
from diagnostics.session import DiagnosticSession
from session_files import read_json, write_json


class Clock:
    def __init__(self, after_sleep=None):
        self.now = 0.0
        self.after_sleep = after_sleep

    def monotonic(self):
        return self.now

    def sleep(self, delay):
        self.now += delay
        if self.after_sleep:
            self.after_sleep(self.now)


class ControllerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.session = DiagnosticSession(self.root)
        self.identity = ProcessIdentity(51, 9876, os.getuid())

    def tearDown(self):
        self.temporary.cleanup()

    def run_controller(self, clock, check=None):
        return run(self.session, 42, self.identity, 1, identity_check=check or (lambda *_: self.identity),
                   monotonic=clock.monotonic, sleep=clock.sleep)

    def test_timeout_advances_closed_heartbeat_and_finishes_cleanly(self):
        self.assertEqual(self.run_controller(Clock()), 0)
        frame = read_json(self.root / "state.json")
        self.assertGreaterEqual(frame["generation"], 10)
        self.assertFalse(frame["isOpen"])
        self.assertEqual(frame["lastSequence"], 0)
        self.assertEqual(frame["notice"], "Diagnostic duration ended")
        self.assertTrue(all(row["enabled"] is False for row in frame["rows"]))

    def test_trusted_commands_open_and_select_no_op_then_exact_stop_closes(self):
        observations = []
        staged = set()
        def actions(now):
            observations.append(read_json(self.root / "state.json"))
            for threshold, sequence, action in [(0.025, 1, "toggle"), (0.1, 2, "select")]:
                if now >= threshold and sequence not in staged:
                    write_json(self.root / "command.json", {"version": 1, "sequence": sequence,
                               "processId": 42, "input": action})
                    staged.add(sequence)
            if now >= 0.2 and 3 not in staged:
                write_json(self.root / "stop-request.json", {"version": 1, "processId": 42})
                staged.add(3)
        self.assertEqual(self.run_controller(Clock(actions)), 0)
        opened = [frame for frame in observations if frame["isOpen"]]
        self.assertTrue(opened)
        self.assertTrue(any(frame["lastSequence"] == 2 for frame in opened))
        self.assertTrue(all(frame["selectedIndex"] == 0 for frame in opened))
        final = read_json(self.root / "state.json")
        self.assertFalse(final["isOpen"])
        self.assertEqual(final["lastSequence"], 2)
        self.assertEqual(final["notice"], "Diagnostic stop requested")

    def test_foreign_stop_does_not_end_session_and_identity_change_closes_open_panel(self):
        write_json(self.root / "stop-request.json", {"version": 1, "processId": 43})
        write_json(self.root / "command.json", {"version": 1, "sequence": 1, "processId": 42, "input": "toggle"})
        clock = Clock()
        def check(*arguments):
            self.assertEqual(arguments, (51, 9876, os.getuid()))
            if clock.now >= 0.2:
                raise ValueError("private process details must not appear")
            return self.identity
        self.assertEqual(self.run_controller(clock, check), 0)
        frame = read_json(self.root / "state.json")
        self.assertFalse(frame["isOpen"])
        self.assertEqual(frame["lastSequence"], 1)
        self.assertEqual(frame["notice"], "Game process exited or its incarnation changed")
        self.assertNotIn("private process details", (self.root / "status.json").read_text())

    def test_protocol_file_failure_closes_and_reports_only_fixed_private_diagnostic(self):
        (self.root / "command.json").symlink_to(self.root / "private-target")
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            self.assertEqual(self.run_controller(Clock()), 2)
        self.assertFalse(read_json(self.root / "state.json")["isOpen"])
        status = read_json(self.root / "status.json")
        self.assertEqual(status["message"], "command.json: rejected read type or size")
        self.assertNotIn(str(self.root), json.dumps(status))
        self.assertEqual(stdout.getvalue() + stderr.getvalue(), "")
        self.assertEqual((self.root / "status.json").stat().st_mode & 0o777, 0o600)

    def test_read_failure_after_opening_publishes_a_closed_error_frame(self):
        write_json(self.root / "command.json", {"version": 1, "sequence": 1, "processId": 42, "input": "toggle"})
        observed_open = []
        def corrupt_after_open(_now):
            frame = read_json(self.root / "state.json")
            if frame["isOpen"] and not observed_open:
                observed_open.append(frame)
                path = self.root / "command.json"
                path.unlink()
                path.mkdir()
        self.assertEqual(self.run_controller(Clock(corrupt_after_open)), 2)
        self.assertTrue(observed_open)
        frame = read_json(self.root / "state.json")
        self.assertFalse(frame["isOpen"])
        self.assertEqual(frame["lastSequence"], 1)
        self.assertTrue(all(row["enabled"] is False for row in frame["rows"]))

    def test_startup_identity_failure_has_private_status_without_starting_game_actions(self):
        arguments = ["--windows-process-id", "42", "--linux-process-id", "2147483647",
                     "--linux-start-ticks", "9876", "--session-directory", str(self.root)]
        self.assertEqual(main(arguments), 2)
        self.assertFalse((self.root / "state.json").exists())
        status = read_json(self.root / "status.json")
        self.assertEqual(status["message"], "Diagnostic startup rejected the supplied process identity")

    def test_cli_duration_and_direct_run_reject_unbounded_values(self):
        parser = bounded_integer(1, 600)
        self.assertEqual(parser("120"), 120)
        for value in ["0", "601", "-1"]:
            with self.assertRaises(argparse.ArgumentTypeError):
                parser(value)
        for value in [0, 601, True]:
            with self.assertRaises(ValueError):
                run(self.session, 42, self.identity, value)

    def test_diagnostic_import_graph_contains_no_feature_or_process_launch_dependencies(self):
        directory = Path(__file__).resolve().parents[3] / "packaging/diagnostics"
        allowed = {"argparse", "dataclasses", "json", "os", "pathlib", "stat", "time", "session_files"}
        owned_modules = {"controller", "panel", "protocol", "process_identity", "session", "__init__"}
        for module in owned_modules:
            file = directory / (module + ".py")
            tree = ast.parse(file.read_text())
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    self.assertTrue(all(name.name.split(".")[0] in allowed for name in node.names), file.name)
                elif isinstance(node, ast.ImportFrom) and node.level == 0:
                    self.assertIn(node.module.split(".")[0], allowed, file.name)
                elif isinstance(node, ast.ImportFrom):
                    self.assertIn(node.module, owned_modules, file.name)
                elif isinstance(node, ast.Call) and isinstance(node.func, ast.Name):
                    self.assertNotIn(node.func.id, {"eval", "exec", "__import__"}, file.name)
