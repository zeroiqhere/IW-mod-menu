import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from controls import read_bindings


class OpeningActionBindingsTests(unittest.TestCase):
    # Required controls keep the fixture independent of optional action groups
    saved = "\n".join([
        'bind MOUSE2 "+speed_throw"',
        'bind V "+melee_zoom"',
        'bind MOUSE1 "+attack"',
        'bind F "+activate"',
    ])

    def test_exact_sprint_stance_and_jump_actions_preserve_saved_keys(self):
        packet = read_bindings(
            self.saved + '\nbind SHIFT "+breath_sprint"\nbind CTRL "+stance"\nbind SPACE "+gostand"'
        )
        self.assertEqual(packet["sprint"], [16])
        self.assertEqual(packet["stance"], [17])
        self.assertEqual(packet["jump"], [32])

    def test_missing_optional_actions_remain_empty_without_guessed_keys(self):
        packet = read_bindings(self.saved)
        for group in ("sprint", "stance", "jump"):
            self.assertEqual(packet[group], [])

    def test_multiple_remapped_keys_stay_in_their_exact_action_group(self):
        packet = read_bindings(
            self.saved + '\nbind Q "+breath_sprint"\nbind MOUSE3 "+breath_sprint"'
            '\nbind E "+stance"\nbind R "+gostand"'
        )
        self.assertEqual(packet["sprint"], [81, 4])
        self.assertEqual(packet["stance"], [69])
        self.assertEqual(packet["jump"], [82])
        self.assertEqual(packet["up"], [2])
        self.assertEqual(packet["down"], [1])

    def test_similar_unverified_action_names_do_not_become_capabilities(self):
        packet = read_bindings(
            self.saved + '\nbind SHIFT "+sprint"\nbind CTRL "+crouch"\nbind SPACE "+jump"'
        )
        self.assertEqual((packet["sprint"], packet["stance"], packet["jump"]), ([], [], []))

    def test_unsupported_optional_physical_key_is_rejected_without_guessing(self):
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            read_bindings(self.saved + '\nbind MWHEELUP "+gostand"')

    def test_duplicate_key_and_oversized_group_are_rejected_before_launch(self):
        with self.assertRaisesRegex(ValueError, "Repeated"):
            read_bindings(self.saved + '\nbind F "+breath_sprint"')
        # The sixteen-key group bound applies to these optional groups too
        actions = "\n".join(
            f'bind {key} "+breath_sprint"' for key in "ABCDEFGHIJKLMNOPQ" if key not in "FV"
        )
        with self.assertRaisesRegex(ValueError, "group size"):
            read_bindings(
                self.saved + "\n" + actions + '\nbind SHIFT "+breath_sprint"\nbind CTRL "+breath_sprint"'
            )
