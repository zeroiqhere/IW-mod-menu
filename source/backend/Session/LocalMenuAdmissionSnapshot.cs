using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Session;

// The owner keeps this local snapshot out of wire packets and rendered text
public sealed record LocalMenuAdmissionSnapshot {
    public ulong Revision { get; }
    public LocalPlayerObservation Observation { get; }
    public object OwnerToken { get; }

    public LocalMenuAdmissionSnapshot(ulong revision, LocalPlayerObservation observation, object? ownerToken = null) {
        if (revision == 0) throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.IsSupported() || observation.ProcessId <= 0 || observation.Identity is null) {
            throw new ArgumentException("Menu admission requires a supported bound observation", nameof(observation));
        }
        Revision = revision;
        Observation = observation;
        OwnerToken = ownerToken ?? new object();
    }
}
