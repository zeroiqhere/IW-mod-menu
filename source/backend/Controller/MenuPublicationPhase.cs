using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Stable keys distinguish added or retired fades without publishing every owner poll
internal static class MenuPublicationPhase {
    // Callback identity separates consecutive waits even when their displayed phase is equal
    public static string Callback(MenuOptionCallback callback) => "callback:" + callback.Kind + ":" + callback.Phase
        + ":" + callback.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Closed-root Back retains the same publication identity until its finite wait retires
    public static string? ComposeMonitor(string? primary, MenuMonitorWait? wait) => wait is null ? primary
        : (primary is null ? "" : primary + "+") + "monitor:" + wait.Kind + ":"
            + wait.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // A retired ready receipt must publish promptly even after its callback clock ended
    public static string? ComposeUse(string? primary, MenuUseContinuation? receipt) => receipt is null ? primary
        : (primary is null ? "" : primary + "+") + "use:" + receipt.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Waiting and ready receipts publish independently of changing feature display values
    public static string? ComposeMechanical(string? primary, MenuMonitorContinuation? receipt) => receipt is null ? primary
        : (primary is null ? "" : primary + "+") + "mechanical:" + receipt.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string? Compose(string? primary, MenuOpening? opening) => opening is null ? primary
        : (primary is null ? "" : primary + "+") + "opening:" + opening.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
