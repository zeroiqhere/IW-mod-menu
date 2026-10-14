using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;

namespace Session.Tests;

internal static class PlayerSelectionRecoveryTests {
    public static async Task ManualRefreshKeepsFreshOwnedOffRouteForNonplayingTeammate() {
        var transport = new PlayerStateReaderRetailTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(PlayerSelection(1, session));
            var original = session.ReadMenuState().SelectedPlayerTarget;
            RetailBackendSessionTests.Check(original?.Route == PlayerMenuRoute.Teammate,
                "The production selection reader did not bind the initial teammate");

            var initialRow = GodmodeRow(session);
            await session.ExecuteAsync(initialRow.Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201,
                "The actual Basic Scripts row did not enable the owned teammate");

            transport.SetSessionState(1, 1);
            int strictReadsBeforeRefresh = transport.StrictReadCalls(1);
            int ownedReadsBeforeRefresh = transport.OwnedReadCalls(1);
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));

            var refreshed = session.ReadMenuState();
            var offRow = GodmodeRow(session);
            var playerRows = OverlayMenuBuilder.Build(MenuPage.Players, refreshed).Rows;
            var recoveryRow = playerRows.Single(row => row.Label == "Return to cleanup: " + original!.Identity.Name);
            var reopenedOptionsRow = OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, refreshed).Rows
                .Single(row => row.Label == "Basic Scripts");
            RetailBackendSessionTests.Check(!refreshed.Players.Any(player => player.Slot == 1)
                && refreshed.SelectedPlayerTarget?.Identity == original!.Identity
                && offRow.Enabled && offRow.Value == "ON"
                && offRow.Action.Page == MenuPage.SelectedBasicScripts
                && recoveryRow.Enabled && recoveryRow.Action.Kind == MenuActionKind.OpenPage
                && recoveryRow.Action.Page == MenuPage.SelectedPlayer
                && !playerRows.Any(row => row.Action.Kind == MenuActionKind.SelectPlayer && row.Action.Slot == 1)
                && reopenedOptionsRow.Enabled && reopenedOptionsRow.Action.Page == MenuPage.SelectedBasicScripts
                && transport.OwnedReadCalls(1) > ownedReadsBeforeRefresh
                && transport.StrictReadCalls(1) == strictReadsBeforeRefresh
                && transport.TeammateBindCalls == 1,
                "The fresh owned cleanup route was not preserved in Basic Scripts or reachable again from Players");

            int writesBeforeOff = transport.FlagWrites;
            await session.ExecuteAsync(offRow.Action);
            var afterOff = session.ReadMenuState();
            var disabledRow = GodmodeRow(session);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x200
                && transport.FlagWrites == writesBeforeOff + 1
                && afterOff.SelectedPlayerTarget?.Identity == original!.Identity
                && !disabledRow.Enabled && disabledRow.Value == "UNKNOWN" && transport.HealthWrites == 0,
                "OFF did not use the retained original owner, or strict living eligibility remained enabled afterward");

            int strictReadsBeforeStaleAction = transport.StrictReadCalls(1);
            await session.ExecuteAsync(offRow.Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x200
                && transport.FlagWrites == writesBeforeOff + 1
                && transport.StrictReadCalls(1) > strictReadsBeforeStaleAction
                && !session.ReadMenuState().CanRecoverSelectedTeammate,
                "A stale pre-OFF Basic Scripts row re-enabled a nonplaying teammate");
        } finally {
            await session.ShutdownAsync();
        }
    }

    public static async Task ManualRefreshWithdrawsRouteForSameNameReplacementWithoutRebinding() {
        var transport = new PlayerStateReaderRetailTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(PlayerSelection(1, session));
            await session.ExecuteAsync(GodmodeRow(session).Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201 && transport.TeammateBindCalls == 1,
                "The replacement test did not establish one original active owner");

            transport.SetGuid(1, "ABCDEF0123456789");
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            var withdrawn = session.ReadMenuState();
            var row = GodmodeRow(session);
            int writes = transport.FlagWrites;
            int strictReads = transport.StrictReadCalls(1);
            await session.ExecuteAsync(PlayerSelection(1, session));

            RetailBackendSessionTests.Check(withdrawn.Players.Any(player => player.Slot == 1)
                && withdrawn.SelectedPlayerTarget is null && !withdrawn.CanRecoverSelectedTeammate && !row.Enabled
                && transport.Flags(1) == 0x201 && transport.FlagWrites == writes
                && transport.TeammateBindCalls == 1 && transport.StrictReadCalls(1) == strictReads
                && transport.HealthWrites == 0,
                "A same-name replacement retained the old route or fell through to strict capture and a new owner");
        } finally {
            await session.ShutdownAsync();
        }
    }

    public static async Task ManualRefreshWithdrawsRouteWhenOriginalOwnedClientBecomesUnreadable() {
        var transport = new PlayerStateReaderRetailTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(PlayerSelection(1, session));
            await session.ExecuteAsync(GodmodeRow(session).Action);
            RetailBackendSessionTests.Check(transport.Flags(1) == 0x201 && transport.TeammateBindCalls == 1,
                "The unreadable-client test did not establish one original active owner");

            transport.SetConnectedState(1, 0);
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            var withdrawn = session.ReadMenuState();
            var row = GodmodeRow(session);
            int writes = transport.FlagWrites;
            await session.ExecuteAsync(new(MenuActionKind.SelectPlayer, Slot: 1,
                ExpectedName: PlayerStateMemoryFixture.Name(1)));

            RetailBackendSessionTests.Check(!withdrawn.Players.Any(player => player.Slot == 1)
                && withdrawn.SelectedPlayerTarget is null && !withdrawn.CanRecoverSelectedTeammate && !row.Enabled
                && transport.Flags(1) == 0x201 && transport.FlagWrites == writes
                && transport.TeammateBindCalls == 1 && transport.HealthWrites == 0,
                "An unreadable original actor kept a cleanup route or authorized another binding");
        } finally {
            await session.ShutdownAsync();
        }
    }

    private static MenuAction PlayerSelection(int slot, RetailBackendSession session) =>
        OverlayMenuBuilder.Build(MenuPage.Players, session.ReadMenuState()).Rows
            .Single(row => row.Action.Kind == MenuActionKind.SelectPlayer && row.Action.Slot == slot).Action;

    private static MenuRow GodmodeRow(RetailBackendSession session) =>
        OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, session.ReadMenuState()).Rows
            .Single(row => row.Label == "God Mode");

}
