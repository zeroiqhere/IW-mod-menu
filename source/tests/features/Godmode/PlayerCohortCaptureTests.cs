using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

// Absent slots in these fixtures are explicit source facts, not inferred retail state
internal static class PlayerCohortCaptureTests {
    private static void Check(bool condition, string reason) {
        if (!condition) throw new InvalidOperationException(reason);
    }

    internal static readonly PlayerCohortSession Session = new(4242, 0x140000000, 0x3000, 4);
    internal static PlayerIdentity Identity(int slot) => PlayerGodmodeRegistryTests.Identity(slot)
        with { ServerClient = 0x5000 + slot * 0x100 };
    internal static PlayerSlotObservation Present(int slot, int time = 100, bool eligible = true) =>
        new(slot, PlayerSlotPresence.Present, new(Identity(slot), time, 0x200, eligible));
    internal static PlayerCohortObservation Observation(int count = 4) => new(Session,
        Enumerable.Range(0, 4).Select(slot => slot < count ? Present(slot)
            : new PlayerSlotObservation(slot, PlayerSlotPresence.ConfirmedAbsent)).ToArray());
    internal static PlayerCohortCapture Capture(int count = 4) =>
        PlayerCohortCapture.TryCreate(Observation(count)) ?? throw new InvalidOperationException("Complete fixture capture rejected");

    public static Task CompleteCohortCopiesAndOrdersEverySlotWithoutTreatingCapacityAsPopulation() {
        var mutable = Observation(2).Slots.Reverse().ToArray();
        var observation = new PlayerCohortObservation(Session, mutable);
        mutable[0] = new(0, PlayerSlotPresence.Unknown);
        var capture = PlayerCohortCapture.TryCreate(observation);
        Check(capture is not null, "Explicit complete membership was rejected");
        TestAssert.Equal(2, capture!.Identities.Count);
        TestAssert.Equal(4, capture.Slots.Count);
        TestAssert.Equal(0, capture.Slots[0].Slot);
        TestAssert.Equal(3, capture.Slots[3].Slot);
        Check(capture.Matches(Observation(2)), "Unchanged membership must match");
        return Task.CompletedTask;
    }

    public static Task UnknownMissingDuplicateAndMalformedSlotsNeverCreateACompleteCohort() {
        var original = Observation().Slots.ToArray();
        foreach (var slots in new[] {
            original.Take(3).ToArray(), original.Concat(new[] { Present(0) }).ToArray(),
            new[] { Present(0), Present(1), Present(2), Present(2) },
            original.Select(slot => slot.Slot == 3 ? new PlayerSlotObservation(3, PlayerSlotPresence.Unknown) : slot).ToArray(),
            original.Select(slot => slot.Slot == 3 ? new PlayerSlotObservation(3, (PlayerSlotPresence)99) : slot).ToArray(),
            original.Select(slot => slot.Slot == 3 ? new PlayerSlotObservation(3, PlayerSlotPresence.Present) : slot).ToArray(),
            original.Select(slot => slot.Slot == 3 ? new PlayerSlotObservation(3, PlayerSlotPresence.ConfirmedAbsent, Present(3).Player) : slot).ToArray(),
            Enumerable.Range(0, 4).Select(slot => new PlayerSlotObservation(slot, PlayerSlotPresence.ConfirmedAbsent)).ToArray()
        }) Check(PlayerCohortCapture.TryCreate(new(Session, slots)) is null, "Incomplete or malformed slots were accepted");
        Check(PlayerCohortCapture.TryCreate(new(null, original)) is null, "Missing session was accepted");
        Check(PlayerCohortCapture.TryCreate(new(Session with { Capacity = 3 }, original)) is null, "Unsupported capacity was accepted");
        return Task.CompletedTask;
    }

    public static Task AliasedActorsAccountsServerRecordsAndSessionAnchorsAreRejected() {
        var first = Identity(0);
        foreach (var replacement in new[] {
            Identity(1) with { Entity = first.Entity }, Identity(1) with { Client = first.Client },
            Identity(1) with { Guid = first.Guid.ToLowerInvariant() }, Identity(1) with { ServerClient = first.ServerClient },
            Identity(1) with { ServerClients = first.ServerClients + 8 }, Identity(1) with { ServerCount = 3 },
            Identity(1) with { Slot = 2 }, Identity(1) with { Name = " " }, Identity(1) with { Guid = "bad" }
        }) {
            var slots = Observation().Slots.ToArray();
            slots[1] = new(1, PlayerSlotPresence.Present, new(replacement, 100, 0x200, true));
            Check(PlayerCohortCapture.TryCreate(new(Session, slots)) is null, "An aliased or mismatched member was accepted");
        }
        return Task.CompletedTask;
    }

    public static Task JoinsDeparturesAndConnectionReplacementInvalidateTheWholeCapturedCohort() {
        var capture = Capture(2);
        Check(!capture.Matches(Observation(3)), "A new participant cannot disappear from whole-cohort proof");
        Check(!capture.Matches(Observation(1)), "A departure cannot preserve aggregate proof");
        foreach (var session in new[] { Session with { ProcessId = 5555 }, Session with { ModuleBase = Session.ModuleBase + 8 },
            Session with { ServerClients = Session.ServerClients + 8 }, Session with { Capacity = 3 } })
            Check(!capture.Matches(new(session, Observation(2).Slots)), "A replacement connection was accepted");
        return Task.CompletedTask;
    }

    public static Task CommandTimeRollbackRejectsTheCaptureWhileLifeAndFlagsHaveSeparateEligibility() {
        var capture = Capture();
        var slots = Observation().Slots.ToArray();
        slots[2] = Present(2, time: 101, eligible: false) with { Player = new(Identity(2), 101, 0x201, false) };
        var current = new PlayerCohortObservation(Session, slots);
        Check(capture.Matches(current), "Death and owned flag changes do not remove a participant");
        Check(!capture.Matches(current, requireEnableEligible: true), "A dead participant cannot authorize a new enable");
        slots[2] = Present(2, time: 99);
        Check(!capture.Matches(new(Session, slots)), "Command time rollback was accepted");
        slots[2] = Present(2) with { Player = new(Identity(2) with { ServerClient = 0x7770 }, 100, 0x200, true) };
        Check(!capture.Matches(new(Session, slots)), "A replaced server record was accepted");
        return Task.CompletedTask;
    }
}
