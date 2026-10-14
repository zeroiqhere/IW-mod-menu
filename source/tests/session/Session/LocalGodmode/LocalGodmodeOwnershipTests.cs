using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

internal static class LocalGodmodeOwnershipTests {
    public static async Task ForeignFlagAppearingAfterCaptureCannotEnableOrBorrowOwnership() {
        await using var fixture = new LocalGodmodeFixture();
        await fixture.Service.CaptureAsync();
        fixture.Flags.Flags = 0x201;
        LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "A fresh foreign bit was borrowed");
        LocalGodmodeFixture.Check(fixture.Service.ReadView().Value == GodmodeValue.Unknown
            && fixture.Flags.Writes == 0, "A captured OFF baseline cannot authorize a later foreign flag");
        await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(fixture.Flags.Flags == 0x201, "Cleanup cannot clear an unowned flag");
    }

    public static async Task ActorReplacementAndCommandRollbackRejectNewWritesRetainingOriginalBinding() {
        foreach (bool changedActor in new[] { false, true }) {
            await using var fixture = new LocalGodmodeFixture(2);
            await fixture.Service.CaptureAsync();
            var original = fixture.Binding;
            fixture.CommandTime += 100;
            await fixture.Service.CaptureAsync();
            if (changedActor) fixture.Binding = original with { Session = original.Session + 8 };
            else fixture.CommandTime = 1050;
            LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "A replaced actor or rolled-back command was accepted");
            var rejected = fixture.Service.ReadView();
            LocalGodmodeFixture.Check(rejected.Binding == original && rejected.Value == GodmodeValue.Unknown
                && fixture.BindCalls == 1 && fixture.Flags.Writes == 0, "A rejected action must keep the original cleanup identity");
            await fixture.Service.CloseAsync();
        }
    }

    public static async Task ReplacementsAfterConfirmedOffRemainRejectedUntilReconnect() {
        await using var fixture = new LocalGodmodeFixture();
        try {
            await fixture.Service.ToggleAsync();
            await fixture.Service.ToggleAsync();
            var original = fixture.Binding;
            fixture.Binding = original with { AccountId = original.AccountId + 1 };
            LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync() && !await fixture.Service.ToggleAsync(),
                "The conservative local service cannot silently follow a new account after OFF");
            LocalGodmodeFixture.Check(fixture.Service.ReadView().Binding == original && fixture.BindCalls == 1
                && fixture.Flags.Writes == 2, "A replacement cannot consume another transport or native write");
        } finally { await fixture.Service.CloseAsync(); }
    }

    public static async Task OwnedFlagsChangedToDifferentWordRetainUnconfirmedCleanup() {
        await using var fixture = new LocalGodmodeFixture();
        await fixture.Service.ToggleAsync();
        fixture.Flags.Flags = 0x401;
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync(), "A changed owned word was confirmed as ON");
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(view.Value == GodmodeValue.Unknown && view.HasPendingCleanup
            && view.Binding == fixture.Binding, "Unknown owned flags must keep the original cleanup owner");
        var failed = await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(failed.Stopped && !failed.Restored && fixture.Flags.Flags == 0x401,
            "Cleanup must not overwrite unrelated changed bits");
        fixture.Flags.Flags = 0x201;
        LocalGodmodeFixture.Check((await fixture.Service.CloseAsync()).Restored && fixture.Flags.Flags == 0x200,
            "Cleanup retry must still use the original confirmed word");
    }

    public static async Task CaptureReadFailureWhileOnPreservesCleanupOwner() {
        await using var fixture = new LocalGodmodeFixture(1);
        await fixture.Service.ToggleAsync();
        var original = fixture.Binding;
        fixture.ReadFailure = true;
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync(), "The strict read unexpectedly succeeded");
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(view.Binding == original && view.Value == GodmodeValue.Unknown
            && view.HasPendingCleanup, "A failed read cannot abandon an owned flag");
        var close = await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(close.Stopped && close.Restored && fixture.Flags.Flags == 0x200,
            "Original cleanup does not depend on the strict capture callback");
    }
    public static async Task ActualWriterBaselineIsRetainedWhenFlagsChangeAfterStrictCapture() {
        await using var fixture = new LocalGodmodeFixture();
        // The native feature may observe unrelated bits after the earlier strict capture
        fixture.Flags.OnRead = count => {
            if (count == 1) fixture.Flags.Flags = 0x400;
            return fixture.Flags.Flags;
        };
        try {
            LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "The actual writer baseline was not enabled");
            LocalGodmodeFixture.Check(await fixture.Service.CaptureAsync()
                && fixture.Service.ReadView().Value == GodmodeValue.On && fixture.Flags.Flags == 0x401,
                "Owned ON must use the actual confirmed writer word, not a stale capture word");
        } finally {
            fixture.Flags.OnRead = null;
            await fixture.Service.CloseAsync();
        }
        LocalGodmodeFixture.Check(fixture.Flags.Flags == 0x400, "Cleanup must preserve the actual original unrelated bits");
    }

    public static async Task RejectedPartialEnableRetainsOwnerAndOffCanRestoreIt() {
        await using var fixture = new LocalGodmodeFixture();
        // A native rejection can leave the desired word applied before reporting failure
        fixture.Flags.OnWrite = (count, _, desired) => {
            if (count != 1) return true;
            fixture.Flags.Flags = desired;
            return false;
        };
        try {
            LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "An unconfirmed partial write was reported as ON");
            var failed = fixture.Service.ReadView();
            LocalGodmodeFixture.Check(failed.Value == GodmodeValue.Unknown && failed.HasPendingCleanup
                && failed.Binding == fixture.Binding, "A partially applied rejected write lost its cleanup owner");
            LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync() && fixture.Flags.Flags == 0x200,
                "Owned OFF must restore a partially applied rejected enable");
        } finally {
            fixture.Flags.OnWrite = null;
            await fixture.Service.CloseAsync();
        }
    }

    public static async Task ObservedActorRetirementCannotRestoreAfterOriginalFactsReturn() {
        foreach (bool changedBinding in new[] { false, true }) {
            await using var fixture = new LocalGodmodeFixture();
            LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Retirement setup enable failed");
            var original = fixture.Binding;
            if (changedBinding) fixture.Binding = original with { AccountId = original.AccountId + 1 };
            else fixture.CommandTime = 999;
            LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync(), "A replaced or rolled-back actor was accepted");

            // A recovered scalar must not revive a writer retired by an earlier complete observation
            fixture.Binding = original;
            fixture.CommandTime = 1000;
            LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "Recovered facts revived the retired OFF writer");
            var closed = await fixture.Service.CloseAsync();
            LocalGodmodeFixture.Check(closed.Stopped && !closed.Restored && fixture.Flags.Writes == 1
                && fixture.Flags.Flags == 0x201 && fixture.Service.ReadView().HasPendingCleanup,
                "Retired actor cleanup must stay unknown without writing after scalar recovery");

            // Only the end of the original process can release this unconfirmed ownership
            fixture.Flags.Connected = false;
            LocalGodmodeFixture.Check((await fixture.Service.CloseAsync()).Restored,
                "An ended original process cannot retain a pending flag change");
        }
    }

    public static async Task RetiredBoundTransportCannotRepublishOffOrEnableCapability() {
        await using var fixture = new LocalGodmodeFixture();
        // The real bound adapter permanently retires after a failed native read
        fixture.Flags.OnRead = _ => throw new IOException("Owned native flags read failed");
        LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync(), "A failing native baseline read was accepted");
        LocalGodmodeFixture.Check(fixture.Flags.Writes == 0, "A failed baseline read reached the writer");

        fixture.Flags.OnRead = null;
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync(),
            "A recovered strict source revived a permanently retired bound transport");
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(view.Value == GodmodeValue.Unknown && !view.CanToggle
            && view.Binding == fixture.Binding && fixture.BindCalls == 1 && fixture.Flags.Writes == 0,
            "An unusable retained writer cannot claim a fresh OFF capability or silently rebind");
    }

}
