using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace syndishanx_iw_tool.Players;

// These values describe one observed identity, not a permanent slot or spawn token
public sealed record PlayerIdentity(int Slot, long Entity, long Client,
    long ServerClients, int ServerCount, string Name, string Guid) {
    // The selected record is separate from the shared session anchor
    public long ServerClient { get; init; }
}

// Scalar values are a read-only snapshot and may change after the read finishes
public sealed record PlayerState(PlayerIdentity Identity, int CommandTime, int Health, uint Flags);

public static partial class PlayerStateReader {
    // Aurora IW7-Mod commit c0a1c6dacd33c86320b308656d9ba895291c2198
    // https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/game/symbols.hpp
    private const long EntityArrayRva = 0x3D22610;
    private const long ActiveUiRva = 0x2246C30;
    private const long ServerClientsRva = 0x6B22950;
    private const long ServerCountRva = 0x6B229E0;
    private const long MapRestartRva = 0x6B2C9D4;
    private const long LoadScriptsRva = 0x6B2C9D8;
    private const long MigrateRva = 0x6B2C9DC;

    // Asserted entity and server layouts in the same pinned structs.hpp
    // https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/game/structs.hpp
    private const int EntitySize = 0x3F8;
    private const int EntityClientIndexOffset = 0x9C;
    private const int EntityClientOffset = 0x170;
    private const int EntityFlagsOffset = 0x1C8;
    private const int ServerStateOffset = 8;
    private const int ServerEntityOffset = 136;
    private const int ServerNameOffset = 1188;
    private const int ServerGuidOffset = 652096;

    // Captured retail isalive code reads signed entity health at this offset
    // The stricter reader guards do not turn that scalar into local-player life proof
    private const int EntityHealthOffset = 0x1E0;
    private const int ClientConnectedOffset = 0x4A90;
    private const int ClientSessionStateOffset = 0x4A94;
    private const int ClientNameOffset = 0x4BB0;
    private const int ClientIndexOffset = 0x4BDC;
    private const int ClientStateNameOffset = 0x4C50;

    // Four server records and sixteen GUID digits were confirmed in the retail host session
    private const int ExpectedServerCount = 4;
    private const int NameWidth = 32;
    private const int GuidWidth = 21;
    private const int GuidLength = 16;
    private const long MinimumAddress = 0x10000;
    private const long MaximumAddress = 0x7FFFFFFF0000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static PlayerState Read(long moduleBase, int slot, Func<long, int, byte[]?> read) {
        if (read is null) {
            throw new InvalidOperationException("A read callback is required");
        }
        // A roster slot does not identify the local account; every slot keeps the same guards
        if (slot is < 0 or > 3) {
            throw new InvalidOperationException("Only player slots 0 through 3 are supported");
        }
        ValidatePointer(moduleBase, "module base");
        Address(moduleBase, MigrateRva, sizeof(int));

        // Follow each entity's client link instead of assuming a gclient array stride
        var globals = ReadGlobals(moduleBase, read);
        long entity = Address(moduleBase, EntityArrayRva + slot * EntitySize, EntitySize);
        PlayerIdentity identity = ReadIdentity(entity, slot, globals, read);
        ReadScalars(identity, read);

        // Reject transitions and reused links observed during this sequence of reads
        var currentGlobals = ReadGlobals(moduleBase, read);
        if (globals != currentGlobals
            || identity != ReadIdentity(entity, slot, currentGlobals, read)) {
            throw new InvalidOperationException("The session or teammate identity changed during the read");
        }

        // Refresh moving values after the identity checks to reject an observed downed transition
        var scalars = ReadScalars(identity, read);
        // Health one is the GSC laststand value, not proof of every downed state
        // https://github.com/AuroraDoesCode/CODIW-Source/blob/d1a226dbd553229503290cfb73f8051ee3b34749/scripts/scripts/cp/cp_laststand.gsc
        // These guards do not establish an atomic snapshot or an engine spawn generation
        return new PlayerState(identity, scalars.CommandTime, scalars.Health, scalars.Flags);
    }

