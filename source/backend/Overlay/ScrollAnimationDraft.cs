using System.Globalization;

namespace syndishanx_iw_tool.Overlay;

// The source stores a numeric candidate in centiseconds and commits only on Use
public sealed record ScrollAnimationDraft(int Units = 12) {
    public const int MinimumUnits = 10;
    public const int MaximumUnits = 25;
    public int Milliseconds => checked(Units * 10);
    public string DisplayValue => Milliseconds.ToString(CultureInfo.InvariantCulture) + " ms";
    public bool IsSupported() => Units is >= MinimumUnits and <= MaximumUnits;
    public static bool IsSupportedMilliseconds(int milliseconds) => milliseconds is >= 100 and <= 250 && milliseconds % 10 == 0;

    public static ScrollAnimationDraft FromAppearance(MenuAppearance appearance) {
        System.ArgumentNullException.ThrowIfNull(appearance);
        if (!appearance.IsSupported()) throw new System.ArgumentException("Unsupported menu appearance", nameof(appearance));
        return new(appearance.ScrollAnimationMilliseconds / 10);
    }

    public bool TryMove(int direction, out ScrollAnimationDraft updated) {
        updated = this;
        if (!IsSupported() || direction is not (-1 or 1)) return false;
        int moved = Units + direction;
        // Numeric sliders wrap at their bounds rather than applying a renderer setting
        updated = new(moved < MinimumUnits ? MaximumUnits : moved > MaximumUnits ? MinimumUnits : moved);
        return true;
    }

    public bool TryCommit(MenuAppearance appearance, out MenuAppearance updated) {
        updated = appearance;
        if (!IsSupported() || !appearance.IsSupported()) return false;
        updated = appearance with { ScrollAnimationMilliseconds = Milliseconds };
        return true;
    }
}
