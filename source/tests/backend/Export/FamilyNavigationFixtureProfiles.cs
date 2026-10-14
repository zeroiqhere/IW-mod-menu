using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// These snapshots cross the real controller without a retail transport or feature write
internal static class FamilyNavigationFixtureProfiles {
    public const int ProfileCount = 3;
    public static void Add(IDictionary<string, StateDocument> fixtures) {
        var session = new SessionBoundary();
        session.State = session.State with { Preview = true };
        var navigation = new OverlayNavigation();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, navigation, false, clock: clock);
        navigation.Open();
        controller.Snapshot();
        navigation.OpenPage(MenuPage.Customization);
        controller.Snapshot();
        navigation.SelectIndex(1);
        navigation.OpenPage(MenuPage.ThemeColors);
        controller.Snapshot();
        navigation.SelectIndex(5);
        // The latest visit replaces only the shared cursor, leaving earlier page-name paths intact
        navigation.OpenPage(MenuPage.Customization);
        controller.Snapshot();
        navigation.SelectIndex(4);
        controller.Consume(new(1, 1, 42, OverlayInput.Back, true), () => true);
        fixtures.Add("navigation-shared-parent-before-back.json", controller.Snapshot());
        controller.Consume(new(2, 1, 42, OverlayInput.Back, true), () => true);
        fixtures.Add("navigation-shared-parent-after-back.json", controller.Snapshot());
        controller.Consume(new(3, 1, 42, OverlayInput.Toggle, true), () => true);
        controller.Consume(new(4, 1, 42, OverlayInput.Toggle, true), () => true);
        MenuOpenTestHarness.Settle(controller, clock);
        fixtures.Add("navigation-shared-parent-after-reopen.json", controller.Snapshot());
        if (session.Executed.Count != 0) throw new InvalidOperationException("Navigation fixture dispatched a game action");
    }
}
