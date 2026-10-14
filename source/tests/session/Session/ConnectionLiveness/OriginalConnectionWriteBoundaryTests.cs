using Apparition.Backend.Session;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// New writes stop at the final identity wait while original owned restoration survives retirement
internal static class OriginalConnectionWriteBoundaryTests {
    public static async Task CloseDuringFinalIdentityQueryDeniesBothLocalAndPeerOnWrites() {
        foreach (bool personal in new[] { true, false }) {
            var transport = new ProcessIdentityBarrierTransport();
            transport.Open(4242);
            var owner = new RetailConnection(transport, processId: 4242);
            Task? enabling = null;
            Task? closing = null;
            try {
                Check(owner.ObserveOriginalConnection(), "The original connection was not confirmed");
                if (personal) Check(await owner.CapturePersonalGodmodeAsync(), "Local capture failed");
                else Check(await owner.Teammates.SelectAsync(1, "Friend1"), "Peer capture failed");
                transport.HoldNextIdentityQuery();
                enabling = Task.Run(async () => {
                    if (personal) await owner.LocalGodmode!.ToggleAsync();
                    else await owner.Teammates.ToggleAsync(1, "Friend1");
                });
                await transport.Entered.WaitAsync(TimeSpan.FromSeconds(3));
                // Close sets permanent admission before waiting for the held feature gate
                closing = owner.CloseGodmodeOwnersAsync();
                transport.ReleaseQuery();
                await Task.WhenAll(enabling, closing).WaitAsync(TimeSpan.FromSeconds(3));
                Check(transport.Storage.FlagWrites == 0 && transport.Storage.Flags(personal ? 0 : 1) == 0x200,
                    "A positive identity return after close entered a new local or peer ON write");
            } finally {
                transport.ReleaseQuery();
                if (enabling is not null) await enabling.WaitAsync(TimeSpan.FromSeconds(3));
                if (closing is not null) await closing.WaitAsync(TimeSpan.FromSeconds(3));
                Check((await owner.CloseGodmodeOwnersAsync()).SafeToRelease, "Original flag cleanup did not join");
                transport.Dispose();
            }
        }
    }

    public static async Task CloseDuringFinalIdentityQueryDeniesTheAmmoReplacementWrite() {
        var transport = new ProcessIdentityBarrierTransport();
        transport.Open(4242);
        var owner = new RetailConnection(transport, processId: 4242);
        Task? enabling = null;
        Task<bool>? closing = null;
        try {
            Check(owner.ObserveOriginalConnection(), "The original connection was not confirmed");
            transport.HoldNextIdentityQuery();
            enabling = Task.Run(() => owner.Ammo.SetAsync(true));
            await transport.Entered.WaitAsync(TimeSpan.FromSeconds(3));
            owner.RequestNewWriteClose();
            closing = owner.Ammo.CloseAsync();
            transport.ReleaseQuery();
            await Task.WhenAll(enabling, closing).WaitAsync(TimeSpan.FromSeconds(3));
            Check(closing.Result && transport.Storage.AmmoWrites == 0,
                "A positive identity return after close entered the new ammo replacement write");
        } finally {
            transport.ReleaseQuery();
            if (enabling is not null) await enabling.WaitAsync(TimeSpan.FromSeconds(3));
            if (closing is not null) await closing.WaitAsync(TimeSpan.FromSeconds(3));
            Check(await owner.Ammo.CloseAsync(), "Original ammo cleanup did not join");
            transport.Dispose();
        }
    }

    public static async Task RetiredCachedOwnerStillRestoresItsOriginalLocalPeerAndAmmoChanges() {
        var transport = new RecordingRetailTransport();
        transport.Open(4242);
        var owner = new RetailConnection(transport, processId: 4242);
        try {
            Check(owner.ObserveOriginalConnection() && await owner.CapturePersonalGodmodeAsync(), "Local setup failed");
            Check(await owner.LocalGodmode!.ToggleAsync(), "Local ON failed");
            Check(await owner.Teammates.SelectAsync(1, "Friend1"), "Peer setup failed");
            await owner.Teammates.ToggleAsync(1, "Friend1");
            Check(transport.Flags(1) == 0x201 && await owner.Ammo.SetAsync(true), "Peer or ammo ON failed");
            int flags = transport.FlagWrites;
            int ammo = transport.AmmoWrites;
            transport.ReportedPid = 7777;
            Check(!owner.ObserveOriginalConnection(), "Replacement process was accepted");
            transport.ReportedPid = 4242;
            Check(!owner.ObserveOriginalConnection() && owner.HasRetiredConnection, "Old facts revived the cached owner");
            Check((await owner.CloseGodmodeOwnersAsync()).SafeToRelease && await owner.Ammo.CloseAsync(),
                "Original changes could not restore after cached connection retirement");
            Check(transport.FlagWrites == flags + 2 && transport.AmmoWrites == ammo + 1
                && transport.Flags(0) == 0x200 && transport.Flags(1) == 0x200
                && transport.ReadAmmoBytes(0x140001000, 4).SequenceEqual(new byte[] { 0x01, 0x7C, 0x90, 0x14 }),
                "Cleanup failed to restore the exact three original owners once");
        } finally {
            transport.ReportedPid = 4242;
            Check((await owner.CloseGodmodeOwnersAsync()).SafeToRelease && await owner.Ammo.CloseAsync(),
                "Original resources still retain cleanup");
            transport.Dispose();
        }
    }
}
