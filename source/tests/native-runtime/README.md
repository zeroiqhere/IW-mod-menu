# Native runtime checks

The executable links the production session parser, input router, private file
handoff and heartbeat code. Its windows and session directories belong to the
test process. It never connects to a game, injects a DLL or writes game state.

Build `iw_native_runtime_tests` with the repository's MinGW CMake configuration.
The optional first argument names the report file. With no arguments the report
goes to standard output. A second argument names fresh controller exports:

```sh
dotnet run --project tests/backend/Backend.Tests.csproj -c Release -- --export-menu-fixtures /tmp/iw-backend-contract-preview
protontricks-launch --appid 292730 build/iw_native_runtime_tests.exe 'Z:\tmp\iw-runtime-contract.log' 'Z:\tmp\iw-backend-contract-preview'
```

The exporter requires a new directory and has no game transport dependency.
The extra contract check loads all sixteen actual controller snapshots through
`ParseState`, verifies the preview identity, selection bounds, default and changed
appearance, independent theme selection and unavailable gameplay controls.
It also preserves the draft/applied distinction and feeds real navigation epochs
to the input router so a draft update continues a held choice while moving rows
requires a new press.
This joins the real C# producer with the real C++ consumer rather than relying
only on separately authored JSON packets.

Appearance tests cover absent/null legacy defaults, every supported display and
box location, copied immutable values, unchanged feature state, missing fields,
unsupported enum strings and wrong value types. A supplied appearance object
requires the seven original fields; optional `quickExit` defaults to false and rejects non-boolean values, including null. The parser never guesses partial preference state.

Optional `isRootMenu` is a strict boolean and defaults to true for legacy producers. Root identity is never inferred from the title. Quick Exit stays off when the appearance packet is absent, null or uses the old seven-field shape. Parsed Quick Exit and root identity select copied normal, nested or root saved-control hints; a missing variant falls back to normal hints.

Close commands require an unsigned `navigationRevision`, including valid epoch zero. Close and every Width Editor command serialize that field; ordinary Back and other legacy commands keep their original shape. Runtime tests cover epoch zero, the unsigned maximum and a missing Close epoch. The controller independently rejects stale Close revisions.

Choice key groups are optional and never receive guessed defaults. Both supported
saved groups are needed to enable the controller's choice rows. Native parsing
rejects malformed keys and accepts only an unsigned navigation epoch; older
packets retain epoch zero.

These checks establish protocol and orchestration behavior. They do not establish
gameplay effects or retail graphics-transition compatibility.


## Width state and bounded private JSON

The integrated main timing wave confirms 60 native runtime checks, including the fresh
sixteen-snapshot producer/consumer join. Legacy appearance packets retain width
260; supplied integer widths must be within 200..500. Optional editor metadata
must have an integer step within 1..10 and occur only in an open, non-root,
empty-row editor snapshot. Immutable snapshots separately copy the five private
editor body hints, contextual Exit help and unchanged normal footer hints.

`increaseWidth`, `decreaseWidth`, `increaseWidthStep`, `decreaseWidthStep` and
`finishWidthEditor` require an unsigned navigation revision, including zero and
the uint64 maximum. Missing epochs are rejected rather than guessed. Parsed
editor metadata selects the real editor router without leaking normal-menu Back
or Quick Exit commands. The controller independently verifies the current epoch.

The optional exact `attack` group is numeric, duplicate-free, at most sixteen
keys, and a subset of `down`. Only attack/down and the existing aim/up overlap
exceptions are permitted. Legacy omitted/empty attack groups remain valid but
cannot enable the editor. Unknown binding fields are rejected instead of
inventing new capabilities.

Private JSON rejects repeated decoded field names within one object, including
escaped aliases, before a later field can overwrite an earlier value. Names may
repeat in separate row objects. Nesting is limited to eight total containers;
excess depth is rejected without silently dropping extension data. These three
bounded-JSON regressions are included in the current 60-check result. The prior main width wave totals 392
distinct checks; the integrated timing wave now totals 441 checks with root native/Windows builds confirmed. Retail/GPU acceptance is
separate and unproved by these protocol tests.


## Editor return phases

Optional `menuUnlock` accepts only the exact strings `rebuild` or `pause`;
absent/null legacy values mean no lock. Other strings and wrong types are rejected.
Phase metadata must represent a valid open normal-page return, not an editor,
closed menu or fabricated context. Copied immutable phase metadata selects
`MenuLocked` input mode. All ordinary commands are suppressed, including Toggle,
Close, selection and width edits. A lock/unlock boundary reseeds held keys; a fresh
press is required afterwards. Root shutdown is an independent owner operation.

The sixteen-export join includes actual rebuild and pause snapshots. The backend
uses elapsed thresholds 50/150 ms, checks phase changes on its 25 ms owner loop,
and publishes changed state separately from the monotonic 100 ms heartbeat. The native
20 ms poll, file handoff and scheduling add visible-time quantization. These
protocol tests do not prove retail draw timing or BO3 easing.

## Credits state

Optional `menuCredits` is an exclusive modal object containing only integer
`elapsedMilliseconds` within 0..39999. It requires an open child menu, no ordinary
rows and selected index zero. Missing/null metadata preserves legacy behavior;
wrong types, extra fields, duplicates, out-of-range values and conflicting editor
or return metadata are rejected. `finishMenuCredits` requires the captured unsigned
navigation revision. Private saved melee hints replace only the active footer,
leaving normal instructions intact for return. Fresh input can retry after release;
pending or held edges never become deferred exit commands.

## Main opening controls

The [integrated editor contract](../../docs/opening-controls-reference-audit.md) covers private saved-action
availability, copied applied chord descriptors, typed spacers and captured
navigation/policy epochs. The preceding Windows runtime suite had 148 registered cases
including its 180-profile actual controller join. Its backend suite had 361
cases. Legacy omitted descriptors retain the Aim plus fresh Melee gesture;
explicit descriptors use a full aggregate chord edge. Quick Menu and saved
persistence remain unavailable, and retail acceptance is pending.

## Private spacer receipts

The [spacer contract](../../docs/spacer-scroll-reference-audit.md) joins 192 actual
controller profiles through the production parser and a private exact-ACK tracker.
The Windows runtime suite passes 164 registered cases; the portable suite passes
174, including its platform-specific preview reader. Sixteen shared cases are new.
No receipt is trusted from JSON, and promotion occurs before immutable publication.
These checks establish local menu transition state, without gameplay acceptance.

## Use follow-up graphs

`use-followup` has independent literal inventories for 51 lower-branch packets
and 17 non-slider packets. Both production readers and the real input router
consume actual controller exports. Non-slider `choiceNoop` is an exact optional
kind with its own outer 50 ms producer phase; it grants no callback capability.
The schema checks reject malformed, modal, closed or unowned copies.
General mechanical held repetition remains a separate fidelity requirement.
