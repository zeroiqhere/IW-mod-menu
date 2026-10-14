import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from diagnostics.module_target import parse_module_target, validate_native_target
from diagnostics.probe_session import validate_artifacts


def valid_report():
    image = "S:\\steamapps\\common\\game\\iw7_ship.exe"
    return {"target": {"windows_pid": 308, "creation_time_100ns": 123456,
            "architecture": "x86_64", "process_name": "iw7_ship.exe", "executable_windows_path": image},
            "modules": [{"module_name": "iw7_ship.exe", "windows_path": image}]}


class ProbeTargetTests(unittest.TestCase):
    def test_valid_report_correlates_with_exact_native_pid_and_window(self):
        target = parse_module_target(valid_report())
        self.assertEqual(target.windows_pid, 308)
        validate_native_target({"version": 1, "processId": 308, "window": 65728}, target)

    def test_wrong_architecture_process_identity_and_path_are_rejected(self):
        for key, value in [("windows_pid", True), ("windows_pid", 0), ("creation_time_100ns", True),
                           ("architecture", "x86"), ("process_name", 3), ("process_name", "other.exe"),
                           ("executable_windows_path", "iw7_ship.exe"), ("executable_windows_path", "C:\\other.exe"),
                           ("executable_windows_path", "C:\\nul\0\\iw7_ship.exe")]:
            with self.subTest(key=key, value=value):
                report = valid_report()
                report["target"][key] = value
                with self.assertRaises(ValueError):
                    parse_module_target(report)

    def test_missing_image_invalid_record_and_existing_payload_reject_clean_trial(self):
        for modules in [[], [None], [{"module_name": 3, "windows_path": "C:\\other.dll"}],
                        valid_report()["modules"] + [{"module_name": "IW_Apparition_Injected.dll", "windows_path": "C:\\menu.dll"}],
                        valid_report()["modules"] + [{"module_name": "iw_apparition_bootstrap_probe.dll", "windows_path": "C:\\probe.dll"}]]:
            report = valid_report()
            report["modules"] = modules
            with self.subTest(modules=modules), self.assertRaises(ValueError):
                parse_module_target(report)

    def test_native_target_cannot_change_pid_or_use_boolean_protocol_and_window(self):
        target = parse_module_target(valid_report())
        for key, value in [("version", True), ("processId", 309), ("processId", True), ("window", True), ("window", 0)]:
            native = {"version": 1, "processId": 308, "window": 65728}
            native[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_native_target(native, target)


class ProbeArtifactTests(unittest.TestCase):
    def test_exact_manifest_allows_only_probe_and_bootstrap_artifacts(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = []
            for name in ("iw_overlay_bootstrap.exe", "iw_apparition_bootstrap_probe.dll"):
                (root / name).write_bytes(name.encode())
                manifest.append({"path": name, "sha256": hashlib.sha256(name.encode()).hexdigest()})
            (root / "manifest.json").write_text(json.dumps(manifest))
            self.assertEqual([path.name for path in validate_artifacts(root)], [row["path"] for row in manifest])
            (root / "iw_apparition_bootstrap_probe.dll").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "differs"):
                validate_artifacts(root)

    def test_duplicate_and_linked_artifacts_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ("iw_overlay_bootstrap.exe", "iw_apparition_bootstrap_probe.dll"):
                (root / name).write_bytes(b"payload")
            checksum = hashlib.sha256(b"payload").hexdigest()
            rows = [{"path": name, "sha256": checksum} for name in ("iw_overlay_bootstrap.exe", "iw_apparition_bootstrap_probe.dll")]
            (root / "manifest.json").write_text(json.dumps(rows + [rows[0]]))
            with self.assertRaises(ValueError):
                validate_artifacts(root)
            (root / "manifest.json").write_text(json.dumps(rows))
            (root / "iw_apparition_bootstrap_probe.dll").unlink()
            (root / "iw_apparition_bootstrap_probe.dll").symlink_to(root / "iw_overlay_bootstrap.exe")
            with self.assertRaises(ValueError):
                validate_artifacts(root)
