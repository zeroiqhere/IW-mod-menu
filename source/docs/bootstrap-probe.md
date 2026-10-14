# Inert bootstrap comparison

The production injected-menu shortcut remains disabled. This separate diagnostic
tests only DLL loading and explicit startup validation in a fresh game process.
It is not a working menu and will not display anything in the game.

The probe exports InitializeOverlay and StopOverlay. It checks its caller packet,
the exact retail executable name, process identity, protocol, and window owner.
It creates no D3D device, installs no hooks, polls no inputs, starts no worker,
and has no feature controller or game-memory transport. Normal Windows compiler
runtime startup still occurs. The helper performs the explicit LoadLibrary and
InitializeOverlay operations; the probe remains loaded until the game exits.

The diagnostic launcher first uses ProtonInformer's read-only module report to
correlate the exact Linux process with the native bootstrap's Windows target.
It rejects an already loaded menu/probe, changed process incarnation, mismatched
discovery, or altered package artifacts. It never starts the production backend
or changes the production disabled marker. No failed load is automatically retried.

## Controlled sequence

1. Launch through normal Steam with the prepared reduced logging options
2. Host Zombies and spawn, leaving the mod shortcuts closed
3. Run only the inert probe after the diagnostic trial is confirmed
4. Leave the match once and report whether the lobby returns
5. Preserve the stage records and log before restarting the game

If the comparison passes, the next stage can isolate graphics-hook installation.
If it fails, capture the exact popup and final exception/thread/module evidence.
A successful inert trial does not prove the renderer works or fix the earlier
fatal error. The rendering-only controller is prepared separately for a later
stage; it has no imports or calls that can enable game features.

## Developer invocation

Run from the installed package directory. Supply a fresh observed game PID and
the existing verified ProtonInformer executable path:

```sh
python3 -m diagnostics.probe_launcher \
  --package-directory "$HOME/Downloads/IW-Apparition-DLL" \
  --proton-informer "$HOME/Downloads/IW-Apparition-injected/tools/proton-informer-v0.1.7-linux-x86_64/proton-informer" \
  --linux-process-id "$IW_GAME_PID"
```

There is no diagnostic desktop shortcut. Evidence is written inside a fresh
private `probe-*` session under the ordinary launcher cache. `probe-ready.json`
confirms inert initialization only; no renderer readiness packet is expected.
