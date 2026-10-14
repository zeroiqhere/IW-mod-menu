using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

// These checks exercise the retained real adapter and feature without process access
internal static class LocalGodmodePreflightTests {
    private static void Check(bool value, string reason) => LocalGodmodeFixture.Check(value, reason);
    private static IGodmodeTransitionFeature Owner(LocalGodmodeFixture fixture) => fixture.Service;

    public static async Task UnboundPreflightAndCachedPropertiesNeverReadOrCreateAnOwner() {
        await using var fixture = new LocalGodmodeFixture();
        var owner = Owner(fixture);
        Check(!owner.Connected && !owner.Active && !owner.HasPendingRestore, "An unbound session has no native owner");
        Check(!owner.Preflight(true).Succeeded && !owner.Preflight(false).Succeeded,
            "Unbound requests must stay unknown without borrowing an actor");
        Check(fixture.CurrentReads == 0 && fixture.Flags.Reads == 0 && fixture.BindCalls == 0
            && fixture.Flags.Writes == 0, "Cached properties and unbound preflight performed I/O");
    }

    public static async Task CapturedOffAndRepeatedOwnedOnPreflightNeverWriteOrRebind() {
        await using var fixture = new LocalGodmodeFixture(3);
        Check(await fixture.Service.CaptureAsync(), "Capture failed");
        var owner = Owner(fixture);
        // Clear unrelated bits are captured by the primitive only when it actually enables
        fixture.Flags.Flags = 0x400;
        int strict = fixture.CurrentReads;
        var off = owner.Preflight(true);
        Check(off.Succeeded && off.Value == GodmodeValue.Off && fixture.CurrentReads == strict + 1,
            "Enable preflight must obtain a fresh strict sample for the retained actor");
        Check(fixture.Flags.Writes == 0 && fixture.BindCalls == 1, "Preflight changed flags or rebound the owner");
        var enabled = await owner.SetWithTransitionAsync(true);
        var repeated = owner.Preflight(true);
        Check(enabled.Succeeded && enabled.ConfirmedOwnedWrite && repeated.Succeeded
            && repeated.Value == GodmodeValue.On && owner.Active && owner.HasPendingRestore,
            "An exact owned ON must remain owned through repeated preflight");
        Check(fixture.Flags.Writes == 1 && fixture.BindCalls == 1, "Repeated preflight added a writer or write");
        int reads = fixture.CurrentReads + fixture.Flags.Reads;
        _ = owner.Connected; _ = owner.Active; _ = owner.HasPendingRestore; _ = owner.FailureReason;
        Check(reads == fixture.CurrentReads + fixture.Flags.Reads, "Cached feature properties performed native I/O");
        Check(await owner.SetAsync(false) && fixture.Flags.Flags == 0x400 && fixture.Flags.Writes == 2,
            "The original primitive must restore the actual clear baseline captured at enable");
        // Confirmed cleanup releases flag-word ownership before a later clear baseline changes
        fixture.Flags.Flags = 0x600;
        Check(owner.Preflight(true).Succeeded && owner.Preflight(false).Value == GodmodeValue.Off,
            "A restored owner must not reserve unrelated clear bits forever");
    }

    public static async Task DeadOwnerOffPreflightUsesOnlyRetainedReadsAndCanRecoverLivingEnable() {
        await using var fixture = new LocalGodmodeFixture(2);
        Check(await fixture.Service.SetAsync(true), "Enable failed");
        var owner = Owner(fixture);
        fixture.Health = 0;
        int strict = fixture.CurrentReads;
        var off = owner.Preflight(false);
        Check(off.Succeeded && off.Value == GodmodeValue.On && fixture.CurrentReads == strict
            && fixture.Flags.Writes == 1, "Dead-owner OFF must validate ownership without strict reads or writes");
        Check(!owner.Preflight(true).Succeeded && fixture.Flags.Writes == 1,
            "A dead actor cannot receive new ON admission");
        var rejectedView = fixture.Service.ReadView();
        Check(rejectedView.Value == GodmodeValue.Unknown && !rejectedView.CanToggle
            && rejectedView.CanRestore && rejectedView.HasPendingCleanup,
            "Life-only denial must withdraw new ON while retaining the exact cleanup capability");
        var restored = await owner.SetWithTransitionAsync(false);
        Check(restored.Succeeded && restored.ConfirmedOwnedWrite && fixture.Flags.Flags == 0x200,
            "Life-independent OFF did not restore the original owner");
        fixture.Health = 100;
        Check(owner.Preflight(true).Succeeded, "A life-only refusal poisoned the retained owner");
    }