    private static (int CommandTime, int Health, uint Flags) ReadScalars(PlayerIdentity identity,
        Func<long, int, byte[]?> read) {
        int commandTime = ReadInt32(read, identity.Client);
        int health = ReadInt32(read, Address(identity.Entity, EntityHealthOffset, sizeof(int)));
        uint flags = ReadUInt32(read, Address(identity.Entity, EntityFlagsOffset, sizeof(uint)));
        if (commandTime < 0 || health is <= 1 or > 10000) {
            throw new InvalidOperationException("The teammate is not in a supported living state");
        }
        return (commandTime, health, flags);
    }

    private static (long Clients, int Count, long Table) ReadGlobals(long moduleBase,
        Func<long, int, byte[]?> read) {
        long activeUi = Address(moduleBase, ActiveUiRva, 34);
        if (ReadInt32(read, Address(activeUi, 8, sizeof(int))) != 9
            || ReadExact(read, Address(activeUi, 32, 1), 1)[0] != 0
            || ReadExact(read, Address(activeUi, 33, 1), 1)[0] != 1) {
            throw new InvalidOperationException("The game client is not active in a map");
        }

        // These globals reject observed restart, script load, and migration transitions
        if (ReadInt32(read, Address(moduleBase, MapRestartRva, sizeof(int))) != 0
            || ReadInt32(read, Address(moduleBase, LoadScriptsRva, sizeof(int))) != 0
            || ReadInt32(read, Address(moduleBase, MigrateRva, sizeof(int))) != 0) {
            throw new InvalidOperationException("The server is changing its map or session");
        }
        // Captured retail SV_GameMP_GetGuid indexes one pointer per slot at this image table
        long table = Address(moduleBase, ServerClientsRva, ExpectedServerCount * sizeof(long));
        long clients = ReadInt64(read, table);
        // Keep the first record as the existing shared session anchor, not an array base
        ValidatePointer(clients, "server clients");
        int count = ReadInt32(read, Address(moduleBase, ServerCountRva, sizeof(int)));
        if (count != ExpectedServerCount) {
            throw new InvalidOperationException("The server client count does not match the confirmed layout");
        }
        return (clients, count, table);
    }

    private static PlayerIdentity ReadIdentity(long entity, int slot,
        (long Clients, int Count, long Table) globals, Func<long, int, byte[]?> read, bool requireEnableEligible = true) {
        if (BinaryPrimitives.ReadInt16LittleEndian(ReadExact(read, entity, sizeof(short))) != slot
            || ReadInt32(read, Address(entity, EntityClientIndexOffset, sizeof(int))) != slot) {
            throw new InvalidOperationException("The entity does not belong to the selected slot");
        }
        long client = ReadInt64(read, Address(entity, EntityClientOffset, sizeof(long)));
        ValidatePointer(client, "entity client");

        // Strict capture keeps its original field order and normal-play restrictions
        if (requireEnableEligible) {
            if (ReadInt32(read, Address(client, ClientConnectedOffset, sizeof(int))) != 2
                || ReadInt32(read, Address(client, ClientSessionStateOffset, sizeof(int))) != 0
                || ReadInt32(read, Address(client, ClientIndexOffset, sizeof(int))) != slot
                || ReadInt32(read, Address(client, 4, sizeof(int))) != 0) {
                throw new InvalidOperationException("The selected client is not in normal active play");
            }
        } else if (ReadInt32(read, Address(client, ClientConnectedOffset, sizeof(int))) != 2
            || ReadInt32(read, Address(client, ClientIndexOffset, sizeof(int))) != slot) {
            // Cleanup still requires a connected client that links to the exact selected slot
            throw new InvalidOperationException("The original owned client is no longer connected");
        }
        string name = ReadName(read, Address(client, ClientNameOffset, NameWidth));
        if (name != ReadName(read, Address(client, ClientStateNameOffset, NameWidth))) {
            throw new InvalidOperationException("The two client names do not match");
        }

        // Follow the selected table entry without assuming neighboring records are contiguous
        long serverClient = ReadInt64(read, Address(globals.Table, (long)slot * sizeof(long), sizeof(long)));
        ValidatePointer(serverClient, "selected server client");
        // Check the farthest required field before reading any selected-record contents
        long guidAddress = Address(serverClient, ServerGuidOffset, GuidWidth);
        // The host server record must link back to the same entity and player label
        if (ReadInt32(read, Address(serverClient, ServerStateOffset, sizeof(int))) != 5
            || ReadInt64(read, Address(serverClient, ServerEntityOffset, sizeof(long))) != entity
            || ReadName(read, Address(serverClient, ServerNameOffset, NameWidth)) != name) {
            throw new InvalidOperationException("The host server record does not match the selected teammate");
        }
        string guid = ReadGuid(read, guidAddress);
        // Record equality detects a replaced pointer even when names and GUID text stay equal
        return new PlayerIdentity(slot, entity, client, globals.Clients, globals.Count, name, guid) {
            ServerClient = serverClient
        };
    }

