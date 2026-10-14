# Local option caller waits and private ammo intent

The reference main commit was fetched again with curl on October 9, 2026 and
remains `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The source audit compares
current and pinned `Menu/base.gsc`, `Menu/menu.gsc`,
`Menu/menu_customization.gsc`, `Menu/utilities.gsc` and `main.gsc` bytes.

## Source behavior and implemented scope

In [Menu/base.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc),
the monitor threads an option handler, gives BOOL options a 180 ms redraw wait,
then performs its 200 ms caller wait and 50 ms outer wait. Slider type is captured
before the handler; the slider branch does not perform the BOOL reread. A handler
returning without a change still consumes the caller wait.

The source [theme declarations](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
use `addOptBool` for all 36 fixed colors and Smooth Rainbow. The controller now
commits the actual color or Rainbow state immediately, retains the last drawn
37 checked indicators until the 180 ms redraw, and resumes input at 430 ms.
Selecting the same color or active Rainbow preserves that entire wait. Current
row values and the confirmed accent remain separate from the retained checked
display; neither action admission nor feature ownership consumes display paint.

Bool Display, Bool Box Location and Scroll Animation Time use a plain 250 ms
caller wait. Changed display choices retain their existing immediate redraw;
Scroll Animation Time keeps its existing redraw behavior. Unchanged candidates
and a rejected Boxes choice under Stealth still consume the wait. Reset
Instructions Position also waits 250 ms, including an already-default position.
Specialized Design, Counter, Stealth, editor and opening-control owners retain
their own timelines and do not receive a second ordinary timer.

Clocks and future callback, menu and paint epochs are admitted before local
mutation. A source-admitted visit owns its timer by actor, family, page and
navigation identity. Replacement actors cannot borrow it; focus hides public
metadata without replaying rejected input. Legacy and disconnected preview
producers retain their preceding metadata contract. The native footer calls
both ordinary timer kinds “Updating option”.

## Evidence

The managed suite contains 668 controller/backend and 107 session regressions.
This wave adds six palette-cache, eight controller, two export and eleven inert
ammo-plan cases. The controller tests exercise exact clock boundaries, same
candidate selection, rejection, focus/actor retirement and failed clock
preparation. Twelve unmodified connected controller packets cover palette before,
0/179/180/430 ms, Rainbow 0/180 ms, changed and rejected sliders, and Reset
0/250 ms. They contain no gameplay dispatch or private actor identifier.
Native parsing, input policy and CPU pixels must consume these exact exported
bytes; profile inventories are not counted as independent regression cases.

The Windows .NET 8 proof runs the exact managed project inventories in the
isolated AppID 0 prefix: 296 backend compile inputs and three content inputs,
148 session compile inputs, 375 distinct source/content/project files in total.
Both suites pass on .NET 8.0.31, Windows 10.0.19045, X64, with byte-identical
inputs before and after. The strict production backend publication also passes.

The full MinGW build completes all 315 steps. Windows native runtime checks pass
191 cases, including the existing 233-profile aggregate; the separate 14 callback,
eight paint and twelve local-option graphs each pass one aggregate case.
Portable schema retains its 207 ordinary cases, and all 15 configured portable
CTest executables pass. CPU rendering passes 158 cases and renders all twelve
actual local profiles; geometry passes 64 cases. There are exactly three new
native/CPU registrations, not twelve new cases. The preceding accepted
233/14/eight exported graphs remain byte-identical.

Proton console redirection initially produced no report for separate native
fixtures. Private test-only wrappers compile each unchanged entry function under
a different name, redirect C++ streams to an explicit owned file, and reuse the
full build's exact test/parser/router objects. All three report files contain
their pass result. These wrappers add no product logic or test cases.

The local graph has its own `iw_local_option_fixture_tests` executable and never
extends the legacy 233-profile inventory. Export it with
`dotnet run --project tests/backend/Backend.Tests.csproj -- --export-local-option-fixtures <new-absolute-directory>`.
Pass that directory to the native executable. For the separate CPU palette
case, configure `tests/native-layout` with
`-DIW_LOCAL_OPTION_FIXTURE_DIRECTORY=<same-directory>` and run
`ApparitionPanelTests`. The pixel oracle constructs the expected checked row
independently, while retaining the packet's actual accent and Rainbow state.

## Private ammo foundation

An immutable, inert refill plan preserves the source choice order Continuous,
Reload and Disable. Continuous requests reserve refill before verified positive
clip refills; Reload requests reserve only; Disable and a missing weapon request
no operations. Supplied facts distinguish source-default, left and right hands.
Duplicate hands, mixed default/explicit hands, negative capacities, invalid
choices and incomplete enumeration reject without a returned partial plan.
The plan owns its storage and never inspects unused clip facts.

IW CP script evidence uses per-player script counters/HUD state and explicit
left/right clip operations. It does not prove a native per-player ammo writer.
The plan supplies no offsets, polling loop, transport or production capability.
Private Unlimited Ammo remains unavailable until current weapon, reserve pool,
hand capacity and mutation ownership are verified and integrated.

## Remaining fidelity and acceptance

Submenu handlers can replace menu structure on a separate thread before the
monitor rereads BOOL type; a fixed guessed submenu timer is not implemented.
The later [instruction callback wave](instruction-caller-waits-reference-audit.md)
integrates the four Menu Instructions BOOL callers.
Source theme fades use historical color transitions; the current native accent
still represents the committed target, not complete 0.5/1 second fade history.
Private ammo transport, other gameplay callbacks, Quick opening, injected
physical input/visual acceptance, co-op, all maps and the super Easter egg remain
unfinished or unproven. The full goal is active, not complete.

This candidate is offline review work. No game, injection, process-memory, GPU
or Steam API operation belongs to this wave. The installed disabled payload and
the separate playable backup remain unchanged.
