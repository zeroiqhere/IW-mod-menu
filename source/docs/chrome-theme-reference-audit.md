# Persistent menu chrome and fixed theme callbacks

This candidate follows the existing source objects behind the scroller, banner,
title, separator and bottom line. Each object's RGB history belongs to its actual
menu creation. A changed setting or a later packet does not create another object.
Full Apparition parity remains open.

## Source facts

The pinned [design HUD definitions](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/designHud.gsc)
define five different creation styles. The [theme callbacks and editor lifecycle](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
define which existing objects receive a fixed 500 ms color request or a direct
Rainbow assignment. Fresh official source matches that pinned commit.

Role bits are scroller `1`, banner `2`, title `4`, separator `8`, bottom line `16`.

| Design | Creation membership | Fixed callback targets | Additional Rainbow targets |
| --- | ---: | ---: | --- |
| Apparition | 31 | 28 | Scroller only when the source keyboard string exists |
| Classic | 7 | 3 | None |
| Native | 15 | 3 | None |
| AIO | 29 | 25 | None |
| Basic | 7 | 4 | Scroller |

Apparition creates its scroller and banner at RGB `(55,55,55)`. Classic and Native
create those objects from the theme. AIO creates its scroller, separator and
bottom line from the theme. Basic creates a black banner and scroller. Apparition
and Basic titles use the theme; the other titles are white. Native's separator is
black. Bodies, counters and secondary titles have separate source assignments.
Native banner, scroller and title alpha remain `0.9`, `0.7` and `0.7`; a color
request does not change those alphas.

Every admitted fixed selection is a callback, including selecting the same color
while Rainbow is active. The callback stops Rainbow. The 430 ms palette input
wait is shorter than the requested 500 ms fade, so accepted callbacks may interrupt
one another. Current controller locks prevent a fixed selection during a design
or editor rebuild before its next actual chrome creation.

## Creation and editor lifetime

Row redraws, ordinary page changes and Back retain chrome. Credits and the two
reposition editors hide the scroller without destroying it. Its RGB continues to
receive admitted direct Rainbow samples while hidden. Width destroys the title;
its role bit remains absent for the rest of that creation. An earlier fixed event
still keeps its original target mask even when Width later removes the title.

Actual close and rebuild destroy menu chrome. Main open, design rebuild, applicable
preference rebuild and SoftUnlock create their replacement at the source 50 ms
boundary. A late observation commits one creation. Quick opening has its separate
immediate creation path. SoftUnlock clock recovery after creation can report a
rebuild phase while retaining that already-created instance; a phase string alone
cannot prove destruction. Focus withdrawal and graphics resource reset retain
private histories.

## Publication and receipt contract

The optional `chromeTheme` descriptor has nine fields: `exists`, `creationRevision`,
`admissionRevision`, `creationDesign`, `creationAccent`, `creationRainbow`,
`currentRoleMask`, `prefixComplete` and `events`. Legacy omission makes no owned
history claim. Explicit JSON null is rejected. Focus projection omits the descriptor.
Temporary Unknown life for the same admitted owner with `HasMenu` also withdraws
publication without destroying its private creation. Actual close, actor replacement,
lost ownership or rebuild precreation absence still publish explicit absence.

A present descriptor belongs to an open menu with the matching chrome revision,
current actor admission, known Alive or Dead life, `HasMenu` and rendered design.
Control locks do not destroy that creation. Creation style and mode are immutable;
current role membership can only lose objects under the same creation revision.
Native drawing suppresses a withdrawn Unknown-life frame and resumes the retained
history only with matching parent identity, style and current known admission.
An unexplained omission from an owned open menu cannot authorize legacy fallback.

Explicit absence has null creation/admission/style/mode fields, zero membership,
a complete prefix and no events. It retires the source creation. It is allowed
independently of the menu phase because revocation and precreation absence are
separate from visibility. The actual controller determines when objects exist.

At most eight events retain positive invocation revisions, the exact creation's
fixed target mask, RGB, elapsed milliseconds, predecessor gap and prior Rainbow
stop fact. Adjacent stored revisions are consecutive even with an incomplete
prefix. The first stored revision may be any positive global invocation revision.
Unclamped unsigned elapsed time preserves ordering after the fade endpoint.
Predecessor gaps preserve intervals below 500 ms and cap quiet intervals at 500 ms.
Pruning only removes a prefix; it cannot conceal an internal hole.

Clock and counter checks precede accepted setting mutations. Snapshot construction
stages the new chrome and its journal before publishing their committed state.
Replacements retire their histories rather than applying them to another actor.

## Native adapter and remaining fidelity

Native drawing must share the owned per-role colors between rectangle and title
rendering, preserving source alpha and object membership. Direct Rainbow updates
only existing source targets. Legacy Basic scroller compatibility must not override
an owned chrome history. A removed title cannot return until a new creation.

The fixed 500 ms interpolation uses a documented linear adapter with floating RGB
until draw conversion. Source specifies duration and targets; it does not prove
the engine's easing, concurrent fade interruption behavior or global Rainbow phase.
A sampled native Rainbow seed is therefore adapter evidence, not engine evidence.

A complete fixed receipt history can reconstruct colors from creation seeds.
Unknown pruned prefixes and unobserved Rainbow starts require independent role
proof. An interrupted historical endpoint is insufficient; a latest quiet callback
must reach its endpoint before recovering a lost fixed-role color. Non-targeted
Basic scroller history can remain unknown after a missed Rainbow interval. Native
extrapolation is bounded and cannot rewind displayed time. Mutated or regressed
ownership and receipt history cannot silently fall back to scalar accent colors.

This wave requires failing regressions before implementation, actual controller
exports, strict runtime and preview parsing, pure timeline checks, CPU rendering
of chrome and titles, independent rational RGB expectations, existing regressions
and Windows cross-builds from the exact reviewed source. Offline CPU images are
not game screenshots.

Row color histories, keyboard-specific scroller behavior, other source widgets,
full gameplay families, personal ammo, complete-cohort All Players actions,
physical injected input/rendering, normal Steam co-op and every map's quests and
super Easter egg remain requirements of the full port. The saved backup remains
available; this candidate does not establish retail runtime acceptance.
