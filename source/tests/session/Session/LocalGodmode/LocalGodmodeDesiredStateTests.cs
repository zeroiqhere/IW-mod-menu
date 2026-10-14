using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

// Desired-state requests keep the same actual local writer used by menu toggles
internal static class LocalGodmodeDesiredStateTests {
    public static async Task RepeatedEnablePreservesOwnedOnWithoutAnotherWriteOrBinding() {
        await using var fixture = new LocalGodmodeFixture(3);
        var first = await fixture.Service.SetWithTransitionAsync(true);
        var repeated = await fixture.Service.SetWithTransitionAsync(true);
        LocalGodmodeFixture.Check(first.Succeeded && first.ConfirmedOwnedWrite
            && repeated.Succeeded && !repeated.ConfirmedOwnedWrite,
            "Only the first enable may report its actual owned write");
        LocalGodmodeFixture.Check(fixture.Flags.Flags == 0x201 && fixture.Flags.Writes == 1
            && fixture.BindCalls == 1 && fixture.Service.ReadView().Value == GodmodeValue.On,
            "Repeated enable must preserve ON and the original owner");
    }

    public static async Task RepeatedDisableRestoresOnceWithoutStrictLivingReads() {
        await using var fixture = new LocalGodmodeFixture(2);
        LocalGodmodeFixture.Check(await fixture.Service.SetAsync(true), "Setup enable failed");
        fixture.ReadFailure = true;
        int reads = fixture.CurrentReads;
        var first = await fixture.Service.SetWithTransitionAsync(false);
        var repeated = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(first.Succeeded && first.ConfirmedOwnedWrite
            && repeated.Succeeded && !repeated.ConfirmedOwnedWrite,
            "Only the actual restoration may report an owned OFF write");
        LocalGodmodeFixture.Check(fixture.Flags.Writes == 2 && fixture.Flags.Flags == 0x200
            && fixture.CurrentReads == reads && fixture.BindCalls == 1,
            "Disable must restore the retained owner without strict living reads");
    }

    public static async Task UnboundDisableCannotBindOrClaimAnObservedOffState() {
        await using var fixture = new LocalGodmodeFixture();
        var result = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(!result.Succeeded && !result.ConfirmedOwnedWrite
            && fixture.CurrentReads == 0 && fixture.BindCalls == 0 && fixture.Flags.Writes == 0
            && fixture.Service.ReadView().Value == GodmodeValue.Unknown,
            "An unbound OFF request cannot invent authenticated OFF or create a writer");
    }

    public static async Task GameClearedOffSucceedsWithoutClaimingAnOwnedWrite() {
        await using var fixture = new LocalGodmodeFixture();
        LocalGodmodeFixture.Check(await fixture.Service.SetAsync(true), "Setup enable failed");
        fixture.Flags.Flags = 0x200;
        var result = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(result.Succeeded && !result.ConfirmedOwnedWrite
            && fixture.Flags.Writes == 1 && !fixture.Service.ReadView().HasPendingCleanup,
            "Observing the game's cleared flag is not an actual owned OFF write");
    }

    public static async Task FailedDisableRetainsCleanupAndRetryReportsOnlyItsOwnWrite() {
        await using var fixture = new LocalGodmodeFixture();
        LocalGodmodeFixture.Check(await fixture.Service.SetAsync(true), "Setup enable failed");
        fixture.Flags.OnWrite = (_, _, _) => false;
        var failed = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(!failed.Succeeded && !failed.ConfirmedOwnedWrite
            && fixture.Service.ReadView().HasPendingCleanup && fixture.Flags.Flags == 0x201,
            "Rejected restoration must retain the original cleanup owner");
        fixture.Flags.OnWrite = null;
        var retried = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(retried.Succeeded && retried.ConfirmedOwnedWrite
            && fixture.Flags.Flags == 0x200 && fixture.BindCalls == 1,
            "Retry must report only the original owner's newly accepted OFF write");
    }

