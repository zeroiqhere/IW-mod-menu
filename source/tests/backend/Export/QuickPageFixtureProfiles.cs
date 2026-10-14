using Apparition.Backend.Ipc;
using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// This owned fixture publishes the controller's unavailable Quick root
internal static class QuickPageFixtureProfiles {
    public const int ProfileCount = 1;
    public static void Add(IDictionary<string, StateDocument> fixtures) {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var navigation = new OverlayNavigation();
        var controller = new BackendController(42, session, navigation, false);
        // Direct owned navigation exercises display only, without a native opening command
        navigation.Open(MenuSurface.Quick);
        fixtures.Add("quick-root-unbound.json", controller.Snapshot());
        if (session.Executed.Count != 0) throw new InvalidOperationException("Quick fixture dispatched a game action");
    }
}
