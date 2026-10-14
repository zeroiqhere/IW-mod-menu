using syndishanx_iw_tool.Features.Godmode;

namespace Features.Tests.Godmode;

internal static class CohortGodmodeAuthorityTests {
    public static async Task LostHostAuthorityCannotReenableAnOwnedOffDuringGroupRollback() {
        foreach (bool throws in new[] { false, true }) {
            await using var fixture = new CohortGodmodeFixture();
            TestAssert.Equal(true, (await fixture.Registry.SetCohortAsync(fixture.Capture, true, fixture.Read)).Succeeded);
            bool host = true;
            var first = fixture.Flags[fixture.Identity(0)];
            first.OnWrite = (_, _, desired) => {
                if (desired == 0x200) host = false;
                return true;
            };
            // Host loss is an authority failure even when all original actors remain linked
            fixture.OnRead = () => host ? PlayerCohortCaptureTests.Observation(2)
                : throw new IOException("Host authority was lost");
            int checks = 0;
            bool Admit() {
                checks++;
                if (throws && !host) throw new IOException("Host proof was unreadable");
                return host;
            }
            var result = await fixture.Registry.SetCohortAsync(fixture.Capture, false, fixture.Read,
                admitRollbackEnable: Admit);
            TestAssert.Equal(false, result.Succeeded);
            TestAssert.Equal(0x200U, first.Flags);
            TestAssert.Equal(2, first.Writes);
            TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(1)].Flags);
            TestAssert.Equal(true, checks > 0);
        }
    }

    public static async Task HostLossDuringRollbackPreflightCannotSlipThroughAnEarlierAdmission() {
        await using var fixture = new CohortGodmodeFixture();
        TestAssert.Equal(true, (await fixture.Registry.SetCohortAsync(fixture.Capture, true, fixture.Read)).Succeeded);
        var first = fixture.Flags[fixture.Identity(0)];
        bool host = true;
        bool rollingBack = false;
        first.OnWrite = (_, _, desired) => {
            if (desired == 0x200) rollingBack = true;
            return true;
        };
        fixture.OnRead = () => !rollingBack ? PlayerCohortCaptureTests.Observation(2)
            : throw new IOException("Group changed after the owned OFF");
        first.OnRead = _ => {
            // The owned flags preflight itself models the later host transition
            if (rollingBack) host = false;
            return first.Flags;
        };
        var result = await fixture.Registry.SetCohortAsync(fixture.Capture, false, fixture.Read,
            admitRollbackEnable: () => host);
        TestAssert.Equal(false, result.Succeeded);
        TestAssert.Equal(0x200U, first.Flags);
        TestAssert.Equal(2, first.Writes);
        TestAssert.Equal(0x201U, fixture.Flags[fixture.Identity(1)].Flags);
    }

    public static async Task ValidRollbackAuthorityRestoresOnlyTheOriginalOwnedOffMember() {
        await using var fixture = new CohortGodmodeFixture();
        TestAssert.Equal(true, (await fixture.Registry.SetCohortAsync(fixture.Capture, true, fixture.Read)).Succeeded);
        var first = fixture.Flags[fixture.Identity(0)];
        first.OnWrite = (_, _, desired) => {
            if (desired == 0x200) fixture.Population = 3;
            return true;
        };
        int checks = 0;
        var result = await fixture.Registry.SetCohortAsync(fixture.Capture, false, fixture.Read,
            admitRollbackEnable: () => { checks++; return true; });
        TestAssert.Equal(false, result.Succeeded);
        TestAssert.Equal(0x201U, first.Flags);
        TestAssert.Equal(3, first.Writes);
        TestAssert.Equal(0, fixture.Flags[fixture.Identity(2)].Writes);
        TestAssert.Equal(2, checks);
    }
}
