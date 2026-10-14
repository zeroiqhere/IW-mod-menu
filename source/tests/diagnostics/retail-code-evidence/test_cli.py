import subprocess
import sys
import unittest
from fixtures import SOURCE_DIRECTORY


class CliTests(unittest.TestCase):
    def test_import_is_inert_even_when_file_reads_and_process_opens_are_forbidden(self):
        root = SOURCE_DIRECTORY
        code = '''import os, pathlib, sys
sys.path.insert(0, sys.argv[1])
def forbidden(*args, **kwargs):
    raise AssertionError('import performed diagnostic I/O')
os.open = forbidden
os.pread = forbidden
pathlib.Path.read_text = forbidden
pathlib.Path.read_bytes = forbidden
import capture
'''
        result = subprocess.run([sys.executable, '-I', '-c', code, str(root)], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)

    def test_cli_requires_explicit_inputs_and_rejects_nonpositive_pid(self):
        script = SOURCE_DIRECTORY / 'capture.py'
        for args in ([], ['--image', 'absent', '--pid', '0', '--output', 'absent']):
            with self.subTest(args=args):
                result = subprocess.run([sys.executable, str(script), *args], capture_output=True, text=True)
                self.assertEqual(2, result.returncode)
