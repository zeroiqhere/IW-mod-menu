using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// This copied feature view is not an atomic snapshot or an engine spawn token
public sealed record LocalActorFlagsSnapshot {
    public LocalActorSnapshot Actor { get; }
    public LocalAccountSessionBinding Binding => Actor.Binding;
    public int Health => Actor.SignedHealth;
    public int CommandTime { get; }
    public int MovementType { get; }
    public uint Flags { get; }

    public LocalActorFlagsSnapshot(LocalActorSnapshot actor, int commandTime, int movementType, uint flags) {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        // These feature guards are deliberately stricter than the general native life predicate
        if (actor.NativeSessionState != 0 || actor.SignedHealth is < 2 or > 10000) {
            throw new ArgumentException("The actor is outside supported normal feature play", nameof(actor));
        }
        if (commandTime < 0) throw new ArgumentOutOfRangeException(nameof(commandTime));
        if (movementType != 0) throw new ArgumentOutOfRangeException(nameof(movementType));
        CommandTime = commandTime;
        MovementType = movementType;
        Flags = flags;
    }
}
