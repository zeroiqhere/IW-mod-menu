import os
from pathlib import Path
import subprocess
import tempfile
import time
import unittest


class DesktopEntryTests(unittest.TestCase):
    def test_desktop_entry_launches_home_relative_python_without_a_console(self):
        source = Path(__file__).resolve().parents[2] / "packaging/iw-apparition-injected.desktop"
        with tempfile.TemporaryDirectory(prefix="iw desktop ; literal-") as directory:
            home = Path(directory)
            package = home / "Downloads/IW-Apparition-DLL"
            package.mkdir(parents=True)
            output = home / "launched.txt"
            # This fixture records launch only and never starts Wine or attaches to a game
            (package / "launch_injected.py").write_text(
                'from pathlib import Path\nPath.home().joinpath("launched.txt").write_text("ok")\n')
            desktop = home / "fixture.desktop"
            desktop.write_bytes(source.read_bytes())
            validated = subprocess.run(["desktop-file-validate", str(desktop)], capture_output=True, text=True)
            self.assertEqual(validated.returncode, 0, validated.stderr)
            launched = subprocess.run(["gio", "launch", str(desktop)],
                                      env=dict(os.environ, HOME=str(home)), capture_output=True, text=True)
            self.assertEqual(launched.returncode, 0, launched.stderr)
            deadline = time.monotonic() + 3
            while not output.exists() and time.monotonic() < deadline:
                time.sleep(0.02)
            self.assertEqual(output.read_text(), "ok")
            self.assertIn("Terminal=false", desktop.read_text())
