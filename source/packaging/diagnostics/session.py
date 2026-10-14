"""Confine controller files to a cached private directory identity."""

import os
from pathlib import Path
import stat
from session_files import read_json, write_json
from .panel import DiagnosticPanel
from .protocol import valid_stop


class DiagnosticFileError(RuntimeError):
    pass


class DiagnosticSession:
    READ_LIMITS = {"command.json": 4096, "stop-request.json": 1024}
    WRITE_NAMES = frozenset({"state.json", "status.json"})

    def __init__(self, directory: Path):
        self.directory = directory
        info = directory.lstat()
        if (not directory.is_absolute() or not stat.S_ISDIR(info.st_mode)
                or info.st_uid != os.getuid() or info.st_mode & 0o077):
            raise ValueError("An absolute same-user private session directory is required")
        # Retaining the inode prevents later commands from switching the supplied directory
        self.identity = (info.st_dev, info.st_ino)
        self.verify()
        if (directory / "state.json").exists() or (directory / "state.json").is_symlink():
            raise ValueError("A fresh diagnostic session without an existing state is required")

    def verify(self) -> None:
        info = self.directory.lstat()
        if (not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid() or info.st_mode & 0o077
                or (info.st_dev, info.st_ino) != self.identity):
            raise DiagnosticFileError("Diagnostic directory identity or privacy changed")

    def read_bytes(self, name: str, maximum: int) -> bytes | None:
        if self.READ_LIMITS.get(name) != maximum:
            raise DiagnosticFileError("Rejected diagnostic read filename or bound")
        # Only the two fixed inbound protocol files can be read
        self.verify()
        path = self.directory / name
        try:
            info = path.lstat()
            if not stat.S_ISREG(info.st_mode) or info.st_size > maximum:
                raise DiagnosticFileError(f"{name}: rejected read type or size")
            # A replaced pathname cannot redirect this read through a symbolic link
            descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
            with os.fdopen(descriptor, "rb") as stream:
                opened = os.fstat(stream.fileno())
                if not stat.S_ISREG(opened.st_mode) or opened.st_size > maximum:
                    raise DiagnosticFileError(f"{name}: rejected open type or size")
                data = stream.read(maximum + 1)
            if len(data) > maximum:
                raise DiagnosticFileError(f"{name}: rejected read size")
            return data
        except FileNotFoundError:
            return None
        except OSError as error:
            raise DiagnosticFileError(f"{name}: read failed ({type(error).__name__})") from error

    def stop_requested(self, process_id: int) -> bool:
        data = self.read_bytes("stop-request.json", 1024)
        if data is None or not valid_stop(data, process_id):
            return False
        # Retain the existing protocol helper's supported-version guard
        document = read_json(self.directory / "stop-request.json")
        return document == {"version": 1, "processId": process_id}

    def write(self, name: str, document: dict) -> None:
        if name not in self.WRITE_NAMES:
            raise DiagnosticFileError("Rejected diagnostic output filename")
        # Shared atomic helpers create private files and never publish a partial JSON frame
        self.verify()
        path = self.directory / name
        if path.is_symlink() or (path.exists() and not path.is_file()):
            raise DiagnosticFileError(f"{name}: rejected write type")
        try:
            write_json(path, document)
        except (OSError, ValueError) as error:
            raise DiagnosticFileError(f"{name}: atomic write failed ({type(error).__name__})") from error

    def publish(self, panel: DiagnosticPanel, **options) -> None:
        frame = panel.snapshot(**options)
        self.write("state.json", frame)
        # A failed atomic replacement never advances the visible heartbeat
        panel.generation = frame["generation"]
