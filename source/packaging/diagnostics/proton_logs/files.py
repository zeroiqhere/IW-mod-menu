import json
import os
from pathlib import Path
import stat

from .parser import analyze_stream


def _identity(status):
    # Metadata checks cover replacement and ordinary writes during the streaming scan
    return status.st_dev, status.st_ino, status.st_size, status.st_mtime_ns, status.st_ctime_ns


def analyze_file(source, windows_pid, *, limits=None, session_label=None):
    if session_label is not None and (type(session_label) is not str or not 1 <= len(session_label) <= 128
            or any(ord(character) < 32 for character in session_label)):
        raise ValueError("The supplied session label is invalid")
    path = Path(source)
    descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(descriptor, "rb") as stream:
        before = os.fstat(stream.fileno())
        if not stat.S_ISREG(before.st_mode):
            raise ValueError("The supplied log must be a regular file")
        report = analyze_stream(stream, windows_pid, limits=limits)
        after = os.fstat(stream.fileno())
        observed = path.stat(follow_symlinks=False)
        if _identity(before) != _identity(after) or _identity(before) != _identity(observed):
            raise ValueError("The source log changed during analysis")
        if report["source"]["bytes"] != before.st_size:
            raise ValueError("The source log size changed during analysis")
    report["source"].update({"name": path.name, "mtime_ns": before.st_mtime_ns,
                              "stable_metadata_check_passed": True})
    # A user-supplied label records provenance without authenticating the game or session
    report["selection"]["supplied_session_label"] = session_label
    return report


def write_new_report(report, output_directory):
    directory = Path(output_directory)
    # mkdir and exclusive creation reject every existing output, including the source path
    os.mkdir(directory, 0o700)
    directory_fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    created = False
    try:
        os.fchmod(directory_fd, 0o700)
        descriptor = os.open("report.json", os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                             0o600, dir_fd=directory_fd)
        created = True
        with os.fdopen(descriptor, "w", encoding="utf-8") as output:
            os.fchmod(output.fileno(), 0o600)
            json.dump(report, output, indent=2, ensure_ascii=True, allow_nan=False)
            output.write("\n")
            output.flush()
            os.fsync(output.fileno())
    except Exception:
        # Only the file created by this invocation is removed on a failed report write
        if created:
            os.unlink("report.json", dir_fd=directory_fd)
        raise
    finally:
        os.close(directory_fd)
    return directory / "report.json"
