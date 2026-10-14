using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;

namespace Session.Tests;

// A restored display label cannot revive an original owner after identity proof was lost
internal static class RetailConnectionRetirementTests {
    public static async Task ObservedWrongProcessCannotReviveWhenItsOldPidReturns() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(session.ReadMenuState().Connected, "The original opened owner was unavailable");
            transport.ReportedPid = 7777;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().Connected, "Wrong process identity stayed connected");
            transport.ReportedPid = 4242;
            await session.RefreshLocalObservationAsync();
            var retired = session.ReadMenuState();
            Check(!retired.Connected && !retired.CanConnect && !retired.CanPersonalGodmode
                && !retired.CanLobbyAmmo && !retired.CanSelectPlayers && retired.SelectedPlayerTarget is null,
                "An old PID revived the retired original owner or advertised a replacement action");
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo));
            Check(transport.FlagWrites == 0 && transport.AmmoWrites == 0,
                "An action wrote after process identity retirement");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task ObservedDisconnectedOwnerDoesNotReviveBeforeExplicitCleanupAndReconnect() {
        var old = new RecordingRetailTransport();
        var fresh = new RecordingRetailTransport();
        var transports = new Queue<RecordingRetailTransport>([old, fresh]);
        var session = new RetailBackendSession(4242, () => true, () => transports.Dequeue(), () => false);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            old.Disposed = true;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().Connected, "A confirmed disconnected owner stayed connected");
            old.Disposed = false;
            await session.RefreshLocalObservationAsync();
            Check(!session.ReadMenuState().Connected, "Recovered native facts revived a retired owner");
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(old.OpenCalls == 1 && fresh.OpenCalls == 0, "A retained owner was replaced without cleanup");
            Check((await session.ShutdownAsync()).Stopped && old.DisposeCalls == 1,
                "Original cleanup did not release the retired owner once");
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            Check(session.ReadMenuState().Connected && fresh.OpenCalls == 1,
                "An explicit new owner could not connect after original cleanup");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task AGetterFailureWithdrawsTheOwnerInsteadOfFaultingDefaultPolling() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            transport.OnConnected = () => throw new IOException("Original process query failed");
            await session.RefreshLocalObservationAsync();
            transport.OnConnected = null;
            Check(!session.ReadMenuState().Connected && session.ReadLocalMenuAdmission() is null,
                "Failed liveness observation left a confirmed owner or cached opening receipt");
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            Check(transport.FlagWrites == 0, "A failed process proof still admitted a new write");
        } finally { transport.OnConnected = null;await session.ShutdownAsync(); }
    }

    public static async Task AWorkerCompletionAcrossProcessReplacementCannotPublishPriorActorFacts() {
        var transport = new RecordingRetailTransport();
        var provider = new RecordingObservationProvider();
        var session = new RetailBackendSession(4242, () => true, () => transport, () => false, _ => provider);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.RefreshLocalObservationAsync();
            Check(session.ReadLocalObservation().Life == syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Alive,
                "The original actor observation must be known before replacement");
            provider.Read = _ => {
                transport.ReportedPid = 7777;
                return Task.FromResult<syndishanx_iw_tool.Players.LocalObservation.LocalPlayerObservation?>(RecordingObservationProvider.Known());
            };
            await session.RefreshLocalObservationAsync();
            transport.ReportedPid = 4242;
            Check(!session.ReadMenuState().Connected
                && session.ReadLocalObservation().Life == syndishanx_iw_tool.Players.LocalObservation.LocalPlayerLife.Unknown
                && session.ReadLocalMenuAdmission() is null,
                "A copied actor observation survived the worker's changed process boundary");
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            Check(transport.FlagWrites == 0, "A stale completed source admitted a new enable");
        } finally { await session.ShutdownAsync(); }
    }

    public static async Task AProcessLossInsidePersonalPreflightCannotEnterTheEnableWrite() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            transport.OnLocalRead = () => {
                transport.OnLocalRead = null;transport.ReportedPid = 7777;
            };
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            Check(transport.FlagWrites == 0 && !session.ReadMenuState().Connected,
                "Process proof lost inside personal preflight still entered a new ON write");
        } finally { transport.ReportedPid = 4242;transport.OnLocalRead = null;await session.ShutdownAsync(); }
    }

    public static async Task AProcessLossDuringAmmoDiscoveryCannotEnterThePatchWrite() {
        var transport = new RecordingRetailTransport();
        var session = RetailBackendSessionTests.Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            transport.Scan = _ => {
                transport.ReportedPid = 7777;
                return Task.FromResult<IReadOnlyList<long>>([0x140001000]);
            };
            await session.ExecuteAsync(new(MenuActionKind.ToggleLobbyAmmo));
            Check(transport.AmmoWrites == 0 && !session.ReadMenuState().Connected,
                "Process proof lost during discovery still entered a new ammo patch write");
        } finally { transport.ReportedPid = 4242;transport.Scan = null;await session.ShutdownAsync(); }
    }

    private static void Check(bool condition, string message) => RetailBackendSessionTests.Check(condition, message);
}
