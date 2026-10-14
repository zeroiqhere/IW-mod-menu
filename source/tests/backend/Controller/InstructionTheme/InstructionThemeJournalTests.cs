using Apparition.Backend.Controller;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// These pure checks isolate report timing and channel ancestry without replacing actual controller tests
internal static class InstructionThemeJournalTests {
    private static InstructionHudState Hud(ulong revision = 1) => new(true, revision, 10, "Apparition", false, true, "#3998FE", false);

    public static void InterleavedChannelsUseOnlyTheirOwnPredecessorAndOneProjectionTimestamp() {
        var clock = new CountingClock();
        var owner = new object();
        var journal = new LocalInstructionThemeJournal().Append(owner, Hud(), "outline", "#FF0000", false, clock.GetTimestamp(), clock);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        journal = journal.Append(owner, Hud(), "background", "#00FF00", false, clock.GetTimestamp(), clock);
        clock.Advance(TimeSpan.FromMilliseconds(330));
        journal = journal.Append(owner, Hud(), "outline", "#0000FF", false, clock.GetTimestamp(), clock);
        clock.TimestampReads = 0;
        var projected = journal.Project(owner, Hud(), clock);
        TestAssert.Equal(1, clock.TimestampReads);
        TestAssert.True(projected.Frame is not null, "An owned paired creation must project its accepted requests");
        var events = projected.Frame!.Events;
        TestAssert.Equal(3, events.Count);
        TestAssert.Equal<ulong>(430, events[0].ElapsedMilliseconds);
        TestAssert.Equal<ulong>(330, events[1].ElapsedMilliseconds);
        TestAssert.Equal<ulong>(0, events[2].ElapsedMilliseconds);
        TestAssert.Equal<int?>(null, events[0].PredecessorGapMilliseconds);
        TestAssert.Equal<int?>(null, events[1].PredecessorGapMilliseconds);
        TestAssert.Equal<int?>(430, events[2].PredecessorGapMilliseconds);
        TestAssert.Equal(3UL, projected.State.Counter);
    }

    public static void ClockRecoveryAndDiscardedProjectionsCannotRestartOrCompleteAPrunedPrefix() {
        var clock = new ManualTimeProvider();
        var owner = new object();
        var journal = new LocalInstructionThemeJournal();
        for (int request = 0; request < 9; request++) {
            journal = journal.Append(owner, Hud(), "outline", "#FF0000", false, clock.GetTimestamp(), clock);
            clock.Advance(TimeSpan.FromMilliseconds(430));
        }
        var first = journal.Project(owner, Hud(), clock);
        TestAssert.True(first.Frame is not null, "A pruned accepted journal must remain explicitly declared");
        TestAssert.True(!first.Frame!.PrefixComplete, "Pruning must remain visible in a staged projection");
        clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1000));
        var recovered = first.State.Project(owner, Hud(), clock);
        TestAssert.True(!recovered.Frame!.PrefixComplete && recovered.Frame.Events.Count == 0,
            "Recovery withdraws uncertain requests without creating a complete empty history");
        TestAssert.Equal(9UL, recovered.State.Counter);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        var next = recovered.State.Append(owner, Hud(), "outline", "#00FF00", false, clock.GetTimestamp(), clock);
        var frame = next.Project(owner, Hud(), clock).Frame!;
        TestAssert.True(!frame.PrefixComplete, "A fresh request cannot restore the lost creation prefix");
        TestAssert.Equal(10UL, frame.Events.Single().Revision);
        // An uncommitted projection cannot spend this retained global counter or adopt a new creation
        var replacement = next.Project(owner, Hud(2), clock);
        TestAssert.Equal(0, replacement.Frame!.Events.Count);
        TestAssert.Equal(1UL, next.Project(owner, Hud(), clock).Frame!.CreationRevision);
        TestAssert.Equal(10UL, next.Counter);

        // An elapsed span outside TimeSpan recovery must not become a fresh complete history
        var overflowClock = new OverflowClock();
        var overflow = new LocalInstructionThemeJournal().Append(owner, Hud(), "outline", "#FF0000", false, 0, overflowClock)
            .Project(owner, Hud(), overflowClock);
        TestAssert.True(!overflow.Frame!.PrefixComplete && overflow.Frame.Events.Count == 0,
            "Elapsed overflow must withdraw uncertain events without restarting them");
        TestAssert.Equal(1UL, overflow.State.Counter);
    }
    private sealed class CountingClock : TimeProvider {
        private readonly ManualTimeProvider time = new();
        public int TimestampReads { get; set; }
        public override long TimestampFrequency => time.TimestampFrequency;
        public override long GetTimestamp() { TimestampReads++; return time.GetTimestamp(); }
        public void Advance(TimeSpan elapsed) => time.Advance(elapsed);
    }

    private sealed class OverflowClock : TimeProvider {
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => long.MaxValue;
    }
}
