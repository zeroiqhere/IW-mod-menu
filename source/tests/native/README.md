# Standalone D3D11 fixture

This fixture draws the menu in its own D3D11 window. Its separate DLL accepts only `Dx11Fixture.exe` and uses a synthetic model with disconnected features. It contains no engine memory transport, player operations, or game input commands.

Build `iw_overlay_fixture` and `Dx11Fixture` with the project's Windows toolchain. Both output files must remain in the same directory. The fixture requires a working hardware D3D11.0 driver and enough free GPU memory for the host and hidden bootstrap devices. Close the game before running it, and avoid other Proton test launches during the focus checks.

Run `Dx11Fixture.exe` with a Windows result-file path as its first argument. Under the configured Steam prefix, a test invocation can use:

```bash
DXVK_LOG_LEVEL=error protontricks-launch --appid 292730 \
  /tmp/iw-injected-build/Dx11Fixture.exe \
  'Z:\tmp\iw-dx11-fixture-result.log'
```

The result file records each check before the process exits. Exit code zero means all checks passed. The fixture checks real shader, output, resource, UAV, and fixed pipeline bindings around menu drawing, alongside bootstrap guards, original Present results, exact window/swapchain binding, resize, cached diagnostics, and resident shutdown. It does not establish compatibility with an untested game executable or feature level.

For scoped visual inspection, append `--hold-ms 30000`. The menu remains inside the fixture window for that bounded interval before shutdown. Use the exact title `IW standalone D3D11 fixture` when inspecting it, and capture only that window. The optional hold accepts values from 1 through 60000 milliseconds.

For the separate loader regression, append `--wait-for-bootstrap`. This mode never loads the DLL itself. It writes `<result-file>.target.json` with its Windows process ID and HWND, pumps its window, and waits up to 60 seconds for the fixture-only helper to complete checked remote loading and initialization. Supply that helper with the fixture ID, `iw_overlay_fixture.dll`, and an isolated session directory. The helper's own result file must be fresh for that launch.

A helper launch can remove focus from the fixture under Proton. The fixture waits for its exact Win32 foreground HWND before continuing. Scoped activation and a click in the fixture's empty client area restore an ordinary focus event when needed. The production foreground guard is never relaxed for this test. After focus returns, the fixture runs the same drawing, state, resize, and shutdown checks and exits automatically.

No DLL is unloaded after hooks exist. Stop disables drawing and the synthetic model while leaving stable pass-through trampolines resident until this owned test process exits.

Append `--closed-transitions` to test the path where the menu has never drawn. This closes the synthetic model before any renderer binding, then repeatedly replaces D3D11 devices and swapchains on the same owned HWND and on replacement owned windows. It checks original Present and ResizeBuffers behavior, confirms all renderer binding/draw diagnostics remain zero, and repeats a transition after resident shutdown. This covers the closed path independently of the ordinary visible-menu checks. It does not emulate Steam's proprietary overlay hook or prove its compatibility with a particular game transition.

An opt-in `--steam-api <absolute-Windows-path-to-installed-steam_api64.dll>` mode calls the documented `SteamAPI_Init` before creating any D3D device. Both `SteamAppId` and `SteamGameId` must already equal `292730`; no missing identity is guessed. The existing local API DLL is loaded read-only and is never copied or distributed. The fixture makes no stats, cloud, settings, or game calls. It requires `GameOverlayRenderer64.dll` to be observed after device creation, and fails normally if API initialization or module loading is unavailable. Module presence is reported separately from active hooks: the installed API has no supported flat overlay-status export, so no private interface layout is inferred. No Valve overlay DLL is manually loaded. Combine this with `--closed-transitions` for flushed before/after traces around original Present and ResizeBuffers calls. Steam initialization is shut down only after owned D3D resources are destroyed.

Use `--steam-guards-only` for six rejection checks that create no window, device, or Steam connection. App identifiers, relative paths, incorrect DLL names, and missing DLLs must fail before loading any API DLL. Test-only environment changes are confined to the owned process and restored on exit.

Use `--opened-transitions` for the separate real-draw lifetime regression. It proves owner-thread closed cleanup, same-HWND device/swapchain recreation and reopening, wrong-thread and wrong-HWND rejection, missing producer cleanup/recovery, and original calls after Stop. Closing or stopping requests cleanup at the next normal Present on the original owner thread. If that thread never presents again, resources remain resident until process exit; a different thread cannot destroy or claim the renderer. The retired owned swapchain reference is released after the original Present finishes. These safeguards address retained renderer resources after drawing. They do not identify the cause of either the earlier no-draw retail crash or the later actual-menu trial failure.

Use `--active-transitions` for same-HWND swapchain replacement while the synthetic menu stays open. Same-device replacements retain the private ImGui context and font resources; different verified D3D11.0 devices rebuild owned resources on the original presenting thread. Wrong HWNDs and threads are rejected. The retirement slot carries the old reference through the original Present, including renderer binding or drawing failures. This mode rejects Steam initialization, external bootstrapping, and visual holds.

The active fixture also directly exercises the production Renderer with a forwarding COM wrapper. Real device and Present calls remain real; only GetDevice or GetBuffer returns a controlled failure or null output. It checks prevalidation preservation, occupied output rejection, zero device reads on rejected threads, deferred reference lifetime, and cleanup. These direct failure checks verify renderer ownership ordering; they do not reproduce proprietary Steam hooks or a retail exception.
