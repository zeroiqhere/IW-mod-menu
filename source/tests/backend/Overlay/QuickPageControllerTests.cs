using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class QuickPageControllerTests {
    public static void UnknownQuickPageShowsOnlyInertRespawnDespiteConnectedFeatureCapabilities() {
        var state = new SessionBoundary().State with {
            Connected = true, CanPersonalGodmode = true, PersonalGodmodeValue = "ON",
            CanLobbyAmmo = true, LobbyAmmoValue = "ON", CanTeammateGodmode = true
        };
        // Process connection and other feature leases cannot invent a local engine life state
        var snapshot = OverlayMenuBuilder.Build(MenuPage.QuickMenu, state);
        TestAssert.Equal("Quick Menu", snapshot.Title);
        TestAssert.Equal(1, snapshot.Rows.Count);
        TestAssert.Equal("Respawn", snapshot.Rows[0].Label);
        TestAssert.Equal("UNAVAILABLE", snapshot.Rows[0].Value);
        TestAssert.True(!snapshot.Rows[0].Enabled, "An unbound Quick view grants no action");
        TestAssert.Equal(MenuActionKind.None, snapshot.Rows[0].Action.Kind);
    }

    public static void ControllerQuickRootPublishesRootMetadataAndBackClosesWithoutSessionWork() {
        var session = new SessionBoundary();
        var navigation = new OverlayNavigation();
        var controller = new BackendController(42, session, navigation, false);
        // An owned navigation fixture reaches the real view without a native opening route
        navigation.Open(MenuSurface.Quick);
        var frame = controller.Snapshot();
        TestAssert.True(frame.IsOpen && frame.IsRootMenu, "Quick root must use root close semantics");
        TestAssert.Equal("Quick Menu", frame.Title);
        TestAssert.True(frame.Rows.All(row => !row.Enabled), "Unbound production Quick rows remain inert");
        controller.Consume(new(1, 1, 42, OverlayInput.Select, true), () => true);
        TestAssert.Equal(0, session.Executed.Count);
        controller.Consume(new(2, 1, 42, OverlayInput.Back, true), () => true);
        TestAssert.True(!controller.Snapshot().IsOpen, "Back closes this family root");
        TestAssert.Equal(0, session.Executed.Count);
        TestAssert.Equal(0, session.ShutdownCalls);
    }

    public static void MainToggleKeepsItsOwnFamilyAfterAnOwnedQuickViewCloses() {
        var session = new SessionBoundary();
        var navigation = new OverlayNavigation();
        var clock = new ManualTimeProvider();
        var controller = new BackendController(42, session, navigation, false,
            clock: clock, openingActions: QuickOpeningTestSupport.AllCapabilities());
        navigation.Open(MenuSurface.Quick);
        controller.Snapshot();
        controller.Consume(new(1, 1, 42, OverlayInput.Back, true), () => true);
        // The existing Toggle command remains Main and cannot stand in for guarded Quick admission
        controller.Consume(new(2, 1, 42, OverlayInput.Toggle, true), () => true);
        MenuOpenTestHarness.Settle(controller, clock);
        TestAssert.Equal(MenuSurface.Main, navigation.Surface);
        TestAssert.Equal("Apparition", controller.Snapshot().Title);
        TestAssert.Equal(0, session.Executed.Count);
    }
}
