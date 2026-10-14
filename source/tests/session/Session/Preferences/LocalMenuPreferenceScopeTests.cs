using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Players.LocalObservation;
using System.Text.Json;

namespace Session.Tests;

// The actual receipt provider and actor reader establish each private account scope
internal static class LocalMenuPreferenceScopeTests {
    public static async Task OwnedReceiptPublishesTheActualAccountScopeWithoutGodmodeOrCachedNativeReads() {
        await using var fixture = new LocalAdmissionFixture();
        // Legacy flags hooks stay inert because this transport has no local Godmode port
        fixture.Transport.Features.LocalSlot = 0;
        fixture.Transport.Features.FailLocalRead = false;
        fixture.Transport.Features.OnLocalRead = null;
        fixture.Transport.Features.RejectLocalFlagRestore = false;
        await fixture.OpenAndObserveAsync();
        var expectedBinding = fixture.Transport.Actor.Read().Binding;
        var scope = Scope(fixture.Session);
        Check(scope.AccountId == expectedBinding.AccountId && scope.AccountId != 0,
            "Preference account key did not come from the verified binding");
        Check(!fixture.Session.ReadMenuState().CanPersonalGodmode && fixture.Transport.Features.LocalBindCalls == 0,
            "Preference scope depended on a personal Godmode adapter");
        int actorReads = fixture.Transport.ActorReads;
        int providerReads = fixture.Provider.Reads;
        fixture.Transport.BeforeActorRead = () => throw new Exception("Cached scope entered its actor source");
        fixture.Transport.Features.OnConnected = () => throw new Exception("Cached scope queried native liveness");
        try {
            for (int read = 0; read < 40; read++)
                Check(ReferenceEquals(Scope(fixture.Session), scope), "Cached scope changed without an owned observation");
            Check(fixture.Transport.ActorReads == actorReads && fixture.Provider.Reads == providerReads,
                "A cached scope read started provider work");
            Check(JsonSerializer.Serialize(scope) == "{}", "Private account or owner data became public JSON properties");
        } finally {
            fixture.Transport.BeforeActorRead = null;
            fixture.Transport.Features.OnConnected = null;
        }
    }

    public static async Task KnownLifeUpdatesRetainTheSamePreferenceScope() {
        await using var fixture = new LocalAdmissionFixture();
        var firstAdmission = await fixture.OpenAndObserveAsync();
        var scope = Scope(fixture.Session);
        fixture.Transport.Actor.Memory.Int32(fixture.Transport.Actor.HealthAddress, 0);
        await fixture.Session.RefreshLocalObservationAsync();
        var dead = fixture.Session.ReadLocalMenuAdmission();
        Check(dead?.Observation.Life == LocalPlayerLife.Dead && dead.Revision != firstAdmission.Revision
            && ReferenceEquals(Scope(fixture.Session), scope),
            "Known death changed the account scope instead of only menu admission");
        fixture.Transport.Actor.Memory.Int32(fixture.Transport.Actor.HealthAddress, 100);
        // The source permission owner retains its grant even when the native host predicate changes
        fixture.Transport.Actor.Memory.Int32(LocalActorMemoryFixture.CurrentParty + 0x2C50, 1);
        await fixture.Session.RefreshLocalObservationAsync();
        var alive = fixture.Session.ReadLocalMenuAdmission();
        Check(alive?.Observation.Life == LocalPlayerLife.Alive && alive.Observation.HasMenu == true
            && ReferenceEquals(Scope(fixture.Session), scope)
            && ReferenceEquals(alive.OwnerToken, scope.OwnerToken),
            "Known life or retained permission replaced the same full-binding preference owner");
    }

