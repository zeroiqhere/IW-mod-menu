using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Overlay.Players;

// This private intent binds a menu row to one owned whole-lobby observation
public sealed class CohortGodmodeTarget {
    internal object OwnerToken { get; }
    internal PlayerCohortCapture Capture { get; }
    internal LocalAccountSessionBinding Binding { get; }

    internal CohortGodmodeTarget(object ownerToken, PlayerCohortCapture capture, LocalAccountSessionBinding binding) {
        OwnerToken = ownerToken;
        Capture = capture;
        Binding = binding;
    }
}
