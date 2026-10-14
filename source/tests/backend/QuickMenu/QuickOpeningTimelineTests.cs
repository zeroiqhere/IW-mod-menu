using Apparition.Backend.QuickMenu;

namespace Backend.Tests;

internal static class QuickOpeningTimelineTests {
    public static void QuickHudIsImmediateAndInputUnlocksAt550Milliseconds() {
        var clock = new Clock();
        var state = QuickOpeningTimeline.Start(clock, 8);
        var initial = state.Read();
        TestAssert.True(initial.Frame.HudVisible && initial.Frame.InputsLocked, "Quick admission must draw immediately without Main's rebuild gap");
        TestAssert.Equal(0, initial.Frame.OpeningElapsedMilliseconds);

        foreach (int elapsed in new[] { 49, 50, 549, 550 }) {
            clock.Timestamp = elapsed;
            var read = state.Read();
            state = read.State;
            TestAssert.True(read.Frame.HudVisible, "The Quick timeline introduced a hidden phase");
            TestAssert.Equal(elapsed < 550, read.Frame.InputsLocked);
            TestAssert.Equal(elapsed, read.Frame.OpeningElapsedMilliseconds);
        }
    }

    public static void LongOpeningFadeOutlivesTheInputWaitWhileZeroAndOneRowsHaveNoFade() {
        foreach (int shown in new[] { 0, 1, 5, 14 }) {
            var clock = new Clock();
            var state = QuickOpeningTimeline.Start(clock, shown);
            var start = state.Read();
            int duration = Math.Max(0, shown - 1) * 100;
            TestAssert.Equal(duration, start.Frame.OpeningDurationMilliseconds);
            TestAssert.Equal<int?>(shown < 2 ? null : 0, start.Frame.OpeningElapsedMilliseconds);

            clock.Timestamp = 550;
            var ready = state.Read();
            TestAssert.True(ready.Frame.HudVisible && !ready.Frame.InputsLocked, "Fade duration stretched the source monitor wait");
            TestAssert.Equal<int?>(duration > 550 ? 550 : null, ready.Frame.OpeningElapsedMilliseconds);
            clock.Timestamp = 1300;
            TestAssert.Equal<int?>(null, ready.State.Read().Frame.OpeningElapsedMilliseconds);
        }
    }

    public static void RollbackAndSaturatedElapsedRestartWithoutPrematureInput() {
        var clock = new Clock();
        var state = QuickOpeningTimeline.Start(clock, 14);
        clock.Timestamp = 600;
        state = state.Read().State;
        clock.Timestamp = 500;
        var recovered = state.Read();
        TestAssert.True(recovered.Frame.HudVisible && recovered.Frame.InputsLocked, "Rollback must retain the HUD and restart a bounded input wait");
        TestAssert.Equal<int?>(null, recovered.Frame.OpeningElapsedMilliseconds);
        TestAssert.Equal(500L, recovered.State.StartedTimestamp);
        clock.Timestamp = 1049;
        TestAssert.True(recovered.State.Read().Frame.InputsLocked, "Recovery unlocked before another 550 ms");
        clock.Timestamp = 1050;
        TestAssert.True(!recovered.State.Read().Frame.InputsLocked, "Recovery did not finish its bounded wait");

        var faultClock = new Clock();
        var beforeOverflow = QuickOpeningTimeline.Start(faultClock, 8);
        faultClock.Timestamp = 100;
        faultClock.ThrowFrequency = true;
        var elapsedFailure = beforeOverflow.Read();
        TestAssert.True(elapsedFailure.Frame.HudVisible && elapsedFailure.Frame.InputsLocked,
            "Elapsed overflow must recover without hiding the existing HUD");
        TestAssert.Equal(100L, elapsedFailure.State.StartedTimestamp);
        TestAssert.Equal<int?>(null, elapsedFailure.Frame.OpeningElapsedMilliseconds);
        faultClock.ThrowFrequency = false;
        faultClock.Timestamp = 650;
        TestAssert.True(!elapsedFailure.State.Read().Frame.InputsLocked, "Elapsed overflow left an unbounded pause");

        foreach (long initial in new[] { 0L, long.MinValue }) {
            var wide = new Clock { Frequency = 1, Timestamp = initial };
            var opening = QuickOpeningTimeline.Start(wide, 14);
            wide.Timestamp = long.MaxValue;
            var overflow = opening.Read();
            TestAssert.True(overflow.Frame.HudVisible && overflow.Frame.InputsLocked, "Saturated or negative elapsed completed a timeline early");
            TestAssert.Equal(long.MaxValue, overflow.State.StartedTimestamp);
            TestAssert.Equal<int?>(null, overflow.Frame.OpeningElapsedMilliseconds);
        }
    }

