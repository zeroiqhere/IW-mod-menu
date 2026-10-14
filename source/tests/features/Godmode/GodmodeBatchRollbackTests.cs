using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class GodmodeBatchRollbackTests {
    public static async Task RejectedButAppliedOffNeverBecomesProofToReapplyTheOwnedFlag() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetBatchAsync(fixture.Players, true)).Succeeded);
            var first = fixture.Transports[fixture.Players[0]];
            first.OnWrite = (count, _, desired) => {
                if (count == 2) { first.Flags = desired; return false; }
                return true;
            };
            var failed = await registry.SetBatchAsync(fixture.Players, false);
            TestAssert.Equal(false, failed.Succeeded);
            TestAssert.Equal(0x200U, first.Flags);
            TestAssert.Equal(2, first.Writes);
            TestAssert.Equal(GodmodeValue.Off, failed.Snapshot.Members[0].Value);
            TestAssert.Equal(1, fixture.Transports[fixture.Players[1]].Writes);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task OffRollbackDoesNotReapplyAFlagClearedByTheGameAfterPreflight() {
        var fixture = new GodmodeRegistryFixture(2);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetBatchAsync(fixture.Players, true)).Succeeded);
            var first = fixture.Transports[fixture.Players[0]];
            int clearAtRead = first.Reads + 2;
            first.OnRead = count => {
                if (count == clearAtRead) { first.Flags = 0x200; }
                return first.Flags;
            };
            fixture.Transports[fixture.Players[1]].OnWrite = (count, _, _) => count != 2;
            var failed = await registry.SetBatchAsync(fixture.Players, false);
            TestAssert.Equal(false, failed.Succeeded);
            TestAssert.Equal(0x200U, first.Flags);
            TestAssert.Equal(1, first.Writes);
            TestAssert.Equal(GodmodeValue.Off, failed.Snapshot.Members[0].Value);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[1]].Flags);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task FailedOffRestoresPriorOnAndNeverChangesPriorOffMembers() {
        var fixture = new GodmodeRegistryFixture(3);
        var registry = fixture.Create();
        try {
            TestAssert.Equal(true, (await registry.SetBatchAsync(fixture.Players.Take(2).ToArray(), true)).Succeeded);
            fixture.Transports[fixture.Players[1]].OnWrite = (count, _, _) => count != 2;
            var result = await registry.SetBatchAsync(fixture.Players, false);
            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[0]].Flags);
            TestAssert.Equal(0x201U, fixture.Transports[fixture.Players[1]].Flags);
            TestAssert.Equal(0x200U, fixture.Transports[fixture.Players[2]].Flags);
            TestAssert.Equal(3, fixture.Transports[fixture.Players[0]].Writes);
            TestAssert.Equal(4, fixture.Transports[fixture.Players[1]].Writes);
            TestAssert.Equal(0, fixture.Transports[fixture.Players[2]].Writes);
            TestAssert.Equal(GodmodeValue.Mixed, result.Snapshot.Value);
            TestAssert.Equal(false, result.Snapshot.HasPendingCleanup);
        } finally {
            await registry.CloseAsync();
        }
    }

    public static async Task ThrownAppliedEnableRollsBackOriginalFlagsAndReportsFailure() {
        var fixture = new GodmodeRegistryFixture(1);
        var registry = fixture.Create();
        var transport = fixture.Transports[fixture.Players[0]];
        transport.OnWrite = (count, _, desired) => {
            if (count == 1) {
                transport.Flags = desired;
                throw new IOException("Synthetic write failed after applying flags");
            }
            return true;
        };
        try {
            var result = await registry.SetAsync(fixture.Players[0], true);
            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(GodmodeValue.Off, result.Snapshot.Value);
            TestAssert.Equal(0x200U, transport.Flags);
            TestAssert.Equal(2, transport.Writes);
        } finally {
            await registry.CloseAsync();
        }
    }
}
