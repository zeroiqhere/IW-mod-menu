# IW Apparition — tested build 1757

An Apparition-style menu drawn inside **Call of Duty: Infinite Warfare**, for normal Steam custom Zombies matches on Arch Linux through Proton. The controller has no separate mod-menu window.

This repository preserves **1757-530c4372**, the build used in the successful solo Zombies in Spaceland test on October 14, 2026. The runtime files are unchanged, and all 1,757 files in `source/` match that build's source snapshot. Later development changes are outside this snapshot.

## Confirmed in this build

| Check | Observed result |
| --- | --- |
| Open menu with Aim + Melee | Menu appeared inside the game |
| Connect after spawning | Connected to the original Steam game |
| Personal God Mode ON / OFF | ON prevented damage; OFF restored damage |
| Lobby Infinite Ammo ON / OFF | ON kept ammo unlimited; OFF restored ordinary consumption in the solo test |
| Exit after both features were OFF | Normal match departure returned to the lobby without a crash |

The scope was **solo Spaceland**. This trial does not establish teammate Godmode, co-op ammo behavior, other maps, or leaving a match with features active. Money changes, perks, doors, main Easter Egg actions and Super EE actions remain unavailable. Disabled rows do not have gameplay handlers.

## Run the saved build

Requirements: the retail Steam game, Python 3, Protontricks, and a Windows .NET 8 runtime in the game's existing Proton prefix. Steam app ID: `292730`. The .NET runtime included with `dotnetdesktop8` also provides the required runtime.

1. Start the game through its normal Steam library entry using the normal Steam account
2. Host a custom Zombies match and spawn; keep other mod tools closed
3. From this repository directory, run:

```bash
python3 tested-build/launch_injected.py --trial
```

4. Hold Aim and tap Melee to open the menu, then select **Connect after spawning**

Aim and Fire move through ordinary rows, Use selects, and Melee returns to the parent or closes the root. With default mouse and keyboard bindings, these are usually right mouse, left mouse, F, and the configured knife key. The launcher reads the game's saved action bindings; custom bindings take precedence.

Tap and release Use once per toggle. Closing the menu is different from selecting **Exit** for cleanup. The confirmed departure sequence is: turn both features OFF, select Exit, then leave the match through the normal game menu. Use one menu build per game process; restart the game before switching builds.

The original `disabled.json` remains in the saved package. `--trial` deliberately selects the controlled test path without removing that marker. To inspect supported Steam/game directory options:

```bash
python3 tested-build/launch_injected.py --help
```

## Repository contents

- `tested-build/`: the saved runtime, launcher, dependency licenses and original sealed manifest
- `source/`: the complete matching source snapshot, including tests and build instructions
- [TEST-RESULTS.md](TEST-RESULTS.md): the later game-test results and their limits
- [LICENSE.md](LICENSE.md): the application's GPLv3 license
- `EXPORT-HASHES.json`: checksums of the exported files

The original `tested-build/SOURCE-STATUS.md` was written before the game test and is preserved as historical build metadata. This README and TEST-RESULTS.md describe the later test. The source's older status documents likewise describe their own checkpoints.

## Source, dependencies and credits

See [source/README.md](source/README.md) for the saved build and test instructions. The source snapshot is checked against `tested-build/SOURCE-HASHES.json`; runtime artifacts are checked against `tested-build/manifest.json`.

Based on [SyndiShanX's IW RTM Tool](https://github.com/SyndiShanX/IW-RTM-Tool), with menu behavior inspired by [CF4x99's Apparition](https://github.com/CF4x99/Apparition). Third-party licenses are preserved for Dear ImGui, MinHook, JSON and Memory.dll. Memory.dll's origin and LGPL license are recorded in `tested-build/backend/Memory-provenance.md` and `Memory.LICENSE`.

No game executable, game FastFiles, captured game memory, private session credentials or local session logs are included.
