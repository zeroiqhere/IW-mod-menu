import struct
import unittest
from fixtures import image_bytes
from pe_image import PeImage
from profile import AMMO_CANDIDATES


class PeImageTests(unittest.TestCase):
    def test_complete_x64_headers_are_required(self):
        for data in (b'', image_bytes()[:180]):
            with self.subTest(size=len(data)), self.assertRaises(ValueError):
                PeImage(data)
        data = image_bytes()
        struct.pack_into('<H', data, 132, 0x14C)
        with self.assertRaises(ValueError):
            PeImage(data)

    def test_metadata_identity_includes_the_exception_directory(self):
        data = image_bytes()
        original = PeImage(data).identity
        struct.pack_into('<I', data, 288, 0xD00010)
        self.assertNotEqual(original, PeImage(data).identity)

    def test_duplicate_unwind_entries_cannot_choose_an_ambiguous_segment(self):
        data = image_bytes()
        begin = AMMO_CANDIDATES[0][1]
        struct.pack_into('<III', data, 1024 + 12, begin, begin + 64, 0)
        with self.assertRaises(ValueError):
            PeImage(data).function_size(begin)

    def test_virtual_padding_is_not_raw_file_backing(self):
        with self.assertRaises(ValueError):
            PeImage(image_bytes()).raw_offset(0xB00900, 1)

    def test_nonexecutable_section_and_oversized_code_ranges_are_rejected(self):
        data = image_bytes()
        struct.pack_into('<I', data, 428, 0)
        with self.assertRaises(ValueError):
            PeImage(data).executable_span(AMMO_CANDIDATES[0][1], 32)
        for rva, size in ((0xB0FFFF, 2), (-1, 1), (0xB01030, 4097)):
            with self.subTest(rva=rva, size=size), self.assertRaises(ValueError):
                PeImage(image_bytes()).executable_span(rva, size)
