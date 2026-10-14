using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

internal static class GodmodeCompletionTests {
    public static async Task FinalIdentityChangeRejectsEnableAndRollsBackOnlyValidOriginalMembers() {
        // The final adapter can invalidate either member before its successful await returns
        foreach (int invalidIndex in new[] { 0, 1 }) {
            PlayerIdentity[] players = { PlayerGodmodeRegistryTests.Identity(1), PlayerGodmodeRegistryTests.Identity(2) };
            var valid = players.ToHashSet();
            var features = players.ToDictionary(identity => identity, _ => new ControlledGodmodeFeature());
            int factories = 0;
            var registry = new PlayerGodmodeRegistry(identity => {
                factories++;
                return features[identity];
            }, valid.Contains);
            foreach (PlayerIdentity player in players) {
                ControlledGodmodeFeature feature = features[player];
                feature.OnSet = (desired, _) => {
                    // A replaced original owner refuses restoration instead of writing its replacement
                    if (!desired && !valid.Contains(player)) return Task.FromResult(false);
                    feature.Active = desired;
                    feature.HasPendingRestore = desired;
                    if (desired && player == players[^1]) valid.Remove(players[invalidIndex]);
                    return Task.FromResult(true);
                };
            }

            try {
                GodmodeOperationResult result = await registry.SetBatchAsync(players, true);

                TestAssert.Equal(false, result.Succeeded);
                TestAssert.Equal(false, result.Cancelled);
                TestAssert.Equal(true, result.FailureReason!.Contains("identity", StringComparison.OrdinalIgnoreCase));
                TestAssert.Equal(GodmodeValue.Unknown, result.Snapshot.Value);
                TestAssert.Equal(true, result.Snapshot.HasPendingCleanup);
                TestAssert.Equal(false, features[players[1 - invalidIndex]].Active);
                TestAssert.Equal(true, features[players[invalidIndex]].HasPendingRestore);
                TestAssert.Equal(2, factories);
            } finally {
                // Fixture cleanup restores only the inert original adapters
                foreach (ControlledGodmodeFeature feature in features.Values) feature.OnSet = null;
                await registry.CloseAsync();
            }
        }
    }

    public static async Task FinalIdentityReadExceptionRejectsSuccessAndRollsBackTheAppliedFlag() {
        var player = PlayerGodmodeRegistryTests.Identity(1);
        var feature = new ControlledGodmodeFeature();
        bool failIdentity = false;
        var registry = new PlayerGodmodeRegistry(_ => feature, _ => {
            if (failIdentity) throw new IOException("Completion identity read failed");
            return true;
        });
        feature.OnSet = (desired, _) => {
            feature.Active = desired;
            feature.HasPendingRestore = desired;
            if (desired) failIdentity = true;
            return Task.FromResult(true);
        };

        try {
            GodmodeOperationResult result = await registry.SetAsync(player, true);

            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal("Completion identity read failed", result.FailureReason);
            TestAssert.Equal(false, feature.Active);
            TestAssert.Equal(false, feature.HasPendingRestore);
            TestAssert.Equal(2, feature.Sets);
        } finally {
            failIdentity = false;
            feature.OnSet = null;
            await registry.CloseAsync();
        }
    }

    public static async Task FinalSnapshotFlagReadFailureRollsBackTheActualOwnedFlags() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        RecordingFlagsTransport transport = fixture.Transports[fixture.Players[0]];
        bool wroteOn = false;
        int afterWriteReads = 0;
        transport.OnWrite = (_, _, desired) => {
            if ((desired & 1U) != 0) wroteOn = true;
            return true;
        };
        transport.OnRead = _ => {
            // Initial write readback succeeds; the later completion preflight fails once
            if (wroteOn && ++afterWriteReads == 2) throw new IOException("Completion flags unavailable");
            return transport.Flags;
        };

