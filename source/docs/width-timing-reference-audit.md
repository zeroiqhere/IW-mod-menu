# Width timing: pinned reference contract

Audit date: 2026-10-08. This timing wave is integrated into main with final root-confirmed source proof.
No GPU/game acceptance, retail crash fix or full Apparition port is claimed here. The prior 392-check Width
Editor candidate is preserved. This audit changes documentation only.

The official Apparition main HEAD fetched with curl still resolves to
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Fresh raw downloads of customization,
base and utilities match the pinned local files byte-for-byte. References below
link to that commit rather than an unpinned branch.

## Width scaling

[MenuWidthEditor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L356-L448)
threads `hudScaleOverTime(0.05, targetWidth, currentHeight)` on each existing
background, banner, separator, bottom line and outer background. The requested
width retains each element's existing width offset. It changes MenuWidth by the
bounded increment immediately, waits 50 ms in the branch and 25 ms at the loop
bottom. Thus the nominal resize cycle is 75 ms; changing the increment has a
100 ms branch wait plus 25 ms loop wait. These waits are independent of any native
IPC acknowledgement or renderer scheduling.

[The scaling helper](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L459-L472)
calls the engine `ScaleOverTime(time, width, height)`, immediately assigns its
script width/height fields to the requested target, then waits for time. The
committed script value is therefore distinct from the currently displayed
intermediate width. No easing parameter, interpolation equation, frame cadence
or rounding rule appears in this helper. Linear native interpolation is a
reasonable declared approximation, not proof of the BO3 engine's exact easing.
No primary engine implementation or authoritative easing definition was found
in this audit; another game's HUD implementation cannot settle that question.

During editor mode the title, menu name, options, scroller and counter are already
removed/hidden. Body text remains anchored to the left. Source scaling changes
listed element widths, not editor text size, camera, game layout or another
player's HUD. Restored parent geometry is constructed from the committed width.

## SoftUnlock visibility and input sequence

[SoftUnlockMenu](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L693-L709)
returns without recreating a menu if the menu has already closed. Otherwise its
normal menu sequence is:

| Nominal interval | Source operation | Visible panel | Normal menu controls |
| --- | --- | --- | --- |
| Start | closeMenu1 notifies menuClosed and destroys menuUI; DisableMenuControls is reset then immediately set true | Editor/panel destroyed | Disabled |
| 0..50 ms | openMenu1(false) sets isInMenu true, then waits 50 ms before creating HUD | Parent panel absent | Disabled |
| At 50 ms | createMenuHud and drawText(false) recreate the retained page/cursor | Parent page visible without option fade-in | Disabled |
| 50..150 ms | SoftUnlock waits another 100 ms after openMenu1 returns | Parent page visible | Disabled |
| At 150 ms | DisableMenuControls becomes undefined | Parent page visible | Enabled on a subsequent monitor poll |

The first hidden interval is material: the parent HUD does **not** appear before
openMenu1's 50 ms wait. [openMenu1 and closeMenu1](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L200-L259)
retain currentMenu, parent stack and menuCursor; drawText clamps the cursor if the
recreated page changed size. `false` disables drawText option fade-in rather than
skipping the initial 50 ms wait. The total 150 ms is nominal script waiting time,
not an exact guarantee of the first rendered frame or next accepted input.

The instruction footer belongs to **menuInstructionsUI**, separate from menuUI.
closeMenu1 does not destroy it. [The independent footer loop](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L372-L466)
may therefore keep normal menu instructions visible during both unlock phases,
subject to its preference/lifetime conditions. A native panel-hidden flag must
not imply footer-hidden unless that additional difference is explicitly chosen.
The current editor's context-aware footer remains an intentional improvement
over source inactive Scroll/Select text; restored normal help should remain the
same independent instructions after editor mode ends.

closeMenu1 also contains BO3-specific action-slot restoration and NoTarget state
changes. Those are source side effects, not authorization or evidence for new IW
writers. This timing wave should model local panel/input phases and preserve
existing feature ownership. It must not copy those game-state writes, call
controller Exit, restore game features, unload a DLL or reopen an externally
closed menu as part of finishing the editor.

## Integrated implementation contract

The implemented producer contract uses optional `menuUnlock`: `rebuild` before 50 ms,
`pause` from 50 through 149 ms, and absent/null from 150 ms onward. A TimeProvider
controls the backend phases without sleeping. Every normal menu input remains
blocked through both phases, including Toggle and Close; shutdown stays a separate
out-of-band owner operation. The native locked mode consumes and reseeds held
controls rather than dispatching an action when the phase changes.

The renderer agent confirms isolated CPU behavior that hides only the menu panel in `rebuild`, preserve
independent help/status, and display the retained parent during `pause`. The
chosen native width interpolation is linear over 50 ms, with engine easing
explicitly unproved. The final source results below establish local behavior only; retail/GPU
acceptance and engine easing remain unproved.

