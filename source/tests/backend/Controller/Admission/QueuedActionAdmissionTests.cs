using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Accepted commands retain their private source scope until actual dispatch
internal static class QueuedActionAdmissionTests {
    public static void AQueuedScopedActionCannotBorrowChangedActorPermissionLifeOrNavigation() {
        foreach (string change in new[] { "owner", "permission", "life", "page", "close", "focus" }) {
            var rig = MenuOptionCallbackControllerTests.ReadyQuick(); rig.Send(OverlayInput.Select);
            switch (change) {
                case "owner": rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); break;
                case "permission": rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false); break;
                case "life": rig.Session.Change(LocalPlayerLife.Dead); break;
                case "page": rig.Navigation.OpenPage(MenuPage.History); break;
                case "close": rig.Navigation.Close(); break;
                case "focus": rig.Focus = false; break;
            }
            // The real runner publishes pending state before admitting the work task
            rig.Controller.Snapshot(); rig.Controller.StartQueuedAction();
            TestAssert.Equal(0, rig.Session.Executed.Count);
            TestAssert.True(!rig.Controller.Busy && rig.Controller.Snapshot().MenuOptionCallback is null,
                "A retired queued scope cannot start its callback clock or game action: " + change);
            rig.Controller.StartQueuedAction(); TestAssert.Equal(0, rig.Session.Executed.Count);
        }
    }

    public static void LobbyPermissionLossDuringPendingPublicationRejectsBeforeAmmoDispatch() {
        var rig = new ScopedOpeningFixture(); rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        var frame = rig.Controller.Snapshot();
        int index = frame.Rows.ToList().FindIndex(row => row.Label == "All Players Menu");
        TestAssert.True(index >= 0, "The lobby route must exist in the actual Main menu");
        for (int moved = 0; moved < frame.Rows.Count && rig.Navigation.SelectedIndex != index; moved++) rig.Send(OverlayInput.Down);
        TestAssert.Equal(index, rig.Navigation.SelectedIndex);
        rig.Send(OverlayInput.Select);
        for (int moved = 0; moved < 11; moved++) rig.Send(OverlayInput.Down);
        rig.Send(OverlayInput.Select);
        TestAssert.True(rig.Controller.Busy, "Ammo intent must remain queued before its pending publication");
        rig.Session.Change(LocalPlayerLife.Alive, hasMenu: false);
        TestAssert.True(!rig.Controller.Snapshot().IsOpen, "Pending publication must retire revoked menu permission");
        rig.Controller.StartQueuedAction();
        TestAssert.Equal(0, rig.Session.Executed.Count);
        TestAssert.True(!rig.Controller.Busy && rig.Controller.Snapshot().MenuOptionCallback is null,
            "An actorless retired menu cannot dispatch the lobby ammo adapter");
    }

    public static void AQueuedScopedActionKeepsItsOriginalOwnerAcrossRepeatedPendingSnapshots() {
        var rig = MenuOptionCallbackControllerTests.ReadyQuick(); rig.Send(OverlayInput.Select);
        rig.Controller.Snapshot(); rig.Controller.Snapshot();
        rig.Controller.StartQueuedAction(); rig.Controller.StartQueuedAction();
        TestAssert.Equal(1, rig.Session.Executed.Count);
        TestAssert.Equal("pending", rig.Controller.Snapshot().MenuOptionCallback!.Phase);
    }

    public static void ShutdownAfterQueueAdmissionNeverStartsTheAcceptedGameplayAction() {
        var rig = MenuOptionCallbackControllerTests.ReadyQuick(); rig.Send(OverlayInput.Select);
        rig.Controller.RequestShutdown(); rig.Controller.StartQueuedAction();
        TestAssert.Equal(0, rig.Session.Executed.Count);
        TestAssert.True(rig.Controller.Snapshot().MenuOptionCallback is null,
            "Shutdown must cancel queued work before callback timing can begin");
    }

}
