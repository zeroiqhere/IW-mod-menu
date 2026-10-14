from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from launch_injected import run
from session_files import read_json, write_json


class LauncherTests(unittest.TestCase):
    def test_explicit_trial_reaches_normal_startup_checks_without_deleting_guard(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_json(root / "disabled.json", {"version": 1, "reason": "Disabled while the fatal game crash is investigated"})
            with patch("launch_injected.subprocess.Popen") as child:
                with self.assertRaises(FileNotFoundError):
                    run(root, root / "missing-game", root / "missing-prefix", root, trial=True)
                child.assert_not_called()
            self.assertTrue((root / "disabled.json").is_file())

    def test_nonboolean_trial_option_cannot_bypass_the_guard(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch("launch_injected.subprocess.Popen") as child:
                with self.assertRaises(ValueError):
                    run(root, root / "missing-game", root / "missing-prefix", root, trial="yes")
                child.assert_not_called()

    def test_disabled_build_stops_before_reading_game_files_or_starting_children(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_json(root / "disabled.json", {"version": 1, "reason": "Disabled while the fatal game crash is investigated"})
            with patch("launch_injected.subprocess.Popen") as child:
                with self.assertRaisesRegex(RuntimeError, "fatal game crash"):
                    run(root, root / "missing-game", root / "missing-prefix", root)
                child.assert_not_called()

    def test_missing_package_stops_before_any_child_starts(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            controls = root / "game/players2"
            controls.mkdir(parents=True)
            (controls / "user_keys_mp_0.cfg").write_text('\n'.join([
                'bind MOUSE2 "+speed_throw"', 'bind V "+melee_zoom"',
                'bind MOUSE1 "+attack"', 'bind F "+activate"']))
            with patch("launch_injected.subprocess.Popen") as child:
                with self.assertRaises(FileNotFoundError):
                    run(root, controls.parent, root / "prefix", root)
                child.assert_not_called()

    def test_bootstrap_failure_requests_backend_cleanup_with_captured_pid(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            game = root / "game with spaces"
            (game / "players2").mkdir(parents=True)
            (game / "players2/user_keys_mp_0.cfg").write_text('\n'.join([
                'bind MOUSE2 "+speed_throw"', 'bind V "+melee_zoom"',
                'bind MOUSE1 "+attack"', 'bind F "+activate"']))
            (root / "backend").mkdir()
            for name in ("iw_apparition_injected.dll", "iw_overlay_bootstrap.exe", "backend/iw_apparition_backend.exe"):
                (root / name).touch()
            devices = root / "prefix/dosdevices"
            devices.mkdir(parents=True)
            (devices / "z:").symlink_to("/")

            class OwnedChild:
                def __init__(self):
                    self.waited = False
                def poll(self):
                    return None
                def wait(self, timeout):
                    self.waited = True
                    return 0

            backend = OwnedChild()
            launched = []
            def start(arguments, **kwargs):
                self.assertIsInstance(arguments, list)
                self.assertNotIn("shell", kwargs)
                launched.append(arguments)
                session = next(root.glob("session-*"))
                if len(launched) == 1:
                    write_json(session / "bootstrap-target.json", {"version": 1, "processId": 123, "window": 456})
                    return OwnedChild()
                if len(launched) == 2:
                    write_json(session / "state.json", {"version": 1, "processId": 123, "generation": 1, "isOpen": False})
                    return backend
                write_json(session / "bootstrap-error.json", {"version": 1, "error": "Unsupported render device"})
                return OwnedChild()
            with patch("launch_injected.subprocess.Popen", side_effect=start):
                with self.assertRaisesRegex(RuntimeError, "Unsupported render device"):
                    run(root, game, devices.parent, root)
            session = next(root.glob("session-*"))
            self.assertEqual(read_json(session / "stop-request.json"), {"version": 1, "processId": 123})
            self.assertTrue(backend.waited)
            self.assertEqual(launched[1][-4:-2], ["--game-pid", "123"])

    def test_editor_capability_flag_requires_every_exact_saved_slot_and_grenade(self):
        # Real private session files exercise launcher arguments while owned children avoid any launch
        bindings = ['bind MOUSE2 "+speed_throw"', 'bind V "+melee_zoom"',
                    'bind MOUSE1 "+attack"', 'bind F "+activate"',
                    'bind UPARROW "+actionslot 1"', 'bind DOWNARROW "+actionslot 2"',
                    'bind 3 "+actionslot 3"', 'bind 4 "+actionslot 4"',
                    'bind G "+frag"', 'bind E "+smoke"']
        for missing in (None, *range(4, 10)):
            with self.subTest(missing=missing), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                game = root / "game"
                (game / "players2").mkdir(parents=True)
                saved = [line for index, line in enumerate(bindings) if index != missing]
                (game / "players2/user_keys_mp_0.cfg").write_text('\n'.join(saved))
                (root / "backend").mkdir()
                for name in ("iw_apparition_injected.dll", "iw_overlay_bootstrap.exe", "backend/iw_apparition_backend.exe"):
                    (root / name).touch()
                devices = root / "prefix/dosdevices"
                devices.mkdir(parents=True)
                (devices / "z:").symlink_to("/")
                launched = []

                class OwnedChild:
                    def poll(self):
                        return None
                    def wait(self, timeout):
                        return 0

                def start(arguments, **kwargs):
                    self.assertNotIn("shell", kwargs)
                    launched.append(arguments)
                    session = next(root.glob("session-*"))
                    if len(launched) == 1:
                        write_json(session / "bootstrap-target.json", {"version": 1, "processId": 123, "window": 456})
                    elif len(launched) == 2:
                        write_json(session / "state.json", {"version": 1, "processId": 123, "generation": 1, "isOpen": False})
                    else:
                        write_json(session / "bootstrap-error.json", {"version": 1, "error": "Owned fixture stops bootstrap"})
                    return OwnedChild()

                with patch("launch_injected.subprocess.Popen", side_effect=start):
                    with self.assertRaisesRegex(RuntimeError, "Owned fixture stops bootstrap"):
                        run(root, game, devices.parent, root)
                self.assertEqual("--menu-position-controls-available" in launched[1], missing is None)
                # Native IPC retains the same exact groups used to derive the optional flag
                controls = read_json(next(root.glob("session-*")) / "bindings.json")
                self.assertEqual(controls["frag"], [] if missing == 8 else [71])
                self.assertEqual(controls["secondaryOffhand"], [] if missing == 9 else [69])
