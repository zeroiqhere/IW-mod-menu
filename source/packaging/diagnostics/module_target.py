"""Validate the exact read-only module report before a bootstrap probe."""

from dataclasses import dataclass
import re


@dataclass(frozen=True)
class ModuleTarget:
    windows_pid: int
    creation_time: int
    executable: str


def parse_module_target(document: dict) -> ModuleTarget:
    target = document.get("target")
    modules = document.get("modules")
    if not isinstance(target, dict) or not isinstance(modules, list) or len(modules) > 1024:
        raise ValueError("The module report has an unsupported structure")
    pid = target.get("windows_pid")
    creation = target.get("creation_time_100ns")
    executable = target.get("executable_windows_path")
    name = target.get("process_name")
    # A live helper report must identify an exact retail process, not an inferred architecture
    if (type(pid) is not int or not 0 < pid <= 0xffffffff
            or type(creation) is not int or creation <= 0
            or target.get("architecture") != "x86_64"
            or not isinstance(name, str) or name.lower() != "iw7_ship.exe"
            or not isinstance(executable, str) or len(executable) > 32768
            or "\0" in executable or not re.match(r"^[A-Za-z]:[\\/]", executable)
            or executable.replace("\\", "/").rsplit("/", 1)[-1].lower() != "iw7_ship.exe"):
        raise ValueError("The module report does not identify the exact retail game")
    matching_image = False
    for module in modules:
        if not isinstance(module, dict):
            raise ValueError("A module record is invalid")
        name, path = module.get("module_name"), module.get("windows_path")
        if not isinstance(name, str) or not isinstance(path, str):
            raise ValueError("A module record is invalid")
        # Another probe or renderer in this incarnation prevents a clean comparison
        if name.lower() in {"iw_apparition_injected.dll", "iw_apparition_bootstrap_probe.dll"}:
            raise ValueError("A menu payload is already loaded; a fresh game is required")
        matching_image |= name.lower() == "iw7_ship.exe" and path.casefold() == executable.casefold()
    if not matching_image:
        raise ValueError("The module report is missing its exact executable image")
    return ModuleTarget(pid, creation, executable)


def validate_native_target(document: dict, target: ModuleTarget) -> None:
    window = document.get("window")
    # Correlate the Windows discovery with ProtonInformer's exact Linux target
    if (document.get("version") != 1 or type(document.get("version")) is not int
            or type(document.get("processId")) is not int or document["processId"] != target.windows_pid
            or type(window) is not int or not 0 < window <= 0xffffffffffffffff):
        raise ValueError("Native discovery and the module report disagree")
