using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

internal static class LocalGodmodeCleanupTests {
    public static async Task TurningOffAfterDeathDoesNotRequireStrictLivingRead() {
        await using var fixture = new LocalGodmodeFixture(3);
        await fixture.Service.ToggleAsync();
        fixture.Health = 0;
        fixture.SessionState = 1;
        fixture.ReadFailure = true;
        int reads = fixture.CurrentReads;
        LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Strict living eligibility blocked owned OFF");
        LocalGodmodeFixture.Check(fixture.Flags.Flags == 0x200 && fixture.CurrentReads == reads
            && fixture.Service.ReadView().Value == GodmodeValue.Off, "OFF must use only the retained original flags transport");
        LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "A later new enable bypassed the strict dead source");
        await fixture.Service.CloseAsync();
    }

    public static async Task GameClearedFlagIsNotReenabledByOffOrCleanup() {
        await using var fixture = new LocalGodmodeFixture();
        await fixture.Service.ToggleAsync();
        fixture.Flags.Flags = 0x200;
        int writes = fixture.Flags.Writes;
        LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "An already cleared owned flag could not finish OFF");
        var close = await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(close.Stopped && close.Restored && fixture.Flags.Writes == writes
            && fixture.Flags.Flags == 0x200, "Game-cleared protection must never be reapplied by restoration");
    }

    public static async Task FailedRestoreRetainsBindingAndRetryRestoresOriginalFlags() {
        await using var fixture = new LocalGodmodeFixture(2);
        await fixture.Service.ToggleAsync();
        var original = fixture.Binding;
        fixture.Flags.OnWrite = (_, _, desired) => desired != 0x200;
        var failed = await fixture.Service.CloseAsync();
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(failed.Stopped && !failed.Restored && view.HasPendingCleanup
            && view.Binding == original && !view.CanToggle, "Failed restoration must preserve the original closed owner");
        LocalGodmodeFixture.Check(view.Status.Contains("Restoration", StringComparison.Ordinal),
            "A finished failed close must retain its restoration failure status");
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync() && !await fixture.Service.ToggleAsync(),
            "A closing service cannot retarget or enable while cleanup remains pending");
        fixture.Flags.OnWrite = null;
        var retried = await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(retried.Stopped && retried.Restored && !fixture.Service.ReadView().HasPendingCleanup
            && fixture.Flags.Flags == 0x200 && fixture.BindCalls == 1, "Retry must restore through the same feature and binding");
    }

    public static async Task CancelledAdmissionDoesNotReadOrWriteAndCloseIsIdempotent() {
        await using var fixture = new LocalGodmodeFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync(cancellation.Token)
            && !await fixture.Service.ToggleAsync(cancellation.Token), "Cancelled admission was accepted");
        LocalGodmodeFixture.Check(fixture.CurrentReads == 0 && fixture.BindCalls == 0 && fixture.Flags.Writes == 0,
            "Cancellation before admission cannot reach source or writer");
        var first = await fixture.Service.CloseAsync();
        var second = await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(first.Stopped && first.Restored && second.Stopped && second.Restored,
            "Closing an unused service must be safe to repeat");
        LocalGodmodeFixture.Check(fixture.Service.ReadView().Status == "Closed",
            "Finished cleanup must not keep reporting an operation in progress");
    }

    public static async Task CloseJoinsHeldRealMonitorAndCachedViewsRemainNonblocking() {
        await using var fixture = new LocalGodmodeFixture(interval: TimeSpan.FromMilliseconds(1));
        LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Monitor setup enable failed");
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Flags.OnRead = _ => {
            entered.TrySetResult();
            release.Wait();
            return fixture.Flags.Flags;
        };
        Task? close = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            close = fixture.Service.CloseAsync();
            int reads = fixture.Flags.Reads;
            for (int index = 0; index < 1000; index++) {
                var view = fixture.Service.ReadView();
                LocalGodmodeFixture.Check(!view.CanToggle && view.Value != GodmodeValue.On,
                    "Closing views cannot confirm active protection while its actual monitor is still running");
            }
            LocalGodmodeFixture.Check(!close.IsCompleted && fixture.Flags.Reads == reads,
                "Close must join the held monitor without native reads on frame publication");
        } finally {
            fixture.Flags.OnRead = null;
            release.Set();
            if (close is not null) await close.WaitAsync(TimeSpan.FromSeconds(5));
            else await fixture.Service.CloseAsync();
        }
        LocalGodmodeFixture.Check(fixture.Flags.Flags == 0x200, "Joined cleanup must restore original flags");
    }

    public static async Task ClosingRejectsQueuedCapturesAndNewEnables() {
        await using var fixture = new LocalGodmodeFixture();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeCurrentRead = () => {
            entered.TrySetResult();
            release.Wait();
        };
        Task<bool> capture = Task.Run(() => fixture.Service.CaptureAsync());
        Task<bool>? queued = null;
        Task? close = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = fixture.Service.CaptureAsync();
            close = fixture.Service.CloseAsync();
            LocalGodmodeFixture.Check(!fixture.Service.ReadView().CanToggle, "Close must disarm admission before waiting for the service gate");
            LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "A new enable entered a closing service");
        } finally {
            fixture.BeforeCurrentRead = null;
            release.Set();
            await capture.WaitAsync(TimeSpan.FromSeconds(5));
            if (queued is not null) LocalGodmodeFixture.Check(!await queued.WaitAsync(TimeSpan.FromSeconds(5)),
                "A queued capture cannot become a replacement after close starts");
            if (close is not null) await close.WaitAsync(TimeSpan.FromSeconds(5));
            else await fixture.Service.CloseAsync();
        }
        LocalGodmodeFixture.Check(fixture.BindCalls == 0 && fixture.Flags.Writes == 0,
            "Late capture completion cannot create an adapter after shutdown admission");
    }
}
