# Independent instruction creation color and observed Rainbow routing

This candidate retains separate outline and background colors for the owned
instruction HUD. Its immutable creation color and mode are captured by the actual
controller, then joined to the production parser and painter. The saved menu and
installed disabled DLL are untouched. Full Apparition parity remains unfinished.

## Reference behavior

Fresh official [Apparition main](https://github.com/CF4x99/Apparition/blob/main/main.gsc)
creates the instruction outline from MainTheme and uses a design-specific initial
background. The [theme callbacks](https://github.com/CF4x99/Apparition/blob/main/Menu/menu_customization.gsc)
choose the existing background when the **current** design is AIO, and otherwise
choose the existing outline. A palette change therefore cannot recolor a stable
AIO outline. Closing ordinary menu chrome does not destroy this independent HUD.

Current design and creation design differ during a changed-design rebuild. The
old AIO HUD can receive a Rainbow outline sample after the committed design
becomes Classic; its background keeps the last sample it received. Conversely,
an old non-AIO HUD can receive a background sample when the committed design
becomes AIO, while retaining its existing outline. Initial paint style alone
cannot decide callback targets.

## Wire ownership and paint

The optional creationAccent and creationRainbow fields form one pair. A current
producer captures both at the independent creation commit; later fixed palette
changes and mode changes retain those creation facts. Legacy descriptors omit or
null both fields. Explicit absence cannot carry a creation color or mode. Syntax,
ownership and immutable creation checks apply before the footer can draw.

Fixed creation uses the captured RGB. Rainbow creation uses one native sample on
its first admitted observation, keyed by the captured creation mode rather than
the current mode. This is a native phase adapter: it does not reconstruct the
source random phase, its 10 ms coroutine order or a sample before first receipt.
The descriptor carries no new player identity or gameplay authority.

Outline and background histories survive graphics resource resets and temporary
focus withdrawal. A newer creation replaces both; changing immutable color or
mode on the same creation is rejected. Rainbow changes only the current design's
targeted existing channel, preserving the untargeted color and background alpha.
The first admitted observation establishes the current fixed-accent baseline; it
does not infer earlier callbacks from differences against creationAccent. Later
observed accent changes use an immediate endpoint proxy on the current target.
Missing intermediate fixed callbacks and their target designs are not recovered.

## Later callback integration

The [owned instruction theme candidate](instruction-theme-reference-audit.md)
adds invocation receipts and a native linear adapter. It supersedes this
checkpoint's scalar-accent proxy for current producer states, while retaining
legacy omission behavior. Missing prefix and Rainbow seed recovery, and the
unproved source engine curve, remain explicit fidelity limits. The following
paragraphs describe what was unfinished at this earlier checkpoint.

## Remaining requirements at this checkpoint

Accepted fixed-callback invocation identity, exact 500 ms color fades and source
interruption behavior still require implementation and evidence. Scalar accent
changes alone do not prove callback identity or missing intermediate calls.
Broader widget histories, remaining gameplay callbacks, private ammo, normal
Steam co-op, all-map/super-Easter-egg behavior and injected appearance/input
acceptance remain unfinished. This candidate is source and offline fixture work,
not an installed or game-ready release.
