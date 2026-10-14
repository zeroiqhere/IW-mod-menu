using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// The private binding and view must come from one successful copied actor read
public sealed record LocalPlayerObservationReceipt {
    public LocalAccountSessionBinding Binding { get; }
    public LocalPlayerObservation Observation { get; }

    public LocalPlayerObservationReceipt(LocalAccountSessionBinding binding, LocalPlayerObservation observation) {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        if (!LocalObservationBindingShape.IsSupported(binding) || !observation.IsSupported() || observation.ProcessId <= 0 || observation.Identity != binding.Identity) {
            throw new ArgumentException("The observation receipt does not describe its bound actor");
        }
    }
}
