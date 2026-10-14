using Apparition.Backend.Controller;

namespace Backend.Tests;

internal static class LocalMenuCreditsStateTests
{
    public static void AnUnrepresentableElapsedDurationRestartsTheVisitInsteadOfCompletingEarly()
    {
        var clock = new WideClock();
        var state = LocalMenuCreditsState.Start(clock);
        // Framework versions can clamp or wrap this duration; the visit must restart in either case
        clock.Timestamp = long.MaxValue;
        var restarted = state.Read(clock);
        TestAssert.Equal(0, restarted.Frame?.ElapsedMilliseconds);
        TestAssert.Equal(long.MaxValue, restarted.State.StartedTimestamp);
        TestAssert.Equal(0, restarted.State.Read(clock).Frame?.ElapsedMilliseconds);
    }
    private sealed class WideClock : TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Timestamp;
    }
}
