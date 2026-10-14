using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// The owning transport must separately verify the executable and process lifetime
public static class LocalActorSnapshotReader {
    public static LocalActorSnapshot Read(long moduleBase, Func<long, int, byte[]?> read) {
        ArgumentNullException.ThrowIfNull(read);
        // Neither scalar address is used until the complete account and actor binding succeeds
        LocalAccountSessionBinding before = LocalAccountSessionReader.Read(moduleBase, read);
        var fields = new ObservedBindingReads(read);
        long healthAddress = ObservedBindingReads.Address(before.Identity.Entity, 0x1E0, sizeof(int));
        long sessionAddress = ObservedBindingReads.Address(before.Identity.Client, 0x4A94, sizeof(int));
        int health = fields.Int32(healthAddress);
        int session = fields.Int32(sessionAddress);
        // Native host fields join the same copied consistency pass as both life scalars
        bool isSessionHost = NativeSessionHostReader.Read(moduleBase, before.Session, before.MemberIndex, fields);

        LocalAccountSessionBinding after = LocalAccountSessionReader.Read(moduleBase, read);
        if (before != after) {
            throw new InvalidOperationException("The local actor binding changed during the snapshot read");
        }
        // Equality and final rechecks reject observed changes but are not an atomic engine snapshot
        fields.VerifyUnchanged();
        // Unsupported session states remain Unknown even when signed health is zero
        return new LocalActorSnapshot(before, health, session, isSessionHost);
    }
}
