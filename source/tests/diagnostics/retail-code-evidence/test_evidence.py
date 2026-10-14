import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from fixtures import CODE_MAPS, image_bytes
from pe_image import PeImage
from segments import capture_segments
from evidence import write_capture


class EvidenceTests(unittest.TestCase):
    def test_successful_manifest_marks_candidates_without_claiming_callable_abi(self):
        segments = capture_segments(PeImage(image_bytes()), lambda a, n: b'A' * n, CODE_MAPS)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'capture'
            write_capture(output, segments, {'processStartTicks': 12345})
            manifest = json.loads((output / 'capture.json').read_text())
            self.assertEqual(False, manifest['callableBindingsVerified'])
            self.assertEqual(False, manifest['completeLogicalFunctionsVerified'])
            self.assertEqual(6, len(manifest['segments']))
            for segment in manifest['segments']:
                code = (output / (segment['name'] + '.bin')).read_bytes()
                self.assertEqual(segment['sha256'], hashlib.sha256(code).hexdigest())
                self.assertEqual(segment['byteCount'], len(code))

    def test_existing_output_is_rejected_without_changing_its_contents(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            retained = output / 'capture.json'
            retained.write_text('retained')
            with self.assertRaises(FileExistsError):
                write_capture(output, [], {})
            self.assertEqual('retained', retained.read_text())

    def test_failed_manifest_serialization_leaves_no_completion_marker(self):
        from unittest.mock import patch
        segments = capture_segments(PeImage(image_bytes()), lambda a, n: b'A' * n, CODE_MAPS)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'capture'
            with patch('evidence.json.dump', side_effect=ValueError('output failed')):
                with self.assertRaises(ValueError):
                    write_capture(output, segments, {})
            self.assertFalse((output / 'capture.json').exists())

