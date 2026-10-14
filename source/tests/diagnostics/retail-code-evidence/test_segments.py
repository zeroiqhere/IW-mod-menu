import unittest

from fixtures import CODE_MAPS, image_bytes
from pe_image import PeImage
from profile import AMMO_CANDIDATES
from segments import capture_segments


class CaptureSegmentsTests(unittest.TestCase):
    def test_six_candidates_are_read_twice_and_preserve_exact_segment_bytes(self):
        reads = []
        def read(address, count):
            reads.append((address, count))
            return bytes([address & 255]) * count
        captured = capture_segments(PeImage(image_bytes()), read, CODE_MAPS)
        self.assertEqual(6, len(captured))
        self.assertEqual(12, len(reads))
        for index, segment in enumerate(captured):
            self.assertEqual(AMMO_CANDIDATES[index], (segment.name, segment.rva))
            self.assertEqual(bytes([segment.rva & 255]) * (32 + index), segment.code)
            self.assertEqual(reads[index * 2], reads[index * 2 + 1])

    def test_changed_or_short_repeated_read_rejects_the_entire_capture(self):
        for bad in (b'changed', b'', None):
            calls = 0
            def read(address, count):
                nonlocal calls
                calls += 1
                return b'A' * count if calls == 1 else bad
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                capture_segments(PeImage(image_bytes()), read, CODE_MAPS)

    def test_invalid_last_candidate_bounds_reject_before_any_code_read(self):
        import struct
        for end in (0, AMMO_CANDIDATES[-1][1] + 4097):
            data = image_bytes()
            struct.pack_into('<I', data, 1024 + 5 * 12 + 4, end)
            reads = []
            with self.subTest(end=end), self.assertRaises(ValueError):
                capture_segments(PeImage(data), lambda a, n: reads.append((a, n)), CODE_MAPS)
            self.assertEqual([], reads)

    def test_missing_unwind_or_unreadable_mapping_rejects_before_code_read(self):
        import struct
        for missing in (True, False):
            data = image_bytes()
            if missing:
                struct.pack_into('<I', data, 1024, 0xB00000)
            reads = []
            maps = CODE_MAPS if missing else CODE_MAPS.replace('r-xp', '--xp')
            with self.subTest(missing=missing), self.assertRaises(ValueError):
                capture_segments(PeImage(data), lambda a, n: reads.append((a, n)), maps)
            self.assertEqual([], reads)

