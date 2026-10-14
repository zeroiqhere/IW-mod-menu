namespace syndishanx_iw_tool.Features.PlayerAmmo;

// Adapter-supplied facts describe intent; this value does not authenticate game memory
public sealed record VerifiedClipCapacity {
    public PlayerAmmoHand Hand { get; }
    public int Capacity { get; }

    public VerifiedClipCapacity(PlayerAmmoHand hand, int capacity) {
        // Invalid hand identifiers cannot masquerade as a source default or a second clip
        if (!Enum.IsDefined(hand)) throw new ArgumentOutOfRangeException(nameof(hand));
        // Zero describes a weapon without a refillable clip; no game maximum is guessed
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Hand = hand;
        Capacity = capacity;
    }
}
