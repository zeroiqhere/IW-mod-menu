using Memory;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// Native process access stays outside the portable session and its tests
internal static class RetailSessionTransportFactory {
    public static IRetailSessionTransport Create() => new RetailSteamTransport(new Mem());

    // Only the original connection can supply this reader; no separate process lookup is started
    public static ILocalPlayerObservationProvider? CreateObservationProvider(IRetailSessionTransport transport) =>
        transport is ILocalActorSnapshotSource source
            ? new OwnedLocalPlayerObservationProvider(transport.ProcessId, source) : null;
}
