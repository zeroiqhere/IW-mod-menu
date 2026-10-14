# Apparition instruction controls

The official repository's current main commit was fetched on 2026-10-08 with
`curl`: `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Fresh `main.gsc` and
`Menu/menu_customization.gsc` files match the pinned reference byte for byte.

## Menu and state contract

The source Menu Instructions page declares six rows: Disable, Alternate Style,
Disable Background, Adaptive Position, Reposition and Reset Position. This wave
implements the first four as local presentation controls. The position editor
and its reset action remain unavailable until a real position state and editor
are implemented; disabled discovery rows do not establish parity.

Disable is the inverse of the existing InstructionsVisible preference. Both
pages use the same immutable appearance value. Disable Background similarly
displays the inverse of InstructionsBackground. AlternateInstructions defaults
false, InstructionsBackground true, and AdaptiveInstructions false.

The source AlternateInstructions handler clears AdaptiveMenuInstructions before
toggling and resets its anchor. AdaptiveMenuInstructions refuses a change while
AlternateInstructions is enabled. The managed appearance and native protocol
reject simultaneous alternate/adaptive states. Existing enum identifiers remain
unchanged; the page and preference identifiers are appended.

The optional protocol fields are `alternateInstructions`,
`instructionsBackground` and `adaptiveInstructions`. Omitted fields retain their
defaults. Supplied values must be booleans; null, numbers and strings cannot
silently select another presentation mode. Normal feature capabilities remain
independent of all instruction controls.

The settings remain local to the controller lifetime, including page return and
menu close/reopen. This does not add the reference's XUID-keyed dvar persistence.

## Rendering requirements

The source GetInstructionString joins alternate instruction lines with the exact
separator `  |  `. Ordinary instructions remain multiple lines. Only bounded
saved-control text or context help can enter either representation.

The default anchor remains x255/y472. Alternate resets it to x320/y472. Source
alternate rectangles use a center anchor; its text is centered with a background
and left-aligned when the background is disabled. Disabling the background
removes both the background and outline, while retaining instruction text.
Hidden instructions remove the whole instruction panel.

For the Apparition design, adaptive positioning uses the menu body's x+1 and
bottom+18. Its width is at least menu width−2, or the measured instruction width
when that is larger. The design-specific formulas for Classic, Native and AIO
are outside this wave because those designs are not implemented yet.

Reference font metrics are unavailable. The existing native font, bounded
viewport fit and status panel remain explicit IW substitutions. CPU rendering
can establish drawing, geometry and protocol behavior; it cannot prove the
engine's coordinate origin, GPU compatibility or retail gameplay acceptance.

Local help is limited to the first six entries and 256 complete UTF-8 bytes per
entry. Empty entries are skipped; embedded CR/LF in alternate help becomes
spaces to keep one physical row. These are defensive IW differences from the
general GSC formatter. The actual saved-control formatter emits no empty or
multiline labels. Contextual editor/unlock help stays separate from inactive
ordinary controls.

## Confirmed checkpoint

This wave has 147 controller, 103 feature and 24 retail session/history checks.
The normal Windows runtime fixture passes 64 checks, including the twenty
actual controller exports. It owns its hidden test window and private files;
it opens no game process or graphics device. The portable schema target passes
43 checks against a header-type shim, overlapping the Windows parser cases
except for two separate CPU-preview-reader regressions. Repeated parser runs
and twenty individual snapshots are not added as extra checks.

The production renderer passes 25 geometry and 46 CPU drawlist checks. Root
reviewed the four instruction PNGs. The alternate no-background preview
originally clipped the final character of `exit`; its new regression failed
before bounded final-size font remeasurement, then passed. Default main/status
CPU output remains byte-identical to the pre-wave output.

The full source checkpoint has 554 distinct checks: controller 147, features
103, retail session/history 24, Windows native runtime 64, preview reader 2,
packaging 70, scroll 9, choice 12, Quick Exit 15, width input 15, unlock input 7,
saved hints 7, animation 8, geometry 25 and CPU panel 46. The unchanged 73
input/hint/animation checks retain preceding results after source comparison.
Full native cross-build and Windows backend publication also pass. This is
source and fixture evidence, not retail acceptance or full Apparition parity.

## Source anchors

- [Menu declarations and handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
- [Instruction formatting and positioning](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc)
- [Instruction height helper](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc)

No gameplay writer, Steam identity binding, renderer hook or input action is
added by these controls. Private ammo, remaining menu families, all-map support
and the unresolved match-exit crash still require separate work.
