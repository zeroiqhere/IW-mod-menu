from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from health import SessionHealth


class SessionHealthTests(unittest.TestCase):
    def setUp(self):
        self.health = SessionHealth(123, 1, 0)
        self.state = {"processId": 123, "generation": 1}
        self.renderer = {"processId": 123, "faults": 0, "drawnFrames": 0}

    def test_closed_menu_is_healthy_without_any_drawn_frames(self):
        self.health.observe(self.state, self.renderer, None, 1)

    def test_advancing_generation_keeps_session_healthy(self):
        for now in range(1, 6):
            self.health.observe(dict(self.state, generation=now + 1), self.renderer, None, now)

    def test_stale_generation_requests_stop_after_three_seconds(self):
        with self.assertRaisesRegex(RuntimeError, "heartbeat stopped"):
            self.health.observe(self.state, self.renderer, None, 3.01)

    def test_rollback_and_foreign_identity_are_rejected(self):
        for state in ({"processId": 123, "generation": 0}, {"processId": 456, "generation": 2}):
            with self.assertRaises(RuntimeError):
                self.health.observe(state, self.renderer, None, 1)

    def test_renderer_fault_requests_graceful_stop(self):
        with self.assertRaisesRegex(RuntimeError, "renderer stopped"):
            self.health.observe(self.state, dict(self.renderer, faults=1), None, 1)

    def test_missing_diagnostics_allow_startup_then_request_stop(self):
        self.health.observe(self.state, None, None, 1)
        with self.assertRaisesRegex(RuntimeError, "graphics diagnostics"):
            self.health.observe(dict(self.state, generation=2), None, None, 3.01)

    def test_stopped_native_input_worker_requests_graceful_stop(self):
        with self.assertRaisesRegex(RuntimeError, "input worker stopped: fixture failure"):
            self.health.observe(self.state, self.renderer, {"processId": 123, "error": "fixture failure"}, 1)
