import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "packaging"))
from diagnostics.panel import DiagnosticPanel
from diagnostics.protocol import parse_command, valid_stop


def command(sequence=1, input_name="toggle", **overrides):
    value = {"version": 1, "processId": 42, "sequence": sequence, "input": input_name}
    value.update(overrides)
    return json.dumps(value).encode()


class ProtocolTests(unittest.TestCase):
    def test_toggle_scroll_back_and_select_remain_harmless(self):
        panel = DiagnosticPanel(42)
        self.assertFalse(panel.is_open)
        panel.consume(parse_command(command(), 42))
        self.assertTrue(panel.is_open)
        panel.consume(parse_command(command(2, "down"), 42))
        self.assertEqual(panel.selected_index, 1)
        before = (panel.is_open, panel.selected_index)
        panel.consume(parse_command(command(3, "select"), 42))
        self.assertEqual((panel.is_open, panel.selected_index), before)
        panel.consume(parse_command(command(4, "up"), 42))
        self.assertEqual(panel.selected_index, 0)
        panel.consume(parse_command(command(5, "back"), 42))
        self.assertFalse(panel.is_open)
        self.assertTrue(all(row["enabled"] is False for row in panel.snapshot()["rows"]))

    def test_foreign_invalid_and_duplicate_payloads_ack_without_ui_changes_or_replay(self):
        panel = DiagnosticPanel(42)
        bodies = [command(processId=43), command(2, version=2), command(3, input_name="enable"),
                  command(4, extra=True), b'{"sequence":5,"version":1,"version":1,"processId":42,"input":"toggle"}',
                  command(6, version=True), command(7, processId=True)]
        for sequence, body in enumerate(bodies, 1):
            self.assertTrue(panel.consume(parse_command(body, 42)))
            self.assertEqual(panel.last_sequence, sequence)
            self.assertFalse(panel.is_open)
        self.assertFalse(panel.consume(parse_command(command(1), 42)))
        self.assertEqual(panel.last_sequence, 7)
        self.assertFalse(panel.is_open)

    def test_malformed_oversized_ambiguous_and_noninteger_sequences_do_not_ack(self):
        panel = DiagnosticPanel(42)
        for body in [b'{', b'[]', b'{}', b'{"sequence":1,"sequence":2}', b'{"sequence":true}',
                     b'{"sequence":-1}', b'{"sequence":1.0}', b'{"sequence":18446744073709551616}',
                     b' ' * 4097, b'{"sequence":NaN}', b'\xff', command().decode().encode("utf-16")]:
            self.assertFalse(panel.consume(parse_command(body, 42)))
        self.assertEqual(panel.last_sequence, 0)

    def test_stop_requires_exact_unique_version_and_process(self):
        self.assertTrue(valid_stop(b'{"version":1,"processId":42}', 42))
        for body in [b'{"version":true,"processId":42}', b'{"version":2,"processId":42}',
                     b'{"version":1,"processId":43}', b'{"version":1,"processId":true}',
                     b'{"version":1,"version":1,"processId":42}', b'{"version":1,"processId":42,"extra":0}',
                     b' ' * 1025]:
            self.assertFalse(valid_stop(body, 42))
