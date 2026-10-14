"""Check the compiler boundary without executing any Windows code"""
import pathlib
import re
import sys
import unittest

BUILD = pathlib.Path(sys.argv.pop())

class SteamIdentityAssemblyTests(unittest.TestCase):
    def test_bridge_uses_the_observed_receiver_return_buffer_and_getter_slot(self):
        assembly = (BUILD / "SteamUser016Bridge.s").read_text()
        body = assembly.split("IwReadSteamUser016Identity:", 1)[1].split(".seh_endproc", 1)[0]
        # The slot-two aggregate call must provide a distinct buffer in RDX
        self.assertRegex(body, r"leaq\s+[^\n]+,%rdx|leaq\s+[^\n]+, %rdx")
        self.assertRegex(body, r"movq\s+%[a-z0-9]+, %rcx\n[^\n]*\n\s*leaq[^\n]*%rdx\n\s*callq\s+\*16")
        self.assertRegex(body, r"callq\s+\*16\(%[a-z0-9]+\)")
        self.assertRegex(body, r"callq\s+\*8\(%[a-z0-9]+\)")

    def test_independent_fixture_callee_writes_the_hidden_return_buffer(self):
        assembly = (BUILD / "FixtureSteamUser.s").read_text()
        # The independently compiled getter uses RDX storage and returns its address
        match = re.search(r'^"\?GetSteamID@FixtureSteamUser@[^\n]*:\s*[^\n]*\n(.*?)# -- End function', assembly, re.M | re.S)
        self.assertIsNotNone(match, "SDK getter definition is missing")
        body = match.group(1)
        self.assertRegex(body, r"movq\s+%rdx, %rax")
        self.assertRegex(body, r"movq\s+%[a-z0-9]+, \(%rdx\)")

if __name__ == "__main__":
    unittest.main()
