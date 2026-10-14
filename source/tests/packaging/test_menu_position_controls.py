from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packaging"))
from controls import menu_position_controls_available, read_bindings


class MenuPositionSavedControlTests(unittest.TestCase):
    def setUp(self):
        # Exact saved actions carry the complete editor without changing required game keys
        self.saved = '\n'.join(['bind MOUSE2 "+speed_throw"', 'bind V "+melee_zoom"',
                                'bind MOUSE1 "+attack"', 'bind F "+activate"',
                                'bind UPARROW "+actionslot 1"', 'bind DOWNARROW "+actionslot 2"',
                                'bind 3 "+actionslot 3"', 'bind 4 "+actionslot 4"',
                                'bind G "+frag"', 'bind E "+smoke"'])

    def test_exact_frag_and_smoke_actions_preserve_saved_virtual_keys(self):
        controls = read_bindings(self.saved)
        self.assertEqual(controls.get("frag"), [71])
        self.assertEqual(controls.get("secondaryOffhand"), [69])
        self.assertEqual(controls["moveUp"], [38])
        self.assertEqual(controls["moveDown"], [40])

    def test_remapped_and_multiple_grenade_keys_are_preserved_without_defaults(self):
        saved = self.saved.replace('bind G "+frag"', 'bind Q "+frag"\nbind MOUSE4 "+frag"')
        saved = saved.replace('bind E "+smoke"', 'bind R "+smoke"\nbind MOUSE5 "+smoke"')
        controls = read_bindings(saved)
        self.assertEqual(controls.get("frag"), [81, 5])
        self.assertEqual(controls.get("secondaryOffhand"), [82, 6])

    def test_missing_grenade_actions_are_empty_without_disabling_ordinary_menu_keys(self):
        saved = self.saved.replace('bind G "+frag"', '').replace('bind E "+smoke"', '')
        controls = read_bindings(saved)
        self.assertEqual(controls.get("frag"), [])
        self.assertEqual(controls.get("secondaryOffhand"), [])
        self.assertEqual(controls["select"], [70])

    def test_partial_grenade_actions_never_invent_the_other_group(self):
        controls = read_bindings(self.saved.replace('bind E "+smoke"', ''))
        self.assertEqual(controls.get("frag"), [71])
        self.assertEqual(controls.get("secondaryOffhand"), [])

    def test_similar_action_names_do_not_create_grenade_capability(self):
        controls = read_bindings(self.saved.replace('"+frag"', '"frag"').replace('"+smoke"', '"+secondaryoffhand"'))
        self.assertEqual(controls.get("frag"), [])
        self.assertEqual(controls.get("secondaryOffhand"), [])

    def test_unsupported_grenade_key_is_rejected_without_guessing(self):
        for key in ("MWHEELUP", "SEMICOLON"):
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "Unsupported"):
                read_bindings(self.saved.replace('bind G ', f'bind {key} '))

    def test_grenade_keys_cannot_reuse_a_required_saved_action_key(self):
        with self.assertRaisesRegex(ValueError, "Repeated"):
            read_bindings(self.saved + '\nbind V "+frag"')

    def test_grenade_group_size_limit_is_enforced_before_launch(self):
        # Letters unused by required actions provide seventeen distinct exact keys
        saved = self.saved.replace('bind G "+frag"', '')
        saved += '\n' + '\n'.join(f'bind {key} "+frag"' for key in "ABCDGHIJKLMNOPQRS")
        with self.assertRaisesRegex(ValueError, "group size"):
            read_bindings(saved)

    def test_capability_requires_all_four_exact_slots_and_both_grenade_groups(self):
        controls = read_bindings(self.saved)
        self.assertTrue(menu_position_controls_available(controls))
        for group in ("moveUp", "moveDown", "previousChoice", "nextChoice", "frag", "secondaryOffhand"):
            with self.subTest(group=group):
                incomplete = dict(controls)
                incomplete[group] = []
                self.assertFalse(menu_position_controls_available(incomplete))

    def test_legacy_packet_cannot_invent_menu_position_capability(self):
        self.assertFalse(menu_position_controls_available({"up": [2], "down": [1], "select": [70]}))
