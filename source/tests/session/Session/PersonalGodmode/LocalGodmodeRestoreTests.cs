using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

internal static class LocalGodmodeRestoreTests {
    public static async Task ExplicitRestoreNeverEnablesAndWorksAfterStrictLivingSourceFails() {
        await using var fixture = new LocalGodmodeFixture(3);
        LocalGodmodeFixture.Check(!await fixture.Service.RestoreAsync() && fixture.CurrentReads == 0
            && fixture.Flags.Writes == 0, "Restore without an owner cannot read or enable");
        LocalGodmodeFixture.Check(await fixture.Service.ToggleAsync(), "Pure OFF setup failed");
        fixture.Health = 0;
        fixture.SessionState = 1;
        fixture.ReadFailure = true;
        int reads = fixture.CurrentReads;
        LocalGodmodeFixture.Check(await fixture.Service.RestoreAsync() && fixture.Flags.Flags == 0x200,
            "Explicit restoration was blocked by strict living eligibility");
        LocalGodmodeFixture.Check(await fixture.Service.RestoreAsync() && fixture.Flags.Writes == 2
            && fixture.CurrentReads == reads && fixture.Service.ReadView().Value == GodmodeValue.Off,
            "Repeated explicit OFF read the strict source or re-enabled protection");
    }
}
