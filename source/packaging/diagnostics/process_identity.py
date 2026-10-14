"""Bind diagnostics to one same-user game process incarnation."""

from dataclasses import dataclass
import os
from pathlib import Path


@dataclass(frozen=True)
class ProcessIdentity:
    linux_pid: int
    start_ticks: int
    uid: int


def _bounded_read(path: Path, maximum: int) -> bytes:
    with path.open("rb") as stream:
        data = stream.read(maximum + 1)
    if not data or len(data) > maximum:
        raise ValueError("The supplied game process metadata is invalid")
    return data


def _stat_identity(data: bytes, linux_pid: int) -> tuple[str, int]:
    stat = data.decode("utf-8", "strict")
    close = stat.rfind(")")
    if not stat.startswith(f"{linux_pid} (") or close < len(f"{linux_pid} ("):
        raise ValueError("The supplied game process metadata is invalid")
    fields = stat[close + 1:].split()
    if len(fields) < 20 or len(fields[0]) != 1 or fields[0] in {"Z", "X", "x"}:
        raise ValueError("The supplied game process is no longer running")
    if not fields[19].isascii() or not fields[19].isdigit() or int(fields[19]) <= 0:
        raise ValueError("The supplied game start time is invalid")
    return fields[0], int(fields[19])


def read_process_identity(linux_pid: int, expected_uid: int | None = None,
                          *, proc_root: Path = Path("/proc")) -> ProcessIdentity:
    if type(linux_pid) is not int or linux_pid <= 0:
        raise ValueError("An exact positive Linux process ID is required")
    uid = os.getuid() if expected_uid is None else expected_uid
    if type(uid) is not int or uid < 0:
        raise ValueError("An exact nonnegative process owner ID is required")
    directory = proc_root / str(linux_pid)
    try:
        # The command name may contain spaces and parentheses, so split after its last close
        _, ticks = _stat_identity(_bounded_read(directory / "stat", 4096), linux_pid)
        status = _bounded_read(directory / "status", 16384).decode("utf-8", "strict")
        owners = [line.split()[1:] for line in status.splitlines() if line.startswith("Uid:")]
        if (len(owners) != 1 or len(owners[0]) != 4
                or any(not value.isascii() or not value.isdigit() or int(value) != uid for value in owners[0])):
            raise ValueError("The supplied game process belongs to another user")
        arguments = _bounded_read(directory / "cmdline", 16384).split(b"\0")
        # Wine may precede the executable with its own loader arguments
        if not any(argument.replace(b"\\", b"/").rsplit(b"/", 1)[-1].lower() == b"iw7_ship.exe"
                   for argument in arguments):
            raise ValueError("The supplied process is not the retail game")
        # Metadata reads must still describe the same incarnation at the final check
        _, final_ticks = _stat_identity(_bounded_read(directory / "stat", 4096), linux_pid)
        if final_ticks != ticks:
            raise ValueError("The supplied game process incarnation changed")
        return ProcessIdentity(linux_pid, ticks, uid)
    except (OSError, UnicodeError, IndexError) as error:
        raise ValueError("The supplied game process metadata is unavailable") from error


def require_process_identity(linux_pid: int, expected_ticks: int, expected_uid: int | None = None,
                             *, proc_root: Path = Path("/proc")) -> ProcessIdentity:
    if type(expected_ticks) is not int or expected_ticks <= 0:
        raise ValueError("The captured Linux start time is invalid")
    identity = read_process_identity(linux_pid, expected_uid, proc_root=proc_root)
    # The saved start time distinguishes a reused PID from the originally selected game
    if identity.start_ticks != expected_ticks:
        raise ValueError("The supplied game process incarnation changed")
    return identity
