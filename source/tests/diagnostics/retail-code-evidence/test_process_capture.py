import hashlib
import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from fixtures import CODE_MAPS, image_bytes, process_stat
import capture
from pe_image import PeImage
from profile import AMMO_CANDIDATES


class ProcessCaptureTests(unittest.TestCase):
    def setUp(self):
        # Owned files model proc records; no real process or proc path is accessed
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.image = self.root / 'owned image.exe'
        self.disk = bytes(image_bytes())
        self.image.write_bytes(self.disk)
        self.process = self.root / '42'
        self.process.mkdir()
        (self.process / 'stat').write_text(process_stat())
        (self.process / 'comm').write_text('iw7_ship.exe\n')
        stat = self.image.stat()
        device = f'{os.major(stat.st_dev):02x}:{os.minor(stat.st_dev):02x}'
        self.maps = f'140000000-140001000 r--p 0 {device} {stat.st_ino} {self.image}\n' + CODE_MAPS
        (self.process / 'maps').write_text(self.maps)
        # Sparse storage gives real pread behavior without allocating the virtual gap
        pe = PeImage(self.disk)
        with (self.process / 'mem').open('wb') as memory:
            memory.seek(pe.base)
            memory.write(self.disk)
            for index, (_, rva) in enumerate(AMMO_CANDIDATES):
                memory.seek(pe.base + rva)
                memory.write(bytes([index + 1]) * (32 + index))
        self.output = self.root / 'output'
        self.fingerprint = hashlib.sha256(self.disk).hexdigest()

    def run_capture(self):
        # Only the fingerprint policy uses a fixture substitution, not process I/O
        with patch.object(capture, 'IMAGE_SHA256', self.fingerprint):
            capture.capture_from_process(self.image, self.process, self.output)

    def test_owned_files_exercise_complete_capture_and_readonly_cloexec_handle(self):
        original_open = os.open
        flags = []
        def open_recorded(path, supplied):
            flags.append(supplied)
            return original_open(path, supplied)
        with patch.object(capture.os, 'open', open_recorded):
            self.run_capture()
        self.assertEqual([os.O_RDONLY | os.O_CLOEXEC], flags)
        manifest = json.loads((self.output / 'capture.json').read_text())
        self.assertEqual(12345, manifest['processStartTicks'])
        self.assertEqual(6, len(manifest['segments']))
        self.assertEqual(0, manifest['writesToGame'])

    def test_unsupported_image_or_wrong_process_rejects_before_memory_open(self):
        for scenario in ('fingerprint', 'name', 'pid', 'mapping'):
            with self.subTest(scenario=scenario):
                (self.process / 'comm').write_text('wrong.exe' if scenario == 'name' else 'iw7_ship.exe')
                (self.process / 'stat').write_text(process_stat(pid=99 if scenario == 'pid' else 42))
                (self.process / 'maps').write_text('' if scenario == 'mapping' else self.maps)
                expected = '0' * 64 if scenario == 'fingerprint' else self.fingerprint
                with patch.object(capture, 'IMAGE_SHA256', expected), patch.object(capture.os, 'open') as opened:
                    with self.assertRaises(ValueError):
                        capture.capture_from_process(self.image, self.process, self.output)
                    opened.assert_not_called()
                self.assertFalse(self.output.exists())

    def test_context_changes_or_header_changes_leave_no_completed_bundle(self):
        original_read = os.pread
        for scenario in ('token', 'maps', 'comm', 'header', 'late-token', 'late-maps', 'late-comm'):
            with self.subTest(scenario=scenario):
                (self.process / 'stat').write_text(process_stat())
                (self.process / 'comm').write_text('iw7_ship.exe')
                (self.process / 'maps').write_text(self.maps)
                calls = 0
                def read_changed(handle, count, address):
                    nonlocal calls
                    calls += 1
                    data = original_read(handle, count, address)
                    if calls == 2:
                        if scenario == 'token': (self.process / 'stat').write_text(process_stat(start=99999))
                        if scenario == 'maps': (self.process / 'maps').write_text(self.maps + '\n')
                        if scenario == 'comm': (self.process / 'comm').write_text('replacement.exe')
                    if calls == 14:
                        if scenario == 'late-token': (self.process / 'stat').write_text(process_stat(start=99999))
                        if scenario == 'late-maps': (self.process / 'maps').write_text(self.maps + '\n')
                        if scenario == 'late-comm': (self.process / 'comm').write_text('replacement.exe')
                    if scenario == 'header' and calls == 14:
                        return data[:-1] + bytes([data[-1] ^ 1])
                    return data
                try:
                    with patch.object(capture.os, 'pread', read_changed), self.assertRaises(ValueError):
                        self.run_capture()
                    self.assertFalse(self.output.exists())
                finally:
                    # Failed variants cannot leave a success bundle for the next independent case
                    if self.output.exists():
                        shutil.rmtree(self.output)

    def test_loaded_header_mismatch_and_short_header_release_the_read_handle(self):
        original_close = os.close
        for header in (b'', bytes(4096)):
            closed = []
            def close_recorded(handle):
                closed.append(handle)
                original_close(handle)
            with self.subTest(length=len(header)), patch.object(capture.os, 'pread', return_value=header), patch.object(capture.os, 'close', close_recorded):
                with self.assertRaises(ValueError):
                    self.run_capture()
                self.assertEqual(1, len(closed))
                self.assertFalse(self.output.exists())

    def test_existing_output_rejects_before_process_memory_open(self):
        self.output.mkdir()
        with patch.object(capture.os, 'open') as opened:
            with self.assertRaises(FileExistsError):
                self.run_capture()
            opened.assert_not_called()