    public static void InvalidRowsAndClockFaultsDoNotCreateOrPartiallyAdvanceTimeline() {
        var clock = new Clock();
        TestAssert.Throws<ArgumentNullException>(() => QuickOpeningTimeline.Start(null!, 3));
        foreach (int invalid in new[] { -1, 15, int.MaxValue })
            TestAssert.Throws<ArgumentOutOfRangeException>(() => QuickOpeningTimeline.Start(clock, invalid));
        TestAssert.Equal(0, clock.Reads);

        var state = QuickOpeningTimeline.Start(clock, 8);
        clock.Throw = true;
        TestAssert.Throws<InvalidOperationException>(() => QuickOpeningTimeline.Start(clock, 8));
        TestAssert.Throws<InvalidOperationException>(() => state.Read());
        TestAssert.Equal(0L, state.StartedTimestamp);
        TestAssert.Equal(0L, state.LastTimestamp);
        clock.Throw = false;
        clock.Timestamp = 50;
        TestAssert.Equal(50, state.Read().Frame.OpeningElapsedMilliseconds);
    }

    public static void CancellationCannotResumeAnOldQuickOpening() {
        var clock = new Clock();
        var state = QuickOpeningTimeline.Start(clock, 14);
        var closed = state.Cancel();
        int reads = clock.Reads;
        clock.Timestamp = 600;
        var frame = closed.Read().Frame;

        TestAssert.True(!frame.HudVisible && !frame.InputsLocked, "Cancel kept local opening controls active");
        TestAssert.Equal<int?>(null, frame.OpeningElapsedMilliseconds);
        TestAssert.Equal(reads, clock.Reads);
        TestAssert.True(state.IsActive, "Cancellation changed a previously captured immutable timeline");
        TestAssert.True(!closed.Cancel().IsActive, "Repeated cancellation revived an opening");
    }

    public static void CancellingOnlyTheFadeKeepsTheOriginalInputWaitAndCannotReviveIt() {
        var clock = new Clock();
        var original = QuickOpeningTimeline.Start(clock, 14);
        clock.Timestamp = 100;
        var state = original.Read().State.CancelFade();
        var frame = state.Read().Frame;
        TestAssert.True(frame.HudVisible && frame.InputsLocked, "Cancelling rows must preserve the ordinary monitor wait");
        TestAssert.Equal<int?>(null, frame.OpeningElapsedMilliseconds);
        clock.Timestamp = 549;
        TestAssert.True(state.Read().Frame.InputsLocked, "Fade cancellation changed the input deadline");
        clock.Timestamp = 550;
        TestAssert.True(!state.Read().Frame.InputsLocked, "Fade cancellation restarted the input wait");
        TestAssert.Equal<int?>(null, state.Read().Frame.OpeningElapsedMilliseconds);
        TestAssert.True(!state.Cancel().CancelFade().Read().Frame.HudVisible, "Fade cancellation reopened a closed timeline");
        clock.Timestamp = 100;
        TestAssert.Equal(100, original.Read().Frame.OpeningElapsedMilliseconds);
    }

    public static void BindingPublishedRowsKeepsTheAdmissionDeadlineAndDoesNotReadTheClock() {
        var clock = new Clock();
        var empty = QuickOpeningTimeline.Start(clock, 0);
        clock.Timestamp = 100;
        int reads = clock.Reads;
        var bound = empty.WithShownRows(8);
        TestAssert.Equal(reads, clock.Reads);
        TestAssert.Equal(0L, bound.StartedTimestamp);
        TestAssert.Equal(100, bound.Read().Frame.OpeningElapsedMilliseconds);
        clock.Timestamp = 550;
        TestAssert.True(!bound.Read().Frame.InputsLocked, "Publishing rows restarted the source input wait");
        TestAssert.Equal<int?>(null, empty.Read().Frame.OpeningElapsedMilliseconds);
        TestAssert.Equal<int?>(null, bound.CancelFade().WithShownRows(14).Read().Frame.OpeningElapsedMilliseconds);
        TestAssert.True(!bound.Cancel().WithShownRows(8).Read().Frame.HudVisible, "Row binding revived a closed timeline");
        foreach (int invalid in new[] { -1, 15, int.MaxValue })
            TestAssert.Throws<ArgumentOutOfRangeException>(() => bound.WithShownRows(invalid));
    }

    private sealed class Clock : TimeProvider {
        public long Timestamp { get; set; }
        public long Frequency { get; init; } = 1000;
        public bool Throw { get; set; }
        public bool ThrowFrequency { get; set; }
        public int Reads { get; private set; }
        public override long TimestampFrequency {
            get {
                if (ThrowFrequency) throw new OverflowException("Owned elapsed calculation fault");
                return Frequency;
            }
        }
        public override long GetTimestamp() {
            Reads++;
            if (Throw) throw new InvalidOperationException("Owned clock fault");
            return Timestamp;
        }
    }
}
