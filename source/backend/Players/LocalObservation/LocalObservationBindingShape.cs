using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// These checks keep copied account binding shapes consistent at both receipt boundaries
internal static class LocalObservationBindingShape {
    internal static bool IsSupported(LocalAccountSessionBinding binding) {
        var identity = binding.Identity;
        const long maximumAddress = 0x7FFFFFFF0000;
        static bool Pointer(long address) => address is >= 0x10000 and <= maximumAddress && address % 8 == 0;
        // Shape checks do not replace the source's complete account and backlink verification
        if (binding.Controller != 0 || binding.AccountId == 0 || binding.MemberIndex is < 0 or > 3
            || identity is null || identity.Slot != binding.MemberIndex || identity.ServerCount != 4
            || !Pointer(binding.SelectorPointer) || !Pointer(binding.Session)
            || !Pointer(identity.ServerClients) || !Pointer(identity.ServerClient)) return false;
        long recordOffset = (long)binding.MemberIndex * 715416;
        return recordOffset <= maximumAddress - identity.ServerClients
            && identity.ServerClient == identity.ServerClients + recordOffset;
    }
}
