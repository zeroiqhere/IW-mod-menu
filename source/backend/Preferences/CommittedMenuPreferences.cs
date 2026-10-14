using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Preferences;

// Only committed settings survive a new controller; runtime authority stays with its owner
public sealed class CommittedMenuPreferences {
    public CommittedMenuPreferences(MenuAppearance appearance, string accent, bool smoothRainbowTheme,
        IReadOnlyList<OpeningAction>? mainOpeningActions = null,
        IReadOnlyList<OpeningAction>? quickOpeningActions = null, bool quickDisabled = false) {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(accent);
        if (!appearance.IsSupported())
            throw new ArgumentException("Unsupported committed menu appearance", nameof(appearance));
        var theme = MenuThemes.All.FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Hex, accent))
            ?? throw new ArgumentException("Unsupported committed menu accent", nameof(accent));
        // Validate both independent tuples before publishing any part of the value
        var main = CommittedOpeningActions.Copy(mainOpeningActions, nameof(mainOpeningActions));
        var quick = CommittedOpeningActions.Copy(quickOpeningActions, nameof(quickOpeningActions));
        Appearance = appearance;
        // Rainbow is the source's saved sentinel, not its previously chosen fixed color
        Accent = smoothRainbowTheme ? MenuThemes.DefaultAccent : theme.Hex;
        SmoothRainbowTheme = smoothRainbowTheme;
        MainOpeningActions = main;
        QuickOpeningActions = quick;
        QuickDisabled = quickDisabled;
    }

    public MenuAppearance Appearance { get; }
    public string Accent { get; }
    public bool SmoothRainbowTheme { get; }
    // Saved IDs describe choices only; loading must revalidate exact current bindings
    public IReadOnlyList<OpeningAction>? MainOpeningActions { get; }
    public IReadOnlyList<OpeningAction>? QuickOpeningActions { get; }
    public bool QuickDisabled { get; }
}
