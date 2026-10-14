namespace syndishanx_iw_tool.Overlay;

// Draft indices belong to the controller and never change the committed renderer appearance
public sealed record MenuChoiceDrafts(int BooleanDisplayIndex, int BoolBoxLocationIndex) {
    private static readonly string[] Displays = { "Boxes", "Text", "Text Color" };
    private static readonly string[] Locations = { "Right", "Left" };
    public static bool IsChoice(int index) => index is (int)MenuPreference.BooleanDisplay or (int)MenuPreference.BoolBoxLocation;
    public bool IsSupported() => BooleanDisplayIndex is >= 0 and < 3 && BoolBoxLocationIndex is >= 0 and < 2;
    public static MenuChoiceDrafts FromAppearance(MenuAppearance appearance) {
        System.ArgumentNullException.ThrowIfNull(appearance);
        if (!appearance.IsSupported()) throw new System.ArgumentException("Unsupported menu appearance", nameof(appearance));
        return new(System.Array.IndexOf(Displays, appearance.BooleanDisplay), System.Array.IndexOf(Locations, appearance.BoolBoxLocation));
    }
    public string Candidate(int preference) => preference switch {
        (int)MenuPreference.BooleanDisplay when IsSupported() => Displays[BooleanDisplayIndex],
        (int)MenuPreference.BoolBoxLocation when IsSupported() => Locations[BoolBoxLocationIndex],
        _ => throw new System.ArgumentOutOfRangeException(nameof(preference))
    };
    public bool TryMove(int preference, int direction, out MenuChoiceDrafts updated) {
        updated = this;
        if (!IsSupported() || !IsChoice(preference) || direction is not (-1 or 1)) return false;
        // Wrapping changes the draft index without applying a renderer setting
        updated = preference == (int)MenuPreference.BooleanDisplay
            ? this with { BooleanDisplayIndex = (BooleanDisplayIndex + direction + Displays.Length) % Displays.Length }
            : this with { BoolBoxLocationIndex = (BoolBoxLocationIndex + direction + Locations.Length) % Locations.Length };
        return true;
    }
    public bool TryCommit(int preference, MenuAppearance appearance, out MenuAppearance updated) {
        updated = appearance;
        if (!IsSupported() || !IsChoice(preference) || !appearance.IsSupported()) return false;
        if (preference == (int)MenuPreference.BooleanDisplay && appearance.StealthUI && Candidate(preference) == "Boxes") return false;
        // Only Use copies a candidate into the renderer's committed appearance
        updated = preference == (int)MenuPreference.BooleanDisplay
            ? appearance with { BooleanDisplay = Candidate(preference) }
            : appearance with { BoolBoxLocation = Candidate(preference) };
        return true;
    }
}
