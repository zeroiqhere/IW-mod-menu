#!/usr/bin/env python3
"""Launch the checked DLL bootstrap and its windowless feature controller."""

import argparse
import fcntl
import os
from pathlib import Path
import subprocess
import tempfile
import time

from controls import menu_position_controls_available, read_bindings
from health import SessionHealth
from session_files import read_json, wait_document, windows_path, write_json


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    steam = Path.home() / ".local/share/Steam"
    parser.add_argument("--steam-directory", type=Path, default=steam)
    parser.add_argument("--game-directory", type=Path)
    parser.add_argument("--trial", action="store_true",
                        help="Run one explicitly requested diagnostic trial while the shortcut stays disabled")
    options = parser.parse_args()
    game = options.game_directory or options.steam_directory / "steamapps/common/Call of Duty - Infinite Warfare"
    prefix = options.steam_directory / "steamapps/compatdata/292730/pfx"
    package = Path(__file__).resolve().parent
    cache = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "iw-apparition-injected"
    cache.mkdir(parents=True, exist_ok=True, mode=0o700)
    if cache.is_symlink() or cache.stat().st_uid != os.getuid() or cache.stat().st_mode & 0o077:
        raise ValueError("The launcher cache must be an owned private directory")
    # A second launcher cannot create a competing feature owner in the same prefix
    with (cache / "launcher.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RuntimeError("The injected menu launcher is already running") from error
        return run(package, game, prefix, cache, trial=options.trial)


def run(package: Path, game: Path, prefix: Path, cache: Path, *, trial: bool = False) -> int:
    if type(trial) is not bool:
        raise ValueError("A diagnostic trial requires an explicit boolean option")
    blocked = read_json(package / "disabled.json")
    if blocked:
        # A confirmed game crash blocks further injection until its cause is reviewed
        reason = blocked.get("reason")
        if not isinstance(reason, str) or not reason or len(reason) > 1500:
            raise RuntimeError("The injected menu is disabled; its status file is invalid")
        if not trial:
            raise RuntimeError(reason)
    controls = read_bindings((game / "players2/user_keys_mp_0.cfg").read_text(encoding="utf-8"))
    payload = package / "iw_apparition_injected.dll"
    helper = package / "iw_overlay_bootstrap.exe"
    backend = package / "backend/iw_apparition_backend.exe"
    for artifact in (payload, helper, backend):
        if not artifact.is_file():
            raise FileNotFoundError(f"Package file missing: {artifact.name}")
    session = Path(tempfile.mkdtemp(prefix="session-", dir=cache))
    os.chmod(session, 0o700)
    write_json(session / "bindings.json", controls)
    # A requested trial is recorded without deleting the guard used by the normal shortcut
    if trial:
        write_json(session / "trial.json", {"version": 1, "purpose": "actual-menu comparison",
                                           "productionShortcutDisabled": bool(blocked)})
    windows_session = windows_path(session, prefix)
    windows_payload = windows_path(payload, prefix)
    backend_child = None
    pid = None
    # Logs and IPC stay in the private session, never in the game directory
    with (session / "launcher.log").open("wb") as log:
        def start(executable: Path, *arguments: str):
            return subprocess.Popen(["protontricks-launch", "--appid", "292730", str(executable), *arguments],
                                    stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT)
        try:
            discover = start(helper, "--discover", "--session-directory", windows_session)
            target = wait_document(session, "bootstrap-target.json", discover)
            pid = target.get("processId")
            window = target.get("window")
            if type(pid) is not int or not 0 < pid <= 0xffffffff or type(window) is not int or not 0 < window <= 0xffffffffffffffff:
                raise ValueError("The game identity returned by bootstrap is invalid")
            # A complete exact action set is required before the backend can advertise this editor
            capability_flags = ["--menu-position-controls-available"] if menu_position_controls_available(controls) else []
            backend_child = start(backend, "--game-pid", str(pid), "--session-directory", windows_session, *capability_flags)
            state = wait_document(session, "state.json", backend_child)
            generation = state.get("generation")
            if state.get("processId") != pid or type(generation) is not int or generation <= 0 or state.get("isOpen") is not False:
                raise ValueError("The controller did not start with a closed, current game session")
            bootstrap = start(helper, "--game-pid", str(pid), "--payload", windows_payload,
                              "--session-directory", windows_session)
            ready = wait_document(session, "bootstrap-result.json", bootstrap)
            if ready.get("processId") != pid or ready.get("ready") is not True:
                raise RuntimeError("The DLL did not confirm initialization")
            # Startup completion does not imply a successful rendered frame
            runtime_ready = wait_document(session, "runtime-ready.json", backend_child)
            if runtime_ready.get("processId") != pid or runtime_ready.get("ready") is not True:
                raise RuntimeError("The input worker did not confirm the current session")
            write_json(cache / "current-session.json", {"version": 1, "directory": str(session), "processId": pid})
            health = SessionHealth(pid, state["generation"], time.monotonic())
            while backend_child.poll() is None:
                health.observe(read_json(session / "state.json"), read_json(session / "renderer-status.json"),
                               read_json(session / "runtime-error.json"), time.monotonic())
                time.sleep(0.25)
            code = backend_child.returncode
            if code:
                raise RuntimeError(f"The background controller stopped with exit {code}; inspect {session}")
            return 0
        finally:
            if backend_child is not None and backend_child.poll() is None and pid is not None:
                # Cleanup is a protocol request so captured feature ownership can be restored
                write_json(session / "stop-request.json", {"version": 1, "processId": pid})
                try:
                    backend_child.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    log.write(b"Cleanup still pending; no process was killed and no DLL was unloaded\n")


if __name__ == "__main__":
    os.umask(0o077)
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError) as error:
        # Terminal=false keeps the menu launcher from adding an external console window
        message = str(error)
        try:
            subprocess.run(["notify-send", "IW injected menu", message], check=False, timeout=3)
        except (OSError, subprocess.TimeoutExpired):
            pass
        raise SystemExit(message)
