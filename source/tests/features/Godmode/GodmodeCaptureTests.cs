using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

// Capture exercises real flag reads without starting features or restoring flags
internal static class GodmodeCaptureTests {
    public static async Task InitialCaptureConfirmsAllOffWithoutAnySetOrWriteAndReusesBindings() {
        var fixture = new GodmodeRegistryFixture();
        var registry = fixture.Create();
        try {
            var first = await registry.CaptureAsync(fixture.Players);
            TestAssert.Equal(true, first.Succeeded);
            TestAssert.Equal(GodmodeValue.Off, first.Snapshot.Value);
            TestAssert.Equal(false, first.Snapshot.HasPendingCleanup);
            TestAssert.Equal(4, fixture.Factories);
            TestAssert.Equal(true, (await registry.CaptureAsync(fixture.Players)).Succeeded);
            TestAssert.Equal(4, fixture.Factories);
            foreach (RecordingFlagsTransport transport in fixture.Transports.Values) {
                TestAssert.Equal(0, transport.Writes);
                TestAssert.Equal(true, transport.Reads >= 2);
            }
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task ForeignMemberRejectsAtomicCaptureWithoutHidingExistingOnOrPromotingNewOff() {
        var fixture = new GodmodeRegistryFixture(3);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
            fixture.Transports[fixture.Players[2]].Flags = 0x201;
            var failed = await registry.CaptureAsync(fixture.Players);
            TestAssert.Equal(false, failed.Succeeded);
            TestAssert.Equal(GodmodeValue.On, failed.Snapshot.Members[0].Value);
            TestAssert.Equal(GodmodeValue.Unknown, failed.Snapshot.Members[1].Value);
            TestAssert.Equal(GodmodeValue.Unknown, failed.Snapshot.Members[2].Value);
            TestAssert.Equal(1, fixture.Transports[fixture.Players[0]].Writes);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[2]].Writes);
            fixture.Transports[fixture.Players[2]].Flags = 0x200;
            TestAssert.Equal(true, (await registry.CaptureAsync(fixture.Players)).Succeeded);
            TestAssert.Equal(GodmodeValue.Mixed, registry.Snapshot(fixture.Players).Value);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task ForeignFlagOnPreviouslyCapturedOffBecomesUnknownWithoutRestoration() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.CaptureAsync(fixture.Players)).Succeeded);
            fixture.Transports[fixture.Players[0]].Flags = 0x201;
            var foreign = await registry.CaptureAsync(fixture.Players);
            TestAssert.Equal(false, foreign.Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, foreign.Snapshot.Value);
            TestAssert.Equal(false, foreign.Snapshot.HasPendingCleanup);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[0]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task CapturePreservesPendingOriginalOwnerWithoutSetCloseOrCleanup() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        adapter.OnSet = (desired, _) => {
            adapter.HasPendingRestore = desired;
            return Task.FromResult(false);
        };
        try {
            TestAssert.Equal(false, (await registry.SetAsync(player, true)).Succeeded);
            // Retain cleanup after the synthetic rollback instead of confirming OFF
            adapter.HasPendingRestore = true;
            int sets = adapter.Sets;
            var pending = await registry.CaptureAsync(new[] { player });
            TestAssert.Equal(true, pending.Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, pending.Snapshot.Value);
            TestAssert.Equal(true, pending.Snapshot.HasPendingCleanup);
            TestAssert.Equal(player, pending.Snapshot.Members[0].Identity);
            TestAssert.Equal(sets, adapter.Sets);
            TestAssert.Equal(0, adapter.Closes);
        } finally {
            adapter.OnSet = null;
            await registry.CloseAsync();
        }
    }

    public static async Task ReadOnlyCaptureRejectsRetargetWithoutClosingTheOldCleanBinding() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var changed = player with { Guid = "FFFFFFFFFFFFFFFF" };
        var adapter = new ControlledGodmodeFeature();
        int factories = 0;
        var registry = new PlayerGodmodeRegistry(_ => { factories++; return adapter; }, _ => true);
        try {
            TestAssert.Equal(true, (await registry.CaptureAsync(new[] { player })).Succeeded);
            TestAssert.Equal(false, (await registry.CaptureAsync(new[] { changed })).Succeeded);
            TestAssert.Equal(1, factories);
            TestAssert.Equal(0, adapter.Closes);
            TestAssert.Equal(0, adapter.Sets);
            TestAssert.Equal(GodmodeValue.Off, registry.Snapshot(new[] { player }).Value);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task InvalidRosterAndIdentityFailuresRejectCaptureBeforeAnyFactoryOrRead() {
        int factories = 0;
        var registry = new PlayerGodmodeRegistry(_ => { factories++; return new ControlledGodmodeFeature(); }, _ => false);
        var player = PlayerGodmodeRegistryTests.Identity(0);
        TestAssert.Equal(false, (await registry.CaptureAsync(new[] { player })).Succeeded);
        TestAssert.Equal(false, (await registry.CaptureAsync(new[] { player, player })).Succeeded);
        TestAssert.Equal(false, (await registry.CaptureAsync(Array.Empty<PlayerIdentity>())).Succeeded);
        TestAssert.Equal(0, factories);
        await registry.CloseAsync();
    }

    public static async Task CancellationAfterLastReadDoesNotPublishStagedOffBindings() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        using var cancellation = new CancellationTokenSource();
        fixture.Transports[fixture.Players[1]].OnRead = _ => {
            cancellation.Cancel();
            return 0x200;
        };
        try {
            var cancelled = await registry.CaptureAsync(fixture.Players, cancellation.Token);
            TestAssert.Equal(false, cancelled.Succeeded);
            TestAssert.Equal(true, cancelled.Cancelled);
            TestAssert.Equal(GodmodeValue.Unknown, cancelled.Snapshot.Value);
            TestAssert.Equal(true, cancelled.Snapshot.Members.All(member => member.Value == GodmodeValue.Unknown));
            TestAssert.Equal(0, fixture.Transports[fixture.Players[0]].Writes);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task IdentityChangeAfterPreflightLeavesAllNewCaptureStatesUnknown() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        fixture.Transports[fixture.Players[1]].OnRead = _ => {
            fixture.Valid.Remove(fixture.Players[0]);
            return 0x200;
        };
        try {
            var changed = await registry.CaptureAsync(fixture.Players);
            TestAssert.Equal(false, changed.Succeeded);
            TestAssert.Equal(true, changed.Snapshot.Members.All(member => member.Value == GodmodeValue.Unknown));
            TestAssert.Equal(0, fixture.Transports[fixture.Players[0]].Writes);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[1]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task CaptureWaitsForSetAndReadsItsActualOwnedOnState() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        var entered = TestAssert.Barrier();
        var release = TestAssert.Barrier();
        fixture.Transports[fixture.Players[0]].OnWrite = (count, _, _) => {
            if (count == 1) { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }
            return true;
        };
        try {
            var setting = Task.Run(() => registry.SetAsync(fixture.Players[0], true));
            await entered.Task;
            var capture = registry.CaptureAsync(fixture.Players);
            TestAssert.Equal(false, capture.IsCompleted);
            release.TrySetResult();
            TestAssert.Equal(true, (await setting).Succeeded);
            var captured = await capture;
            TestAssert.Equal(true, captured.Succeeded);
            TestAssert.Equal(GodmodeValue.On, captured.Snapshot.Value);
            TestAssert.Equal(1, fixture.Transports[fixture.Players[0]].Writes);
        } finally {
            release.TrySetResult();
            await registry.CloseAsync();
        }
    }

    public static async Task CaptureWaitsForCloseThenRejectsWithoutReadingOrBinding() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var adapter = new ControlledGodmodeFeature();
        var registry = new PlayerGodmodeRegistry(_ => adapter, _ => true);
        TestAssert.Equal(true, (await registry.CaptureAsync(new[] { player })).Succeeded);
        var entered = TestAssert.Barrier();
        var release = TestAssert.Barrier();
        adapter.OnClose = async () => { entered.TrySetResult(); await release.Task; return new(true, true); };
        try {
            var closing = registry.CloseAsync();
            await entered.Task;
            var capturing = registry.CaptureAsync(new[] { player });
            TestAssert.Equal(false, capturing.IsCompleted);
            release.TrySetResult();
            TestAssert.Equal(true, (await closing).SafeToRelease);
            TestAssert.Equal(false, (await capturing).Succeeded);
            TestAssert.Equal(0, adapter.Sets);
            TestAssert.Equal(1, adapter.Closes);
        } finally {
            release.TrySetResult();
            adapter.OnClose = null;
            await registry.CloseAsync();
        }
    }
}
