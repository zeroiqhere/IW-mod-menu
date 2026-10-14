# Godmode completion receipt audit

The retail Godmode adapter serializes captured targets, rolls back its own writes when a batch fails, and returns a snapshot describing the operation. A successful enable must confirm the captured original actors through completion. A successful disable must preserve the existing distinction between confirmed restoration and live display availability.

## Confirmed gap

`GodmodeBatchCoordinator.ApplyAsync` checks the captured roster before each member operation. The previous implementation returns success after the final awaited write without a completion check. `PlayerGodmodeRegistry.Result` subsequently constructs a fresh snapshot. A final write that invalidates one captured identity can therefore produce a successful operation with an already unknown returned member and retained cleanup.

The deterministic regression must invalidate an original captured identity during the final accepted write, before that write returns. Completion validation must still run while the batch retains its touched members and rollback evidence. The returned completion receipt must be the receipt that was validated.

## Ownership and cleanup requirements

An invalid identity cannot be substituted by a display name, a slot, or a replacement actor. An earlier failed cleanup remains owned by its original adapter. A late completion failure must restore only changes owned by the current operation and must preserve prior confirmed states according to the existing transition evidence.

OFF cleanup can succeed after a participant becomes nonliving or disconnects when the original feature confirms that its worker stopped and its owned restoration completed. Unknown live display alone does not grant a new ON or invalidate a confirmed OFF cleanup. Capture operations retain their existing separate semantics.

## Proof and remaining work

The focused regression reproduced four failures against the preceding production behavior. Seven focused cases and the affected feature suite pass with the completion receipt change. All 110 feature regressions and 125 session regressions pass. The production backend compiles with zero warnings or errors. Frozen source/input identities and independent review bind the final candidate before source promotion. Fake flag storage exercises the real flag feature without operating on a game process; controllable adapters are reserved for lifecycle timing.

All Players Godmode still requires a complete current participant receipt, including the host, and reuse of each participant's individual owner. The current name-only discovery list skips unreadable slots and cannot establish that receipt. Personal ammo still requires a verified weapon transport and the source Continuous/Reload/Disable event behavior. Target-bound source menus, remaining callbacks, physical input/graphics, normal Steam co-op, all maps and quests remain required for full Apparition parity.

The saved playable menu and installed products remain unchanged during this offline source work.
