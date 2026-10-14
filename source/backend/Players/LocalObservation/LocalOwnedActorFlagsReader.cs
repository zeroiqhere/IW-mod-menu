using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// Only the already bound owner can decide whether a copied flag word is safe to restore
public static class LocalOwnedActorFlagsReader {
    // These are the same verified fields used by the strict local feature reader
    private const int EntityFlagsOffset = 0x1C8;
    private const int EntityHealthOffset = 0x1E0;
    private const int ClientSessionOffset = 0x4A94;
    private const int ClientMovementOffset = 4;

    public static LocalOwnedActorFlagsSnapshot Read(long moduleBase, Func<long, int, byte[]?> read) {
        ArgumentNullException.ThrowIfNull(read);
        // Full account and actor proof is required even after death or a movement change
        LocalActorSnapshot before = LocalActorSnapshotReader.Read(moduleBase, read);
        var fields = new ObservedBindingReads(read);
        long entity = before.Binding.Identity.Entity;
        long client = before.Binding.Identity.Client;
        int health = fields.Int32(ObservedBindingReads.Address(entity, EntityHealthOffset, sizeof(int)));
        int session = fields.Int32(ObservedBindingReads.Address(client, ClientSessionOffset, sizeof(int)));
        if (health != before.SignedHealth || session != before.NativeSessionState) {
            throw new InvalidOperationException("The local actor life fields changed before ownership inspection");
        }
        long commandTimeAddress = ObservedBindingReads.Address(client, 0, sizeof(int));
        int commandTime = fields.Int32(commandTimeAddress);
        if (commandTime < 0) throw new InvalidOperationException("The local actor command time is unsupported");
        int movement = fields.Int32(ObservedBindingReads.Address(client, ClientMovementOffset, sizeof(int)));
        // Movement and life do not select another actor or authorize enabling during cleanup
        long flagsAddress = ObservedBindingReads.Address(entity, EntityFlagsOffset, sizeof(uint));
        uint flags = unchecked((uint)fields.Int32(flagsAddress));
        LocalActorSnapshot after = LocalActorSnapshotReader.Read(moduleBase, read);
        if (before != after) {
            throw new InvalidOperationException("The complete local actor changed during ownership inspection");
        }

        // Observed consistency is not atomic and cannot identify an engine spawn generation
        int finalCommandTime = fields.VerifyUnchangedCommandTime(commandTimeAddress);
        return new LocalOwnedActorFlagsSnapshot(before, finalCommandTime, movement, flags);
    }
}
