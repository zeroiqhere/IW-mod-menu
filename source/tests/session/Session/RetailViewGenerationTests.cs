using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// A read-only flag callback crosses the real cleanup and reconnect boundary
internal static class RetailViewGenerationTests {
    public static async Task AFrameWhoseOwnerRetiresDuringReadCannotConfirmReplacementFeatureValues() {
        var old = new RecordingRetailTransport();
        var fresh = new RecordingRetailTransport();
        var candidates = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => candidates.Dequeue(), () => false);
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        await session.ExecuteAsync(RetailBackendSessionTests.Select(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? refresh = null;
        old.OnFlagRead = () => {
            old.OnFlagRead = null;entered.TrySetResult();release.Task.GetAwaiter().GetResult();
        };
        try {
            refresh = session.RefreshLocalObservationAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var closing = session.ShutdownAsync();
            var retired = await Task.Run(session.ReadMenuState).WaitAsync(TimeSpan.FromSeconds(5));
            RetailBackendSessionTests.Check(retired.PersonalGodmodeValue == "UNKNOWN"
                && retired.LobbyAmmoValue == "UNKNOWN" && retired.TeammateGodmodeValue == "UNKNOWN",
                "A retired owner confirmed values with replacement metadata");
            RetailBackendSessionTests.Check(!retired.CanConnect && !retired.CanPersonalGodmode
                && !retired.CanLobbyAmmo && !retired.CanSelectPlayers && !retired.CanTeammateGodmode,
                "A mixed-generation frame enabled an action");
            release.TrySetResult();
            await refresh.WaitAsync(TimeSpan.FromSeconds(5));
            RetailBackendSessionTests.Check((await closing.WaitAsync(TimeSpan.FromSeconds(5))).Stopped,
                "The original held worker did not join before owner release");
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            RetailBackendSessionTests.Check(old.Disposed && fresh.LocalFlagWrites == 1 && fresh.HealthWrites == 0,
                "The replacement owner did not retain its own feature values");
            // The next frame comes entirely from the new connection and shows its real ON
            var current = session.ReadMenuState();
            RetailBackendSessionTests.Check(current.Connected && !current.Busy
                && current.PersonalGodmodeValue == "ON" && current.SelectedSlot == -1,
                "The new owner could not publish its own state after the stale frame");
        } finally {
            old.OnFlagRead = null;release.TrySetResult();
            if (refresh is not null) await refresh.WaitAsync(TimeSpan.FromSeconds(5));
            await session.ShutdownAsync();
        }
    }
}
