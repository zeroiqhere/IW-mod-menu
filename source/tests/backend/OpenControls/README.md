# Main opening controls core

The original 11 registered cases test the immutable core. The Integration folder adds 27 registered controller, boundary, capability, export and stale-scope cases; native input and parsing have separate suites. Quick Menu can be a selector candidate, but applying it rejects with `UnsupportedTarget`.

Run from the isolated source root:

```sh
dotnet run --project tests/backend/Backend.Tests.csproj -c Release
dotnet build backend/IW.Apparition.Backend.csproj -c Release
```

The pinned reference is Apparition commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. `Menu/menu_customization.gsc:16` declares the 13 action rows; its defaults at line 750 use Aim and Melee for the main menu. `Menu/base.gsc:1205` checks duplicates, consecutive slots, None truncation, and the offhand conflict. Physical bindings, source alive/dead checks, saved dvars, and Quick Menu behavior are outside this core.

The registered groups cover:

- Exact canonical IDs and source tokens
- Explicit capabilities, invalid input, and immutable copies
- Default chord availability and input ownership
- Factory rejection and positive revision bounds
- One, two, and three action commits
- Duplicates, reselect, slots, and gaps
- None truncation and accepted unchanged policies
- Offhand conflicts and permitted same-slot replacement
- Stale revisions, missing capabilities, and revision exhaustion
- Independent selectors and unsupported Quick application
- Invalid selectors and stale draft application

`MainOpeningChord.TrySetSlot` returns the original owner on rejection. Accepted tuple changes increment the checked revision; None at the first unused consecutive slot retains it. Capabilities are caller-supplied proof of available actions, not discovered bindings. The expected revision is checked against the supplied immutable chord. The controller owns and publishes its active immutable chord atomically; the core is not a shared mutable compare-and-swap store.

The integrated descriptor changes immediately after a committed tuple change, while source row booleans remain captured until deferred redraw. A typed refresh cause and captured navigation/policy epochs govern this handoff. See [the integration audit](../../../docs/opening-controls-reference-audit.md) for source timings and remaining gaps.