    private static string ReadName(Func<long, int, byte[]?> read, long address) {
        // A name must end within its own field and decode without replacement characters
        byte[] bytes = ReadExact(read, address, NameWidth);
        int end = Array.IndexOf(bytes, (byte)0);
        if (end <= 0) {
            throw new InvalidOperationException("A player name is empty or lacks a bounded terminator");
        }
        string name;
        try {
            name = StrictUtf8.GetString(bytes, 0, end);
        } catch (DecoderFallbackException exception) {
            throw new InvalidOperationException("A player name is not valid UTF-8", exception);
        }
        if (string.IsNullOrWhiteSpace(name)) {
            throw new InvalidOperationException("A player name contains only whitespace");
        }
        foreach (char character in name) {
            if (char.IsControl(character)) {
                throw new InvalidOperationException("A player name contains control characters");
            }
        }
        return name;
    }

    private static string ReadGuid(Func<long, int, byte[]?> read, long address) {
        // GUID text is restricted to the observed fixed length and ASCII hexadecimal form
        byte[] bytes = ReadExact(read, address, GuidWidth);
        if (Array.IndexOf(bytes, (byte)0) != GuidLength) {
            throw new InvalidOperationException("The player GUID must contain sixteen bounded hexadecimal digits");
        }
        for (int index = 0; index < GuidLength; index++) {
            byte character = bytes[index];
            if (!(character is >= (byte)'0' and <= (byte)'9'
                or >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F')) {
                throw new InvalidOperationException("The player GUID contains a non-hexadecimal character");
            }
        }
        return Encoding.ASCII.GetString(bytes, 0, GuidLength);
    }

    private static int ReadInt32(Func<long, int, byte[]?> read, long address) =>
        BinaryPrimitives.ReadInt32LittleEndian(ReadExact(read, address, sizeof(int)));

    private static uint ReadUInt32(Func<long, int, byte[]?> read, long address) =>
        BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(read, address, sizeof(uint)));

    private static long ReadInt64(Func<long, int, byte[]?> read, long address) =>
        BinaryPrimitives.ReadInt64LittleEndian(ReadExact(read, address, sizeof(long)));

    private static byte[] ReadExact(Func<long, int, byte[]?> read, long address, int count) {
        Address(address, 0, count);
        byte[]? bytes;
        try {
            bytes = read(address, count);
        } catch (Exception exception) when (exception is IOException or InvalidOperationException
            or System.ComponentModel.Win32Exception) {
            throw new IOException("The player-state read failed", exception);
        }
        if (bytes is null || bytes.Length != count) {
            throw new IOException("The player-state read did not return the exact requested span");
        }

        // Keep parsing independent of any buffer retained by the callback
        return (byte[])bytes.Clone();
    }

    private static void ValidatePointer(long pointer, string field) {
        if (pointer < MinimumAddress || pointer > MaximumAddress || pointer % 8 != 0) {
            throw new InvalidOperationException($"The {field} pointer is outside supported aligned memory");
        }
    }

    private static long Address(long origin, long offset, int width) {
        // Bounds are checked before any callback receives an address
        if (origin < MinimumAddress || offset < 0 || width <= 0
            || origin > MaximumAddress || offset > MaximumAddress - origin
            || width - 1L > MaximumAddress - origin - offset) {
            throw new InvalidOperationException("The requested player-state span is outside supported memory");
        }
        return origin + offset;
    }
}
