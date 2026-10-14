using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// The owning transport must separately verify the executable and process lifetime
public static class LocalActorFlagsReader {
    // Pinned IW7 layouts place entity flags at 456 and player-state movement at four
    // https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/game/structs.hpp
    private const int EntityFlagsOffset = 0x1C8;
    // Captured retail actor helpers independently prove these health and session offsets
    private const int EntityHealthOffset = 0x1E0;
    private const int ClientSessionOffset = 0x4A94;
    private const int ClientMovementOffset = 4;

    public static LocalActorFlagsSnapshot Read(long moduleBase, Func<long, int, byte[]?> read) {
        ArgumentNullException.ThrowIfNull(read);
        // Every slot, including zero, comes from the complete local account and member proof
        LocalActorSnapshot before = LocalActorSnapshotReader.Read(moduleBase, read);
        if (before.NativeSessionState != 0 || before.SignedHealth is < 2 or > 10000) {
            throw new InvalidOperationException("The local actor is outside supported normal feature play");
        }

        var fields = new ObservedBindingReads(read);
        long entity = before.Binding.Identity.Entity;
        long client = before.Binding.Identity.Client;
        int health = fields.Int32(ObservedBindingReads.Address(entity, EntityHealthOffset, sizeof(int)));
        int session = fields.Int32(ObservedBindingReads.Address(client, ClientSessionOffset, sizeof(int)));
        if (health != before.SignedHealth || session != before.NativeSessionState) {
            throw new InvalidOperationException("The local actor life fields changed before feature admission");
        }
        long commandTimeAddress = ObservedBindingReads.Address(client, 0, sizeof(int));
        int commandTime = fields.Int32(commandTimeAddress);
        int movement = fields.Int32(ObservedBindingReads.Address(client, ClientMovementOffset, sizeof(int)));
        // Reject unsupported movement or time before reading the entity flag word
        if (commandTime < 0 || movement != 0) {
            throw new InvalidOperationException("The local actor is not in supported normal movement");
        }
        // Reinterpret the signed layout field without losing its high flag bits
        long flagsAddress = ObservedBindingReads.Address(entity, EntityFlagsOffset, sizeof(uint));
        uint flags = unchecked((uint)fields.Int32(flagsAddress));
        LocalActorSnapshot after = LocalActorSnapshotReader.Read(moduleBase, read);
        if (before != after) {
            throw new InvalidOperationException("The complete local actor changed during the feature read");
        }

        // The final copied-field pass also covers a life change after the second actor proof
        // Rechecks are not an atomic engine snapshot or a replacement for bound write validation
        int finalCommandTime = fields.VerifyUnchangedCommandTime(commandTimeAddress);
        return new LocalActorFlagsSnapshot(before, finalCommandTime, movement, flags);
    }
}
