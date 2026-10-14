using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// Apparition retains its verification level for one actor, independent of life and menu paint
internal sealed class LocalMenuAccessState {
    private LocalAccountSessionBinding? actor;
    private int? verificationLevel;
    private const int HostLevel = 6;

    public bool? Observe(LocalActorSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (actor != snapshot.Binding) {
            // A replacement actor cannot inherit the earlier actor's verification level
            actor = snapshot.Binding;
            verificationLevel = null;
        }
        // The positive native session predicate supports the source Host initialization branch
        // Its negative result does not identify the actor-method override or a saved access level
        if (snapshot.IsSessionHost) verificationLevel = HostLevel;
        // Source HasMenu compares the retained verification level with None at level one
        return verificationLevel is int level ? level > 1 : null;
    }
}
