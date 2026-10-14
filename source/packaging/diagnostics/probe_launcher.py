"""Explicit diagnostic CLI; this never enables or starts the production menu."""

import argparse
import fcntl
import os
from pathlib import Path
import subprocess
import tempfile

from .process_identity import read_process_identity
from .probe_session import run_probe


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-directory", type=Path, required=True)
    parser.add_argument("--proton-informer", type=Path, required=True)
    parser.add_argument("--linux-process-id", type=int, required=True)
    parser.add_argument("--steam-directory", type=Path, default=Path.home() / ".local/share/Steam")
    options = parser.parse_args()
    identity = read_process_identity(options.linux_process_id)
    cache = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "iw-apparition-injected"
    cache.mkdir(mode=0o700, parents=True, exist_ok=True)
    if cache.is_symlink() or cache.stat().st_uid != os.getuid() or cache.stat().st_mode & 0o077:
        raise ValueError("The diagnostic cache must be an owned private directory")
    # Both launchers share one lock so a diagnostic cannot overlap a feature owner
    with (cache / "launcher.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RuntimeError("Another menu or diagnostic launcher is running") from error
        session = Path(tempfile.mkdtemp(prefix="probe-", dir=cache))
        os.chmod(session, 0o700)
        prefix = options.steam_directory / "steamapps/compatdata/292730/pfx"
        run_probe(options.package_directory.resolve(strict=True), prefix,
                  options.proton_informer.resolve(strict=True), identity, session)
        print(f"Inert bootstrap ready; no graphics hooks or features started. Evidence: {session}")
    return 0


if __name__ == "__main__":
    os.umask(0o077)
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        # A failed probe is not retried and no game process or resident DLL is terminated
        raise SystemExit(str(error))
