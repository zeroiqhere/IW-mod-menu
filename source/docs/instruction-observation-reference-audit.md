# Footer color observation before graphics preparation

The independent instruction panel must keep an admitted color observation even
when the current graphics frame cannot be prepared. Its logical creation, fixed
callback history and native Rainbow sample belong to the existing player-owned
widgets. Backbuffer readiness does not create or destroy those widgets.

## Reference contract

Official HEAD fetched with curl on 2026-10-09 still matches commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
[The independent instruction loop](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L372)
owns creation and destruction separately from menu chrome.
[Fixed callbacks](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L129)
and [Rainbow assignments](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L228)
target existing instruction widgets outside the menu-body condition. The source
has no Direct3D preparation condition on those logical color updates.

The preceding renderer observed footer lifetime before graphics preparation but
updated its colors only after swap-chain, device, target and buffer preparation.
An available creation or Rainbow sample could therefore be lost on a failed
frame. A later fixed stop could incorrectly appear to have an unobserved seed.
This is an ordering defect in the port's observation adapter; it is not evidence
of a retail crash cause or the source engine's numerical fade curve.

## Production phase and ownership

The portable instruction phase must be the phase used by `Renderer::Draw`.
It observes admitted lifetime and colors exactly once from one immutable snapshot,
clock and native sample, then invokes graphics preparation. Preparation failure
skips painting; an exception keeps the logical observation and propagates to the
existing hook error handling. Successful preparation passes the copied footer
result to paint without another history update or sample.

The existing presenting-thread, null-chain and occupied-retirement guards remain
before the phase. The hook mutex, target window checks, resource ownership,
swap-chain handoff, old return/exception behavior and rendering state restoration
remain authoritative. Logical history grants no device, drawing, input or
gameplay permission.

Canonical absent or unowned footers still observe with admission false and consume
no color sample. Unknown or focus withdrawal retains dormant logical state. Same
creation mutation stays quarantined; a newer valid creation can replace its own
history even if its first graphics preparation fails. A known closed footer uses
the drawing route; nondrawable closed cleanup must not invent a Rainbow sample.

Backend event ages and native presentation time remain separate clock domains.
The phase uses the one native monotone time sampled by the renderer. It does not
subtract a backend clock epoch or rewind native time after backend recovery.

## Regression evidence and limits

Acceptance requires a behavior-preserving production seam extraction, genuine
compiled failing-before regressions, fixed and Rainbow creation/stop cases,
false and throwing preparation, repeated receipts, replacement/quarantine and
withdrawal cases, and final strict source-bound native builds. Actual controller
packets must cross both readers and the same phase used by the production renderer.
The actual callsite must place every graphics preparation exit inside its
preparation callback and retain all drawing inside its paint callback.

Portable preparation callbacks model failure ordering. They do not create a GPU
device or prove physical COM failure recovery. CPU replay does not prove injected
game rendering. Reused managed build and snapshot evidence is valid only after
checking all original managed inputs and packet hashes against the current source;
it is not a fresh managed test run.

Frames rejected before the renderer phase, including foreign targets, test
presents, resize rejection, disabled hooks and lost focus, provide no admitted
color observation. This change does not reconstruct unseen engine updates.
Exact engine fades, 10 ms scheduling and Rainbow phase remain open, along with
other widgets, gameplay callbacks, private ammo, complete-cohort actions, normal
Steam co-op, all-map quests and the super Easter egg, and physical menu controls.
The saved backup remains available while this offline candidate is developed.
