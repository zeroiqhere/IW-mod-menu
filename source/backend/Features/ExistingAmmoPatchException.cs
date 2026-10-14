using System;

namespace syndishanx_iw_tool.Features;

// An observed patch does not give this session ownership of the original bytes
public sealed class ExistingAmmoPatchException : InvalidOperationException {
    public ExistingAmmoPatchException() : base(
        "Infinite Ammo appears to be already active, but this tool session did not record its original bytes. " +
        "Fully exit Infinite Warfare and restart it, then connect before enabling Infinite Ammo.") {
    }
}
