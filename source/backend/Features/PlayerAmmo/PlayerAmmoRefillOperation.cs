namespace syndishanx_iw_tool.Features.PlayerAmmo;

// Reserve and clip refill remain separate source operations
public enum PlayerAmmoRefillOperationKind { RefillReserve = 0, FillClip = 1 }

public sealed record PlayerAmmoRefillOperation {
    public PlayerAmmoRefillOperationKind Kind { get; }
    public PlayerAmmoHand? Hand { get; }
    public int? Capacity { get; }

    private PlayerAmmoRefillOperation(PlayerAmmoRefillOperationKind kind, PlayerAmmoHand? hand, int? capacity) {
        Kind = kind;
        Hand = hand;
        Capacity = capacity;
    }

    // Reserve intent has no guessed pool size or captured counter value
    internal static PlayerAmmoRefillOperation Reserve() => new(PlayerAmmoRefillOperationKind.RefillReserve, null, null);
    // Copy scalar facts so later changes to input storage cannot alter the plan
    internal static PlayerAmmoRefillOperation Clip(VerifiedClipCapacity clip) => new(PlayerAmmoRefillOperationKind.FillClip, clip.Hand, clip.Capacity);
}
