using Apparition.Backend.Session;
using syndishanx_iw_tool.Players;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// Reservations arbitrate ownership only; copied identities do not prove a retail actor
internal static class GodmodeTargetReservationsTests {
    private static PlayerIdentity Identity(int slot) => new(slot, 0x10000 + slot * 0x400,
        0x20000 + slot * 0x10000, 0x90000, 4, "Player " + slot, "1234567890ABCDEF") {
        ServerClient = 0x100000 + slot * 0x100000
    };
    private static void Reject(Action action) {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("A conflicting or invalid reservation was accepted");
    }

    public static Task DistinctActorsReserveIndependentlyAndTheOriginalTokenIsIdempotent() {
        var reservations = new GodmodeTargetReservations();
        for (int slot = 0; slot < 4; slot++) {
            var owner = new object(); var identity = Identity(slot);
            reservations.Acquire(owner, identity);
            reservations.Retain(owner);
            reservations.Acquire(owner, identity);
            Reject(() => reservations.Acquire(owner, identity with { Guid = "ABCDEF0123456789" }));
        }
        return Task.CompletedTask;
    }

    public static Task EitherAcquisitionOrderRejectsOverlappingStorageAndSlotAliases() {
        foreach (bool personalFirst in new[] { true, false }) {
            var reservations = new GodmodeTargetReservations();
            object personal = new(), teammate = new();
            object first = personalFirst ? personal : teammate;
            object second = personalFirst ? teammate : personal;
            var identity = Identity(2);
            reservations.Acquire(first, identity); reservations.Retain(first);
            foreach (var alias in new[] { identity, Identity(3) with { Entity = identity.Entity },
                Identity(3) with { Client = identity.Client }, Identity(2) with { Entity = 0x80000, Client = 0xA0000 } }) {
                Reject(() => reservations.Acquire(second, alias));
            }
        }
        return Task.CompletedTask;
    }

    public static Task UnboundFactoryFailureCanReleaseButRetainedWritersCannotTransfer() {
        var reservations = new GodmodeTargetReservations(); object owner = new(), replacement = new();
        var identity = Identity(1);
        reservations.Acquire(owner, identity); reservations.ReleaseUnbound(owner);
        reservations.Acquire(replacement, identity); reservations.Retain(replacement);
        Reject(() => reservations.ReleaseUnbound(replacement));
        Reject(() => reservations.Acquire(owner, identity));
        // Another token's release cannot remove the retained owner
        reservations.ReleaseUnbound(owner);
        Reject(() => reservations.Acquire(owner, identity));
        return Task.CompletedTask;
    }

    public static Task MalformedTargetsFailBeforeCreatingAnyReservation() {
        var reservations = new GodmodeTargetReservations(); var owner = new object();
        foreach (var identity in new[] { Identity(-1), Identity(4), Identity(1) with { ServerCount = 3 },
            Identity(1) with { Entity = 0 }, Identity(1) with { Client = 0x20001 },
            Identity(1) with { Entity = 0x7FFFFFFF0000 }, Identity(1) with { ServerClients = 0 },
            Identity(1) with { ServerClient = 0 }, Identity(1) with { Name = "" }, Identity(1) with { Guid = "Invalid" } }) {
            Reject(() => reservations.Acquire(owner, identity));
        }
        reservations.Acquire(owner, Identity(1));
        return Task.CompletedTask;
    }

    public static async Task SimultaneousDifferentOwnersCannotBothAcquireTheSameTarget() {
        var reservations = new GodmodeTargetReservations(); int winners = 0;
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
            start.Wait();
            try { reservations.Acquire(new object(), Identity(1)); Interlocked.Increment(ref winners); }
            catch (InvalidOperationException) { }
        })).ToArray();
        start.Set(); await Task.WhenAll(tasks);
        Check(winners == 1, "Concurrent acquisition gave multiple owners the same target");
    }

    public static Task EqualLookingOwnerObjectsStillHaveSeparateOwnership() {
        var reservations = new GodmodeTargetReservations(); object first = new EqualOwner(), second = new EqualOwner();
        reservations.Acquire(first, Identity(2));
        Reject(() => reservations.Acquire(second, Identity(2)));
        reservations.ReleaseUnbound(second);
        reservations.Retain(first);
        Reject(() => reservations.Acquire(second, Identity(2)));
        return Task.CompletedTask;
    }

    // Equality overrides must never let a second object impersonate the original writer
    private sealed class EqualOwner {
        public override bool Equals(object? obj) => obj is EqualOwner;
        public override int GetHashCode() => 1;
    }
}
