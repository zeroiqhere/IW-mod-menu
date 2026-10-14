# Active swapchain ownership and saved crash evidence

This wave fixes a native renderer lifecycle defect, independently of the BO3
reference's script HUD semantics. It adds no game feature writer, player-state
binding, input action, game offset, external menu window or Steam API call.

## Reproduced behavior and ownership

Before the fix, an already bound renderer rejected every different swapchain
while the menu remained open, including a new chain from the same target HWND
on the original presenting thread. The owned D3D11 fixture reproduced that
missing draw before the production change.

The renderer now validates the candidate HWND, owner thread and D3D11.0 device
before changing the prior binding. Same-device replacement retains the private
ImGui context and backend resources and recreates only the render target. A
different verified device resets the old resources on the original owner thread
and follows the existing device-binding path. A foreign thread or HWND cannot
claim the renderer; an occupied retirement output slot cannot be overwritten.

The old owned chain reference survives until the original Present returns, even
if candidate binding or back-buffer access fails. The hook's retirement slot
lives outside its exception handler. Stop still disables drawing with resident
pass-through hooks, and close still requests cleanup on the presenting owner
thread. No hook removal or guessed replacement owner was introduced.

The dedicated fixture covers an open-menu replacement with a different real
device, repeated same-device chains, rejection and cleanup. A forwarding COM
proxy injects explicit GetDevice/GetBuffer failures while retaining real D3D11
resources and original Present calls. Its controlled failures do not emulate
proprietary Steam hooks or an unknown retail crash.

## Crash evidence remains separate

A new bounded offline reader prevents the saved later-menu fatal from being
missed. Thirty-one registered tests cover exact PID/thread selection, actual
Wine fatal grammar, malformed/duplicate/orphan parameters, retention bounds,
source stability, read failures and private non-overwriting output. The existing
84 packaging tests also pass independently.

Root scanned the saved 508548029-byte source twice and matched its earlier hash.
The final parser found one fatal across 4446175 lines and preserved the preceding
execute-access violation and secondary fail-fast. No crash-time allocation map
identifies the attempted code address; no reliable stack or specific fast-fail
reason is established. [Crash evidence and offline-reader usage](crash-investigation.md)
records those limits.

## Independent regression checkpoint

Root rebuilt the production targets in 62 steps and completed the remaining
full cross-build in 179 steps. Its separate owned graphics build passed all four
modes: normal42, never-drawn closed60, opened/closed47 and active69 assertion
executions. Counts overlap through shared bootstrap and repeated state checks;
they are not added as independent registered cases. The 31 dedicated Python
cases and existing 84 packaging cases also pass independently.

The prior 1044 registered logical checks remain retained proof for unchanged
producer, feature, protocol, input and CPU rendering source. Only the 31 new
log-reader cases are added to that registered total. Producer snapshots, backend
binaries and 160 CPU previews remain unchanged.

Two earlier post-patch fixture runs failed intermittently: one at the third
same-device replacement and one on reopening after cleanup. Their failed calls
lack foreground/fault samples, so their cause is unknown. Eight instrumented
repeats and the final root/agent runs passed; those successes do not explain
or erase the failures. Both failures remain in the sealed evidence.

## Acceptance limits

A passing owned graphics fixture proves the exercised rendering and ownership
paths only. It does not establish a retail crash fix, Steam overlay coexistence,
map exit, player-state validity or full Apparition parity. The installed launcher
remains unchanged and disabled. Personal ammo, validated all-player Godmode and
other reference gameplay families still require actual IW bindings.

[DXGI device access](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgidevicesubobject-getdevice)
and [Present semantics](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-present)
were fetched from Microsoft during this wave.
