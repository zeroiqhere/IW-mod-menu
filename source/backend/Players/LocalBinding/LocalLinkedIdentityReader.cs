namespace syndishanx_iw_tool.Players.LocalBinding;

// Local identity proof stays separate from strict living teammate feature admission
internal static class LocalLinkedIdentityReader {
    internal static PlayerIdentity Read(long moduleBase, int slot, long anchor, int count,
        long serverClient, ObservedBindingReads fields) {
        long entity = ObservedBindingReads.Address(moduleBase, 0x3D22610 + (long)slot * 0x3F8, 0x3F8);
        // Known record endpoints are checked before any server contents are requested
        long guidAddress = ObservedBindingReads.Address(serverClient, 0x9F340, 21);
        if (fields.Int16(entity) != slot
            || fields.Int32(ObservedBindingReads.Address(entity, 0x9C, sizeof(int))) != slot) {
            throw new InvalidOperationException("The local entity does not match the session slot");
        }
        long client = fields.Int64(ObservedBindingReads.Address(entity, 0x170, sizeof(long)));
        ObservedBindingReads.Pointer(client);
        if (fields.Int32(ObservedBindingReads.Address(client, 0x4A90, sizeof(int))) != 2) {
            throw new InvalidOperationException("The local client is not connected");
        }
        if (fields.Int32(ObservedBindingReads.Address(client, 0x4BDC, sizeof(int))) != slot) {
            throw new InvalidOperationException("The local client does not match the session slot");
        }
        string name = fields.Name(ObservedBindingReads.Address(client, 0x4BB0, 32));
        if (name != fields.Name(ObservedBindingReads.Address(client, 0x4C50, 32))) {
            throw new InvalidOperationException("The local client names do not match");
        }
        if (fields.Int32(ObservedBindingReads.Address(serverClient, 8, sizeof(int))) != 5
            || fields.Int64(ObservedBindingReads.Address(serverClient, 136, sizeof(long))) != entity
            || fields.Name(ObservedBindingReads.Address(serverClient, 1188, 32)) != name) {
            throw new InvalidOperationException("The local server record does not match the linked entity");
        }
        // No health, movement or playing-state scalar is promoted to a life or menu fact
        return new(slot, entity, client, anchor, count, name, fields.Guid(guidAddress)) {
            ServerClient = serverClient
        };
    }
}