        try {
            GodmodeOperationResult result = await registry.SetAsync(fixture.Players[0], true);

            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(false, result.Cancelled);
            TestAssert.Equal(GodmodeValue.Off, result.Snapshot.Value);
            TestAssert.Equal(false, result.Snapshot.HasPendingCleanup);
            TestAssert.Equal(0x200U, transport.Flags);
            TestAssert.Equal(2, transport.Writes);
        } finally {
            transport.OnRead = null;
            transport.OnWrite = null;
            await registry.CloseAsync();
        }
    }

    public static async Task StableOnOffAndAlreadyDesiredBatchesReturnConfirmedSnapshotsWithoutExtraWrites() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        try {
            GodmodeOperationResult enabled = await registry.SetBatchAsync(fixture.Players, true);
            GodmodeOperationResult repeatedOn = await registry.SetBatchAsync(fixture.Players, true);

            TestAssert.Equal(true, enabled.Succeeded);
            TestAssert.Equal(GodmodeValue.On, enabled.Snapshot.Value);
            TestAssert.Equal(true, repeatedOn.Succeeded);
            TestAssert.Equal(GodmodeValue.On, repeatedOn.Snapshot.Value);
            TestAssert.Equal(true, fixture.Transports.Values.All(transport => transport.Writes == 1));

            GodmodeOperationResult disabled = await registry.SetBatchAsync(fixture.Players, false);
            GodmodeOperationResult repeatedOff = await registry.SetBatchAsync(fixture.Players, false);

            TestAssert.Equal(true, disabled.Succeeded);
            TestAssert.Equal(GodmodeValue.Off, disabled.Snapshot.Value);
            TestAssert.Equal(true, repeatedOff.Succeeded);
            TestAssert.Equal(GodmodeValue.Off, repeatedOff.Snapshot.Value);
            TestAssert.Equal(false, repeatedOff.Snapshot.HasPendingCleanup);
            TestAssert.Equal(true, fixture.Transports.Values.All(transport => transport.Writes == 2 && transport.Flags == 0x200U));
            TestAssert.Equal(GodmodeValue.On, enabled.Snapshot.Value);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task SuccessfulEnableReturnsItsValidatedSnapshotWithoutASecondFlagRead() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        RecordingFlagsTransport transport = fixture.Transports[fixture.Players[0]];
        bool wroteOn = false;
        int afterWriteReads = 0;
        transport.OnWrite = (_, _, desired) => {
            if ((desired & 1U) != 0) wroteOn = true;
            return true;
        };
        transport.OnRead = _ => {
            // A second result projection would see an unrelated later change
            if (wroteOn && ++afterWriteReads > 2) transport.Flags = 0x240;
            return transport.Flags;
        };

        try {
            GodmodeOperationResult result = await registry.SetAsync(fixture.Players[0], true);

            TestAssert.Equal(true, result.Succeeded);
            TestAssert.Equal(GodmodeValue.On, result.Snapshot.Value);
            TestAssert.Equal(false, result.Snapshot.HasPendingCleanup);
            TestAssert.Equal(2, afterWriteReads);
            TestAssert.Equal(0x201U, transport.Flags);
        } finally {
            transport.OnRead = null;
            transport.OnWrite = null;
            await registry.CloseAsync();
        }
    }

    public static async Task CancellationDuringFinalSnapshotRollsBackBeforeReturningCancelled() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        using var cancellation = new CancellationTokenSource();
        RecordingFlagsTransport transport = fixture.Transports[fixture.Players[0]];
        bool wroteOn = false;
        int afterWriteReads = 0;
        transport.OnWrite = (_, _, desired) => {
            if ((desired & 1U) != 0) wroteOn = true;
            return true;
        };
        transport.OnRead = _ => {
            if (wroteOn && ++afterWriteReads == 2) cancellation.Cancel();
            return transport.Flags;
        };

        try {
            GodmodeOperationResult result = await registry.SetAsync(fixture.Players[0], true, cancellation.Token);

            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(true, result.Cancelled);
            TestAssert.Equal(GodmodeValue.Off, result.Snapshot.Value);
            TestAssert.Equal(false, result.Snapshot.HasPendingCleanup);
            TestAssert.Equal(0x200U, transport.Flags);
        } finally {
            transport.OnRead = null;
            transport.OnWrite = null;
            await registry.CloseAsync();
        }
    }

    public static async Task OwnedOffAfterDisconnectAndRetainedUnknownCaptureKeepTheirCleanupContracts() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        RecordingFlagsTransport transport = fixture.Transports[fixture.Players[0]];
        try {
            TestAssert.Equal(true, (await registry.SetAsync(fixture.Players[0], true)).Succeeded);
            transport.Connected = false;

            GodmodeOperationResult off = await registry.SetAsync(fixture.Players[0], false);

            TestAssert.Equal(true, off.Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, off.Snapshot.Value);
            TestAssert.Equal(false, off.Snapshot.HasPendingCleanup);
            TestAssert.Equal(false, off.Snapshot.Members[0].Active);
            TestAssert.Equal(false, off.Snapshot.Members[0].HasPendingRestore);
            TestAssert.Equal(1, transport.Writes);
        } finally {
            await registry.CloseAsync();
        }

        var player = PlayerGodmodeRegistryTests.Identity(1);
        var retained = new ControlledGodmodeFeature();
        var retainedRegistry = new PlayerGodmodeRegistry(_ => retained, _ => true);
        retained.OnSet = (desired, _) => {
            retained.HasPendingRestore = desired;
            return Task.FromResult(false);
        };
        try {
            TestAssert.Equal(false, (await retainedRegistry.SetAsync(player, true)).Succeeded);
            retained.HasPendingRestore = true;

            GodmodeOperationResult capture = await retainedRegistry.CaptureAsync(new[] { player });

            TestAssert.Equal(true, capture.Succeeded);
            TestAssert.Equal(GodmodeValue.Unknown, capture.Snapshot.Value);
            TestAssert.Equal(true, capture.Snapshot.HasPendingCleanup);
        } finally {
            retained.OnSet = null;
            await retainedRegistry.CloseAsync();
        }
    }
}
