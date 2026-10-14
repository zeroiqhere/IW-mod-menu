namespace Apparition.Backend.QuickMenu;

// Player facts are supplied by a verified source, never derived from a running process
public enum QuickPlayerLife { Unknown, Living, Dead }
public sealed record QuickMenuEligibility {
    public QuickPlayerLife Life { get; }
    public bool? IsHost { get; }
    public bool? IsDeveloper { get; }
    public bool IsLifeKnown => Life != QuickPlayerLife.Unknown;
    public bool HasVerifiedAuthority => IsHost == true || IsDeveloper == true;
    public QuickMenuEligibility(QuickPlayerLife life, bool? isHost, bool? isDeveloper) {
        if (!Enum.IsDefined(life)) throw new ArgumentOutOfRangeException(nameof(life));
        // An unknown authority field remains unknown even when the other field is false
        Life = life;
        IsHost = isHost;
        IsDeveloper = isDeveloper;
    }
}
