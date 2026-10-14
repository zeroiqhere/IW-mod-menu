using syndishanx_iw_tool.Features.Godmode;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

internal static class SharedGodmodeRollbackTests {
    public static async Task FailedSharedOffRestoresOnlyItsConfirmedPersonalOffTransition() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        var identities = new[] { fixture.Identity(3), fixture.Identity(0) };
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, true)).Succeeded, "Shared enable failed");
        fixture.Transport.RejectFlagRestore = true;
        var failed = await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, false);
        Check(!failed.Succeeded && fixture.Transport.Flags(3) == 0x201 && fixture.Transport.LocalFlagWrites == 3
            && fixture.Transport.Flags(0) == 0x201 && failed.Snapshot.HasPendingCleanup,
            "Failed shared OFF did not reverse only its own confirmed local restoration");
        fixture.Transport.RejectFlagRestore = false;
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, false)).Succeeded
            && fixture.Transport.Flags(3) == 0x200 && fixture.Transport.Flags(0) == 0x200,
            "Shared cleanup retry did not use both original owners");
    }

    public static async Task FailedSharedOffNeverReenablesAPersonalFlagAlreadyClearedByTheGame() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        var identities = new[] { fixture.Identity(3), fixture.Identity(0) };
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, true)).Succeeded, "Shared enable failed");
        fixture.Transport.SetFlags(3, 0x200);
        fixture.Transport.RejectFlagRestore = true;
        var failed = await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, false);
        Check(!failed.Succeeded && fixture.Transport.Flags(3) == 0x200 && fixture.Transport.LocalFlagWrites == 1,
            "Rollback turned an observed game-cleared personal flag back ON");
        fixture.Transport.RejectFlagRestore = false;
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, false)).Succeeded, "Cleanup retry failed");
    }

    public static async Task CancelledSharedOffKeepsTheConfirmedReceiptNeededToRestoreEarlierOn() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        var identities = new[] { fixture.Identity(3), fixture.Identity(0) };
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, true)).Succeeded, "Shared enable failed");
        using var cancellation = new CancellationTokenSource();
        fixture.Transport.OnLocalFlagRead = () => {
            if (fixture.Transport.LocalFlagWrites == 2 && fixture.Transport.Flags(3) == 0x200) cancellation.Cancel();
        };
        try {
            var failed = await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, false, cancellation.Token);
            Check(!failed.Succeeded && failed.Cancelled && fixture.Transport.LocalFlagWrites == 3
                && fixture.Transport.Flags(3) == 0x201 && fixture.Transport.Flags(0) == 0x201,
                "Cancellation erased the actual local OFF receipt or changed the untouched peer");
        } finally { fixture.Transport.OnLocalFlagRead = null; }
    }
}
