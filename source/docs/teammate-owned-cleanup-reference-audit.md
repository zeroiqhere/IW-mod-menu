# Retained teammate Godmode cleanup

This wave fixes the production path that asks a strict living-player reader to validate an already owned flag during restoration. Personal Godmode already separates new-enable admission from restoration; selected teammate Godmode must keep the same separation.

## Source behavior and IW ownership

Apparition toggles the selected player's `playerGodmode` state; its damage override consumes that flag. IW uses an existing checked entity flag adapter. This port does not establish equivalent protection against every scripted kill, map transition or damage path.

Selection and every new ON must still admit a currently living, normally playing player. Retained OFF and shutdown may inspect the original owner after a life or movement change, but they cannot select a different slot, client link, server record, GUID, name or session. Observed command-time rollback and unreadable identity remain failures. No additional game offsets or spawn-generation claims are introduced.

The optional owned reader returns an exact original identity, current command time and flags plus separate enable eligibility. Life/session/movement ineligibility cannot by itself invalidate the retained cleanup owner. It cannot grant new ON: both session admission and the final bound write must check eligibility. Every write still changes only the Godmode bit and compares the exact expected flag word.

Legacy transports retain their strict behavior. A replacement identity, disconnected original process, changing map globals or failed ownership read must fail closed. Unconfirmed cleanup retains the original transport and ownership; it cannot silently dispose or reopen into a replacement.

## Required regression evidence

Tests must first expose the nonliving-cleanup failure through existing behavior. The new reader and transport then need all four slots, normal admission, downed/dead health, session/movement changes, late new-ON ineligibility, identity replacement, command-time rollback, map/global change, exact flags and read failure cases. Session tests must prove the actual selected-player view and menu row permit original OFF while rejecting nonliving ON, and prove shutdown restoration, independent targets, failed cleanup retention and legacy compatibility.

No retail process or injected menu is used for this wave. These offline checks cannot prove a spawn token, atomic multi-read snapshot, retail damage equivalence, all-map co-op or super Easter egg behavior. Private ammo, complete-cohort All Players actions, remaining Apparition callbacks and full visual/input/runtime acceptance remain required.