## Acceptance boundaries and confirmed subsets

The final root-confirmed current-main source total is 441 distinct checks:
backend 133, features 48, native-runtime 60, packaging 70, scroll 9, choice 12,
Quick Exit 15, width-input 15, unlock-input 7, saved-hints 7, animation 8, geometry 19
and production-panel CPU 38. Root confirms the full native build and actual
Windows .NET8 publication. Native runtime and CPU panel join the same 16 fresh
main-controller exports; identical-source portable runners and unchanged
feature/packaging suites retain confirmed results. CPU clone images were reviewed;
no GPU/game launch or installed deployment occurred. The acceptance scope is:

- Keep committed width separate from render width; animate only the applicable
  panel geometry over 50 ms and explicitly label the chosen easing approximation
- Use one monotonic clock and bounded arithmetic; cover first frame, midpoint,
  exact completion, retargeting, equal values, clock rollback and large timestamps
- Publish explicit hidden/reopened-but-locked phases instead of inferring them
  from title, empty rows or editor absence; preserve the independent footer
- Confirm no parent panel before 50 ms, visible parent but no accepted navigation
  before 150 ms, then normal page/cursor resume on the next legitimate input
- Reject stale process/sequence/navigation identity throughout both phases;
  cancel scheduled resume on close or owner teardown and cancel input on focus/context invalidation
- Consume held exit/navigation keys and pending edges so reenable cannot replay
  a stale action; source held-melee replay is an intentional remaining difference
- Preserve existing feature/session callbacks and restore ownership unchanged;
  local finish should not create a new backend/gameplay action
- Repeat actual producer/parser/CPU joins for both phases and normal resume;
  polling, IPC acknowledgements and scheduling may stretch observed timing

The full port, unavailable gameplay families, stable retail map-exit baseline and
crash cause remain open. Source timing and CPU fixtures cannot establish retail
GPU transition compatibility or literal engine easing parity.


## Publication and protocol limits

`menuUnlock` is optional: absent/null legacy packets mean unlocked; supplied
values must be exactly `rebuild` or `pause` in an open non-root normal-page
context, never alongside editor metadata. Wrong types/strings and invalid contexts
are rejected. Locked mode consumes all ordinary menu inputs, including Toggle
and Close; owner shutdown stays independent. Lock completion reseeds held keys.

Backend elapsed phase thresholds are 0..49 ms rebuild, 50..149 ms pause and
unlocked from 150 ms. A 25 ms owner-loop check publishes phase/focus changes separately
from the monotonic 100 ms generation heartbeat. The native worker's 20 ms poll,
file handoff, command acknowledgement and GPU frame scheduling introduce visible
quantization. Animation starts when a committed target is observed on the render
thread, not at a reconstructed engine timestamp. Nominal local duration is now
modeled; exact BO3 timing/easing and retail observation remain unproved.

The parent panel is hidden during rebuild while independent help and IW status
remain visible under their own preferences. Paused help advertises no active
selection. This is an explicit status/text extension over the source's independent
ordinary footer. The prior contextual editor-footer difference remains deliberate.


The root identified that backwards wall-clock adjustment could stall periodic
publication. The implemented guard now uses TimeProvider monotonic timestamps for
its 100 ms heartbeat and tracks focus/owner-phase changes independently. Its
interval starts only after a successful publication; failed writes do not commit
phase/focus/time state, and rollback remains due until a successful frame commits.
Phase publication first failed with 122 passing/five failing, then passed 127.
Heartbeat cadence then failed with 127 passing/five failing, passed 132, and an
additional overflow-boundary case produced the final confirmed 133/133. Strict
Windows .NET8 compilation completed with zero warnings/errors. Root final main-source runs and native/Windows builds are confirmed.


The renderer agent's final run confirms all sixteen actual export coverage checks,
38 production CPU cases, eight animation cases and 19 geometry cases, with a
25-step Windows renderer/fixture/helper cross-build. Default main pixels match
the preceding candidate byte-for-byte. Rebuild, pause and midpoint PNGs were
reviewed as CPU previews; no retail/GPU launch occurred. Root final main-source and heartbeat-regression confirmation is complete.

The [current official GetTickCount64 documentation](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64),
fetched with curl, describes typical timer resolution of 10..16 ms. The renderer
samples this clock only on its owner thread. That API limit is another source of
quantization; its documented Windows resolution is not a measurement of this
Proton session. Linear duration tests use an explicit owned clock and therefore
cannot establish real timer/frame cadence.


An isolated replay of the preceding panel failed the new unlock assertion with
“Rebuild retained a supposedly destroyed menu HUD”, while its new animation
checks passed. The current panel CTest targets pass. This is a deliberately
labelled previous-panel replay, not a claim that the production implementation
was rerun chronologically before its fix. Root reviewed the midpoint, rebuild
and pause CPU PNGs; no GPU/game launch was involved.
