# Basic scroller color history

The Basic design creates its scroller black. An existing scroller receives direct
Rainbow RGB assignments, but a fixed MenuTheme callback excludes Basic. Returning
to a fixed palette must therefore retain the last observed scroller RGB until a
new scroller is created. This candidate connects that history to the production
renderer; the broader per-widget fade foundation remains separate and unwired.

## Source and creation boundaries

The fresh official source still resolves to Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. `Menu/designHud.gsc` line 95
creates the black Basic scroller. `Menu/menu_customization.gsc` lines 108–109
exclude it from fixed callbacks; lines 207–208 assign Rainbow to its existing
object. `Menu/base.gsc` lines 650–689 retain the scroller during SoftLock and
hide its alpha rather than destroying it. Empty rows, editors, clipping and
cursor movement cannot establish destruction.

The optional positive `menuChromeRevision` identifies an actual source chrome
creation within its retained controller. The controller reserves replacement identity before changing local
state and commits it only after successful row publication. Main creates chrome
at its observed draw boundary, including a late first draw; Quick creates it
immediately. Changed Design, qualifying Counter/Stealth rebuilds and SoftUnlock
create new chrome. The ordinary palette BOOL redraw at 180 ms, page changes and
ordinary preference redraws preserve it. A closed or focus-projected frame retains
the committed identity. Missing or null legacy identity keeps rendering stateless.

The supported launcher creates a fresh private session directory and the payload
initializes once. Reusing that directory with a separate replacement backend is
outside this identity contract; no publisher instance token is claimed.

SoftUnlock reports a pending creation even when first read after 150 ms. An
expired but uncommitted clock remains available for rollback detection. Actual
successful publication clears the expired lock immediately, including actorless
legacy views. Clock recovery after a committed creation cannot create it again.

## Production rendering contract

The renderer owns Basic RGB on its presenting thread. Both the runtime reader
and CPU preview reader use the same strict optional unsigned identity parser.
Existing hidden Basic scrollers still observe direct native Rainbow samples.
Fixed frames retain the sampled RGB and source opacity; a new creation starts
black. Missing or regressing identity cannot restore an earlier sampled color.
Other designs and the committed accent remain separate.

Logical source color history survives graphics resource reset. The production
close path observes its closed descriptor before resetting graphics resources;
it does not sample a color or claim a new rendering owner. Real source recreation
is distinguished by the next creation identity, even when an intermediate closed
or rebuilding frame was never rendered.

## Evidence limits and remaining scope

This wave uses TDD lifecycle, parser, history and production CPU pixel regressions,
plus an actual offline controller chronology. Those checks prove native observed
RGB retention and declared creation behavior. They do not prove the BO3 engine's
unobserved random Rainbow phase, exact scheduling, numerical fade easing, alpha
composition or physical font/screen parity. The existing native Rainbow sampler
remains an explicit adaptation.

Fixed frames retain the last RGB observed by the native renderer. A skipped or
never observed Rainbow interval cannot establish an exact engine sample, and
this change does not invent that sample from the committed fixed accent.

No live game, GPU, injection, installed package or saved playable backup is changed.
The full Apparition goal remains active: other widget histories and accepted fade
callbacks, private ammo, remaining gameplay families, normal Steam co-op, all IW
maps and super Easter egg acceptance still require work.
