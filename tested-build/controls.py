"""Translate saved game actions into the native input protocol."""

import re


def virtual_key(name: str) -> int:
    # Only keys with an exact Windows key representation are accepted
    mouse = {"MOUSE1": 1, "MOUSE2": 2, "MOUSE3": 4, "MOUSE4": 5, "MOUSE5": 6}
    fixed = {"UPARROW": 38, "DOWNARROW": 40, "LEFTARROW": 37,
             "RIGHTARROW": 39, "SPACE": 32, "SHIFT": 16, "CTRL": 17, "ALT": 18}
    if name in mouse:
        return mouse[name]
    if name in fixed:
        return fixed[name]
    if re.fullmatch(r"[A-Z0-9]", name):
        return ord(name)
    if re.fullmatch(r"F(?:[1-9]|1[0-2])", name):
        return 111 + int(name[1:])
    raise ValueError(f"Unsupported menu control in saved bindings: {name}")


def read_bindings(text: str) -> dict:
    if len(text.encode("utf-8")) > 65536:
        raise ValueError("Saved controls exceed the supported file size")
    actions: dict[str, list[int]] = {}
    keys: dict[str, str] = {}
    for line in text.splitlines():
        match = re.fullmatch(r'\s*bind\s+(\S+)\s+"([^"\r\n]+)"\s*', line)
        if match:
            key, action = match.groups()
            # Repeated keys are ambiguous even if their actions happen to match
            if key in keys:
                raise ValueError(f"Repeated saved key binding: {key}")
            keys[key] = action
    needed = {"+speed_throw", "+melee_zoom", "+attack", "+activate",
              "+actionslot 1", "+actionslot 2", "+actionslot 3", "+actionslot 4", "+frag", "+smoke",
              "+breath_sprint", "+stance", "+gostand"}
    for key, action in keys.items():
        if action in needed:
            actions.setdefault(action, []).append(virtual_key(key))
    for action in ("+speed_throw", "+melee_zoom", "+attack", "+activate"):
        if not actions.get(action):
            raise ValueError(f"No saved key for required action: {action}")
    packet = {"version": 1, "aim": actions["+speed_throw"],
            "melee": actions["+melee_zoom"],
            "up": list(dict.fromkeys(actions["+speed_throw"] + actions.get("+actionslot 1", []))),
            "down": list(dict.fromkeys(actions["+attack"] + actions.get("+actionslot 2", []))),
            # Width editing uses saved fire keys without the extra scroll-down action
            "attack": actions["+attack"],
            "select": actions["+activate"],
            # Missing optional actions stay empty rather than assuming number keys
            "previousChoice": actions.get("+actionslot 3", []),
            "nextChoice": actions.get("+actionslot 4", []),
            # Position editing uses the exact vertical slots without aim or fire
            "moveUp": actions.get("+actionslot 1", []),
            "moveDown": actions.get("+actionslot 2", []),
            # Menu positioning uses exact grenade actions without fixed G or E assumptions
            "frag": actions.get("+frag", []),
            "secondaryOffhand": actions.get("+smoke", []),
            # Opening choices use only these exact saved action names
            "sprint": actions.get("+breath_sprint", []),
            "stance": actions.get("+stance", []),
            "jump": actions.get("+gostand", [])}
    # Reject unsupported packets before a native worker can be started
    if any(len(group) > 16 for group in packet.values() if isinstance(group, list)):
        raise ValueError("Saved menu controls exceed the supported group size")
    return packet


def menu_position_controls_available(controls: dict) -> bool:
    # This capability is computed only after read_bindings validates the saved packet
    return all(controls.get(group) for group in (
        "moveUp", "moveDown", "previousChoice", "nextChoice", "frag", "secondaryOffhand"))
