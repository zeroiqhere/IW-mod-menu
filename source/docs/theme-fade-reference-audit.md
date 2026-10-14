# Theme callback targets and widget lifetimes

The source applies the committed accent immediately. This foundation wave adds the
source callback-command foundation needed for separate widget color histories.
It does not connect a new fade renderer, change the production palette action,
or establish completed theme appearance parity. The saved playable backup and
installed disabled package remain untouched.

## Primary source

Fresh official Apparition main resolves to
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The audit captures the exact main,
menu, base, utilities, design HUD and customization files. Fixed MenuTheme has
no early return for selecting the already committed color. Its assignments and
durations are observable in GSC; the engine's numerical easing and concurrent
FadeOverTime interruption rules are not defined by those assignments.

## Separate targets

| Existing widget | Fixed palette operation | Rainbow sample operation | Lifetime |
| --- | --- | --- | --- |
| ON inner checkbox | Color fade, 500 ms | Direct color | Rows |
| OFF checkbox and checkbox background | No change | No change | Rows |
| Source invalidOption blank decoration | Color fade, 500 ms | Direct color | Rows |
| Text, BOOL text, submenu and slider text | Color fade, 1000 ms | Direct color | Rows |
| Classic/Native/AIO scroller | Color fade, 500 ms | Direct color | Chrome |
| Basic scroller | No change | Direct color | Chrome |
| Apparition scroller | No change | Direct only when source keyboard HUD exists | Chrome |
| Classic/Native banner | Color fade, 500 ms | Direct color | Chrome |
| Apparition/Basic title | Color fade, 500 ms | Direct color | Chrome |
| Existing Apparition/AIO separator and bottom line | Color fade, 500 ms | Direct color | Chrome |
| AIO instruction/player-info background | Color fade, 500 ms | Direct color | Independent footer/info |
| Other instruction/player-info outline | Color fade, 500 ms | Direct color | Independent footer/info |
| AIO outline, footer/info text | No change | No change | Independent footer/info |
| Enabled existing counter/crosshair LUI target | Direct color | Direct color | Independent LUI |

Row text targets green for the cached source menuStructure BOOL ON with Text Color. Otherwise it targets the
selected theme only for the selected row with Colored Cursor; other text targets
white. The callback receives the source GetOption BOOL facts, not a fresh live feature
read or a later managed row value. Copied display strings are not a source of
authority for these facts. Missing widgets produce no command. IW capability strike marks
are not the source invalidOption blank HUD and cannot inherit its role by name.

## Lifetime boundaries

The nominal 180 ms BOOL monitor redraw destroys and recreates the eight row HUD
families, ending their old fades at the actual redraw. Chrome and the independent
instruction objects survive that redraw. Back at 430 ms draws a new set of rows
without recreating chrome or instructions. A new palette callback can therefore
retarget a still-running 500 ms chrome fade; the starting RGB must be the sampled
existing widget color, not the last committed accent.

Closing a menu destroys menuUI chrome and rows. The instruction loop lives
outside menuUI and can continue while the ordinary menu is closed. Its color
history must survive close/reopen until its own destruction or force refresh.
Design, Alternate, Background and instruction enable/disable can recreate it.
Adaptive/position updates do not automatically recreate it.

AIO instruction fill starts black, then receives palette callbacks. Its outline
retains the color from its own creation until recreated. Basic fixed palette
callbacks leave its scroller untouched, including a color left by Rainbow.
Neither case can be reproduced by redrawing every part from MainTheme.

## Foundation contract and proof limits

The immutable command planner consumes supplied existing-widget identities,
lifetimes and raw source option facts. It emits exact target RGB, operation kind
and source duration. It owns its result storage and rejects ambiguous or malformed
input without a partial returned plan. It neither starts a thread nor authorizes
engine access. RainbowSample represents one already-running loop sample; it is
not the selector's start/restart behavior.

No guessed easing, interpolation, source rainbow phase, current RGB, engine
widget identity or new gameplay writer is introduced by this foundation. Source
callback-command regressions prove this planning scope only. That wave kept GUI
and native packets unchanged; it did not implement a visible color fade. The
later [Basic scroller history candidate](basic-scroller-history-reference-audit.md)
connects one source creation lifetime to observed native RGB retention. The
broader command planner still has no production callback or fade binding.

## Required integration

The complete renderer needs accepted invocation identity even for same-color
reselect; exact observed row/chrome/footer creation epochs; independently sampled
RGB histories; actual redraw retirement; preserved footer state while closed;
and source-specific target masks in every supported design. New packet readers,
render history and CPU pixel checks must join an actual controller graph before
this foundation can be called a working fade implementation.

The random source Rainbow phase, numerical engine fade behavior, interaction with
alpha fades, source submenu scheduling and final physical appearance still need
stronger evidence. Private ammo transport, remaining gameplay families, normal
Steam co-op, all IW maps and the super Easter egg remain incomplete. The full
Apparition goal remains active.
