using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// Native source queries belong to the retained worker, not the connection commit gate
internal static class LocalObservationSourceQueryTests {
    public static async Task BlockedSourceQueryCannotHoldShutdownGateAndItsTransportRemainsRetained() {
        var entered = RecordingObservationProvider.Completion();
        var release = RecordingObservationProvider.Completion();
        var transport = new RecordingRetailTransport();
        var provider = new RecordingObservationProvider();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false,
            _ => provider, TimeSpan.FromMilliseconds(30));
        await session.ExecuteAsync(new(MenuActionKind.Connect));
        transport.OnConnected = () => {
            transport.OnConnected = null;
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        };
        var read = session.RefreshLocalObservationAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var failed = await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(3));
            RetailBackendSessionTests.Check(!failed.Stopped && transport.DisposeCalls == 0,
                "A live source query blocked the lifecycle gate or lost its transport");
            release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
            RetailBackendSessionTests.Check(provider.Reads == 0, "A cancelled source query started provider work");
            session.AllowShutdownRetry();
            RetailBackendSessionTests.Check((await session.ShutdownAsync()).Stopped && transport.DisposeCalls == 1,
                "Ended source query did not allow original cleanup retry");
        } finally { release.TrySetResult(); session.AllowShutdownRetry(); await session.ShutdownAsync(); }
    }
}
