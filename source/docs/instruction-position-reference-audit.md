# Apparition instruction position editor

The official main commit was checked with `curl` on 2026-10-08 and remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. This wave completes Reposition and
Reset Position on the existing six-row Menu Instructions page. The preceding
[instruction controls audit](instruction-controls-reference-audit.md) records
its earlier four-control checkpoint.

## State, controls and timing

Reposition requires visible instructions and all four exact saved action-slot
groups. Slot 1 moves Up, slot 2 Down, slot 3 Left and slot 4 Right. The import
retains these exact groups separately from combined menu scrolling, so ADS and
Attack cannot substitute for missing vertical action slots. Multiple saved keys
per group remain supported. Unknown, overlapping, oversized or malformed
bindings fail closed before startup. Legacy packets retain their prior controls
and leave this editor unavailable.

The source moves eight units per command, checks vertical movement before
horizontal movement, and chooses Down or Right when both directions are held.
Melee exits only without movement. A shared nominal 25 ms repeat clock reproduces
that branch order without catchup bursts. Five hints describe the actual saved
keys. Native 20 ms polling, IPC acknowledgement and scheduling affect real cadence.
Fresh release after entry, focus loss or context replacement is a deliberate
safety difference from source held-button loops. Inputs still reach gameplay.

The editor retains the Menu Instructions title, hides rows/scroller/counter,
and uses source body height 20 and banner height 55. Entry holds controls for
100 ms. Melee begins a further 100 ms exit phase, then the existing 50 ms parent
rebuild and 100 ms pause return to the retained page and cursor. Phase publication
uses the committed owner snapshot; focus-hidden frames cannot re-arm a visit.
All editor commands require its current navigation revision and focus. Normal
menu inputs cannot select gameplay features from this panel.

The typed saved anchor is an optional pair of signed 32-bit integers. The
ordinary default is (255, 472), and the alternate default is (320, 472).
Reset clears the override even when instructions are hidden. Either Alternate
Style change clears the override, matching the source reset handler. Arithmetic
uses a wider intermediate and refuses overflow; it never invents a viewport
clamp. Omitted/null coordinates keep the style default; partial objects, extra
fields and nonintegral or out-of-range values are rejected.

Adaptive placement remains enabled in appearance. It still applies during
entry, is bypassed while editing and exiting, and resumes after return. This
matches the reference's reposition flag lifetime. Toggle/shutdown abort local
editing coherently; a local close does not stop gameplay features. Position
persists only for the controller lifetime, without XUID-keyed dvar persistence.

## Drawing and scope

Saved coordinates may be negative or offscreen. The renderer clips emitted
pixels to the viewport and preserves the logical anchor. Instruction text and
outline jointly determine any collision with the separate status panel. The
status region is bounded and hidden instructions cannot shrink it. Extreme
signed coordinates must produce finite geometry without unbounded draw buffers.

The existing native font and 640-by-480 coordinate mapping remain IW choices.
The engine coordinate origin, exact font metrics, GPU transitions and retail
acceptance are not established by CPU geometry or preview tests. Alternate,
background and adaptive interactions retain the previous strict protocol rules.
No feature writer, Steam identity binding, renderer hook or game operation is
added by the position editor.

## Regression evidence

The new binding, schema, input, controller, publication and drawing regressions
failed against their earlier implementations before the corresponding fixes.
Checks include signed overflow, fresh focus/epoch guards, hidden editor entry,
missing exact keys, down/right priority, transition deadlines, no replay,
reset/default behavior and actual producer-to-parser-to-panel snapshots.
The default main/status CPU output is unchanged. The completed checkpoint passes **624 distinct checks**: controller 174,
features 103, session/history 24, Windows native runtime 71, CPU preview reader 4,
packaging 73, scroll 9, choices 12, Quick Exit 15, width input 15, unlock input 7,
position input 11, saved hints 9, animation 8, geometry 33 and CPU panel 56.
The portable schema run passes 52 overlapping checks; only its four separate
preview-reader regressions are counted additionally. Twenty-six actual producer
snapshots join the parser and renderer without being counted as separate tests.
Full fresh MinGW cross-build and Windows backend publication pass. The Windows
runtime fixture owns a hidden test window and private files; it opens no game
process or graphics device. Build evidence is preserved with the candidate.

These are isolated source and fixture checks. Private ammo, other menu families,
all-map and super-EE behavior, and the retail match-exit crash remain separate
unfinished work; displayed rows do not establish full Apparition parity.

## Reference anchors

- [Reposition and reset handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
- [Soft-lock and unlock geometry/timing](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc)
- [Adaptive position flag lifetime](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc)
