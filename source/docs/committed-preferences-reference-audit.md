# Committed menu preferences

The candidate restores the 23 supported `SaveMenuTheme` settings from a verified
local account's complete immutable record before its first menu frame. Accepted
settings survive controller and connection recreation in the same backend
process. Staged editor choices, open visits, player selections, input capabilities,
and HUD or action revisions are not stored. This is source work; it has not been
installed or accepted in a retail match.

## Reference and setting map

The official Apparition `main` commit was fetched again during this wave and is
still `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
[SaveMenuTheme and LoadMenuVars](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L700-L806)
store 25 XUID-keyed engine values. The saved marker maps to repository entry
presence. `MenuNoTarget` remains unsupported and cannot enable a gameplay write.
The other 23 map as follows:

| Source values | Applied state |
| --- | --- |
| menuX, menuY | MenuPosition X and Y |
| instructionsX, instructionsY | InstructionPosition X and Y |
| MenuWidth | MenuWidth |
| DisableMenuInstructions | inverse InstructionsVisible |
| AlternateInstructions | AlternateInstructions |
| DisableInstructionsBackground | inverse InstructionsBackground |
| AdaptiveMenuInstructions | AdaptiveInstructions |
| MainTheme | fixed palette RGB or Rainbow mode |
| MenuDesign | Design |
| OpenControls, QuickControls | ordered Main and Quick semantic action groups |
| QuickExit | QuickExit |
| BoolDisplay, BoolLocation | BooleanDisplay and BoolBoxLocation |
| ScrollAnimationTime | ScrollAnimationMilliseconds |
| DisableQM | QuickDisabled |
| SpotlightCursor, ColoredCursor, LargeCursor | matching appearance flags |
| OptionCounter, StealthUI | matching appearance flags |

Rainbow stores its mode instead of a previous fixed color. Reload uses the source
default accent as the adapter fallback. The action editor's Target and Slot
selectors are fresh defaults; only accepted action groups are saved. All five
design editors start at the loaded design rather than an unrelated default.

## Ownership and loading

The retail session publishes a private preference scope from its confirmed
original account and connection receipt. The cached getter performs no native
query and does not need a Godmode port. A known life or menu-permission change
retains the account scope; a missing or replaced receipt withdraws it. The account
identifier and owner token are not public state-document fields.

The memory repository keys complete validated values by account. A new owner
must present its own current scope before reading or saving. Unknown account
state uses unowned defaults. A scope change retires delayed saves and editor
candidates before loading the next record. Read failure reports defaults without
overwriting the older stored record. Save failure reports that current settings
remain active.

Saved action IDs never establish physical input capability. Reload rebuilds Main
and Quick policies from the current verified actions, with fresh revisions. An
unavailable saved chord uses a verified default where one exists and explains the
fallback. Unrelated appearance saves retain the configured unavailable groups.
Revision exhaustion cannot recycle a policy on a later account.

## Save boundaries

Fixed palette choices, accepted opening bindings, ordinary accepted preferences,
instruction reset, and accepted alternate instruction changes save immediately.
Selecting the same fixed color or scroll duration still saves, as in the source.
Unchanged Bool Display, Bool Location, Design, or already-active Rainbow, and
rejected adaptive changes return without saving.

Design, Option Counter, and counter-dependent Stealth changes save after the
actual redraw frame succeeds. Reading a cheap publication phase cannot commit a
record. A failed frame leaves its pending value for the next successful frame.
Normal Width and Menu Position completion saves after the 150 ms SoftUnlock.
Instruction Position completion includes its 100 ms exit wait before that
150 ms SoftUnlock. Live movement samples remain unsaved.

A canceled caller discards its pending save and readiness, including permission
loss under an unchanged authenticated account. This prevents a cleared timer from
being interpreted as completed time. Restored permission cannot revive the old
pending write. Terminal closure disables saving before editor leave hooks;
repeated terminal frames cannot save a live unfinished editor value. A supported
non-disconnect width or menu-position close still saves its last value immediately.

## Evidence and limits

Separate domain, retail scope, controller reload, source boundary, cancellation,
and terminal closure regressions use actual immutable records and controller
commands. Cancellation tests open with the real scoped `OpenMain` route. They
retain the same positive account scope while withdrawing menu permission, so
scope invalidation cannot hide the original bug. Both cancellation paths and
both terminal editor paths failed before their fixes. Earlier fixture setup
failures are recorded separately from those genuine behavior failures.

The repository ends with the backend process. It does not yet reproduce source
engine-variable availability across a backend restart within the same running
game. Source dvars are untyped and loaded unconditionally after the marker;
the adapter validates one complete typed record instead of guessing malformed
engine conversions. No disk persistence, Steam configuration writes, or native
game dvar writes are added.

Permission-loss cancellation is a conservative adapter rule, not proof of an
identical GSC event. Width and Menu Position have source non-disconnect loop
exits. Instruction Position uses an infinite source loop until melee; its native
Toggle abort is still a documented difference. Physical input, engine drawing,
full gameplay callbacks, private weapon ammo, complete retail lobby membership,
normal Steam co-op, and all-map quest and super Easter egg acceptance remain
unfinished. Automated CPU and managed regressions do not prove those requirements.
