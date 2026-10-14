# Inert bootstrap probe

The production `iw_apparition_bootstrap_probe.dll` accepts only `iw7_ship.exe`. The separate `iw_bootstrap_probe_fixture.dll` accepts only `BootstrapProbeTests.exe`. Both copy and validate the local fixed bootstrap packet, confirm the current process and HWND ownership, require a bounded path string, and accept one valid initialization. Duplicate valid calls reject permanently; Stop is inert and never resets initialization. The probe performs no path access, graphics work, hook installation, input polling, or feature operations. Its only memory read is `ReadProcessMemory` on the current process for the caller packet.

Build `iw_bootstrap_probe`, `iw_bootstrap_probe_fixture`, and `BootstrapProbeTests`. Keep the two DLLs beside the test executable. Run the invisible fixture with a Windows result-file path:

```bash
protontricks-launch --appid 292730 \
  /tmp/iw-injected-build/BootstrapProbeTests.exe \
  'Z:\tmp\iw-bootstrap-probe-tests.log'
```

The owned fixture creates hidden windows only, including one in a bounded child process. No window is shown or activated and no GPU device is created. Tests cover null, unreadable and partly unreadable packets; size, protocol and PID mismatches; absent, invalid and foreign HWNDs; unterminated paths; the production wrong-process guard; valid initialization; duplicate and concurrent duplicate rejection; and repeated Stop behavior. The child inherits only its ready pipe and stop event, exits on that event or a timeout, and is never forcibly terminated.

Static PE checks should find only KERNEL32, USER32 and Windows CRT api-set dependencies. Windows C runtime startup and relocation support remain the compiler's normal loader behavior; a minimal source DllMain does not mean no compiler startup code exists. The probe does not link D3D, DXGI, MinHook, the overlay runtime, the input worker, or any game transport. It is compiled without C++ exceptions or RTTI, keeps the normal CRT entry point, and exports only InitializeOverlay and StopOverlay. No Valve DLL is copied or distributed.

Passing these tests establishes the probe's local contract. It does not establish compatibility with an untested retail executable or fix the earlier game's fatal exit. A retail diagnostic trial must remain a separate explicit step with a fresh game process and preserved logs.

Run the five host-side PE regressions with `IW_PROBE_TEST_DLL` set to the built production DLL path and `python -m unittest discover -s tests/diagnostics -p test_probe_imports.py`. They inspect dependency names, contract exports, x64 format, and the absence of remote write, hook-patching, file, thread-creation, and input APIs. CRT relocation support can import VirtualProtect; that import alone is not evidence of MinHook or process patching.
