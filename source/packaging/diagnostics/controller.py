"""Run a bounded renderer-only controller without feature dependencies."""

import argparse
from pathlib import Path
import time
from .panel import DiagnosticPanel
from .process_identity import ProcessIdentity, require_process_identity
from .protocol import parse_command
from .session import DiagnosticFileError, DiagnosticSession


def run(session: DiagnosticSession, process_id: int, identity: ProcessIdentity, duration_seconds: int,
        *, identity_check=require_process_identity, monotonic=time.monotonic, sleep=time.sleep) -> int:
    if type(process_id) is not int or not 1 <= process_id < 1 << 32:
        raise ValueError("An exact Windows process ID is required")
    if type(duration_seconds) is not int or not 1 <= duration_seconds <= 600:
        raise ValueError("A bounded diagnostic duration is required")
    panel = DiagnosticPanel(process_id)
    deadline = monotonic() + duration_seconds
    next_frame = 0.0
    notice = "Diagnostic duration ended"
    try:
        # Initial state is closed before any native input or render hooks are present
        session.publish(panel)
        next_frame = monotonic() + 0.1
        while True:
            try:
                identity_check(identity.linux_pid, identity.start_ticks, identity.uid)
            except ValueError:
                notice = "Game process exited or its incarnation changed"
                break
            if session.stop_requested(process_id):
                notice = "Diagnostic stop requested"
                break
            if monotonic() >= deadline:
                break
            command = parse_command(session.read_bytes("command.json", 4096) or b"", process_id)
            advanced = panel.consume(command)
            if advanced or monotonic() >= next_frame:
                session.publish(panel)
                next_frame = monotonic() + 0.1
            sleep(0.025)
        session.publish(panel, closed=True, notice=notice)
        session.write("status.json", {"version": 1, "processId": process_id, "message": notice})
        return 0
    except (OSError, ValueError, RuntimeError) as error:
        # Fixed operation text avoids private paths, command bodies, and memory values
        message = str(error) if isinstance(error, DiagnosticFileError) else "Diagnostic controller failed: " + type(error).__name__
        try:
            session.publish(panel, closed=True, notice="Diagnostic controller stopped after a protocol or IO failure")
            session.write("status.json", {"version": 1, "processId": process_id, "message": message[:1500]})
        except (OSError, ValueError, RuntimeError):
            pass
        return 2


def bounded_integer(minimum: int, maximum: int):
    def parse(value: str) -> int:
        number = int(value)
        if not minimum <= number <= maximum:
            raise argparse.ArgumentTypeError("Value is outside its permitted range")
        return number
    return parse


def main(arguments=None) -> int:
    parser = argparse.ArgumentParser(description="Windowless renderer diagnostic controller")
    parser.add_argument("--windows-process-id", type=bounded_integer(1, (1 << 32) - 1), required=True)
    parser.add_argument("--linux-process-id", type=bounded_integer(1, (1 << 31) - 1), required=True)
    parser.add_argument("--linux-start-ticks", type=bounded_integer(1, (1 << 64) - 1), required=True)
    parser.add_argument("--session-directory", type=Path, required=True)
    parser.add_argument("--duration-seconds", type=bounded_integer(1, 600), default=120)
    options = parser.parse_args(arguments)
    session = None
    try:
        session = DiagnosticSession(options.session_directory)
        identity = require_process_identity(options.linux_process_id, options.linux_start_ticks)
        return run(session, options.windows_process_id, identity, options.duration_seconds)
    except (OSError, ValueError, RuntimeError):
        # Startup failures cannot become a GUI or an echo of supplied private arguments
        if session is not None:
            try:
                session.write("status.json", {"version": 1, "processId": options.windows_process_id,
                                              "message": "Diagnostic startup rejected the supplied process identity"})
            except (OSError, ValueError, RuntimeError):
                pass
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
