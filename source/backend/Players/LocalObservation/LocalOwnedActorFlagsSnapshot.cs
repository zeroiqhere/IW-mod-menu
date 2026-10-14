using syndishanx_iw_tool.Players.LocalBinding;

namespace syndishanx_iw_tool.Players.LocalObservation;

// Restore inspection keeps the full binding even when life no longer allows enabling
public sealed record LocalOwnedActorFlagsSnapshot {
    public LocalActorSnapshot Actor { get; }
    public LocalAccountSessionBinding Binding => Actor.Binding;
    public int Health => Actor.SignedHealth;
    public int CommandTime { get; }
    public int MovementType { get; }
    public uint Flags { get; }
    public bool IsEnableEligible => Actor.NativeSessionState == 0 && Health is >= 2 and <= 10000
        && MovementType == 0 && CommandTime >= 0;

    public LocalOwnedActorFlagsSnapshot(LocalActorSnapshot actor, int commandTime, int movementType, uint flags) {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        if (commandTime < 0) throw new ArgumentOutOfRangeException(nameof(commandTime));
        // Dead, nonplaying and unknown life states retain cleanup facts without enable eligibility
        CommandTime = commandTime;
        MovementType = movementType;
        Flags = flags;
    }
}
