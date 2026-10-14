using Apparition.Backend.Controller;

namespace Backend.Tests.Controller.Callbacks;

// Manual monotonic timestamps check the source wait without task or wall-clock scheduling
internal static class LocalMenuOptionCallbackStateTests {
    public static void BooleanRefreshStartsPendingAndRequestsRedrawAtExactly180Milliseconds() {
        var clock = new ManualTimeProvider();
        var state = LocalMenuOptionCallbackState.Start(7, "boolean", clock);
        var first = state.Read(clock);
        TestAssert.Equal("pending", first.Frame?.Phase);
        TestAssert.Equal(7UL, first.Frame!.Revision);
        TestAssert.Equal("boolean", first.Frame.Kind);
        TestAssert.True(!first.RefreshPresentation, "Admission cannot redraw the current boolean HUD");
        clock.Advance(TimeSpan.FromMilliseconds(179));
        // Wall-clock changes cannot move the captured monotonic deadline
        clock.MoveWallClock(TimeSpan.FromDays(2));
        var pending = first.State!.Read(clock);
        TestAssert.Equal("pending", pending.Frame?.Phase);
        TestAssert.True(!pending.RefreshPresentation, "The original boolean HUD stays through 179 ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var redraw = pending.State!.Read(clock);
        TestAssert.Equal("cooldown", redraw.Frame?.Phase);
        TestAssert.True(redraw.RefreshPresentation, "The owner requests one redraw at 180 ms");
        TestAssert.True(!redraw.State!.PresentationApplied, "Read cannot pretend that a caller committed the redraw");
    }

    public static void CommittedBooleanRedrawNeverRepeatsAndRetiresAtExactly430Milliseconds() {
        var clock = new ManualTimeProvider();
        var initial = LocalMenuOptionCallbackState.Start(9, "boolean", clock);
        clock.Advance(TimeSpan.FromMilliseconds(180));
        var redraw = initial.Read(clock);
        TestAssert.True(redraw.RefreshPresentation, "The caller must receive the real redraw boundary");
        var committed = redraw.State! with { PresentationApplied = true };
        clock.Advance(TimeSpan.FromMilliseconds(249));
        var cooldown = committed.Read(clock);
        TestAssert.Equal("cooldown", cooldown.Frame?.Phase);
        TestAssert.True(!cooldown.RefreshPresentation, "A committed redraw cannot be requested twice");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var complete = cooldown.State!.Read(clock);
        TestAssert.True(complete.State is null && complete.Frame is null && !complete.RefreshPresentation,
            "The boolean monitor ends at 430 ms independently of callback completion");
        TestAssert.True(!initial.PresentationApplied, "An older immutable state cannot change with its caller copy");
    }

    public static void PlainCallbackUses250MillisecondsWithoutRequestingARebuild() {
        var clock = new ManualTimeProvider();
        var initial = LocalMenuOptionCallbackState.Start(3, "plain", clock);
        var admission = initial.Read(clock);
        TestAssert.Equal("plain", admission.Frame?.Kind);
        TestAssert.Equal("cooldown", admission.Frame?.Phase);
        TestAssert.True(!admission.RefreshPresentation, "Plain callbacks have no boolean redraw");
        clock.Advance(TimeSpan.FromMilliseconds(249));
        var cooldown = admission.State!.Read(clock);
        TestAssert.True(cooldown.State is not null && !cooldown.RefreshPresentation, "The plain monitor remains locked at 249 ms");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var complete = cooldown.State!.Read(clock);
        TestAssert.True(complete.State is null && complete.Frame is null && !complete.RefreshPresentation,
            "Plain callbacks retire at 250 ms");
    }

    public static void LateBooleanPollRequestsOneOwnedRedrawBeforeRetiring() {
        var clock = new ManualTimeProvider();
        var state = LocalMenuOptionCallbackState.Start(ulong.MaxValue, "boolean", clock);
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        var late = state.Read(clock);
        TestAssert.True(late.State is null && late.Frame is null && late.RefreshPresentation,
            "A late first poll still requests its boolean redraw before retirement");
        var previouslyApplied = (state with { PresentationApplied = true }).Read(clock);
        TestAssert.True(!previouslyApplied.RefreshPresentation, "A completed redraw cannot repeat after a late poll");
        var plain = LocalMenuOptionCallbackState.Start(1, "plain", clock);
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        TestAssert.True(!plain.Read(clock).RefreshPresentation, "Late plain callbacks never gain a boolean redraw");
    }

    public static void RollbackBeforeBooleanRedrawRestartsOneFinite430MillisecondWait() {
        var clock = new ManualTimeProvider();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var state = LocalMenuOptionCallbackState.Start(5, "boolean", clock);
        clock.Advance(TimeSpan.FromMilliseconds(179));
        state = state.Read(clock).State!;
        clock.RollBackTimestamp(TimeSpan.FromMilliseconds(50));
        var recovered = state.Read(clock);
        TestAssert.Equal("pending", recovered.Frame?.Phase);
        TestAssert.True(!recovered.RefreshPresentation && recovered.State!.BaseMilliseconds == 0,
            "Rollback cannot count obsolete elapsed time or invent a redraw");
        TestAssert.Equal(5UL, recovered.State!.Revision);
        clock.Advance(TimeSpan.FromMilliseconds(180));
        var redraw = recovered.State.Read(clock);
        TestAssert.True(redraw.RefreshPresentation, "A recovered pending callback redraws after its fresh 180 ms");
        var committed = redraw.State! with { PresentationApplied = true };
        clock.Advance(TimeSpan.FromMilliseconds(250));
        TestAssert.True(committed.Read(clock).State is null, "Recovered pending callbacks have a finite 430 ms deadline");
    }

    public static void RollbackAfterAppliedRedrawRestartsOnlyTheRemaining250Milliseconds() {
        var clock = new ManualTimeProvider();
        var state = LocalMenuOptionCallbackState.Start(6, "boolean", clock);
        clock.Advance(TimeSpan.FromMilliseconds(180));
        state = state.Read(clock).State! with { PresentationApplied = true };
        clock.Advance(TimeSpan.FromMilliseconds(100));
        state = state.Read(clock).State!;
        clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1));
        var recovered = state.Read(clock);
        TestAssert.Equal("cooldown", recovered.Frame?.Phase);
        TestAssert.True(recovered.State!.PresentationApplied && recovered.State.BaseMilliseconds == 180
            && !recovered.RefreshPresentation, "Completed presentation survives recovery without a second redraw");
        clock.Advance(TimeSpan.FromMilliseconds(249));
        var pending = recovered.State.Read(clock);
        TestAssert.True(pending.State is not null && !pending.RefreshPresentation, "Recovery still owns its remaining wait");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TestAssert.True(pending.State!.Read(clock).State is null, "Applied recovery retires at 250 ms");
    }

    public static void PlainRollbackAndExtremeElapsedRecoverWithoutAnUnboundedWait() {
        var clock = new CallbackClock();
        var plain = LocalMenuOptionCallbackState.Start(1, "plain", clock);
        clock.Timestamp = 200;
        plain = plain.Read(clock).State!;
        clock.Timestamp = 199;
        var recovered = plain.Read(clock);
        TestAssert.Equal("cooldown", recovered.Frame?.Phase);
        TestAssert.True(!recovered.RefreshPresentation, "Plain rollback never creates boolean work");
        clock.Timestamp = 449;
        TestAssert.True(recovered.State!.Read(clock).State is null, "Plain recovery remains finite 250 ms");
        var extremeClock = new CallbackClock { Frequency = 1 };
        var boolean = LocalMenuOptionCallbackState.Start(8, "boolean", extremeClock);
        extremeClock.Timestamp = long.MaxValue;
        var overflow = boolean.Read(extremeClock);
        TestAssert.Equal("pending", overflow.Frame?.Phase);
        TestAssert.True(!overflow.RefreshPresentation && overflow.State!.StartedTimestamp == long.MaxValue,
            "An elapsed overflow restarts rather than retiring or requesting a guessed redraw");
        extremeClock.Timestamp = long.MinValue;
        var wrapped = overflow.State!.Read(extremeClock);
        TestAssert.True(wrapped.State is not null && !wrapped.RefreshPresentation,
            "Observed timestamp wrap cannot create an old completed callback");
        // Overflow recovery uses the same completed stage as a normal timestamp rollback
        extremeClock.Timestamp = long.MaxValue;
        var appliedOverflow = (boolean with { PresentationApplied = true }).Read(extremeClock);
        TestAssert.Equal("cooldown", appliedOverflow.Frame?.Phase);
        TestAssert.True(appliedOverflow.State!.PresentationApplied && appliedOverflow.State.BaseMilliseconds == 180
            && !appliedOverflow.RefreshPresentation, "Elapsed overflow cannot redraw committed rows again");
    }

    public static void InvalidRevisionKindAndRecoveryCopiesRejectBeforeClockReads() {
        var clock = new CallbackClock();
        TestAssert.Throws<ArgumentOutOfRangeException>(() => LocalMenuOptionCallbackState.Start(0, "boolean", clock));
        TestAssert.Throws<ArgumentException>(() => LocalMenuOptionCallbackState.Start(1, "slider", clock));
        TestAssert.Throws<ArgumentException>(() => LocalMenuOptionCallbackState.Start(1, "Boolean", clock));
        TestAssert.Throws<ArgumentException>(() => LocalMenuOptionCallbackState.Start(1, null!, clock));
        TestAssert.Equal(0, clock.Reads);
        var valid = LocalMenuOptionCallbackState.Start(1, "boolean", clock);
        int reads = clock.Reads;
        // Direct record copies are validated before an external provider can be sampled
        foreach (var invalid in new[] { valid with { Revision = 0 }, valid with { Kind = "unknown" },
            valid with { BaseMilliseconds = -1 }, valid with { BaseMilliseconds = 180 },
            valid with { BaseMilliseconds = 430 }, valid with { Kind = "plain", PresentationApplied = true } }) {
            TestAssert.Throws<InvalidOperationException>(() => invalid.Read(clock));
        }
        TestAssert.Equal(reads, clock.Reads);
        TestAssert.True(!valid.PresentationApplied && valid.BaseMilliseconds == 0, "Rejected copies cannot mutate their source");
    }

    public static void ClockExceptionsPropagateWithoutMutatingTheCapturedState() {
        var clock = new CallbackClock();
        TestAssert.Throws<ArgumentNullException>(() => LocalMenuOptionCallbackState.Start(1, "boolean", null!));
        var state = LocalMenuOptionCallbackState.Start(4, "boolean", clock);
        TestAssert.Throws<ArgumentNullException>(() => state.Read(null!));
        clock.Fail = true;
        TestAssert.Throws<IOException>(() => state.Read(clock));
        TestAssert.True(state.StartedTimestamp == 0 && state.LastTimestamp == 0 && !state.PresentationApplied,
            "A provider failure leaves no partial immutable timing commit");
        TestAssert.Throws<IOException>(() => LocalMenuOptionCallbackState.Start(4, "plain", clock));
        clock.Fail = false;
        clock.Timestamp = 180;
        TestAssert.True(state.Read(clock).RefreshPresentation, "A failed clock read cannot consume the later redraw");
    }

    private sealed class CallbackClock : TimeProvider {
        public long Timestamp { get; set; }
        public long Frequency { get; init; } = 1000;
        public int Reads { get; private set; }
        public bool Fail { get; set; }
        public override long TimestampFrequency => Frequency;
        public override long GetTimestamp() {
            Reads++;
            // The owned failure fixture never starts a timer or reads an external clock
            if (Fail) throw new IOException("Controlled clock failure");
            return Timestamp;
        }
    }
}
