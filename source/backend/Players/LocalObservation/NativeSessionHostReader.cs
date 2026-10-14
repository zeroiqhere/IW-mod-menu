using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// The supplied session and member must already belong to the verified current local binding
// This replica reports Session_IsHost, not actor-method overrides or native menu permission
public static class NativeSessionHostReader {
    private const long FirstPartyRva = 0x524C4A0;
    private const long SecondPartyRva = 0x5254DA0;
    private const int InPartyOffset = 0x2D10;
    private const int CapturedHostMemberOffset = 0x2C50;
    private const int LastSupportedServerMember = 3;

    // This convenience path owns one copied role-field consistency pass
    public static bool Read(long moduleBase, long verifiedSession, int verifiedMemberIndex,
        Func<long, int, byte[]?> read) {
        ArgumentNullException.ThrowIfNull(read);
        var fields = new ObservedBindingReads(read);
        bool result = Read(moduleBase, verifiedSession, verifiedMemberIndex, fields);
        // No result escapes after an observed party or host field changes
        fields.VerifyUnchanged();
        return result;
    }

    // The composing actor reader owns the final shared consistency pass
    internal static bool Read(long moduleBase, long verifiedSession, int verifiedMemberIndex,
        ObservedBindingReads fields) {
        ArgumentNullException.ThrowIfNull(fields);
        ObservedBindingReads.Pointer(moduleBase);
        ObservedBindingReads.Pointer(verifiedSession);
        if (verifiedMemberIndex is < 0 or > LastSupportedServerMember) {
            throw new InvalidOperationException("The verified local member is outside the supported server domain");
        }

        // Bound both complete fixed-party spans before any callback observes a field
        long firstParty = PartyAddress(moduleBase, FirstPartyRva);
        long secondParty = PartyAddress(moduleBase, SecondPartyRva);
        long matchedParty = 0;
        // The original lookup gives the first matching party priority
        if (fields.Int64(firstParty) == verifiedSession) matchedParty = firstParty;
        else if (fields.Int64(secondParty) == verifiedSession) matchedParty = secondParty;

        if (matchedParty != 0 && fields.Int32(ObservedBindingReads.Address(
            matchedParty, InPartyOffset, sizeof(int))) != 0) {
            // This exact captured offset must not be replaced by a different published host layout
            int host = fields.Int32(ObservedBindingReads.Address(matchedParty, CapturedHostMemberOffset, sizeof(int)));
            return verifiedMemberIndex == host;
        }

        // The native fallback compares the supplied member, without selecting or authenticating it
        return verifiedMemberIndex == 0;
    }

    private static long PartyAddress(long moduleBase, long partyRva) {
        long party = ObservedBindingReads.Address(moduleBase, partyRva, sizeof(long));
        ObservedBindingReads.Address(party, InPartyOffset, sizeof(int));
        ObservedBindingReads.Address(party, CapturedHostMemberOffset, sizeof(int));
        return party;
    }
}
