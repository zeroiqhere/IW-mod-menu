# Self Third Person: offline feasibility

Audit date: 2026-10-08. **Source-grounded candidate only; no new feature implementation, live reads, injection, or writes.** The normal retail Steam/Arch/Proton requirement remains unchanged. This work does not establish teammate camera control or all-map safety.

## Exact upstream operation

The pinned [IW-RTM handler](https://github.com/SyndiShanX/IW-RTM-Tool/blob/857faa87fe691624040dd48917bd4e916058fc96/syndishanx_iw_tool/MainWindow.xaml.cs#L226-L238) flips its local `thirdPerson` boolean and calls `Memlib.WriteMemory("1475D76F0", "int", "1" or "0")`. It performs a single write, not a freeze/loop. The address is **absolute**, unlike this source's `base+...` features. It does not read the original, check the write result, verify gameplay, or restore a captured original; its Enabled/Disabled log follows the requested local value.

The current [Memory.dll int branch](https://github.com/erfg12/memory.dll/blob/6f0b21756a60a3a7ee815d623a8dc2c99eab323b/Memory/Methods/Write.cs#L210-L214) converts to Int32 and uses four bytes. This corroborates the requested upstream type/width; it does not identify the camera field or prove the bundled historical library is byte-for-byte the same release.

Apparition's actual [BO3 handler](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/basic.gsc#L542-L548) changes the selected player's boolean and calls `SetClientThirdPerson`. Its AC-130/Flyable-UFO conflict check and per-player engine API cannot be carried over by assigning the upstream IW address to every player.

## Installed executable: offline facts

Inspected only `$HOME/.local/share/Steam/steamapps/common/Call of Duty - Infinite Warfare/iw7_ship.exe`, using Python standard-library PE parsing, SHA-256, and installed `objdump`.

| Property | Observed result |
| --- | --- |
| File size | 48,612,264 bytes |
| SHA-256 | `A883EE28EE9F552C63D8ED1E352F60501B2E256D8857CA178C6DD3AAE622723B` |
| Machine / format | AMD64 `0x8664`; PE32+ `0x20B` |
| Preferred image base | `0x140000000` |
| SizeOfImage | `0x0C301400` |
| COFF timestamp | `0x5AD6A265` (2018-04-18 01:41:57 UTC); header metadata, not a Steam update date |
| Exact upstream address | `0x1475D76F0` |
| Derived RVA at this preferred base | `0x075D76F0`, four-byte end `0x075D76F4` within image size |
| Containing section | `.data`, RVA `0x01741000`, VirtualSize `0x0995249C`, RawSize `0x004B0800` |
| Section flags | `0xC0000040`: readable/writable initialized-data section, **not executable** |
| Target within section | Offset `0x05E966F0`, beyond its raw file data; no target file offset/value exists |
| Relocation metadata | COFF flags include relocations stripped; base-relocation directory is zero; DYNAMIC_BASE bit is absent |

[Microsoft's PE documentation](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#section-table-section-headers) specifies zero filling when VirtualSize exceeds raw data. Thus the target lies in the section's initially zero-filled virtual tail; that says nothing about its initialized gameplay value. Reading adjacent file bytes would inspect unrelated data, not this field.

There is no instruction context **at** this target because it is non-executable, unbacked data. An `objdump -d -Mintel` search across executable sections found no emitted direct reference to the exact target or its four-byte span. The file also has no literal 64-bit pointer equal to that address. Neither negative result proves the field unused: indirect access, object-relative access, protected code, or runtime initialization can avoid such references. No function/field symbol or instruction signature was recovered, and no substitute offset was invented.

The installed hash matches the existing teammate transport's pinned-build check. That check applies to that reviewed teammate layout, **not** to this camera candidate. The current `IFeatureTransport`/`RetailSteamTransport` has health/ammo operations and guarded teammate binding, but no camera read, ownership state, feature lifecycle, or menu action. Existing transport machinery is not camera implementation proof. Header relocation facts likewise do not replace checking the actual loaded module identity/base in a future authorized observation.

## Remaining validation before implementation

1. Establish the exact current field identity/type from read-only observations in a spawned supported match. Observe normal first-person, ADS, spectator/death/After Life, scripted cameras and lobby transitions; record whether the candidate changes with them. A normal value of 0 alone does not prove a boolean camera flag.
2. Establish scope: host-local view, per-player state, or global camera setting. The upstream single address does not prove selected-player control, server authority or a client stride.
3. Bind observations to this process/build/map and a validated camera/spawn/session lifetime. Stop on unsupported states and conflicts; stable health or a player name alone does not identify a camera generation.
4. If semantics are established, define exact original capture, supported typed values, guarded ownership, immediate readback and visible camera confirmation. OFF must restore the captured owned state rather than unconditionally write 0. External game changes, failed writes/readbacks or replaced lifetime must produce UNKNOWN and prevent stale restoration.
5. Add organized pure lifecycle/regression tests before wiring a capability row. Controlled retail proof must then cover enable/disable, ADS, damage/down/respawn, focus changes, disconnect/map exit and cleanup on every claimed supported map. A label changing is not acceptance.

**Conclusion:** the historical four-byte 0/1 operation and its location within this exact installed module can be grounded offline. The field's actual camera meaning, normal state transitions, ownership and safe lifetime cannot. It remains a read-only research candidate; no gameplay action should be enabled from these file facts alone.
