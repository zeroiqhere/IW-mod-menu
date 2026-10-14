import os
import re
import subprocess
import unittest
from pathlib import Path


class InertProbePeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # Inspect the explicitly supplied artifact without loading it or guessing a build path
        cls.path = Path(os.environ["IW_PROBE_TEST_DLL"])
        cls.report = subprocess.run(
            ["x86_64-w64-mingw32-objdump", "-p", str(cls.path)],
            check=True,
            capture_output=True,
            text=True,
        ).stdout

    def test_only_windows_core_and_crt_dependencies_are_imported(self):
        imports = re.findall(r"DLL Name:\s*(\S+)", self.report)
        self.assertTrue(imports)
        for name in imports:
            # Compiler CRT support is expected, but graphics and third-party libraries are not
            allowed = name.lower() in {"kernel32.dll", "user32.dll"}
            allowed |= bool(re.fullmatch(r"api-ms-win-crt-[\w-]+\.dll", name.lower()))
            self.assertTrue(allowed, name)

    def test_only_the_two_bootstrap_contract_exports_are_present(self):
        names = set(re.findall(
            r"^\s*\[\s*\d+\]\s+\+base\[\s*\d+\]\s+[0-9a-fA-F]+\s+(\w+)\s*$",
            self.report,
            re.MULTILINE,
        ))
        self.assertEqual({"InitializeOverlay", "StopOverlay"}, names)

    def test_no_remote_memory_write_or_process_open_api_is_imported(self):
        # The checked caller-packet read is local and is intentionally permitted
        self.assertIn("ReadProcessMemory", self.report)
        for name in ("WriteProcessMemory", "OpenProcess", "VirtualAllocEx", "CreateRemoteThread"):
            self.assertNotRegex(self.report, rf"\b{re.escape(name)}\b")

    def test_no_hook_thread_patch_file_or_input_api_is_imported(self):
        for name in (
            "FlushInstructionCache", "SuspendThread", "ResumeThread", "OpenThread",
            "GetThreadContext", "SetThreadContext", "CreateThread", "CreateFileA",
            "CreateFileW", "ReadFile", "WriteFile", "GetAsyncKeyState", "SendInput",
        ):
            self.assertNotRegex(self.report, rf"\b{re.escape(name)}\b")

    def test_artifact_is_windows_x64(self):
        self.assertIn("file format pei-x86-64", self.report)


if __name__ == "__main__":
    unittest.main()
