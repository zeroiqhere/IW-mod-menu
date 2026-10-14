namespace syndishanx_iw_tool.Players;

public static partial class PlayerStateReader {
    // Ownership reads reuse all layout and identity checks while separating new-enable eligibility
    public static OwnedPlayerFlagsState ReadOwnedFlags(long moduleBase, PlayerIdentity expectedIdentity,
        Func<long, int, byte[]?> read) {
        if (read is null || expectedIdentity is null || expectedIdentity.Slot is < 0 or > 3) {
            throw new InvalidOperationException("A supported original identity and read callback are required");
        }
        ValidatePointer(moduleBase, "module base");
        Address(moduleBase, MigrateRva, sizeof(int));
        var globals = ReadGlobals(moduleBase, read);
        long entity = Address(moduleBase, EntityArrayRva + expectedIdentity.Slot * EntitySize, EntitySize);
        var identity = ReadIdentity(entity, expectedIdentity.Slot, globals, read, requireEnableEligible: false);
        if (identity != expectedIdentity) throw new InvalidOperationException("The original owned player identity changed");
        var first = ReadOwnedScalars(identity, read);

        // Repeated globals and backlinks reject a replacement without borrowing nearby storage
        var currentGlobals = ReadGlobals(moduleBase, read);
        if (globals != currentGlobals || identity != ReadIdentity(entity, identity.Slot, currentGlobals, read, requireEnableEligible: false)) {
            throw new InvalidOperationException("The owned player or server changed during the read");
        }
        // The two samples must not hide a rollback that stays above a retained adapter watermark
        var current = ReadOwnedScalars(identity, read);
        if (current.CommandTime < first.CommandTime) {
            throw new InvalidOperationException("The owned player command time rolled back during the read");
        }
        // Flags and life are the latest copied sample, not an atomic engine snapshot
        return current;
    }

    private static OwnedPlayerFlagsState ReadOwnedScalars(PlayerIdentity identity, Func<long, int, byte[]?> read) {
        int commandTime = ReadInt32(read, identity.Client);
        int health = ReadInt32(read, Address(identity.Entity, EntityHealthOffset, sizeof(int)));
        uint flags = ReadUInt32(read, Address(identity.Entity, EntityFlagsOffset, sizeof(uint)));
        int session = ReadInt32(read, Address(identity.Client, ClientSessionStateOffset, sizeof(int)));
        int movement = ReadInt32(read, Address(identity.Client, 4, sizeof(int)));
        if (commandTime < 0) throw new InvalidOperationException("The owned player command time is invalid");
        bool eligible = health is >= 2 and <= 10000 && session == 0 && movement == 0;
        return new(identity, commandTime, flags, eligible);
    }
}
