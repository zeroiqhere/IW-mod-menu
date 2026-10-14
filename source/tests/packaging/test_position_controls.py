import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from controls import read_bindings


class PositionControlTests(unittest.TestCase):
    def setUp(self):
        self.saved = '\n'.join(['bind MOUSE2 "+speed_throw"', 'bind V "+melee_zoom"',
                                'bind MOUSE1 "+attack"', 'bind F "+activate"'])

    def test_position_groups_preserve_all_remapped_slot_keys_without_aim_or_fire(self):
        saved = self.saved + '\n' + '\n'.join(['bind Q "+actionslot 1"', 'bind E "+actionslot 1"',
                   'bind G "+actionslot 2"', 'bind H "+actionslot 2"',
                   'bind LEFTARROW "+actionslot 3"', 'bind RIGHTARROW "+actionslot 4"'])
        controls = read_bindings(saved)
        self.assertEqual(controls.get("moveUp"), [81, 69])
        self.assertEqual(controls.get("moveDown"), [71, 72])
        self.assertEqual(controls["up"], [2, 81, 69])
        self.assertEqual(controls["down"], [1, 71, 72])
        self.assertEqual(controls["previousChoice"], [37])
        self.assertEqual(controls["nextChoice"], [39])
        self.assertTrue(set(controls["moveUp"]).isdisjoint(controls["aim"]))
        self.assertTrue(set(controls["moveDown"]).isdisjoint(controls["attack"]))

    def test_missing_partial_and_misspelled_position_actions_never_invent_keys(self):
        for addition, up, down in [('', [], []), ('\nbind Q "+actionslot 1"', [81], []),
                                  ('\nbind G "+actionslot 2"', [], [71]),
                                  ('\nbind Q "+actionslot1"\nbind G "+actionslot2"', [], [])]:
            with self.subTest(addition=addition):
                controls = read_bindings(self.saved + addition)
                self.assertEqual(controls.get("moveUp"), up)
                self.assertEqual(controls.get("moveDown"), down)
                self.assertEqual(controls["aim"], [2])
                self.assertEqual(controls["attack"], [1])

    def test_oversized_exact_or_combined_groups_reject_before_native_startup(self):
        # The native protocol permits at most sixteen keys in any one group
        for count in (16, 17):
            slots = '\n'.join(f'bind {key} "+actionslot 1"' for key in 'ABCDEFGHIJKLMNOPQ'[:count])
            base = self.saved.replace('bind F "+activate"', 'bind F1 "+activate"')
            with self.subTest(count=count), self.assertRaisesRegex(ValueError, "supported group size"):
                read_bindings(base + '\n' + slots)
