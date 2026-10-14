using Apparition.Backend.Session;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.SessionLifecycle;

namespace Features.Tests.Session;

// Cleanup runs against the same real flag owner and shared shutdown coordinator
internal static class TeammateGodmodeCleanupTests {
    public static async Task UnconfirmedTeammateRestoreRetainsResourceAndStillAttemptsAmmoCleanup() {
        var identity = new PlayerIdentity(1, 0x10000, 0x20000, 0x30000, 4, "Friend", "0000000000000001");
        var transport = new RecordingFlagsTransport();
        var session = new TeammateGodmodeSession(_ => new(identity, 100, 100, transport.Flags),
            _ => new FlagsPlayerGodmodeFeature(transport, TimeSpan.FromSeconds(10)));
        await session.SelectAsync(1, identity.Name);
        await session.ToggleAsync(1, identity.Name);
        var cleanup = new TeammateGodmodeCleanup(session);
        int ammoAttempts = 0, releases = 0;
        SessionShutdown CreateShutdown() => new(new Func<Task>[] { cleanup.StopAsync }, () => {
            // Independent ammo cleanup is attempted even if teammate restoration fails
            ammoAttempts++;
            return Task.FromResult(cleanup.RestoreConfirmed);
        }, () => releases++);
        try {
            transport.OnWrite = (_, _, _) => false;
            var failed = await CreateShutdown().ShutdownAsync();
            TestAssert.Equal(false, failed.Stopped);
            TestAssert.Equal(false, failed.Restored);
            TestAssert.Equal(false, cleanup.RestoreConfirmed);
            TestAssert.Equal(1, ammoAttempts);
            TestAssert.Equal(0, releases);
            TestAssert.Equal(0x201U, transport.Flags);
            TestAssert.Equal(true, session.ReadView().HasPendingCleanup);
            // Retry uses the retained original owner after the guarded write recovers
            transport.OnWrite = null;
            var recovered = await CreateShutdown().ShutdownAsync();
            TestAssert.Equal(true, recovered.Stopped);
            TestAssert.Equal(true, recovered.Restored);
            TestAssert.Equal(true, cleanup.RestoreConfirmed);
            TestAssert.Equal(2, ammoAttempts);
            TestAssert.Equal(1, releases);
            TestAssert.Equal(0x200U, transport.Flags);
            TestAssert.Equal(false, session.ReadView().HasPendingCleanup);
        } finally {
            transport.OnWrite = null;
            await session.CloseAsync();
        }
    }

    public static async Task EmptyTeammateOwnerAllowsIndependentShutdownWithoutAFabricatedFailure() {
        var cleanup = new TeammateGodmodeCleanup(null);
        int ammoAttempts = 0, releases = 0;
        var shutdown = new SessionShutdown(new Func<Task>[] { cleanup.StopAsync }, () => {
            ammoAttempts++;
            return Task.FromResult(cleanup.RestoreConfirmed);
        }, () => releases++);
        var result = await shutdown.ShutdownAsync();
        TestAssert.Equal(true, result.Stopped);
        TestAssert.Equal(true, result.Restored);
        TestAssert.Equal(1, ammoAttempts);
        TestAssert.Equal(1, releases);
    }
}
