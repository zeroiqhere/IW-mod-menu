using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;

namespace Features.Tests.Godmode;

internal static class CohortGodmodeTests {
    private static void Check(bool condition, string reason) {
        if (!condition) throw new InvalidOperationException(reason);
    }

    public static async Task CohortToggleUsesSourceAllOnMeaningAndSharesIndividualOwners() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        var enabled = await fixture.Registry.ToggleCohortAsync(capture, fixture.Read);
        Check(enabled.Succeeded && enabled.Snapshot.Value == GodmodeValue.On, "Complete cohort ON was not confirmed");
        Check((await fixture.Registry.SetAsync(fixture.Identity(0), false)).Succeeded, "Individual OFF failed");
        Check(fixture.Registry.Snapshot(capture.Identities).Value == GodmodeValue.Mixed, "Individual state did not update aggregate");
        Check((await fixture.Registry.ToggleCohortAsync(capture, fixture.Read)).Succeeded, "Mixed cohort must enable the missing member");
        TestAssert.Equal(3, fixture.Flags[fixture.Identity(0)].Writes);
        TestAssert.Equal(1, fixture.Flags[fixture.Identity(1)].Writes);
        var disabled = await fixture.Registry.ToggleCohortAsync(capture, fixture.Read);
        Check(disabled.Succeeded && disabled.Snapshot.Value == GodmodeValue.Off, "All ON must toggle the cohort OFF");
        TestAssert.Equal(2, fixture.Factories);
        Check(fixture.Flags.Values.All(flags => flags.Flags == 0x200), "Cohort OFF did not restore original flags");
    }

    public static async Task ChangedOrUnreadableWholeCohortRejectsBeforeBindingOrWriting() {
        foreach (int population in new[] { 1, 3 }) {
            await using var fixture = new CohortGodmodeFixture();
            var capture = fixture.Capture;
            fixture.Population = population;
            var result = await fixture.Registry.SetCohortAsync(capture, true, fixture.Read);
            Check(!result.Succeeded, "Changed membership was accepted");
            TestAssert.Equal(0, fixture.Factories);
            Check(fixture.Flags.Values.All(flags => flags.Writes == 0), "Changed membership reached a write");
        }
        await using var unknown = new CohortGodmodeFixture();
        var original = unknown.Capture;
        unknown.OnRead = () => new(PlayerCohortCaptureTests.Session, Enumerable.Range(0, 4)
            .Select(slot => new PlayerSlotObservation(slot, PlayerSlotPresence.Unknown)).ToArray());
        Check(!(await unknown.Registry.SetCohortAsync(original, true, unknown.Read)).Succeeded, "Unknown membership was accepted");
        TestAssert.Equal(0, unknown.Factories);
    }

    public static async Task JoinAfterFirstOwnedWriteRollsBackOriginalActorsWithoutBindingTheJoiner() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        fixture.Flags[fixture.Identity(0)].OnWrite = (_, _, desired) => {
            if (desired == 0x201) fixture.Population = 3;
            return true;
        };
        var result = await fixture.Registry.SetCohortAsync(capture, true, fixture.Read);
        Check(!result.Succeeded, "A join during the group write was accepted");
        TestAssert.Equal(2, fixture.Flags[fixture.Identity(0)].Writes);
        TestAssert.Equal(0, fixture.Flags[fixture.Identity(1)].Writes);
        TestAssert.Equal(0, fixture.Flags[fixture.Identity(2)].Writes);
        Check(fixture.Flags.Values.All(flags => flags.Flags == 0x200), "Owned ON was not rolled back after the join");
        TestAssert.Equal(2, fixture.Factories);
    }

    public static async Task CompletionObservationCannotPublishSuccessAfterASnapshotTimeJoin() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        bool bothWritten = false;
        fixture.Flags[fixture.Identity(1)].OnWrite = (_, _, desired) => { bothWritten = desired == 0x201; return true; };
        // Member zero is read again for completion only after both owned writes
        fixture.Flags[fixture.Identity(0)].OnRead = _ => {
            if (bothWritten) fixture.Population = 3;
            return fixture.Flags[fixture.Identity(0)].Flags;
        };
        var result = await fixture.Registry.SetCohortAsync(capture, true, fixture.Read);
        Check(!result.Succeeded, "Completion borrowed stale membership");
        Check(fixture.Flags.Values.All(flags => flags.Flags == 0x200), "Completion failure lost batch rollback ownership");
        TestAssert.Equal(2, fixture.Flags[fixture.Identity(0)].Writes);
        TestAssert.Equal(2, fixture.Flags[fixture.Identity(1)].Writes);
    }

    public static async Task AdvancingThenRolledBackCommandTimeFailsEvenAboveTheInitialCaptureTime() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        int time = 200;
        fixture.OnRead = () => new(PlayerCohortCaptureTests.Session, PlayerCohortCaptureTests.Observation(2).Slots
            .Select(slot => slot.Player is null ? slot : slot with { Player = slot.Player with { CommandTime = time } }).ToArray());
        fixture.Flags[fixture.Identity(0)].OnWrite = (_, _, desired) => { if (desired == 0x201) time = 150; return true; };
        var result = await fixture.Registry.SetCohortAsync(capture, true, fixture.Read);
        Check(!result.Succeeded, "The batch forgot its advancing cohort command-time watermark");
        TestAssert.Equal(2, fixture.Flags[fixture.Identity(0)].Writes);
        TestAssert.Equal(0, fixture.Flags[fixture.Identity(1)].Writes);
        Check(fixture.Flags.Values.All(flags => flags.Flags == 0x200), "Time rollback left an owned ON behind");
    }

    public static async Task NonlivingCohortCanRestoreOwnedFlagsButCannotAuthorizeANewEnable() {
        await using var fixture = new CohortGodmodeFixture();
        var capture = fixture.Capture;
        Check((await fixture.Registry.SetCohortAsync(capture, true, fixture.Read)).Succeeded, "Living cohort enable failed");
        fixture.OnRead = () => new(PlayerCohortCaptureTests.Session, PlayerCohortCaptureTests.Observation(2).Slots
            .Select(slot => slot.Player is null ? slot : slot with { Player = slot.Player with { IsEnableEligible = false } }).ToArray());
        Check((await fixture.Registry.ToggleCohortAsync(capture, fixture.Read)).Succeeded, "Dead retained actors could not restore OFF");
        int writes = fixture.Flags.Values.Sum(flags => flags.Writes);
        Check(!(await fixture.Registry.ToggleCohortAsync(capture, fixture.Read)).Succeeded, "A dead cohort admitted new ON");
        TestAssert.Equal(writes, fixture.Flags.Values.Sum(flags => flags.Writes));
    }

    public static async Task CancelledCohortEnableRestoresOnlyThisOperationAndRetainsEarlierIndividualOn() {
        await using var fixture = new CohortGodmodeFixture();
        Check((await fixture.Registry.SetAsync(fixture.Identity(0), true)).Succeeded, "Earlier individual ON failed");
        using var cancellation = new CancellationTokenSource();
        fixture.Flags[fixture.Identity(1)].OnWrite = (_, _, desired) => { if (desired == 0x201) cancellation.Cancel(); return true; };
        var result = await fixture.Registry.SetCohortAsync(fixture.Capture, true, fixture.Read, cancellation.Token);
        Check(!result.Succeeded && result.Cancelled, "Cancellation after the final owned write was accepted");
        TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(0)].Flags);
        TestAssert.Equal(0x200U, fixture.Flags[fixture.Identity(1)].Flags);
        TestAssert.Equal(1, fixture.Flags[fixture.Identity(0)].Writes);
        TestAssert.Equal(2, fixture.Flags[fixture.Identity(1)].Writes);
    }
}
