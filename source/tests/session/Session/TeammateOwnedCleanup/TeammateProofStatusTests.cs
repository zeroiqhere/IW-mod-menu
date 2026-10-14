using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

internal static class TeammateProofStatusTests {
    public static async Task UnavailableOwnedReadReportsUnconfirmedIdentityWithoutClaimingAReplacement() {
        var fixture = new TeammateOwnedCleanupFixture();
        try {
            RetailBackendSessionTests.Check(await fixture.Session.SelectAsync(1, "Friend1"),
                "The status test did not capture the original teammate");
            var identity = fixture.Session.ReadView().Identity;
            fixture.ReadFails = true;
            // Settle the first retired-proof revision before reading its stable notice
            await fixture.Session.RefreshAsync();
            await fixture.Session.RefreshAsync();
            var unavailable = fixture.Session.ReadView();

            RetailBackendSessionTests.Check(unavailable.Identity == identity
                && unavailable.Value == GodmodeValue.Unknown && !unavailable.CanToggle
                && !unavailable.HasCurrentIdentity && fixture.Binds == 1 && fixture.Writes == 0,
                "An unavailable proof changed ownership or published a writable state");
            RetailBackendSessionTests.Check(unavailable.Status.Contains("identity could not be confirmed", StringComparison.OrdinalIgnoreCase)
                && !unavailable.Status.Contains("identity changed", StringComparison.OrdinalIgnoreCase),
                "An unavailable read claimed an identity replacement without evidence");
        } finally {
            fixture.ReadFails = false;
            await fixture.Session.CloseAsync();
        }
    }
}
