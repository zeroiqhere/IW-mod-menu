using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class SharedCohortGodmodeTests {
    private static PlayerCohortObservation Read(SharedGodmodeFixture fixture) => new(
        new(fixture.Transport.ProcessId, 0x140000000, fixture.Identity(0).ServerClients, 4),
        Enumerable.Range(0, 4).Select(slot => new PlayerSlotObservation(slot, PlayerSlotPresence.Present,
            fixture.Transport.ReadOwnedTeammateFlags(fixture.Identity(slot)))).ToArray());

    public static async Task PersonalAndSelectedPeerOwnersShareCohortToggleForEveryVerifiedLocalSlot() {
        foreach (int localSlot in Enumerable.Range(0, 4)) {
            await using var fixture = new SharedGodmodeFixture(localSlot);
            await fixture.CaptureLocalAsync();
            int peer = (localSlot + 1) % 4;
            Check(await fixture.Owner.Teammates.SelectAsync(peer, fixture.Identity(peer).Name), "Selected peer capture failed");
            var capture = PlayerCohortCapture.TryCreate(Read(fixture));
            Check(capture is not null, "Complete owned fixture cohort was rejected");
            var enabled = await fixture.Owner.GodmodeOwners.ToggleCohortAsync(capture!, () => Read(fixture));
            await fixture.Owner.Teammates.RefreshAsync();
            Check(enabled.Succeeded && enabled.Snapshot.Value == GodmodeValue.On
                && fixture.Owner.LocalGodmode!.ReadView().Value == GodmodeValue.On
                && fixture.Owner.Teammates.ReadView().Value == GodmodeValue.On,
                "Cohort toggle did not publish through the original personal and selected-peer owners");
            Check((await fixture.Owner.Teammates.ToggleAsync(peer, fixture.Identity(peer).Name))?.Succeeded == true,
                "Original peer owner could not turn its cohort flag off");
            Check((await fixture.Owner.GodmodeOwners.ToggleCohortAsync(capture!, () => Read(fixture))).Succeeded,
                "Mixed cohort did not restore the missing peer ON");
            var disabled = await fixture.Owner.GodmodeOwners.ToggleCohortAsync(capture!, () => Read(fixture));
            Check(disabled.Succeeded && disabled.Snapshot.Value == GodmodeValue.Off
                && Enumerable.Range(0, 4).All(slot => fixture.Transport.Flags(slot) == 0x200),
                "Cohort OFF left an original actor enabled");
            Check(fixture.Transport.LocalBindCalls == 1 && fixture.Transport.TeammateBindCalls == 3
                && fixture.Transport.HealthWrites == 0, "Cohort created duplicate or fallback writers");
        }
    }

    public static async Task ChangedLocalAccountRejectsCohortEvenWhenItsMemberNamesAndPointersStillMatch() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        var capture = PlayerCohortCapture.TryCreate(Read(fixture));
        Check(capture is not null, "Positive complete membership capture failed");
        fixture.Transport.LocalAccountId++;
        var denied = await fixture.Owner.GodmodeOwners.ToggleCohortAsync(capture!, () => Read(fixture));
        Check(!denied.Succeeded && fixture.Transport.FlagWrites == 0
            && fixture.Transport.LocalBindCalls == 1 && fixture.Transport.TeammateBindCalls == 0,
            "Matching membership substituted for original Steam account proof");
    }
}
