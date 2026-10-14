using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class LocalMenuAdmissionLifetimeTests {
    public static async Task AnEarlierInflightObservationCannotStandInForTheRequiredFreshRead() {
        await using var fixture = new LocalAdmissionFixture();
        var admission = await fixture.OpenAndObserveAsync();
        var priorEntered = RecordingObservationProvider.Completion();
        var priorRelease = RecordingObservationProvider.Completion();
        var freshEntered = RecordingObservationProvider.Completion();
        var freshRelease = RecordingObservationProvider.Completion();
        fixture.Provider.BeforeRead = async (count, _) => {
            if (count == 2) { priorEntered.TrySetResult(); await priorRelease.Task; }
            if (count == 3) { freshEntered.TrySetResult(); await freshRelease.Task; }
        };
        var prior = fixture.Session.RefreshLocalObservationAsync();
        Task<LocalMenuAdmissionSnapshot?>? validation = null;
        try {
            await priorEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            validation = fixture.Session.ValidateLocalMenuOpeningAsync(admission.Revision);
            Check(!validation.IsCompleted && fixture.Provider.Reads == 2, "Validation skipped the retained prior observation join");
            priorRelease.TrySetResult();
            await freshEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Session.ExecuteAsync(new(MenuActionKind.None)).WaitAsync(TimeSpan.FromSeconds(2));
            Check(!validation.IsCompleted && fixture.Provider.Reads == 3,
                "The earlier read was reused as fresh command validation or held the lifecycle gate");
            freshRelease.TrySetResult();
            var result = await validation.WaitAsync(TimeSpan.FromSeconds(5));
            Check(result?.Revision == admission.Revision && fixture.Transport.ActorReads == 3,
                "Fresh same-actor validation changed the epoch or failed to perform its own copied read");
        } finally {
            priorRelease.TrySetResult();
            freshRelease.TrySetResult();
            await prior.WaitAsync(TimeSpan.FromSeconds(5));
            if (validation is not null) await validation.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    public static async Task CancellationReturnsNullButShutdownStillRetainsTheValidationSourceUntilJoined() {
        await using var fixture = new LocalAdmissionFixture(TimeSpan.FromMilliseconds(30));
        var admission = await fixture.OpenAndObserveAsync();
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion();
        fixture.Provider.BeforeRead = async (_, _) => { entered.TrySetResult(); await release.Task; };
        using var cancellation = new CancellationTokenSource();
        var validation = fixture.Session.ValidateLocalMenuOpeningAsync(admission.Revision, cancellation.Token);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Check(await validation.WaitAsync(TimeSpan.FromSeconds(5)) is null,
                "A canceled caller received an opening receipt");
            var blocked = await fixture.Session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Check(!blocked.Stopped && fixture.Transport.Features.DisposeCalls == 0
                && fixture.Session.ReadLocalMenuAdmission() is null,
                "Shutdown released a live validation source or retained public admission");
        } finally { release.TrySetResult(); }
        fixture.Session.AllowShutdownRetry();
        Check((await fixture.Session.ShutdownAsync()).Stopped && fixture.Transport.Features.DisposeCalls == 1,
            "Cleanup retry did not join the original validation source");
    }

    public static async Task ReconnectNeverRecyclesAnEarlierConnectionAdmissionEpoch() {
        var first = new OwnedActorSourceTransport();
        var second = new OwnedActorSourceTransport();
        var candidates = new Queue<IRetailSessionTransport>([first, second]);
        var session = new RetailBackendSession(4242, () => true, () => candidates.Dequeue(), () => false,
            transport => new OwnedLocalPlayerObservationProvider(4242, (ILocalActorSnapshotSource)transport));
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            var original = session.ReadLocalMenuAdmission() ?? throw new InvalidOperationException("Original admission missing");
            await session.ShutdownAsync();
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            var current = session.ReadLocalMenuAdmission();
            Check(current is not null && current.Revision > original.Revision
                && current.Observation.Identity == original.Observation.Identity,
                "A new connection reused an old epoch with the same displayed actor");
            Check(await session.ValidateLocalMenuOpeningAsync(original.Revision) is null && second.ActorReads == 1,
                "A retired connection receipt validated against its replacement source");
        } finally { await session.ShutdownAsync(); }
    }
    public static async Task ADelayedValidationCompletionCannotAuthorizeAReconnectedOwner() {
        var first = new OwnedActorSourceTransport();
        var second = new OwnedActorSourceTransport();
        var providers = new Dictionary<IRetailSessionTransport, LocalAdmissionFixture.ReceiptProvider> {
            [first] = new(new OwnedLocalPlayerObservationProvider(4242, first)),
            [second] = new(new OwnedLocalPlayerObservationProvider(4242, second))
        };
        var candidates = new Queue<IRetailSessionTransport>([first, second]);
        var session = new RetailBackendSession(4242, () => true, () => candidates.Dequeue(), () => false,
            transport => providers[transport]);
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion();
        var context = new HeldContinuationContext();
        Task<LocalMenuAdmissionSnapshot?>? validation = null;
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            var original = session.ReadLocalMenuAdmission() ?? throw new InvalidOperationException("Original admission missing");
            providers[first].BeforeRead = async (_, _) => {
                entered.TrySetResult();
                await release.Task;
            };
            // The real source completes, but its outer session continuation is held after the await
            _ = context.Start(() => validation = session.ValidateLocalMenuOpeningOnWorkerAsync(original.Revision));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            release.TrySetResult();
            await context.WaitForPostAsync();
            await session.ShutdownAsync();
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            var replacement = session.ReadLocalMenuAdmission();
            await context.DrainAsync(validation!);
            Check(await validation! is null && replacement is not null && replacement.Revision > original.Revision
                && !ReferenceEquals(replacement.OwnerToken, original.OwnerToken),
                "An old validation completion authorized the replacement connection");
        } finally {
            release.TrySetResult();
            if (validation is not null && !validation.IsCompleted) await context.DrainAsync(validation);
            await session.ShutdownAsync();
        }
    }

}
