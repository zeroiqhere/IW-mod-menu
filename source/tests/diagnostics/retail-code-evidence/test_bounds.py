import unittest
from fixtures import process_stat
from bounds import process_token, readable_span


class BoundTests(unittest.TestCase):
    def test_live_process_token_uses_creation_time_despite_spaces_in_command(self):
        self.assertEqual((42, 12345), process_token(process_stat()))

    def test_nonpositive_process_identity_or_dead_state_is_rejected(self):
        for raw in (process_stat(pid=0), process_stat(start=0), process_stat(state='Z'), 'invalid'):
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                process_token(raw)

    def test_reads_require_one_readable_mapping_and_fit_the_unsigned_domain(self):
        maps = '1000-2000 r-xp 0 00:00 0\n2000-3000 r-xp 0 00:00 0'
        self.assertEqual((0x1000, 0x2000), readable_span(maps, 0x1100, 69))
        for address, count in ((0x1FFF, 2), (-1, 1), (2**64 - 1, 2), (0x1000, 0), (0x1000, 4097)):
            with self.subTest(address=address, count=count), self.assertRaises(ValueError):
                readable_span(maps, address, count)
