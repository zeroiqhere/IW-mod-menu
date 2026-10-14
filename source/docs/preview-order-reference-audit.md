# Stable CPU preview exports

This wave changes test fixture ordering only. The production controller, native
renderer, gameplay operations, launcher and DLL hooks retain their sealed
1040-check bytes. Independent final isolated proof is complete.

## Observed regression

Two identical Release CPU exporter binaries had SHA256
`a8995ba015837fdd2b6a68a730dd859d5b6c76e196817fa45224842af3e6404d`.
They consumed byte-identical 130-profile inventories. Different directory
iteration orders generated small differences in 97 of the 160 whole images.
Rerunning the same binary against the same root fixture directory reproduced
all 160 root images byte for byte. Footer geometry, default main/reference
pixels and registered layout/glyph-coverage checks had already passed.

The exporter retained one ImGui frame/font atlas and iterated directory entries
without sorting them. Directory order therefore changed the glyph warmup order.
Atlas packing and bilinear rounding explain the observed edge-pixel variation
as an inference; there is no claim of a production GPU rendering fault.

## Intended test contract

Collect the JSON paths and sort them lexically before the existing actual-profile
render loop. Do not change profile contents, persistent-frame testing, production
panel calls, font configuration, CPU raster rules or glyph-coverage checks.
Checked-in test manifests declare the exact producer and image inventories.
A real integration regression copies the 130 profiles into directories created
in opposite orders and a relocated directory, runs the real exporter and
compares the complete 160-image hash maps. Missing, swapped or extra artifacts
and a failed exporter cannot count as passing evidence.

Root independently reproduced the final four-test suite's original failure
against the preceding exporter: one real order-comparison failure, with the
three failure-path harness checks passing. After the sorting fix, the separate
Release CPU build and both CTest targets pass: 59 geometry and 129 panel checks.
The final four integration checks also pass. All six forward/reverse/relocated
runs from root and renderer agent match the full 160-image hash map exactly.
Copied producer bytes and differing observed directory orders are verified.

The source checkpoint now has **1044 distinct checks**: the preceding 1040
remain applicable and four registered integration tests are added once. The
three exporter executions, 130 profiles and 160 image comparisons are joined
evidence, not extra registered cases. The final tests reject exact inventory
changes, same-count helper swaps, output symlinks and an exporter exit failure.

Root reviewed main, adaptive AIO rebuild, AIO cooldown and normal-opening drawn
PNGs. The three normal main/reference-main/reference-status baselines remain
byte-identical to the preceding checkpoint. Final lexical-order output also
matches the original failing test's forward-order output, proving the fix
standardizes traversal rather than modifying the rasterizer.

All production source hashes and the packaged native/backend binary files stay
identical to the preceding 1040-check checkpoint. Its 234-step MinGW build,
Windows publication and 128 owned Windows runtime checks remain retained
proof, not newly rerun results. No game/GPU test, deployment or new feature
claim is made. Earlier checkpoints and installed files remain unchanged.

## Separate unresolved Option Counter timing

The pinned Apparition handler commits the counter, closes the menu, waits for
normal reopening and saves the theme. That guarantees its handler has a 50 ms
HUD gap; current IW counter presentation changes immediately. The calling
monitor starts the handler as a script thread, then tests option metadata before
an optional 180 ms refresh. The declarations alone do not prove which state the
monitor observes. A precise input-release schedule and uninterrupted opening
fade therefore remain unimplemented rather than guessed. This exporter fix
makes no counter behavior change.

- [Counter handler](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L661)
- [Monitor and script-thread dispatch](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L76)
- [Normal opening](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L200)

Full Apparition parity, gameplay/map coverage, source scheduling fidelity and
retail match-exit acceptance remain incomplete. No live game or GPU is used.
