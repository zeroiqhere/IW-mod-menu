using Apparition.Backend.Session;
using syndishanx_iw_tool.Players;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// The real connection owns only flags stored in this fixture
internal sealed class SharedGodmodeFixture : IAsyncDisposable {
    public RecordingRetailTransport Transport { get; }
    public RetailConnection Owner { get; }

    public SharedGodmodeFixture(int localSlot = 3) {
        Transport = new() { LocalSlot = localSlot };
        Transport.Open(4242);
        Owner = new(Transport, processId: 4242);
    }

    public PlayerIdentity Identity(int slot) => Transport.ReadTeammateState(slot).Identity;

    public async Task CaptureLocalAsync() {
        Check(await Owner.CapturePersonalGodmodeAsync(), "The original personal owner was not registered");
    }

    public async ValueTask DisposeAsync() {
        // Failed restoration stays test-visible instead of silently releasing an owned worker
        var closed = await Owner.CloseGodmodeOwnersAsync();
        Check(closed.SafeToRelease, "The shared fixture still owns unfinished cleanup");
        Transport.Dispose();
    }
}
