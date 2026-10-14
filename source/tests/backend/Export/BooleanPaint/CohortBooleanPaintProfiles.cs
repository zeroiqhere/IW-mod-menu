using Apparition.Backend.Ipc;

namespace Backend.Tests;

// These frames follow accepted controller actions with inert connected session results
internal static class CohortBooleanPaintProfiles {
    public const int ProfileCount = 8;

    public static IReadOnlyDictionary<string, StateDocument> Build() {
        var packets = new Dictionary<string, StateDocument>(StringComparer.Ordinal);
        var immediate = new CohortGodmodeMenuFixture();
        immediate.EnterAllPlayers();
        packets.Add("cohort-off.json", immediate.Controller.Snapshot());
        immediate.SelectGodmode();
        immediate.Controller.StartQueuedAction();
        immediate.Session.State = immediate.Session.State with { AllPlayersGodmodeValue = "ON" };
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(179));
        packets.Add("cohort-on179.json", immediate.Controller.Snapshot());
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(1));
        packets.Add("cohort-on180.json", immediate.Controller.Snapshot());
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(250));
        packets.Add("cohort-on430.json", immediate.Controller.Snapshot());

        // A second actual controller holds work across the redraw without replacing its target
        var held = new CohortGodmodeMenuFixture();
        held.EnterAllPlayers();
        held.Session.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        held.SelectGodmode();
        held.Controller.StartQueuedAction();
        held.Clock.Advance(TimeSpan.FromMilliseconds(179));
        packets.Add("cohort-unknown179.json", held.Controller.Snapshot());
        held.Clock.Advance(TimeSpan.FromMilliseconds(1));
        packets.Add("cohort-unknown180.json", held.Controller.Snapshot());
        held.Session.State = held.Session.State with { AllPlayersGodmodeValue = "ON" };
        held.Session.Pending.SetResult();
        held.Clock.Advance(TimeSpan.FromMilliseconds(250));
        packets.Add("cohort-complete430.json", held.Controller.Snapshot());
        // An idle observation read withdraws action eligibility but keeps the same source paint
        var observing = new CohortGodmodeMenuFixture();
        observing.EnterAllPlayers();
        observing.Session.State = observing.Session.State with {
            CanAllPlayersGodmode = false, AllPlayersGodmodeValue = "UNKNOWN", Busy = false
        };
        packets.Add("cohort-worker-busy.json", observing.Controller.Snapshot());
        return packets;
    }
}
