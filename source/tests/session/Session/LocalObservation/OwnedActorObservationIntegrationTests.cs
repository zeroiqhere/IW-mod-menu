using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// The actual session owns the compiled reader, provider and permission lifetime together
internal static class OwnedActorObservationIntegrationTests {
    private static readonly MenuAction Connect = new(MenuActionKind.Connect);
    private static void Check(bool value, string reason) => RetailBackendSessionTests.Check(value, reason);
    private static ILocalPlayerObservationProvider? Provider(IRetailSessionTransport transport) =>
        transport is ILocalActorSnapshotSource source ? new OwnedLocalPlayerObservationProvider(transport.ProcessId, source) : null;

    public static async Task CompiledReaderPublishesCurrentActorAndLifeWithoutFeatureWrites() {
        var transport = new OwnedActorSourceTransport();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false, Provider);
        await session.ExecuteAsync(Connect);
        await session.RefreshLocalObservationAsync();
        var observed = session.ReadLocalObservation();
        Check(observed.ProcessId == 4242 && observed.Identity?.Slot == 0 && observed.Life == LocalPlayerLife.Alive,
            "The actual session failed to publish the compiled reader's bound actor");
        Check(observed.HasMenu == true && observed.SessionHostRole == true && observed.ControlsDisabled is null,
            "Menu-owned host permission and native session role must remain distinct from controller locks");
        transport.Actor.Memory.Int32(transport.Actor.HealthAddress, 0);
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation().Life == LocalPlayerLife.Dead && session.ReadLocalObservation().HasMenu == true,
            "A dead actor must retain source menu permission without becoming Alive");
        Check(transport.ActorReads == 2 && transport.Features.HealthWrites == 0 && transport.Features.AmmoWrites == 0
            && transport.Features.FlagWrites == 0, "Read-only observations invoked a feature write");
        Check((await session.ShutdownAsync()).Stopped && transport.Features.DisposeCalls == 1,
            "The session did not release its one source after stopping reads");
    }

    public static async Task TransitionFailureRevokesFactsAndReconnectCannotBorrowOldPermission() {
        var first = new OwnedActorSourceTransport();
        var second = new OwnedActorSourceTransport { Actor = new LocalActorMemoryFixture(host: false) };
        var queue = new Queue<IRetailSessionTransport>([first, second]);
        var session = new RetailBackendSession(4242, () => true, () => queue.Dequeue(), () => false, Provider);
        await session.ExecuteAsync(Connect);
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation().HasMenu == true, "The first actor must initialize Host permission");
        first.Actor.Memory.Int32(LocalBindingMemoryFixture.ModuleBase + 0x6B2C9D8, 1);
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation().Identity is null && session.ReadLocalObservation().HasMenu is null,
            "A loading-map read must revoke published actor facts");
        Check((await session.ShutdownAsync()).Stopped, "The first source did not stop");
        await session.ExecuteAsync(Connect);
        await session.RefreshLocalObservationAsync();
        Check(session.ReadLocalObservation().Identity is not null && session.ReadLocalObservation().SessionHostRole == false
            && session.ReadLocalObservation().HasMenu is null, "A new connection inherited the old actor's private permission");
        await session.ShutdownAsync();
        Check(first.Features.DisposeCalls == 1 && second.Features.DisposeCalls == 1, "Connection sources were released incorrectly");
    }

    public static async Task BlockedCopiedReadRetainsSourceUntilShutdownCanJoinIt() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var transport = new OwnedActorSourceTransport { BeforeActorRead = () => { entered.TrySetResult(); release.Wait(); } };
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false, Provider,
            TimeSpan.FromMilliseconds(50));
        await session.ExecuteAsync(Connect);
        Task read = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var stopped = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check(!stopped.Stopped && transport.Features.DisposeCalls == 0,
                "A source was released while its actual provider still held a copied read");
        } finally { release.Set(); }
        await read.WaitAsync(TimeSpan.FromSeconds(3));
        Check(session.ReadLocalObservation().Identity is null, "Canceled read completion resurrected an actor");
        session.AllowShutdownRetry();
        Check((await session.ShutdownAsync()).Stopped && transport.Features.DisposeCalls == 1,
            "Cleanup retry failed to join and release the retained source");
    }
}
