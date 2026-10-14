using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay;
using Features.Tests;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class SharedGodmodeAdmissionTests {
    public static async Task QueuedPersonalAndPeerTogglesUseTheStateLeftByAnEarlierSharedEnable() {
        foreach (int toggledSlot in new[] { 0, 3 }) {
            await using var fixture = new SharedGodmodeFixture();
            await fixture.CaptureLocalAsync();
            Check(await fixture.Owner.Teammates.SelectAsync(0, "Host"), "Peer capture failed");
            using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Transport.OnLocalRead = () => { entered.TrySetResult(); release.Wait(); };
            var enabling = fixture.Owner.GodmodeOwners.SetBatchAsync(new[] { fixture.Identity(3), fixture.Identity(0) }, true);
            Task? queued = null;
            try {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (toggledSlot == 0) queued = fixture.Owner.Teammates.ToggleAsync(0, "Host");
                else queued = fixture.Owner.GodmodeOwners.ToggleAsync(fixture.Identity(3));
                Check(!queued.IsCompleted, "The toggle bypassed the held shared registry gate");
                fixture.Transport.OnLocalRead = null;
                release.Set();
                Check((await enabling.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded, "Shared enable failed");
                await queued.WaitAsync(TimeSpan.FromSeconds(5));
                Check(fixture.Transport.Flags(toggledSlot) == 0x200
                    && fixture.Transport.Flags(toggledSlot == 0 ? 3 : 0) == 0x201
                    && fixture.Transport.FlagWrites == 3 && fixture.Transport.LocalBindCalls == 1
                    && fixture.Transport.TeammateBindCalls == 1,
                    "The queued toggle used pre-batch state or created another writer");
            } finally {
                fixture.Transport.OnLocalRead = null;
                release.Set();
                await enabling.WaitAsync(TimeSpan.FromSeconds(5));
                if (queued is not null) await queued.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    public static async Task FreshAccountProofFailureWithdrawsThePersonalMenuCapability() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(session.ReadMenuState().CanPersonalGodmode, "Initial personal capability was unavailable");
            transport.LocalAccountId++;
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            var frame = session.ReadMenuState();
            Check(frame.PersonalGodmodeValue == "UNKNOWN" && !frame.CanPersonalGodmode && transport.FlagWrites == 0,
                "Rejected fresh account proof left the cached personal checkbox actionable");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task UnregisteredLocalOwnerIsStillJoinedByConnectionCleanup() {
        await using var fixture = new SharedGodmodeFixture();
        Check(await fixture.Owner.LocalGodmode!.CaptureAsync()
            && await fixture.Owner.LocalGodmode.SetAsync(true), "Unregistered local owner setup failed");
        Check(!fixture.Owner.GodmodeOwners.OwnsFeature(fixture.Owner.LocalGodmode),
            "The fixture unexpectedly registered its standalone local owner");
        var closed = await fixture.Owner.CloseGodmodeOwnersAsync();
        Check(closed.SafeToRelease && fixture.Transport.Flags(3) == 0x200
            && fixture.Transport.LocalFlagWrites == 2 && fixture.Transport.TeammateBindCalls == 0,
            "Connection cleanup skipped or duplicated an unregistered original local owner");
    }

    public static async Task DisplayIdentityValidationCannotAuthorizeARegistryWrite() {
        var flags = new RecordingFlagsTransport();
        await using var fixture = new SharedGodmodeFixture();
        var identity = fixture.Identity(3);
        bool current = true;
        int displayReads = 0;
        var registry = new PlayerGodmodeRegistry(_ => new FlagsPlayerGodmodeFeature(flags, TimeSpan.FromHours(1)),
            _ => current, _ => { displayReads++; return true; });
        try {
            Check((await registry.CaptureAsync(new[] { identity })).Succeeded, "Registry capture failed");
            current = false;
            Check(registry.SnapshotForDisplay(new[] { identity }).Value == GodmodeValue.Off && displayReads > 0,
                "The display-only validation seam was not exercised");
            Check(!(await registry.ToggleAsync(identity)).Succeeded
                && !(await registry.SetAsync(identity, true)).Succeeded && flags.Writes == 0,
                "Display-only identity evidence reached mutation admission");
        } finally { await registry.CloseAsync(); }
    }
}
