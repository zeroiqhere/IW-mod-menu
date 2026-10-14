import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from diagnostics.module_target import ModuleTarget
from diagnostics.probe_session import run_probe
from diagnostics.process_identity import ProcessIdentity
from session_files import read_json, write_json


class ProbeSessionTests(unittest.TestCase):
    def prepare(self, root):
        package = root / "package"
        package.mkdir()
        rows = []
        for name in ("iw_overlay_bootstrap.exe", "iw_apparition_bootstrap_probe.dll"):
            (package / name).write_bytes(name.encode())
            rows.append({"path": name, "sha256": hashlib.sha256(name.encode()).hexdigest()})
        (package / "manifest.json").write_text(json.dumps(rows))
        write_json(package / "disabled.json", {"version": 1, "reason": "Production menu disabled"})
        prefix = root / "prefix"
        (prefix / "dosdevices").mkdir(parents=True)
        (prefix / "dosdevices/z:").symlink_to("/")
        session = root / "session"
        session.mkdir(mode=0o700)
        return package, prefix, session

    def execute(self, root, native_pid=308, ready=True):
        package, prefix, session = self.prepare(root)
        identity = ProcessIdentity(12345, 67890, 1000)
        target = ModuleTarget(308, 555, "S:\\game\\iw7_ship.exe")
        launches = []

        class Child:
            def poll(self):
                return None

        def start(arguments, **options):
            # A staged native response replaces unsafe real process loading in this orchestration check
            self.assertNotIn("shell", options)
            self.assertEqual(Path(arguments[3]).name, "iw_overlay_bootstrap.exe")
            launches.append(arguments)
            if len(launches) == 1:
                write_json(session / "bootstrap-target.json", {"version": 1, "processId": native_pid, "window": 65728})
            else:
                write_json(session / "bootstrap-result.json", {"version": 1, "processId": 308, "ready": ready})
            return Child()

        with patch("diagnostics.probe_session.inspect_target", return_value=target), \
                patch("diagnostics.probe_session.require_process_identity", return_value=identity), \
                patch("diagnostics.probe_session.subprocess.Popen", side_effect=start):
            if native_pid != 308 or not ready:
                with self.assertRaises((ValueError, RuntimeError)):
                    run_probe(package, prefix, root / "proton-informer", identity, session)
            else:
                result = run_probe(package, prefix, root / "proton-informer", identity, session)
                self.assertTrue(result["ready"])
                self.assertFalse(result["featuresAvailable"])
        self.assertTrue((package / "disabled.json").is_file())
        return session, launches

    def test_success_loads_only_inert_probe_and_keeps_production_disabled(self):
        with tempfile.TemporaryDirectory() as directory:
            session, launches = self.execute(Path(directory))
            self.assertEqual(len(launches), 2)
            self.assertIn("--discover", launches[0])
            payload = launches[1][launches[1].index("--payload") + 1]
            self.assertTrue(payload.endswith("iw_apparition_bootstrap_probe.dll"))
            self.assertFalse(any("backend" in argument for command in launches for argument in command))
            ready = read_json(session / "probe-ready.json")
            self.assertFalse(ready["featuresAvailable"])
            self.assertEqual(ready["stage"], "inert-bootstrap")

    def test_disagreeing_windows_pid_stops_before_any_load(self):
        with tempfile.TemporaryDirectory() as directory:
            session, launches = self.execute(Path(directory), native_pid=309)
            self.assertEqual(len(launches), 1)
            self.assertFalse((session / "probe-stage.json").exists())
            self.assertFalse((session / "probe-ready.json").exists())

    def test_failed_initialization_is_not_retried_or_reported_ready(self):
        with tempfile.TemporaryDirectory() as directory:
            session, launches = self.execute(Path(directory), ready=False)
            self.assertEqual(len(launches), 2)
            self.assertFalse((session / "probe-ready.json").exists())

    def test_changed_process_incarnation_blocks_discovery_and_load(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package, prefix, session = self.prepare(root)
            identity = ProcessIdentity(12345, 67890, 1000)
            with patch("diagnostics.probe_session.inspect_target", return_value=ModuleTarget(308, 555, "S:\\game\\iw7_ship.exe")), \
                    patch("diagnostics.probe_session.require_process_identity", side_effect=ValueError("incarnation changed")), \
                    patch("diagnostics.probe_session.subprocess.Popen") as launch:
                with self.assertRaisesRegex(ValueError, "incarnation changed"):
                    run_probe(package, prefix, root / "proton-informer", identity, session)
                launch.assert_not_called()
