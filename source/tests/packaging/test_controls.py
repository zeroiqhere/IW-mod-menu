import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from controls import read_bindings


class SavedControlTests(unittest.TestCase):
    def setUp(self):
        self.saved = '\n'.join(['bind MOUSE2 "+speed_throw"', 'bind V "+melee_zoom"',
                                'bind MOUSE5 "+melee_zoom"', 'bind MOUSE1 "+attack"',
                                'bind F "+activate"', 'bind UPARROW "+actionslot 1"',
                                'bind DOWNARROW "+actionslot 2"'])

    def test_saved_aim_melee_fire_use_and_arrows_match_native_keys(self):
        self.assertEqual(read_bindings(self.saved), {"version": 1, "aim": [2], "melee": [86, 6],
                         "up": [2, 38], "down": [1, 40], "select": [70],
                         "previousChoice": [], "nextChoice": [], "attack": [1],
                         "moveUp": [38], "moveDown": [40], "frag": [], "secondaryOffhand": [], "sprint": [], "stance": [], "jump": []})

    def test_attack_group_uses_the_saved_attack_action_without_scroll_slot_two(self):
        bindings = read_bindings(self.saved)
        # The editor consumes actual fire while normal scrolling also accepts slot two
        self.assertEqual(bindings.get("attack"), [1])
        self.assertEqual(bindings["down"], [1, 40])

    def test_multiple_remapped_attack_keys_are_all_preserved(self):
        saved = self.saved.replace('bind MOUSE1 "+attack"',
                                  'bind Q "+attack"\nbind E "+attack"')
        bindings = read_bindings(saved)
        # Import every saved fire key instead of choosing the first down binding
        self.assertEqual(bindings.get("attack"), [81, 69])
        self.assertEqual(bindings["down"], [81, 69, 40])
        self.assertTrue(set(bindings.get("attack", [])).issubset(bindings["down"]))

    def test_earlier_remapped_slot_two_binding_never_enters_attack(self):
        saved = 'bind G "+actionslot 2"\n' + self.saved.replace(
            'bind DOWNARROW "+actionslot 2"', "")
        bindings = read_bindings(saved)
        # File order does not turn an unrelated scrolling action into fire
        self.assertEqual(bindings.get("attack"), [1])
        self.assertEqual(bindings["down"], [1, 71])

    def test_attack_addition_preserves_all_existing_packet_groups(self):
        bindings = read_bindings(self.saved)
        self.assertEqual(bindings.pop("attack", None), [1])
        # Existing consumers keep the same groups and optional choice defaults
        self.assertEqual(bindings, {"version": 1, "aim": [2], "melee": [86, 6],
                         "up": [2, 38], "down": [1, 40], "select": [70],
                         "previousChoice": [], "nextChoice": [], "moveUp": [38], "moveDown": [40], "frag": [], "secondaryOffhand": [], "sprint": [], "stance": [], "jump": []})

    def test_missing_attack_is_rejected_even_when_slot_two_is_saved(self):
        saved = self.saved.replace('bind MOUSE1 "+attack"', "")
        # A saved scroll-down action cannot replace the required attack action
        with self.assertRaisesRegex(ValueError, r"required action: \+attack"):
            read_bindings(saved)

    def test_missing_required_action_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "required action"):
            read_bindings(self.saved.replace('bind F "+activate"', ""))

    def test_duplicate_key_is_rejected_before_launch(self):
        with self.assertRaisesRegex(ValueError, "Repeated"):
            read_bindings(self.saved + '\nbind V "+activate"')

    def test_wheel_melee_is_rejected_instead_of_guessed(self):
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            read_bindings(self.saved.replace("MOUSE5", "MWHEELUP"))

    def test_arrows_are_optional_and_controls_still_work(self):
        keys = read_bindings('\n'.join(self.saved.splitlines()[:-2]))
        self.assertEqual(keys["up"], [2])
        self.assertEqual(keys["down"], [1])

    def test_oversized_saved_file_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "file size"):
            read_bindings(" " * 65537)

    def test_optional_choice_controls_are_imported_from_saved_action_slots(self):
        keys = read_bindings(self.saved + '\nbind 3 "+actionslot 3"\nbind 4 "+actionslot 4"')
        self.assertEqual(keys["previousChoice"], [51])
        self.assertEqual(keys["nextChoice"], [52])

    def test_remapped_choice_controls_use_actual_keys_and_not_number_defaults(self):
        keys = read_bindings(self.saved + '\nbind LEFTARROW "+actionslot 3"\nbind RIGHTARROW "+actionslot 4"')
        self.assertEqual(keys["previousChoice"], [37])
        self.assertEqual(keys["nextChoice"], [39])

    def test_missing_and_partial_choice_groups_do_not_guess_bindings(self):
        keys = read_bindings(self.saved)
        self.assertEqual(keys["previousChoice"], [])
        self.assertEqual(keys["nextChoice"], [])
        keys = read_bindings(self.saved + '\nbind 3 "+actionslot 3"')
        self.assertEqual(keys["previousChoice"], [51])
        self.assertEqual(keys["nextChoice"], [])

    def test_choice_actions_with_unknown_spellings_do_not_become_saved_bindings(self):
        keys = read_bindings(self.saved + '\nbind 3 "+actionslot3"\nbind 4 "+actionslot4"')
        self.assertEqual(keys["previousChoice"], [])
        self.assertEqual(keys["nextChoice"], [])

    def test_unsupported_choice_key_rejects_without_guessing_a_virtual_key(self):
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            read_bindings(self.saved + '\nbind MWHEELUP "+actionslot 3"')
