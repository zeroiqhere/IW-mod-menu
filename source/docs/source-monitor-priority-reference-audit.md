# Source menu monitor priority

The native router now gives eligible scrolling priority over Select, slider
changes, and Back. This includes an established held scroll whose interval is
due. Select precedes slider changes; sliders precede knife and dead Quick Jump.
The change removes the earlier fresh-Select and knife priority deviations.
Main and Quick use the same ordinary monitor ordering.

The official Apparition commit remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. A fresh copy of
[Menu/base.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L61-L161)
confirms one exclusive chain: scrolling, Use, sliders, then the shared
`Melee || dead Jump` branch. Opposing scroll directions are neutral, so Use or
another eligible later branch can still run. This is a branch-order correction,
not proof that native polling and GSC held-button scheduling are identical.

## Knife loop ownership

A newly pressed knife no longer starts Quick Exit before a preceding eligible
branch. Its discarded edge remains consumed until release; it cannot become a
delayed Back or invisible hold after scrolling, Use, or a slider wins. Existing
source scroll waits continue after release and capture the configured duration.
No catch-up burst or queued lower-priority feature action is introduced.

Once an enabled child Quick Exit hold is admitted, its own source loop owns input
until release or the nominal 90 ms threshold. Later ordinary keys cannot escape
that loop. Context, mode, permission, focus, pending-command, and clock guards
still retire invalid input. Root and disabled Quick Exit keep immediate Back.

The dead Quick Jump and knife condition is one source branch. Jump alone remains
an immediate scoped Back. In an enabled child, Jump and a held knife enter the
knife hold together: short release returns one page; a confirmed 90 ms hold
closes the menu once. A fresh qualified Jump can admit this combined branch even
when an earlier knife edge was discarded under Use. Held Jump cannot replay after
completion, and the branch remains behind scrolling, Select, and sliders.

## Regression and review

The new actual-router runner reaches fifteen cases. The first ten compiled
against the prior source: eight priority assertions failed, while active-hold and
blocked-owner controls passed. Review then found the combined dead Jump/knife
case; three reached regressions failed before its fix. All fifteen now pass.
The earlier knife-first and Select-before-due-scroll test expectations were
updated to the freshly checked source order. The choice suite retains separate
Use interruption and slider-before-knife checks; its count grows from twelve to
thirteen. The Quick Exit fixture admits the knife loop before testing input
exclusion within that loop. The broader schema run reached two more historical
knife-first expectations in normal-opening and ordinary-input fixtures. Both
now use source order and retain knife-only Back controls; no existing named
aggregate was removed.

Portable and Windows CMake targets link the same production router. Managed
controller, session, state schema, and gameplay writer code are unchanged.
This wave adds no gameplay callback, key rewrite, DLL reload, or native offset.
Source code and compiled review artifacts remain separate from installed
products and the playable backup.

## Remaining fidelity and lifecycle gaps

Fresh-edge selection and Back, initial/focus/context reseeding, acknowledgment
gating, modal ownership, and a discarded edge's release requirement remain native
adapters. The source uses held buttons and its own row-type checks and waits.
The router still does not model every source monitor wait or the script's action
slot writes. The later [monitor waits checkpoint](source-monitor-waits-reference-audit.md)
adds the ordinary choice and final Back pauses; generic submenu Use and remaining
held-button scheduling still need separate treatment. Keyboard tests and a successful Windows build do not prove physical
game input, GPU appearance, Steam co-op, map quests, or the super Easter egg.

The separate game-lifetime audit confirms three independent reattachment
refusals: the payload startup latch, the loader's already-loaded-module guard,
and the resident hook owner. A fresh backend also resets command, heartbeat, and
HUD counters while native logical color histories survive graphics reset.
Restart persistence therefore needs a resident memory-only settings broker and
an explicit clean-owner handoff to the existing hooks. Stale heartbeat or backend
death cannot prove feature restoration. Worker-side acknowledged preference I/O,
fresh account/input proof, and present-thread logical owner retirement remain
required before that gap can close. No reattachment or broker implementation is
claimed by this input change.
