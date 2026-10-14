# Stealth UI and source preference inventory

This wave implements the local Stealth layout rules and aligns the Preferences
page with the twelve declarations in the pinned Apparition customization file.
It adds no game feature writer, AI ignore state or Quick Menu implementation.

## Reference behavior

Stealth UI toggles a local flag. Enabling it while boolean display is Boxes forces
Text; Text Color remains unchanged. An existing Option Counter is cleared.
Turning Stealth off restores neither Boxes nor the counter. The source retains
staged slider indices independently of this forced display change; attempting
to apply Boxes while Stealth is on is rejected. The Option Counter is also
unavailable while Stealth is on, independently of Design restrictions.

The source returns a maximum of five visible options before ordinary instruction,
display or counter adjustments. Absolute selection and the centered scrolling
window remain intact. Body dimensions follow the selected Design and shown-row
count; Stealth adds no separate palette, opacity, width or instruction preference.
For five ordinary visible rows, opening fades have a maximum nominal 400 ms.

The Preferences page follows this declaration order:

1. Design
2. Bool Display
3. Bool Box Location
4. Scroll Animation Time (ms)
5. Quick Exit
6. Disable Quick Menu
7. Spotlight Cursor
8. Colored Cursor
9. Large Cursor
10. Option Counter
11. Stealth UI
12. No Target While In Menu

Disable Quick Menu remains unavailable because the Quick Menu itself is not
ported. No Target While In Menu remains unavailable because no validated IW AI
operation exists. Their rows have no gameplay action. The duplicate Menu
Instructions shortcut is removed from Preferences; all six real instruction
controls remain on their dedicated submenu.

## Presentation and timing contract

With no counter present, the ordinary boolean path retains the previous valid
Stealth/display/counter presentation until nominal 180 ms. One actual redraw
updates its rows and capacity, then the native monitor remains paused until
nominal 430 ms. Keeping coupled old fields together prevents an invalid transient
packet such as Stealth plus Boxes. Changing capacity preserves absolute cursor
identity and cancels an older opening animation at the actual redraw.

With a counter present, the source clears it and closes/reopens its HUD. The
initial 50 ms rebuild is explicit. The monitor examines menuStructure after a
threaded handler invocation; whether its later 180 ms boolean wait is skipped
remains unproved. The native implementation uses a distinct preference-rebuild
transaction with 50 ms HUD absence and a total 250 ms input pause, following its
synchronous local handler model. It does not reuse changed-Design footer state
or the normal opening's 550 ms remainder. This 250 ms schedule is a native
adaptation, not evidence for the original GSC scheduler. Exact 250 versus 430 ms
source ordering and possible later redraw/fade interruption remain open.

The same distinct transaction replaces the prior immediate local Option Counter
redraw. Its actual redraw uses new capacity and an independent opening event.
Counter/Stealth actions remain separate from game session operations.

## Regressions found during implementation

Normal opening unlocks input before every row fade has finished. Selecting a
refreshable preference in that interval previously produced a packet rejected
by the state factory. The corrected regression reproduces that state through
real controller inputs. Pending refresh now retains the existing opening fade;
the actual redraw cancels it. Opening plus refresh cooldown remains invalid.
Hidden preference rebuild cannot contain visible opening rows, while its visible
cooldown can own the newly activated opening. An earlier test accidentally
settled the entire fade; its failure is preserved but excluded as defect proof.

Long remapped instruction text exposed a separate bundled-font assertion. A
positive fractional font size can round to zero when ImGui creates a baked font.
The fit now keeps measurement and drawing requests at least one pixel, with the
existing viewport clipping. Small-viewport regressions use the production panel
and bundled font rather than a stand-in measurement function. This bounds font
requests; it does not make every overflowing hint readable at tiny resolutions.

The managed suite independently passes 323 registered checks, including nineteen
new cases. All 158 actual controller packets independently exported by root match
the agent export byte for byte. The session suite passes its existing 24 checks,
and the Windows .NET 8 backend publishes without warnings or errors.

Root also confirms the 241-step full MinGW build, 139 Windows runtime checks,
148 portable schema checks, 64 geometry checks and 136 production-panel CPU
checks. Portable and Windows shared cases are counted once; the portable-only
preview reader contributes 25 cases. Existing input, control-hint and pure
presentation suites pass. The complete logical inventory totals 1,120 distinct
registered cases, including the retained 31 offline-log cases. Actual profile
comparisons, inner assertions and repeated runs do not add registrations.

Four owned D3D fixture modes pass with 42/60/47/69 assertion executions. These
overlap and are excluded from the logical total. No retail game, Steam API
initialization or game feature write occurs. This wave preserves the preceding
active-swapchain investigation's two intermittent postpatch failures and unknown
cause; successful current fixture runs do not erase that evidence or identify
the retail crash.

The four preview-order integration cases pass on the final 158 packets. All 188
PPM images match byte for byte across opposite creation orders and a relocated
copy; all are converted to PNG without changing pixels. The default main,
reference main and reference status PNGs match the preceding sealed checkpoint.
Root visually reviewed Stealth ON, redraw cooldown, rejected Boxes and Counter
ON. Long unavailable-option labels still crowd their values in this fixed-width
layout; reserving value space is a separate presentation follow-up.

An independent previous-file replay restores only the preceding production
InstructionPanel implementation beneath the new frozen CPU suite. It reproduces
the bundled font-size assertion. This is labeled a previous-file replay, not a
chronological pre-fix run. Genuine agent RED logs remain alongside it.

All 130 prior packets gain the explicit default-false Stealth field. Source
inventory and dedicated Instructions navigation explain row counts and input
epochs; selected logical actions and all previous committed appearance fields
remain unchanged. Three menu revisions increase by eight from the additional
Instructions context changes and row replacements. Fourteen opening revisions
increase by one from Counter's new preparatory fade, with elapsed values
unchanged. The other 28 packets cover the new Stealth/Counter branches and
retained opening. These packets are intentionally not described as unchanged.

## Fidelity and privacy limits

Apparition also uses BO3 client-HUD archival flags. The injected renderer draws
only in the local process and has no validated remote HUD route. This wave does
not promise hidden screenshots, recordings, streams or a port of BO3 archival
behavior. The supported layout and unsupported gameplay rows are described
explicitly. Source timing, all-map retail acceptance and full Apparition parity
remain incomplete. The installed launcher remains unchanged and disabled.

[Customization declarations and handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
and [monitor, row layout and scrolling](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc)
are the primary reference. A fresh official customization download matched the
local pin during this wave.