    public static async Task UnknownBindingAndAccountChangesWithdrawOrReplaceTheOriginalScope() {
        await using var fixture = new LocalAdmissionFixture();
        await fixture.OpenAndObserveAsync();
        var first = Scope(fixture.Session);
        fixture.Transport.Actor.Memory.Int32(fixture.Transport.Actor.SessionStateAddress, 4);
        await fixture.Session.RefreshLocalObservationAsync();
        Check(CachedScope(fixture.Session) is null, "Unknown life retained current preference authority");
        fixture.Transport.Actor.Memory.Int32(fixture.Transport.Actor.SessionStateAddress, 0);
        await fixture.Session.RefreshLocalObservationAsync();
        var recovered = Scope(fixture.Session);
        Check(!ReferenceEquals(first.OwnerToken, recovered.OwnerToken) && recovered.AccountId == first.AccountId,
            "Recovered proof revived the withdrawn scope or changed its verified account key");

        // A new actor on the same account creates a new scope, including an unknown grant boundary
        fixture.Transport.Actor = new LocalActorMemoryFixture(2, host: false);
        await fixture.Session.RefreshLocalObservationAsync();
        Check(CachedScope(fixture.Session) is null,
            "A new actor with an undefined source permission retained the old account scope");
        fixture.Transport.Actor.Memory.Int32(LocalActorMemoryFixture.CurrentParty + 0x2C50, 2);
        await fixture.Session.RefreshLocalObservationAsync();
        var replaced = Scope(fixture.Session);
        Check(replaced.AccountId == recovered.AccountId && !ReferenceEquals(replaced.OwnerToken, recovered.OwnerToken),
            "A same-account actor replacement inherited the earlier scope owner");

        // Account changes must pass both the live-controller and registered-member reader checks
        const ulong nextAccount = 0xFEDCBA9876543210;
        var memory = fixture.Transport.Actor.Memory;
        memory.UInt64(LocalBindingMemoryFixture.AccountAddress, nextAccount);
        memory.Registered(memory.Session, memory.Member, nextAccount);
        await fixture.Session.RefreshLocalObservationAsync();
        var newAccount = Scope(fixture.Session);
        Check(newAccount.AccountId == fixture.Transport.Actor.Read().Binding.AccountId
            && newAccount.AccountId == nextAccount && !ReferenceEquals(newAccount.OwnerToken, replaced.OwnerToken),
            "A new verified account retained an old preference owner or used a display identity as its key");
        memory.UInt64(LocalBindingMemoryFixture.AccountAddress, 0);
        memory.Registered(memory.Session, memory.Member, 0);
        await fixture.Session.RefreshLocalObservationAsync();
        Check(CachedScope(fixture.Session) is null, "Zero account source bytes created a preference scope");
    }

    public static async Task ProcessRetirementAndClosingCannotReviveAnEarlierPreferenceScope() {
        foreach (bool wrongPid in new[] { true, false }) {
            await using var fixture = new LocalAdmissionFixture();
            await fixture.OpenAndObserveAsync();
            _ = Scope(fixture.Session);
            if (wrongPid) fixture.Transport.Features.ReportedPid = 99;
            else fixture.Transport.Features.Disposed = true;
            try {
                await fixture.Session.RefreshLocalObservationAsync();
                Check(CachedScope(fixture.Session) is null,
                    "A retired original process retained its preference scope");
                fixture.Transport.Features.ReportedPid = 4242;
                fixture.Transport.Features.Disposed = false;
                await fixture.Session.RefreshLocalObservationAsync();
                Check(CachedScope(fixture.Session) is null,
                    "Restored native process scalars revived a terminal account scope");
            } finally {
                fixture.Transport.Features.ReportedPid = 4242;
                fixture.Transport.Features.Disposed = false;
            }
        }

        await using var closingFixture = new LocalAdmissionFixture();
        await closingFixture.OpenAndObserveAsync();
        _ = Scope(closingFixture.Session);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        closingFixture.Provider.BeforeRead = async (_, _) => { entered.TrySetResult(); await release.Task; };
        Task read = closingFixture.Session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var closing = closingFixture.Session.ShutdownAsync();
            Check(CachedScope(closingFixture.Session) is null,
                "Closing exposed preference authority while joining its retained provider");
            release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
            Check((await closing.WaitAsync(TimeSpan.FromSeconds(3))).Stopped
                && CachedScope(closingFixture.Session) is null,
                "A late provider completion restored a closed preference owner");
        } finally {
            release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    private static LocalMenuPreferenceScope Scope(IBackendSession session) =>
        CachedScope(session) ?? throw new Exception("An actual owned account receipt did not publish a preference scope");
    private static LocalMenuPreferenceScope? CachedScope(IBackendSession session) => session.ReadLocalMenuPreferenceScope();
    private static void Check(bool value, string message) => RetailBackendSessionTests.Check(value, message);
}
