using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class CohortGodmodeSchedulingTests {
    private static void Check(bool condition, string reason) {
        if (!condition) throw new InvalidOperationException(reason);
    }

    public static async Task ClearedOwnedFlagCannotTurnAStaleAllOnCohortIntoASuccessfulOffToggle() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        Check((await fixture.Registry.SetCohortAsync(capture, true, fixture.Read)).Succeeded,
            "Positive two-owner cohort enable failed");
        // The hour-long monitor delay keeps both Active fields true during this controlled clear
        fixture.Flags[fixture.Identity(0)].Flags = 0x200;
        var before = fixture.Registry.Snapshot(capture.Identities);
        Check(before.Members[0].Active && before.Members[0].Value == GodmodeValue.Unknown
            && before.Members[1].Active && before.Members[1].Value == GodmodeValue.On,
            "The fixture did not retain stale Active alongside fresh cleared flags");
        int writes = fixture.Flags.Values.Sum(flags => flags.Writes);
        var rejected = await fixture.Registry.ToggleCohortAsync(capture, fixture.Read);
        Check(!rejected.Succeeded, "Stale Active allowed a successful all-ON OFF decision");
        TestAssert.Equal(writes, fixture.Flags.Values.Sum(flags => flags.Writes));
        TestAssert.Equal(0x200U, fixture.Flags[fixture.Identity(0)].Flags);
        TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(1)].Flags);
        // Original explicit cleanup reconciles the cleared owner without guessing a new ON
        Check((await fixture.Registry.SetAsync(fixture.Identity(0), false)).Succeeded,
            "The original cleared owner could not reconcile cleanup");
        Check((await fixture.Registry.ToggleCohortAsync(capture, fixture.Read)).Succeeded
            && fixture.Registry.Snapshot(capture.Identities).Value == GodmodeValue.On,
            "Reconciled mixed cohort could not enable its missing member");
    }

    public static async Task QueuedCohortToggleUsesTheMembershipAndOnStateLeftByAnEarlierIndividualWrite() {
        foreach (bool cancelQueued in new[] { false, true }) {
            await using var fixture = new CohortGodmodeFixture { Population = 1 };
            var capture = fixture.Capture;
            using var released = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Flags[fixture.Identity(0)].OnWrite = (_, _, desired) => {
                if (desired == 0x201) {
                    entered.TrySetResult();
                    if (!released.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Owned write was not released");
                }
                return true;
            };
            Task<GodmodeOperationResult> first = fixture.Registry.SetAsync(fixture.Identity(0), true);
            try {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                int membershipReads = 0;
                fixture.OnRead = () => { membershipReads++; return PlayerCohortCaptureTests.Observation(1); };
                using var cancellation = new CancellationTokenSource();
                Task<GodmodeOperationResult> queued = fixture.Registry.ToggleCohortAsync(capture, fixture.Read, cancellation.Token);
                TestAssert.Equal(0, membershipReads);
                if (cancelQueued) {
                    cancellation.Cancel();
                    var denied = await queued.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!denied.Succeeded && denied.Cancelled, "Queued cancellation admitted the group");
                    TestAssert.Equal(0, membershipReads);
                }
                released.Set();
                Check((await first).Succeeded, "Earlier individual ON failed");
                if (!cancelQueued) {
                    var result = await queued;
                    Check(result.Succeeded && result.Snapshot.Value == GodmodeValue.Off, "Queued group derived stale desired state");
                    Check(membershipReads > 0, "Queued group omitted fresh membership proof");
                }
                TestAssert.Equal(cancelQueued ? 0x201U : 0x200U, fixture.Flags[fixture.Identity(0)].Flags);
                TestAssert.Equal(1, fixture.Factories);
            } finally {
                released.Set();
                await first;
            }
        }
    }

    public static async Task GroupOffFailureReenablesOnlyItsConfirmedOffWriteOnTheOriginalParticipants() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        Check((await fixture.Registry.SetCohortAsync(capture, true, fixture.Read)).Succeeded, "Original group ON failed");
        fixture.Flags[fixture.Identity(0)].OnWrite = (_, _, desired) => {
            if (desired == 0x200) fixture.Population = 3;
            return true;
        };
        var result = await fixture.Registry.SetCohortAsync(capture, false, fixture.Read);
        Check(!result.Succeeded, "Group OFF accepted changed membership");
        TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(0)].Flags);
        TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(1)].Flags);
        TestAssert.Equal(0x200U, fixture.Flags[fixture.Identity(2)].Flags);
        TestAssert.Equal(3, fixture.Flags[fixture.Identity(0)].Writes);
        TestAssert.Equal(1, fixture.Flags[fixture.Identity(1)].Writes);
        TestAssert.Equal(0, fixture.Flags[fixture.Identity(2)].Writes);
        TestAssert.Equal(2, fixture.Factories);
    }

    public static async Task ForeignGroupMemberFlagsRejectAllWritesDuringReadOnlyPreflight() {
        await using var fixture = new CohortGodmodeFixture { Population = 4 };
        fixture.Flags[fixture.Identity(2)].Flags = 0x201;
        var result = await fixture.Registry.SetCohortAsync(fixture.Capture, true, fixture.Read);
        Check(!result.Succeeded, "A foreign flag was treated as owned group ON");
        Check(fixture.Flags.Values.All(flags => flags.Writes == 0), "Group preflight changed an earlier member");
        TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(2)].Flags);
    }
}