    public static async Task ForeignOrChangedFlagsCannotBecomeOwnedThroughDesiredEnable() {
        await using var foreign = new LocalGodmodeFixture();
        foreign.Flags.Flags = 0x201;
        var rejected = await foreign.Service.SetWithTransitionAsync(true);
        LocalGodmodeFixture.Check(!rejected.Succeeded && !rejected.ConfirmedOwnedWrite
            && foreign.Flags.Writes == 0 && foreign.BindCalls == 0,
            "A foreign ON flag cannot become an owned transition");
        await using var changed = new LocalGodmodeFixture();
        LocalGodmodeFixture.Check(await changed.Service.SetAsync(true), "Setup enable failed");
        changed.Flags.Flags = 0x401;
        var repeated = await changed.Service.SetWithTransitionAsync(true);
        LocalGodmodeFixture.Check(!repeated.Succeeded && !repeated.ConfirmedOwnedWrite
            && changed.Flags.Writes == 1 && changed.Service.ReadView().HasPendingCleanup,
            "Repeated ON must reject a changed flag word without toggling or borrowing it");
        changed.Flags.Flags = 0x201;
    }

    public static async Task CancelledAndClosedDesiredRequestsPerformNoSourceReadsOrWrites() {
        await using var fixture = new LocalGodmodeFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await fixture.Service.SetWithTransitionAsync(true, cancellation.Token);
        fixture.Service.RequestClose();
        var closed = await fixture.Service.SetWithTransitionAsync(true);
        LocalGodmodeFixture.Check(!cancelled.Succeeded && !cancelled.ConfirmedOwnedWrite
            && !closed.Succeeded && !closed.ConfirmedOwnedWrite && fixture.CurrentReads == 0
            && fixture.BindCalls == 0 && fixture.Flags.Writes == 0,
            "Cancellation and close must deny new desired-state admission");
    }

    public static async Task ConcurrentDesiredRequestsReturnReceiptsForTheirSerializedOperations() {
        await using var fixture = new LocalGodmodeFixture();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeCurrentRead = () => {
            entered.TrySetResult();
            release.Wait();
        };
        Task<GodmodeTransitionResult> first = Task.Run(() => fixture.Service.SetWithTransitionAsync(true));
        Task<GodmodeTransitionResult>[] queued = [];
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // The first operation holds the actual session gate while every later enable queues
            queued = Enumerable.Range(0, 15).Select(_ => fixture.Service.SetWithTransitionAsync(true)).ToArray();
            LocalGodmodeFixture.Check(queued.All(task => !task.IsCompleted), "Requests did not wait for the held owner");
            fixture.BeforeCurrentRead = null;
            release.Set();
            var results = await Task.WhenAll(queued.Prepend(first)).WaitAsync(TimeSpan.FromSeconds(5));
            LocalGodmodeFixture.Check(results.All(result => result.Succeeded)
                && results.Count(result => result.ConfirmedOwnedWrite) == 1 && fixture.Flags.Writes == 1,
                "Queued enable requests must attribute exactly one actual transition");
        } finally {
            fixture.BeforeCurrentRead = null;
            release.Set();
            await Task.WhenAll(queued.Prepend(first)).WaitAsync(TimeSpan.FromSeconds(5));
        }
        var off = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(off.Succeeded && off.ConfirmedOwnedWrite && fixture.Flags.Writes == 2,
            "The later OFF receipt must remain separate from the earlier enable requests");
        // Opposite requests overlap while the next enable holds the same real session gate
        release.Reset();
        var oppositeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeCurrentRead = () => { oppositeEntered.TrySetResult(); release.Wait(); };
        var nextOn = Task.Run(() => fixture.Service.SetWithTransitionAsync(true));
        Task<GodmodeTransitionResult>? nextOff = null;
        try {
            await oppositeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            nextOff = fixture.Service.SetWithTransitionAsync(false);
            LocalGodmodeFixture.Check(!nextOff.IsCompleted, "OFF did not wait for the held enable");
            fixture.BeforeCurrentRead = null;
            release.Set();
            var onResult = await nextOn.WaitAsync(TimeSpan.FromSeconds(5));
            var offResult = await nextOff.WaitAsync(TimeSpan.FromSeconds(5));
            LocalGodmodeFixture.Check(onResult.Succeeded && onResult.ConfirmedOwnedWrite
                && offResult.Succeeded && offResult.ConfirmedOwnedWrite && fixture.Flags.Writes == 4
                && fixture.BindCalls == 1 && fixture.Flags.Flags == 0x200,
                "Overlapping ON and OFF must each retain the receipt from their own gated operation");
        } finally {
            fixture.BeforeCurrentRead = null;
            release.Set();
            await nextOn.WaitAsync(TimeSpan.FromSeconds(5));
            if (nextOff is not null) await nextOff.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
