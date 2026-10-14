import hashlib
import io
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[4] / "packaging"))
from diagnostics.proton_logs.models import Limits
from diagnostics.proton_logs.parser import analyze_stream


FATAL = "1.001:0134:0264:err:seh:NtRaiseException Unhandled exception code c0000409 flags 1 addr 0x1412ba464\n"
DISPATCH = "1.000:0134:0264:trace:seh:dispatch_exception code=c0000005 (EXCEPTION_ACCESS_VIOLATION) flags=0 addr=0000000280C12580\n"
INFO = "1.000:0134:0264:trace:seh:dispatch_exception  info[0]=0000000000000008\n"
MODULE = '0.000:0134:0138:trace:loaddll:build_module Loaded L"Z:\\private\\iw7_ship.exe" at 0000000140000000: native\n'


def analyze(text, **kwargs):
    return analyze_stream(io.BytesIO(text.encode()), 308, **kwargs)


class ProtonLogParserTests(unittest.TestCase):
    def test_actual_missed_fatal_record_selects_exact_pid_and_same_thread_dispatch(self):
        # Grammar and addresses match the raw excerpt; private paths are replaced
        fixture = Path(__file__).parent / "fixtures" / "missed-fatal.log"
        with fixture.open("rb") as stream:
            report = analyze_stream(stream, 308)
        self.assertEqual(report["counts"]["fatal_events"], 1)
        event = report["fatal_events"][0]
        self.assertEqual(event["code"], "0xc0000409")
        self.assertEqual(event["thread_hex"], "0264")
        self.assertEqual(event["address"], "0x1412ba464")
        recent = event["recent_same_thread_dispatch"]
        self.assertEqual(recent["code"], "0xc0000005")
        self.assertEqual(recent["information"], {"0": "0x8", "1": "0x280c12580"})
        self.assertEqual(recent["access_operation"], "execute")
        self.assertEqual(recent["access_address"], "0x280c12580")
        self.assertIsNone(event["cause"])
        self.assertIsNone(event["allocation_owner"])
        self.assertIsNone(event["causal_module"])
        self.assertFalse(report["interpretation"]["stack_corruption_established"])

    def test_windows_pid_is_decimal_and_unrelated_pid_is_excluded(self):
        report = analyze_stream(io.BytesIO((FATAL + FATAL.replace("0134", "0139")).encode()), 313)
        self.assertEqual(report["counts"]["fatal_events"], 1)
        self.assertEqual(report["selection"]["wine_pid_hex"], "0139")
        self.assertFalse(report["selection"]["process_or_session_identity_verified"])
        self.assertFalse(report["interpretation"]["pid_reuse_or_multiple_sessions_excluded"])

    def test_oleacc_and_unhandled_substrings_are_not_fatal_records(self):
        noise = FATAL.replace("seh:NtRaiseException", "oleacc:find_class_data")
        noise += FATAL.replace("Unhandled exception", "Earlier Unhandled exception")
        noise += FATAL.replace("NtRaiseException", "other_function")
        self.assertEqual(analyze(noise)["fatal_events"], [])

    def test_other_threads_and_processes_cannot_supply_exception_parameters(self):
        noise = DISPATCH.replace("0264", "0290") + INFO.replace("0134", "0139")
        self.assertIsNone(analyze(noise + FATAL)["fatal_events"][0]["recent_same_thread_dispatch"])

    def test_new_dispatch_replaces_old_parameters_without_changing_saved_events(self):
        text = DISPATCH + INFO + FATAL + DISPATCH.replace("280C12580", "000001234") + FATAL
        events = analyze(text)["fatal_events"]
        self.assertEqual(events[0]["recent_same_thread_dispatch"]["information"], {"0": "0x8"})
        self.assertEqual(events[1]["recent_same_thread_dispatch"]["information"], {})
        self.assertEqual(events[0]["recent_same_thread_dispatch"]["address"], "0x280c12580")

    def test_stale_line_distance_and_time_gap_do_not_correlate(self):
        report = analyze(DISPATCH + "noise\n" * 3 + FATAL, limits=Limits(correlation_lines=2))
        self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])
        report = analyze(DISPATCH + FATAL.replace("1.001", "5.000"))
        self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])

    def test_timestamp_rollback_does_not_correlate(self):
        report = analyze(DISPATCH.replace("1.000", "2.000") + FATAL)
        self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])

    def test_missing_timestamp_uses_explicit_bounded_line_correlation(self):
        report = analyze(DISPATCH.replace("1.000:", "") + FATAL.replace("1.001:", ""))
        self.assertIsNotNone(report["fatal_events"][0]["recent_same_thread_dispatch"])
        self.assertIsNone(report["fatal_events"][0]["wine_time"])

    def test_malformed_dispatch_and_duplicate_info_clear_ambiguous_correlation(self):
        malformed = DISPATCH.replace("code=c0000005", "code=invalid")
        report = analyze(DISPATCH + INFO + malformed + FATAL)
        self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])
        self.assertEqual(report["counts"]["malformed_dispatch_records"], 1)
        report = analyze(DISPATCH + INFO + INFO + FATAL)
        self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])

    def test_malformed_information_invalidates_earlier_valid_parameters(self):
        prefix = "1.000:0134:0264:trace:seh:dispatch_exception "
        for malformed in ["info[0]=invalid", "info[1]=ZZ", "info[1]=1234 extra", "info[0]8", "info[2]=invalid"]:
            with self.subTest(record=malformed):
                report = analyze(DISPATCH + INFO + prefix + malformed + "\n" + FATAL)
                self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])
                self.assertEqual(report["counts"]["invalidated_information_records"], 1)

    def test_orphan_and_stale_valid_information_do_not_count_as_invalid(self):
        for text in [INFO + FATAL, DISPATCH + INFO.replace("1.000", "5.000") + FATAL.replace("1.001", "5.001")]:
            with self.subTest(text=text):
                report = analyze(text)
                self.assertEqual(report["counts"]["invalidated_information_records"], 0)
                self.assertIsNone(report["fatal_events"][0]["recent_same_thread_dispatch"])

    def test_valid_higher_parameters_are_ignored_and_fastfail_parameters_are_not_access_types(self):
        higher = "1.000:0134:0264:trace:seh:dispatch_exception info[2]=0000000000000008\n"
        report = analyze(DISPATCH + INFO + higher + FATAL)
        recent = report["fatal_events"][0]["recent_same_thread_dispatch"]
        self.assertEqual(recent["information"], {"0": "0x8"})
        self.assertEqual(report["counts"]["ignored_higher_information_records"], 1)
        report = analyze(DISPATCH.replace("c0000005", "c0000409") + INFO + higher + FATAL)
        recent = report["fatal_events"][0]["recent_same_thread_dispatch"]
        self.assertIsNone(recent["access_operation"])
        self.assertIsNone(recent["access_address"])
        self.assertIsNone(report["fatal_events"][0]["cause"])

    def test_malformed_fatal_is_counted_without_inventing_an_event(self):
        report = analyze(FATAL.replace("c0000409", "nothex") + FATAL.replace("flags 1", "flags 100000000"))
        self.assertEqual(report["counts"]["malformed_fatal_records"], 2)
        self.assertEqual(report["fatal_events"], [])

    def test_module_records_keep_only_observed_names_and_bases_for_exact_pid(self):
        report = analyze(MODULE + MODULE + MODULE.replace("0134", "0139") + FATAL)
        self.assertEqual(report["modules"], [{"name": "iw7_ship.exe", "base": "0x140000000", "line": 1}])
        self.assertFalse(report["interpretation"]["module_address_ranges_known"])
        self.assertNotIn("private", str(report["modules"]))
        self.assertIsNone(report["fatal_events"][0]["causal_module"])

    def test_event_module_context_and_thread_retention_are_bounded(self):
        extra_module = MODULE.replace("iw7_ship.exe", "other.dll")
        limits = Limits(max_events=1, max_modules=1, max_threads=1, context_lines=1, context_characters=8)
        report = analyze(MODULE + extra_module + DISPATCH + INFO + FATAL + DISPATCH.replace("0264", "0290") + FATAL,
                         limits=limits)
        self.assertEqual(len(report["fatal_events"]), 1)
        self.assertEqual(report["counts"]["fatal_events"], 2)
        self.assertEqual(report["counts"]["omitted_events"], 1)
        self.assertEqual(report["counts"]["omitted_modules"], 1)
        self.assertEqual(report["counts"]["evicted_threads"], 2)
        self.assertEqual(len(report["fatal_events"][0]["context"]), 1)
        self.assertLessEqual(len(report["fatal_events"][0]["context"][0]["message"]), 8)

    def test_oversized_line_line_count_and_source_size_fail_closed(self):
        for text, limits in [("a" * 20, Limits(max_line_bytes=10)),
                             ("a\nb\n", Limits(max_lines=1)), ("aaaa\n", Limits(max_source_bytes=4))]:
            with self.subTest(limits=limits), self.assertRaises(ValueError):
                analyze(text, limits=limits)

    def test_invalid_utf8_and_text_stream_are_rejected(self):
        for stream in [io.BytesIO(b"\xff\n"), io.StringIO(FATAL)]:
            with self.subTest(stream=stream), self.assertRaises(ValueError):
                analyze_stream(stream, 308)

    def test_hash_covers_every_source_byte_including_noise_and_other_pids(self):
        raw = ("unparsed noise\n" + FATAL + FATAL.replace("0134", "0139")).encode()
        report = analyze_stream(io.BytesIO(raw), 308)
        self.assertEqual(report["source"], {"sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw)})

    def test_streaming_never_requests_a_whole_file_read(self):
        class StreamingOnly(io.BytesIO):
            def read(self, *args):
                raise AssertionError("A whole-file read is forbidden")
        report = analyze_stream(StreamingOnly(("noise\n" * 5000 + FATAL).encode()), 308)
        self.assertEqual(report["counts"]["lines"], 5001)
        self.assertEqual(report["counts"]["fatal_events"], 1)

    def test_invalid_pid_and_unbounded_limit_values_are_rejected(self):
        for value in [True, 0, -1, 0x100000000, "308", 308.0]:
            with self.subTest(pid=value), self.assertRaises(ValueError):
                analyze_stream(io.BytesIO(), value)
        for name in vars(Limits()):
            for value in [True, 0, -1, 100_000_000_000]:
                with self.subTest(limit=name, value=value), self.assertRaises(ValueError):
                    Limits(**{name: value})
        with self.assertRaises(ValueError):
            analyze_stream(io.BytesIO(), 308, limits={})

    def test_mid_stream_read_failure_returns_no_partial_report(self):
        class FailingStream(io.BytesIO):
            def __init__(self):
                super().__init__(FATAL.encode())
                self.calls = 0

            def readline(self, maximum):
                self.calls += 1
                if self.calls == 2:
                    raise OSError("Controlled read failure")
                return super().readline(maximum)

        with self.assertRaises(OSError):
            analyze_stream(FailingStream(), 308)

    def test_empty_log_reports_unknown_identity_without_claiming_a_clean_session(self):
        report = analyze("")
        self.assertEqual(report["fatal_events"], [])
        self.assertFalse(report["selection"]["process_or_session_identity_verified"])
        self.assertIsNone(report["interpretation"]["cause"])


if __name__ == "__main__":
    unittest.main()
