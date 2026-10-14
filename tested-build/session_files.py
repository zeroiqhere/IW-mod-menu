"""Keep launcher IPC bounded and confined to a fresh private directory."""

import json
import os
from pathlib import Path
import time


def windows_path(path: Path, prefix: Path) -> str:
    # The existing Z mapping is checked before any host path crosses into Wine
    mapping = prefix / "dosdevices" / "z:"
    if not mapping.is_symlink() or mapping.resolve() != Path("/"):
        raise ValueError("The existing Proton Z: mapping could not be confirmed")
    resolved = path.resolve(strict=True)
    if "\\" in str(resolved):
        raise ValueError("A path contains an unsupported Windows separator")
    return "Z:" + str(resolved).replace("/", "\\")


def write_json(path: Path, document: dict) -> None:
    temporary = path.with_name(path.name + ".launcher-tmp")
    data = json.dumps(document, separators=(",", ":")).encode("utf-8")
    # A private temporary file is replaced only after the complete frame reaches disk
    with temporary.open("xb") as stream:
        os.chmod(temporary, 0o600)
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    temporary.replace(path)


def read_json(path: Path) -> dict | None:
    try:
        if path.is_symlink() or path.stat().st_size > 32768:
            raise ValueError("A session document exceeds supported bounds")
        document = json.loads(path.read_bytes())
    except FileNotFoundError:
        return None
    if not isinstance(document, dict) or type(document.get("version")) is not int or document["version"] != 1:
        raise ValueError("A session document has an unsupported version")
    return document


def wait_document(directory: Path, name: str, child, timeout: float = 25) -> dict:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        error = read_json(directory / "bootstrap-error.json")
        if error:
            raise RuntimeError(str(error.get("error", "DLL bootstrap failed")))
        document = read_json(directory / name)
        if document:
            return document
        # Wrapper failure cannot be mistaken for a completed Windows operation
        code = child.poll()
        if code is not None:
            status = read_json(directory / "status.json")
            if status:
                raise RuntimeError(str(status.get("message", "The background controller stopped")))
            raise RuntimeError(f"The background command stopped before {name} (exit {code})")
        time.sleep(0.05)
    raise TimeoutError(f"The background command did not publish {name} in time")
