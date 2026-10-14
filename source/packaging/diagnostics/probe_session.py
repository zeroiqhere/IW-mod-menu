"""Run only an inert DLL bootstrap and preserve its bounded stage evidence."""

import hashlib
import json
from pathlib import Path
import subprocess
import time

from session_files import wait_document, windows_path, write_json
from .module_target import parse_module_target, validate_native_target
from .process_identity import require_process_identity


def validate_artifacts(package: Path) -> tuple[Path, Path]:
    manifest_path = package / "manifest.json"
    if manifest_path.is_symlink() or manifest_path.stat().st_size > 131072:
        raise ValueError("The probe package manifest is invalid")
    manifest = json.loads(manifest_path.read_bytes())
    if not isinstance(manifest, list):
        raise ValueError("The probe package manifest is invalid")
    files = []
    for name in ("iw_overlay_bootstrap.exe", "iw_apparition_bootstrap_probe.dll"):
        artifact = package / name
        expected = [row.get("sha256") for row in manifest if isinstance(row, dict) and row.get("path") == name]
        if artifact.is_symlink() or not artifact.is_file() or len(expected) != 1:
            raise ValueError("A verified probe artifact is missing")
        if hashlib.sha256(artifact.read_bytes()).hexdigest() != expected[0]:
            raise ValueError("A probe artifact differs from its package manifest")
        files.append(artifact)
    return files[0], files[1]


def inspect_target(tool: Path, identity, session: Path):
    report = session / "protoninformer-modules.json"
    # Module enumeration never loads a payload or starts the feature controller
    with report.open("xb") as output, (session / "module-check.log").open("xb") as error:
        result = subprocess.run([str(tool), "modules", "--pid", str(identity.linux_pid), "--json"],
                                stdin=subprocess.DEVNULL, stdout=output, stderr=error, timeout=25)
    if result.returncode:
        raise RuntimeError("Read-only module inspection failed; inspect module-check.log")
    if report.stat().st_size > 131072:
        raise ValueError("The module report exceeds supported bounds")
    require_process_identity(identity.linux_pid, identity.start_ticks, identity.uid)
    return parse_module_target(json.loads(report.read_bytes()))


def run_probe(package: Path, prefix: Path, tool: Path, identity, session: Path) -> dict:
    helper, payload = validate_artifacts(package)
    target = inspect_target(tool, identity, session)
    windows_session = windows_path(session, prefix)
    windows_payload = windows_path(payload, prefix)
    with (session / "probe-launcher.log").open("xb") as log:
        def start(*arguments):
            # Only the checked native bootstrap can run; no backend or feature executable is selected
            require_process_identity(identity.linux_pid, identity.start_ticks, identity.uid)
            return subprocess.Popen(["protontricks-launch", "--appid", "292730", str(helper), *arguments],
                                    stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT)
        discover = start("--discover", "--session-directory", windows_session)
        native = wait_document(session, "bootstrap-target.json", discover)
        validate_native_target(native, target)
        write_json(session / "probe-stage.json", {"version": 1, "stage": "inert-bootstrap",
            "linuxProcessId": identity.linux_pid, "linuxStartTicks": identity.start_ticks,
            "processId": target.windows_pid, "creationTime": target.creation_time,
            "startedUnixSeconds": time.time(), "featuresAvailable": False})
        bootstrap = start("--game-pid", str(target.windows_pid), "--payload", windows_payload,
                          "--session-directory", windows_session)
        result = wait_document(session, "bootstrap-result.json", bootstrap)
        if type(result.get("processId")) is not int or result["processId"] != target.windows_pid or result.get("ready") is not True:
            raise RuntimeError("The inert probe did not confirm initialization")
        require_process_identity(identity.linux_pid, identity.start_ticks, identity.uid)
        outcome = {"version": 1, "stage": "inert-bootstrap", "processId": target.windows_pid,
                   "ready": True, "featuresAvailable": False, "readyUnixSeconds": time.time()}
        write_json(session / "probe-ready.json", outcome)
        # The inert DLL has no ongoing worker or cleanup action and stays loaded until exit
        return outcome
