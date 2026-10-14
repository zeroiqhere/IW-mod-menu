using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class RetailAmmoCleanupStatusTests {
    public static async Task AmmoCleanupUnconfirmedRestorationExplainsRestartAndPreservesItAcrossCleanupRetry() {
        var transport = new RecordingRetailTransport();
        transport.OnAmmoRead = () => {
            // The real enable applies its patch, but confirmation and original restoration cannot read it
            if (transport.AmmoReadCalls >= 3) throw new IOException("Original ammo instruction could not be confirmed");
        };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo)).WaitAsync(TimeSpan.FromSeconds(5));
            var frame = session.ReadMenuState();
            Check(!frame.Connected && !frame.CanConnect && !frame.CanLobbyAmmo
                && transport.DisposeCalls == 1 && transport.AmmoWrites == 1,
                "Unconfirmed original restoration admitted another connection or feature write");
            Check(frame.LobbyAmmoStatus.Contains("not confirmed", StringComparison.OrdinalIgnoreCase)
                && frame.LobbyAmmoStatus.Contains("Fully exit Infinite Warfare", StringComparison.Ordinal)
                && frame.RecentMessages.Contains(frame.LobbyAmmoStatus, StringComparison.Ordinal),
                "The blocked ammo control did not explain unconfirmed cleanup and the required full game restart");
            var row = OverlayMenuBuilder.Build(MenuPage.AllPlayers, frame).Rows[11];
            Check(!row.Enabled && row.Detail.Contains(frame.LobbyAmmoStatus, StringComparison.Ordinal),
                "The visible lobby ammo detail hid its required recovery step");
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo));
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
            frame = session.ReadMenuState();
            Check(!frame.CanConnect && frame.LobbyAmmoStatus.Contains("Fully exit Infinite Warfare", StringComparison.Ordinal)
                && transport.OpenCalls == 1 && transport.AmmoWrites == 1 && transport.DisposeCalls == 1,
                "An empty cleanup retry erased the restart latch or reused the closed native source");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task AmmoCleanupConfirmedRestorationKeepsReconnectAvailableWithoutAFalseRestartWarning() {
        var transport = new RecordingRetailTransport { RejectAmmoWrites = true };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo)).WaitAsync(TimeSpan.FromSeconds(5));
            var frame = session.ReadMenuState();
            Check(!frame.Connected && frame.CanConnect && frame.LobbyAmmoStatus == "Not connected"
                && !frame.RecentMessages.Contains("Fully exit Infinite Warfare", StringComparison.Ordinal)
                && transport.DisposeCalls == 1 && transport.AmmoWrites == 1,
                "Confirmed cleanup incorrectly latched a restart requirement or withheld reconnection");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task AmmoCleanupIncompleteStopExplainsRetryAndRetainsTheOriginalSourceUntilItSucceeds() {
        var transport = new RecordingRetailTransport { RejectAmmoWrites = true, DisposeThrows = true };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo)).WaitAsync(TimeSpan.FromSeconds(5));
            var frame = session.ReadMenuState();
            Check(!frame.CanConnect && !frame.CanLobbyAmmo && !transport.Disposed,
                "Incomplete cleanup released the original source or admitted another feature");
            Check(frame.LobbyAmmoStatus.Contains("Cleanup has not finished", StringComparison.Ordinal)
                && frame.LobbyAmmoStatus.Contains("Select Exit to retry cleanup", StringComparison.Ordinal)
                && !frame.LobbyAmmoStatus.Contains("Fully exit Infinite Warfare", StringComparison.Ordinal)
                && frame.RecentMessages.Contains(frame.LobbyAmmoStatus, StringComparison.Ordinal),
                "Incomplete cleanup hid its retry route or incorrectly required a full game restart");
            var exit = OverlayMenuBuilder.Build(MenuPage.Main, frame).Rows.Last();
            Check(exit.Label == "Exit" && exit.Enabled && exit.Action.Kind == MenuActionKind.Exit,
                "Cleanup guidance named a control absent from the actual root menu");
            transport.DisposeThrows = false;
            session.AllowShutdownRetry();
            Check((await session.ShutdownAsync()).Stopped && session.ReadMenuState().CanConnect
                && transport.DisposeCalls == 2 && transport.AmmoWrites == 1,
                "Cleanup retry could not release the retained original source without another patch write");
        } finally {
            transport.DisposeThrows = false;
            session.AllowShutdownRetry();
            await session.ShutdownAsync();
        }
    }
}
