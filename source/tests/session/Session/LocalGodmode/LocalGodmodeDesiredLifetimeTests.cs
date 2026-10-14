using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

// Desired requests keep the existing account, shutdown, and cleanup barriers
internal static class LocalGodmodeDesiredLifetimeTests {
    public static async Task CloseDuringDesiredBindingRetainsTheAdapterWithoutAnEnableReceipt() {
        await using var fixture = new LocalGodmodeFixture(2);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeBind = () => { entered.TrySetResult(); release.Wait(); };
        var enabling = Task.Run(() => fixture.Service.SetWithTransitionAsync(true));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Service.RequestClose();
            var closing = fixture.Service.CloseAsync();
            LocalGodmodeFixture.Check(!closing.IsCompleted, "Close passed the held binding factory");
            release.Set();
            var result = await enabling.WaitAsync(TimeSpan.FromSeconds(5));
            var closed = await closing.WaitAsync(TimeSpan.FromSeconds(5));
            LocalGodmodeFixture.Check(!result.Succeeded && !result.ConfirmedOwnedWrite
                && closed.Stopped && closed.Restored && fixture.BindCalls == 1 && fixture.Flags.Writes == 0
                && fixture.Service.ReadView().HasBoundTarget,
                "Close must join the retained adapter without admitting the delayed enable");
        } finally {
            fixture.BeforeBind = null;
            release.Set();
            await enabling.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    public static async Task DesiredEnableCannotRecoverAnObservedAccountOrTimeRetirement() {
        foreach (bool replaceAccount in new[] { true, false }) {
            await using var fixture = new LocalGodmodeFixture(3);
            LocalGodmodeFixture.Check(await fixture.Service.CaptureAsync(), "Initial capture failed");
            var original = fixture.Binding;
            if (replaceAccount) fixture.Binding = original with { AccountId = original.AccountId + 1 };
            else fixture.CommandTime--;
            var retired = await fixture.Service.SetWithTransitionAsync(true);
            fixture.Binding = original;
            fixture.CommandTime = 1000;
            var recovered = await fixture.Service.SetWithTransitionAsync(true);
            LocalGodmodeFixture.Check(!retired.Succeeded && !retired.ConfirmedOwnedWrite
                && !recovered.Succeeded && !recovered.ConfirmedOwnedWrite
                && fixture.Flags.Writes == 0 && fixture.BindCalls == 1,
                "Restored scalar values cannot revive the retired original local owner");
        }
    }

    public static async Task RejectedPartiallyAppliedEnableRetainsCleanupWithoutAConfirmedReceipt() {
        await using var fixture = new LocalGodmodeFixture();
        fixture.Flags.OnWrite = (_, _, desired) => {
            fixture.Flags.Flags = desired;
            return false;
        };
        var failed = await fixture.Service.SetWithTransitionAsync(true);
        LocalGodmodeFixture.Check(!failed.Succeeded && !failed.ConfirmedOwnedWrite
            && fixture.Flags.Flags == 0x201 && fixture.Service.ReadView().HasPendingCleanup,
            "Rejected partial enable must retain cleanup without claiming confirmed ownership");
        fixture.Flags.OnWrite = null;
        var restored = await fixture.Service.SetWithTransitionAsync(false);
        LocalGodmodeFixture.Check(restored.Succeeded && restored.ConfirmedOwnedWrite
            && fixture.Flags.Flags == 0x200 && fixture.BindCalls == 1,
            "Only the successful cleanup write may produce a confirmed transition receipt");
    }

    public static async Task CancellationAfterConfirmedOffDoesNotEraseTheActualRestorationReceipt() {
        await using var fixture = new LocalGodmodeFixture();
        LocalGodmodeFixture.Check(await fixture.Service.SetAsync(true), "Setup enable failed");
        using var cancellation = new CancellationTokenSource();
        fixture.Flags.OnWrite = (_, _, _) => { cancellation.Cancel(); return true; };
        var restored = await fixture.Service.SetWithTransitionAsync(false, cancellation.Token);
        LocalGodmodeFixture.Check(restored.Succeeded && restored.ConfirmedOwnedWrite
            && cancellation.IsCancellationRequested && fixture.Flags.Flags == 0x200,
            "Cancellation after confirmed restoration cannot erase the write needed by aggregate rollback");
    }
}