    public static async Task ForeignWordsAndInactivePendingRestoreCannotPassEnablePreflight() {
        await using var fixture = new LocalGodmodeFixture();
        Check(await fixture.Service.SetAsync(true), "Enable failed");
        fixture.Flags.Flags = 0x401;
        var owner = Owner(fixture);
        Check(!owner.Preflight(true).Succeeded && !owner.Preflight(false).Succeeded
            && fixture.Flags.Writes == 1 && owner.HasPendingRestore,
            "A foreign changed word must not become owned ON or OFF");
        fixture.Flags.Flags = 0x201;
        await using var pending = new LocalGodmodeFixture();
        pending.Flags.OnWrite = (_, _, desired) => { pending.Flags.Flags = desired; return false; };
        Check(!await pending.Service.SetAsync(true), "Partial-write setup unexpectedly succeeded");
        var pendingOwner = Owner(pending);
        Check(!pendingOwner.Active && pendingOwner.HasPendingRestore
            && !pendingOwner.Preflight(true).Succeeded && pendingOwner.Preflight(false).Succeeded,
            "An inactive pending change permits cleanup only");
        pending.Flags.OnWrite = null;
    }

    public static async Task ObservedBindingReplacementAndTimeRollbackRetireTheOriginalOwner() {
        foreach (bool replacement in new[] { true, false }) {
            await using var fixture = new LocalGodmodeFixture();
            Check(await fixture.Service.CaptureAsync(), "Capture failed");
            var original = fixture.Binding;
            Check(Owner(fixture).Preflight(true).Succeeded, "Initial retained actor preflight failed");
            if (replacement) fixture.Binding = original with { AccountId = original.AccountId + 1 };
            else fixture.CommandTime--;
            var owner = Owner(fixture);
            Check(!owner.Preflight(true).Succeeded, "Changed binding or rollback passed strict preflight");
            Check(fixture.Service.ReadView().Value == GodmodeValue.Unknown && !fixture.Service.ReadView().CanToggle,
                "A proven strict binding failure left an actionable cached checkbox");
            fixture.Binding = original; fixture.CommandTime = 1000;
            Check(!owner.Preflight(true).Succeeded && !owner.Preflight(false).Succeeded
                && fixture.Flags.Writes == 0 && fixture.BindCalls == 1,
                "Recovered scalars revived a retired original writer");
        }
        await using var unreadable = new LocalGodmodeFixture();
        Check(await unreadable.Service.CaptureAsync(), "Capture failed");
        unreadable.Flags.OnRead = _ => throw new IOException("Owned observation failed");
        Check(!Owner(unreadable).Preflight(false).Succeeded, "An unreadable retained actor passed OFF inspection");
        Check(unreadable.Service.ReadView().Value == GodmodeValue.Unknown && !unreadable.Service.ReadView().CanToggle,
            "A failed retained proof left the cached OFF capability actionable");
        unreadable.Flags.OnRead = null;
        Check(Owner(unreadable).Connected && !Owner(unreadable).Preflight(true).Succeeded
            && !Owner(unreadable).Preflight(false).Succeeded && unreadable.Flags.Writes == 0,
            "Recovered bytes revived a retired adapter or pretended its process ended");
    }

    public static async Task BusyPreflightAndClosingEnableFailWithoutEnteringCallbacks() {
        await using var fixture = new LocalGodmodeFixture();
        Check(await fixture.Service.CaptureAsync(), "Capture failed");
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeCurrentRead = () => { entered.TrySetResult(); release.Wait(); };
        var running = Task.Run(() => fixture.Service.SetAsync(true));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            int reads = fixture.CurrentReads + fixture.Flags.Reads;
            var owner = Owner(fixture);
            Check(!owner.Preflight(true).Succeeded && !owner.Preflight(false).Succeeded
                && reads == fixture.CurrentReads + fixture.Flags.Reads,
                "Busy synchronous preflight entered a callback instead of failing promptly");
            fixture.Service.RequestClose();
            Check(!owner.Preflight(true).Succeeded, "Closing admitted a new enable");
        } finally {
            fixture.BeforeCurrentRead = null; release.Set();
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Check(fixture.Flags.Writes == 0 && Owner(fixture).Preflight(false).Succeeded,
            "Close admission wrote ON or blocked exact retained OFF inspection");
    }

    public static async Task DisconnectedCleanupAndExplicitInterfaceCloseJoinTheOriginalOwner() {
        await using var fixture = new LocalGodmodeFixture();
        Check(await fixture.Service.SetAsync(true), "Enable failed");
        var owner = Owner(fixture);
        fixture.Flags.Connected = false;
        int reads = fixture.CurrentReads + fixture.Flags.Reads;
        var cleanup = owner.Preflight(false);
        Check(cleanup.Succeeded && cleanup.Value == GodmodeValue.Unknown && !owner.Preflight(true).Succeeded
            && reads == fixture.CurrentReads + fixture.Flags.Reads,
            "Disconnected cleanup should not inspect ended-process flags or admit ON");
        var closed = await ((IPlayerGodmodeFeature)owner).CloseAsync();
        Check(closed.SafeToRelease && !owner.Active && !owner.HasPendingRestore && fixture.Flags.Writes == 1,
            "The interface close must join and release only the original ended-process owner");
    }
}
