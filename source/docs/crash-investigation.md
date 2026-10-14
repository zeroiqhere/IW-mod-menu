# Retail match-exit crash investigation

The injected launcher is disabled by `disabled.json` in the installed package.
The original desktop menu and its verified backup are retained. A successful
standalone test does not authorize calling the injected version ready.

## Observed failure

The first retail injection completed DLL loading and native worker startup.
The menu remained closed, no command was submitted, no feature was enabled,
and renderer diagnostics recorded no device binding or completed menu frame.
The controller then stopped with a file replacement permission error.

The user reported a fatal application-crashed popup immediately after dying or
leaving Zombies, before returning to the lobby. A normal Steam launch without
the injected menu returned to the lobby successfully. No usable crash dump or
exception stack was found in the scoped game, prefix, Steam dump, or journal
checks. The injected version is suspect; the exact failing module is unknown.

## Reproduced and fixed separately

Proton can reject replacing an IPC file while a Windows reader still has the
destination open, even when deletion sharing is allowed. Bounded replacement
retries now preserve complete temporary files and advance heartbeat generation
only after a successful commit. Persistent failures remain errors.

The cross-runtime regression completed 3,000 managed commits and 3,000 native
reads, with the Python health reader running alongside them. This explains the
controller error, but does not establish the cause of the game's fatal error.

## Graphics coverage and limits

The standalone renderer passed 41 drawing and graphics-state checks. A separate
closed-menu fixture passed 60 checks across device, swapchain, window, resize,
and stopped-overlay transitions. It never bound a renderer or drew a menu.
A further opt-in fixture passed 61 closed-menu checks after documented
`SteamAPI_Init` succeeded and `GameOverlayRenderer64.dll` was observed loaded.
Its trace records original Present and ResizeBuffers calls across transitions.
The fixture did not confirm active Steam graphics hooks. None of these results
reproduces the retail game's map-exit sequence or identifies its failing module.

An opened-then-closed fixture separately reproduced retained renderer bindings.
Closing now releases owned drawing resources on the original presenting thread;
the retired swapchain reference survives until its original Present completes.
Reopening can bind a recreated swapchain for the exact same target window and
original presenting thread. A different thread or window cannot claim it.
If the original thread never presents again, cleanup waits until process exit.
This defect required a prior draw and therefore does not explain the observed
zero-draw retail crash.

Final graphics regressions passed 47 opened/closed checks and 60 never-drawn
transition checks. With Steam initialized and its overlay DLL observed loaded,
the corresponding runs passed 48 and 61 checks. Six Steam fixture input guards
also passed. All runs used owned test processes, with no game injection or game
feature writes. Steam module presence remains weaker than active-hook proof.

## Capture a normal Steam diagnostic log

The private log directory is ready. These launch options were entered through
Infinite Warfare's Steam Properties and confirmed in the saved app settings:

```text
PROTON_LOG="-unwind" PROTON_LOG_DIR="$HOME/.cache/iw-apparition-injected/game-logs" %command%
```

Launch normally without the injected shortcut, enter Zombies, and leave the
match. The expected log is `steam-292730.log` in that private directory. Preserve
the baseline log before a later run because Proton reuses the filename. Remove
the diagnostic launch options when collection is complete.

The logged normal Steam comparison returned to the lobby. Its compressed
snapshot and bounded final exit tail are preserved in the private log directory.
The original default log contained millions of unwind lines. The next-run options
disable that channel while retaining Proton's exception and module channels.
The exact updated launch options were confirmed in Steam's saved app settings.

The baseline included 3,824 breakpoint/illegal-instruction exceptions resumed
by the game's vectored handlers and 163,344 longjmp unwind records. Those events
alone are not evidence of the fatal error. No fatal/unhandled game exception
appeared in the captured comparison.

Another injected-game trial requires a concrete logging and isolation plan;
the disabled marker remains in place until that plan is ready.

The inert bootstrap comparison subsequently passed: the DLL loaded and explicitly
initialized, ProtonInformer confirmed its exact module path, and the user left
Zombies without a crash. Its log is preserved separately. This excludes plain
loading as a reproduced failure in that run; it does not prove hook compatibility.

The user then requested the actual menu with Godmode and Infinite Ammo. A
one-session `--trial` launcher option permits that explicit diagnostic comparison
without deleting the normal shortcut's disabled marker. It retains the ordinary
startup checks and begins closed and disconnected, with no feature automatically
enabled. A fresh normal Steam game is required. Actual drawing, input, feature
validation, and match-exit results must be observed before release readiness.

