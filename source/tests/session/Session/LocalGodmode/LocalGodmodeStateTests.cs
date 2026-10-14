using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

internal static class LocalGodmodeStateTests {
    public static Task UnknownViewStartsWithoutReadsOrBinding() {
        var fixture = new LocalGodmodeFixture();
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(view.Binding is null && view.Identity is null
            && view.Value == GodmodeValue.Unknown && !view.CanToggle && !view.HasPendingCleanup,
            "An uncaptured local service must remain unknown");
        LocalGodmodeFixture.Check(fixture.CurrentReads == 0 && fixture.BindCalls == 0
            && fixture.Flags.Reads == 0 && fixture.Flags.Writes == 0, "Initial views must not access their source");
        return Task.CompletedTask;
    }

    public static async Task CaptureInitialOffIsReadOnlyAndCachesFullBinding() {
        await using var fixture = new LocalGodmodeFixture(3);
        LocalGodmodeFixture.Check(await fixture.Service.CaptureAsync(), "The verified OFF actor was not captured");
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(view.Binding == fixture.Binding && view.Identity == fixture.Binding.Identity
            && view.Value == GodmodeValue.Off && view.CanToggle && !view.HasPendingCleanup,
            "Read-only capture must retain the full local account/actor binding");
        LocalGodmodeFixture.Check(fixture.BindCalls == 1 && fixture.Flags.Writes == 0,
            "Lazy binding cannot write a flag during capture");
        await fixture.Service.CloseAsync();
    }

    public static async Task CaptureForeignFlagNeverClaimsOffOrOwnedOn() {
        await using var fixture = new LocalGodmodeFixture(2);
        fixture.Flags.Flags = 0x201;
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync(), "An unowned enabled flag was accepted");
        var view = fixture.Service.ReadView();
        LocalGodmodeFixture.Check(view.Binding == fixture.Binding && view.Value == GodmodeValue.Unknown
            && !view.CanToggle && !view.HasPendingCleanup, "A foreign flag cannot be OFF or owned ON");
        LocalGodmodeFixture.Check(!await fixture.Service.ToggleAsync() && fixture.Flags.Writes == 0,
            "A foreign flag cannot be adopted by a new enable");
        var close = await fixture.Service.CloseAsync();
        LocalGodmodeFixture.Check(close.Stopped && close.Restored && fixture.Flags.Flags == 0x201,
            "No owned change may clear another writer's flag");
    }

    public static async Task ToggleUsesNativeBitForAnyVerifiedLocalSlotAndRestoresOtherBits() {
        foreach (int slot in new[] { 0, 1, 2, 3 }) {
            await using var fixture = new LocalGodmodeFixture(slot);
            try {
                LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Verified local native enable failed");
                var enabled = fixture.Service.ReadView();
                LocalGodmodeFixture.Check(enabled.Value == GodmodeValue.On && enabled.Binding == fixture.Binding
                    && fixture.Flags.Flags == 0x201, "The service must own only bit zero for the actual local slot");
                LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Owned OFF failed");
                LocalGodmodeFixture.Check(fixture.Flags.Flags == 0x200 && fixture.Service.ReadView().Value == GodmodeValue.Off,
                    "OFF must restore the original flag word");
            } finally { await fixture.Service.CloseAsync(); }
        }
    }

    public static async Task ReadViewDoesNotReadNativeSourceOrBindTransport() {
        await using var fixture = new LocalGodmodeFixture();
        try {
            LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Setup enable failed");
            int source = fixture.CurrentReads;
            int flags = fixture.Flags.Reads;
            int writes = fixture.Flags.Writes;
            var retained = fixture.Service.ReadView();
            for (int index = 0; index < 1000; index++) fixture.Service.ReadView();
            LocalGodmodeFixture.Check(fixture.CurrentReads == source && fixture.Flags.Reads == flags
                && fixture.Flags.Writes == writes && fixture.BindCalls == 1, "Frame views must use immutable cached metadata only");
            LocalGodmodeFixture.Check(retained.Value == GodmodeValue.On && retained.Binding == fixture.Binding,
                "The retained view must remain an immutable copied value");
        } finally { await fixture.Service.CloseAsync(); }
    }

    public static async Task ReadAndBindFailuresRemainUnknownAndCanRetrySameActor() {
        await using var fixture = new LocalGodmodeFixture();
        fixture.ReadFailure = true;
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync() && !fixture.Service.ReadView().CanToggle,
            "A failed source cannot produce OFF eligibility");
        fixture.ReadFailure = false;
        fixture.BindFailure = true;
        LocalGodmodeFixture.Check(!await fixture.Service.CaptureAsync() && fixture.Service.ReadView().Value == GodmodeValue.Unknown,
            "A failed factory cannot publish a feature baseline");
        fixture.BindFailure = false;
        LocalGodmodeFixture.Check(await fixture.Service.CaptureAsync() && fixture.Service.ReadView().Value == GodmodeValue.Off,
            "The same verified actor can retry a read-only binding");
        LocalGodmodeFixture.Check(fixture.Flags.Writes == 0, "Capture failures and retry cannot write flags");
        await fixture.Service.CloseAsync();
    }
}
