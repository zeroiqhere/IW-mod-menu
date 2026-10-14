using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class LocalMenuAdmissionTests {
    public static async Task CompleteOwnedObservationPublishesCachedAdmissionWithoutFurtherReads() {
        await using var fixture = new LocalAdmissionFixture();
        var admission = await fixture.OpenAndObserveAsync();
        Check(admission.Revision > 0 && admission.Observation.Life == LocalPlayerLife.Alive
            && admission.Observation.HasMenu == true, "The admission does not retain verified actor facts");
        int reads = fixture.Provider.Reads;
        fixture.Provider.BeforeRead = (_, _) => throw new IOException("Cached admission cannot query the provider");
        for (int index = 0; index < 1000; index++) {
            Check(fixture.Session.ReadLocalMenuAdmission() == admission, "A cached read changed admission without a source observation");
        }
        Check(fixture.Provider.Reads == reads && fixture.Transport.ActorReads == 1,
            "Cached menu admission performed a source read");
    }

    public static async Task FullAccountReplacementWithTheSamePlayerIdentityRejectsOldAdmission() {
        await using var fixture = new LocalAdmissionFixture();
        var original = await fixture.OpenAndObserveAsync();
        var memory = fixture.Transport.Actor.Memory;
        memory.UInt64(LocalBindingMemoryFixture.AccountAddress, LocalBindingMemoryFixture.Account + 1);
        // Replace the account and selected lobby while retaining the same server/entity identity
        memory.Registered(LocalBindingMemoryFixture.TrueSession, memory.Member, LocalBindingMemoryFixture.Account + 1);
        memory.Bytes(LocalBindingMemoryFixture.Selector + 0x10, [1]);
        memory.Int64(LocalActorMemoryFixture.CurrentParty, LocalBindingMemoryFixture.TrueSession);
        Check(await fixture.Session.ValidateLocalMenuOpeningAsync(original.Revision) is null,
            "A changed full account binding reused the old player-only admission");
        var current = fixture.Session.ReadLocalMenuAdmission();
        Check(current is not null && current.Revision > original.Revision
            && current.Observation.Identity == original.Observation.Identity
            && !ReferenceEquals(current.OwnerToken, original.OwnerToken),
            "Equal displayed identity hid a complete account-binding replacement");
    }

    public static async Task LifeChangeRevokesTheCapturedOpeningRevision() {
        await using var fixture = new LocalAdmissionFixture();
        var alive = await fixture.OpenAndObserveAsync();
        fixture.Transport.Actor.Memory.Int32(fixture.Transport.Actor.HealthAddress, 0);
        Check(await fixture.Session.ValidateLocalMenuOpeningAsync(alive.Revision) is null,
            "A life transition accepted the earlier opening command");
        var dead = fixture.Session.ReadLocalMenuAdmission();
        Check(dead is not null && dead.Revision > alive.Revision && dead.Observation.Life == LocalPlayerLife.Dead
            && ReferenceEquals(dead.OwnerToken, alive.OwnerToken),
            "A recognized dead actor did not receive a separate current admission epoch");
    }

    public static async Task FailedReadAndRecoveryCannotReplayTheEarlierAdmissionRevision() {
        await using var fixture = new LocalAdmissionFixture();
        var original = await fixture.OpenAndObserveAsync();
        fixture.Provider.BeforeRead = (_, _) => throw new IOException("Owned receipt unavailable");
        await fixture.Session.RefreshLocalObservationAsync();
        Check(fixture.Session.ReadLocalMenuAdmission() is null, "A failed read retained an earlier admission");
        fixture.Provider.BeforeRead = null;
        await fixture.Session.RefreshLocalObservationAsync();
        var recovered = fixture.Session.ReadLocalMenuAdmission();
        Check(recovered is not null && recovered.Revision > original.Revision
            && !ReferenceEquals(recovered.OwnerToken, original.OwnerToken),
            "Recovery recycled the revoked admission epoch");
        int reads = fixture.Provider.Reads;
        Check(await fixture.Session.ValidateLocalMenuOpeningAsync(original.Revision) is null
            && fixture.Provider.Reads == reads, "A stale epoch began another source read or replayed admission");
    }

    public static async Task LegacyObservationProviderCannotAuthorizeNewMenuOpening() {
        var transport = new RecordingRetailTransport();
        var provider = new RecordingObservationProvider();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false, _ => provider);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            Check(session.ReadLocalObservation().Identity is not null && session.ReadLocalMenuAdmission() is null,
                "A legacy display observation fabricated private account admission proof");
            Check(await session.ValidateLocalMenuOpeningAsync(1) is null && provider.Reads == 1,
                "An unproved legacy receipt authorized opening or triggered a source query");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task CancelledOrZeroRevisionValidationDoesNotStartASourceRead() {
        await using var fixture = new LocalAdmissionFixture();
        var original = await fixture.OpenAndObserveAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int reads = fixture.Provider.Reads;
        Check(await fixture.Session.ValidateLocalMenuOpeningAsync(original.Revision, cancellation.Token) is null
            && await fixture.Session.ValidateLocalMenuOpeningAsync(0) is null && fixture.Provider.Reads == reads,
            "Rejected admission reached the native observation source");
    }

    public static Task AdmissionSnapshotRejectsZeroOrUnsupportedUnboundObservations() {
        var valid = RecordingObservationProvider.Known();
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new LocalMenuAdmissionSnapshot(0, valid));
        TestAssert.Throws<ArgumentNullException>(() => new LocalMenuAdmissionSnapshot(1, null!));
        TestAssert.Throws<ArgumentException>(() => new LocalMenuAdmissionSnapshot(1, LocalPlayerObservation.BindingUnavailable));
        TestAssert.Throws<ArgumentException>(() => new LocalMenuAdmissionSnapshot(1, valid with { Life = (LocalPlayerLife)99 }));
        return Task.CompletedTask;
    }
}