Proton documents these options in its [official runtime configuration](https://github.com/ValveSoftware/Proton#runtime-config-options).
Valve documents Steam overlay initialization and Direct3D integration in its
[Steam Overlay documentation](https://partner.steamgames.com/doc/features/overlay).

## ProtonInformer

The current official release is v0.1.7, matching the existing research tool.
Its offline x64 helper verification passed, and `inspect` accepted the installed
menu DLL as an x64 Windows PE DLL. Runtime verification requires a live target;
offline success does not confirm Wine path translation or module discovery.

Live `doctor` subsequently passed all six helper self-tests against the logged
baseline process. Read-only `modules` identified the exact game and loaded Steam
overlay, D3D11, DXGI and D3D12 modules, with no injected menu DLL. D3D12 module
presence does not establish which API or feature level renders the game.

The [official CLI](https://github.com/locainin/ProtonInformer/blob/main/docs/cli.md)
provides `doctor` and `modules` for a subsequent live diagnostic check. Its loader
uses remote LoadLibrary and verifies module paths. This payload also requires an
explicit InitializeOverlay call outside DllMain, supplied by the existing
bootstrap. Earlier loading and initialization succeeded, so a loader replacement
alone does not explain or establish a fix for the match-exit crash.

## Later actual-menu failure found in the saved log

A bounded read-only audit on 2026-10-08 found a fatal exception in the later
actual-menu run, separate from the original zero-draw run. The raw log is
508548029 bytes, SHA256
`af3e2515adba14b46480a842c7b5ecd6fe7d8e7291ddd9eb5a644f903a21964a`.
The recorded payload path and Windows PID308 correlate with the saved
`session-rbwl1t3z` trial. Its final renderer status records 22637 completed drawn
frames and zero renderer faults; its final cleanup status says original-state
restoration could not be confirmed. Neither metric establishes game health.

At Wine time49252.411, thread0264 records `c0000005`, parameter0=8 and attempted
execution address `0x280c12580`. Wine identifies that address as an unknown module.
The next millisecond reports unhandled `c0000409` in the game at RVA`0x12ba464`.
Microsoft describes parameter0=8 as an execute/DEP violation. Its fast-fail
reason is a separate parameter, absent from this saved final line; the code alone
does not prove stack-buffer corruption or a stack-cookie failure.

The payload, Steam overlay, D3D11 and DXGI loaded at distinct high image bases:
`0x6ffff8cc0000`, `0x6ffffa190000`, `0x6ffffc2a0000` and `0x6ffffc9e0000`.
No crash-time allocation map identifies the lower execute-fault address. That
absence does not exclude a dynamically allocated trampoline or another owner.
The backtrace lists a game return RVA`0x945395` among many implausible unknown
frames; it is a reported frame, not a verified caller stack.

The unchanged installed game PE has candidate exception-table spans
`140944B00–140945ADB`, `1412BA448–1412BA519` and `1412BA038–1412BA055` for
that reported return, fail-fast site and logged handler. Raw on-disk unwind data
contains unsupported flags and out-of-image handler values. These table spans
cannot establish decoded runtime instructions, callable functions or named CRT
handlers. No guessed nearest symbol or encrypted-byte disassembly is accepted
as a crash explanation.

The next causal evidence must identify the crash-time allocation state/protection
at the execute-fault address, actual runtime code/unwind bytes, a usable original
exception stack and the secondary fast-fail parameters. Hook-only closed-menu,
opened UI without features, and enabled-feature comparisons remain separate
conditions. The active swapchain recovery work below addresses a separately
reproduced renderer limitation; it is not a claimed fix for this fatal exception.

- [Exception record parameters](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-exception_record)
- [Fast-fail code and reason parameter](https://learn.microsoft.com/en-us/cpp/intrinsics/fastfail)

## Bounded offline log reader

The saved-log analyzer lives in `packaging/diagnostics/proton_log.py`. It takes
an explicitly supplied decimal Windows PID and scans Wine's hexadecimal PID
records. It hashes every source byte, checks that the opened regular file and
its path remain unchanged, and writes only to a fresh private output directory.
It never inspects a running process or changes Steam logging settings.

```sh
python3 packaging/diagnostics/proton_log.py SAVED_LOG \
  --windows-pid 308 --output-dir NEW_REPORT_DIRECTORY \
  --session-label SUPPLIED_SESSION_LABEL
```

PID and session-label selection are supplied provenance, not authenticated
process identity. Records from different processes or threads cannot supply
exception parameters. Recent same-thread records have explicit line/time bounds;
malformed or duplicate parameters invalidate correlation. A matching earlier
exception is recorded as nearby evidence, without asserting that it caused the
fatal. Module entries retain observed basenames and image bases only. No module
range, allocation owner, reliable stack or crash cause is inferred.

The independent scan of the saved 508548029-byte log found 4446175 lines, one
matching fatal event, 78 module-load records and 77 unique observed name/base
pairs. Its byte hash matches the earlier read-only audit. The access violation
and later fail-fast record described above were preserved. These findings do
not establish a clean gameplay session or a crash fix.

The parser's dedicated tests run separately from the existing packaging suite:

```sh
python3 -m unittest discover -s tests/packaging/diagnostics/proton_logs -v
python3 -m unittest discover -s tests/packaging -v
```
