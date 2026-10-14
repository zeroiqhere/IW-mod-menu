using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuInitialOpenRowsTests
{
    public static void RedrawClampsTheRetainedCursorAndBindsFreshPlayerIdentityWithoutAnExtraRevision()
    {
        var clock = new ManualTimeProvider();
        var session = new SessionBoundary();
        session.State = session.State with
        {
            Connected = true,
            CanSelectPlayers = true,
            Players = new[]
            {
                new OverlayPlayer(1, "First"),
                new OverlayPlayer(2, "Second"),
                new OverlayPlayer(3, "Third")
            }
        };
        var navigation = RetainPage(MenuPage.Players, session, 3);
        var controller = new BackendController(42, session, navigation, false, clock: clock);
        controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
        var admitted = controller.Snapshot();
        TestAssert.Equal(3, admitted.SelectedIndex);

        // Fresh redraw must replace the old roster before calculating its fade or cursor
        clock.Advance(TimeSpan.FromMilliseconds(50));
        session.State = session.State with { Players = new[] { new OverlayPlayer(1, "Replacement") } };
        var drawn = controller.Snapshot();
        TestAssert.Equal(2, drawn.Rows.Count);
        TestAssert.Equal(1, drawn.SelectedIndex);
        TestAssert.Equal("Replacement", drawn.Rows[1].Label);
        TestAssert.Equal("Replacement", navigation.Rows[1].Action.ExpectedName);
        TestAssert.Equal(1, navigation.Rows[1].Action.Slot);
        TestAssert.Equal(admitted.MenuRevision!.Value + 1, drawn.MenuRevision);
        TestAssert.Equal(admitted.NavigationRevision + 1, drawn.NavigationRevision);
        TestAssert.Equal(1UL, drawn.MenuOpening!.Revision);
        TestAssert.Equal(0, drawn.MenuOpening.ElapsedMilliseconds);

        // A later identity change replaces only the row presentation, not the opening wait
        clock.Advance(TimeSpan.FromMilliseconds(20));
        session.State = session.State with { Players = new[] { new OverlayPlayer(1, "Renamed") } };
        var renamed = controller.Snapshot();
        TestAssert.Equal(drawn.MenuRevision!.Value + 1, renamed.MenuRevision);
        TestAssert.Equal(drawn.NavigationRevision, renamed.NavigationRevision);
        TestAssert.Equal("Renamed", navigation.Rows[1].Action.ExpectedName);
        TestAssert.True(renamed.MenuOpening is null && renamed.MenuOpenTransition?.Phase == "cooldown",
            "New row identity cancels the old fade without releasing the opening control wait");
        clock.Advance(TimeSpan.FromMilliseconds(530));
        TestAssert.True(controller.Snapshot().MenuOpenTransition is null,
            "Roster changes retain the original redraw plus 550 ms input boundary");
        TestAssert.Equal(0, session.Executed.Count);
    }

    public static void ValueAndCapabilityChangesRetainOpeningOpacityWithoutRestartingEitherTimeline()
    {
        var clock = new ManualTimeProvider();
        var session = new SessionBoundary();
        session.State = session.State with { Connected = true, CanLobbyAmmo = true, LobbyAmmoValue = "OFF" };
        var navigation = RetainPage(MenuPage.AllPlayers, session, 0);
        var controller = new BackendController(42, session, navigation, false, clock: clock);
        controller.Consume(new(1, 1, 42, OverlayInput.Toggle, true), () => true);
        var admitted = controller.Snapshot();

        // A confirmed display value and availability are independent of row structure
        clock.Advance(TimeSpan.FromMilliseconds(50));
        session.State = session.State with { LobbyAmmoValue = "ON", CanLobbyAmmo = false };
        var drawn = controller.Snapshot();
        TestAssert.Equal("ON", drawn.Rows[11].Value);
        TestAssert.True(!drawn.Rows[11].Enabled, "Fresh capability is displayed while input remains paused");
        TestAssert.Equal(admitted.MenuRevision, drawn.MenuRevision);
        TestAssert.Equal(admitted.NavigationRevision, drawn.NavigationRevision);
        TestAssert.Equal(1UL, drawn.MenuOpening!.Revision);

        clock.Advance(TimeSpan.FromMilliseconds(20));
        session.State = session.State with { LobbyAmmoValue = "OFF", CanLobbyAmmo = true };
        var updated = controller.Snapshot();
        TestAssert.Equal("OFF", updated.Rows[11].Value);
        TestAssert.True(updated.Rows[11].Enabled, "Capability updates do not remain cached during opening");
        TestAssert.Equal(drawn.MenuRevision, updated.MenuRevision);
        TestAssert.Equal(drawn.NavigationRevision, updated.NavigationRevision);
        TestAssert.Equal(drawn.MenuOpening.Revision, updated.MenuOpening!.Revision);
        TestAssert.Equal(20, updated.MenuOpening.ElapsedMilliseconds);

        // The larger source inventory keeps its fade separate from the monitor pause
        clock.Advance(TimeSpan.FromMilliseconds(480));
        var faded = controller.Snapshot();
        TestAssert.True(faded.MenuOpening is not null && faded.MenuOpenTransition?.Phase == "cooldown",
            "Value updates neither restart the fade nor prematurely unlock controls");
        clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.True(controller.Snapshot().MenuOpenTransition is null,
            "Value updates retain the original redraw plus 550 ms input boundary");
        clock.Advance(TimeSpan.FromMilliseconds(350));
        TestAssert.True(controller.Snapshot().MenuOpening is null, "The original ten-row fade finishes without a value-change restart");
        TestAssert.Equal(0, session.Executed.Count);
    }

    private static OverlayNavigation RetainPage(MenuPage page, SessionBoundary session, int selected)
    {
        var navigation = new OverlayNavigation();
        navigation.Open();
        navigation.OpenPage(page);
        navigation.SetRows(OverlayMenuBuilder.Build(page, session.State).Rows);
        navigation.SelectIndex(selected);
        navigation.Close();
        return navigation;
    }
}
