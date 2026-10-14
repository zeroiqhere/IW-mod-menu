namespace syndishanx_iw_tool.Players.LocalBinding;

// The owning transport must separately verify the process lifetime and executable fingerprint
public static class LocalAccountSessionReader {
    private const long ControllerRva = 0x2204464;
    private const long AccountRva = 0x779C7A8;
    private const long SelectorRva = 0x6005468;
    private const long FalseSessionRva = 0x6B4E080;
    private const long TrueSessionRva = 0x6B50B30;
    private const long ServerTableRva = 0x6B22950;
    private const long ServerCountRva = 0x6B229E0;
    private const int MemberCount = 18;
    private const int MemberStride = 0x38;
    private const int ServerRecordStride = 715416;
    private static readonly long[] MapTransitionRvas = { 0x6B2C9D4, 0x6B2C9D8, 0x6B2C9DC };

    public static LocalAccountSessionBinding Read(long moduleBase, Func<long, int, byte[]?> read) {
        ArgumentNullException.ThrowIfNull(read);
        ObservedBindingReads.Pointer(moduleBase);
        var fields = new ObservedBindingReads(read);
        // The named PC controller count is one, so only the captured index zero is supported
        int controller = fields.Int32(ObservedBindingReads.Address(moduleBase, ControllerRva, sizeof(int)));
        if (controller != 0) throw new InvalidOperationException("The local controller is outside the supported PC domain");
        ulong account = fields.UInt64(ObservedBindingReads.Address(moduleBase, AccountRva + (long)controller * 0x70, sizeof(ulong)));
        if (account == 0) throw new InvalidOperationException("The local controller has no account identifier");

        // Reproduce the observed branch without assigning an unproved name to its predicate
        long selector = fields.Int64(ObservedBindingReads.Address(moduleBase, SelectorRva, sizeof(long)));
        ObservedBindingReads.Pointer(selector);
        byte branch = fields.Byte(ObservedBindingReads.Address(selector, 0x10, 1));
        long session = ObservedBindingReads.Address(moduleBase, branch == 0 ? FalseSessionRva : TrueSessionRva, 1);
        // Both retail callers require this session byte before using registered members
        if (fields.Byte(ObservedBindingReads.Address(session, 0x10, 1)) == 0) {
            throw new InvalidOperationException("The selected session handle is unavailable");
        }
        ObservedBindingReads.Address(session, (MemberCount - 1L) * MemberStride + 0x68, sizeof(ulong));
        int selectedMember = -1;
        for (int member = 0; member < MemberCount; member++) {
            long entry = ObservedBindingReads.Address(session, (long)member * MemberStride, 1);
            byte registered = fields.Byte(ObservedBindingReads.Address(entry, 0x60, 1));
            if (registered == 0) continue;
            if (fields.UInt64(ObservedBindingReads.Address(entry, 0x68, sizeof(ulong))) != account) continue;
            if (selectedMember >= 0) throw new InvalidOperationException("The local account matches multiple registered members");
            selectedMember = member;
        }
        if (selectedMember < 0) throw new InvalidOperationException("The local account has no registered session member");

        // Map freshness is separate from player life and permission to open a menu
        ReadMapContext(moduleBase, fields);
        int count = fields.Int32(ObservedBindingReads.Address(moduleBase, ServerCountRva, sizeof(int)));
        if (count != 4) throw new InvalidOperationException("The server count does not match the supported domain");
        if (selectedMember >= count) throw new InvalidOperationException("The local member is outside the supported server domain");
        long table = ObservedBindingReads.Address(moduleBase, ServerTableRva, count * sizeof(long));
        long anchor = fields.Int64(table);
        ObservedBindingReads.Pointer(anchor);
        long serverClient = fields.Int64(ObservedBindingReads.Address(table, (long)selectedMember * sizeof(long), sizeof(long)));
        ObservedBindingReads.Pointer(serverClient);
        long expected = ObservedBindingReads.Address(anchor, (long)selectedMember * ServerRecordStride, 1);
        // The retail caller's member domain must divide exactly into this same record index
        if (serverClient < anchor || serverClient != expected
            || (serverClient - anchor) % ServerRecordStride != 0
            || (serverClient - anchor) / ServerRecordStride != selectedMember) {
            throw new InvalidOperationException("The selected server record does not match the session member domain");
        }
        PlayerIdentity identity = LocalLinkedIdentityReader.Read(moduleBase, selectedMember, anchor, count, serverClient, fields);
        // Rechecks reject observed changes but do not establish an atomic engine snapshot
        // A copied result is not returned until every observed source byte has been rechecked
        fields.VerifyUnchanged();
        return new(controller, account, selector, branch, session, selectedMember, identity);
    }

    private static void ReadMapContext(long moduleBase, ObservedBindingReads fields) {
        long ui = ObservedBindingReads.Address(moduleBase, 0x2246C30, 34);
        if (fields.Int32(ObservedBindingReads.Address(ui, 8, sizeof(int))) != 9
            || fields.Byte(ObservedBindingReads.Address(ui, 32, 1)) != 0
            || fields.Byte(ObservedBindingReads.Address(ui, 33, 1)) != 1) {
            throw new InvalidOperationException("The local binding map context is unavailable");
        }
        foreach (long offset in MapTransitionRvas) {
            if (fields.Int32(ObservedBindingReads.Address(moduleBase, offset, sizeof(int))) != 0) {
                throw new InvalidOperationException("The local binding map context is changing");
            }
        }
    }
}
