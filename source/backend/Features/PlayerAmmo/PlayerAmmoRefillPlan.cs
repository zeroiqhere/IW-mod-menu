namespace syndishanx_iw_tool.Features.PlayerAmmo;

// Plans never perform game operations or grant a production capability
public sealed class PlayerAmmoRefillPlan {
    public PlayerAmmoMode Mode { get; }
    public IReadOnlyList<PlayerAmmoRefillOperation> Operations { get; }

    private PlayerAmmoRefillPlan(PlayerAmmoMode mode, IReadOnlyList<PlayerAmmoRefillOperation> operations) {
        Mode = mode;
        Operations = operations;
    }

    public static PlayerAmmoRefillPlan Create(PlayerAmmoMode mode, bool hasCurrentWeapon,
        IEnumerable<VerifiedClipCapacity>? clipCapacities = null) {
        // Invalid choices fail even when no weapon is present
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == PlayerAmmoMode.Disable || !hasCurrentWeapon) {
            // Disable adds no counter restoration and never inspects unused clip facts
            return new(mode, Array.AsReadOnly(Array.Empty<PlayerAmmoRefillOperation>()));
        }

        // Both active source modes refill reserve before any optional clip refill
        var operations = new List<PlayerAmmoRefillOperation>(3) { PlayerAmmoRefillOperation.Reserve() };
        if (mode == PlayerAmmoMode.Continuous && clipCapacities is not null) {
            int handMask = 0;
            foreach (var clip in clipCapacities) {
                if (clip is null) throw new ArgumentException("A clip fact is missing", nameof(clipCapacities));
                int handBit = 1 << (int)clip.Hand;
                if ((handMask & handBit) != 0) {
                    throw new ArgumentException("A clip hand is repeated", nameof(clipCapacities));
                }
                handMask |= handBit;
                // The omitted source hand has unresolved native semantics and stays distinct
                if ((handMask & 1) != 0 && handMask != 1) {
                    throw new ArgumentException("Source default and explicit hands cannot be mixed", nameof(clipCapacities));
                }
                // Zero still participates in validation but never requests a clip write
                if (clip.Capacity > 0) operations.Add(PlayerAmmoRefillOperation.Clip(clip));
            }
        }

        // Only complete validation returns a plan; input storage is never retained
        return new(mode, operations.AsReadOnly());
    }
}
