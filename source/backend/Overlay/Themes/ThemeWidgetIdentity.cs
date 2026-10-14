namespace syndishanx_iw_tool.Overlay.Themes;

public enum ThemeWidgetLifetime { Rows, MenuChrome, Instructions, PlayerInfo, ExternalLui }
public enum ThemeWidgetRole {
    RowText, RowBooleanText, RowSubmenu, RowIntegerSlider, RowStringSlider,
    RowBooleanInterior, RowBooleanBackground, RowInvalidOption,
    Scroller, Banner, Title, Separator, BottomLine, Body, SecondaryTitle, Counter,
    InstructionsBackground, InstructionsOutline, InstructionsText,
    PlayerInfoBackground, PlayerInfoOutline, PlayerInfoText,
    ZombieCounter, EntityCount, CustomCrosshair
}

// The supplied key identifies an existing widget instance, not its position or permission
public sealed record ThemeWidgetIdentity {
    public string Key { get; }
    public ThemeWidgetRole Role { get; }
    public ThemeWidgetLifetime Lifetime { get; }

    public ThemeWidgetIdentity(string key, ThemeWidgetRole role, ThemeWidgetLifetime lifetime) {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An observed widget key is required", nameof(key));
        if (!Enum.IsDefined(role) || !Enum.IsDefined(lifetime) || ExpectedLifetime(role) != lifetime) {
            throw new ArgumentException("Widget role and lifetime must match the source object", nameof(role));
        }
        Key = key;
        Role = role;
        Lifetime = lifetime;
    }

    // Source drawText destroys rows independently from chrome and instructions
    private static ThemeWidgetLifetime ExpectedLifetime(ThemeWidgetRole role) => role switch {
        <= ThemeWidgetRole.RowInvalidOption => ThemeWidgetLifetime.Rows,
        <= ThemeWidgetRole.Counter => ThemeWidgetLifetime.MenuChrome,
        <= ThemeWidgetRole.InstructionsText => ThemeWidgetLifetime.Instructions,
        <= ThemeWidgetRole.PlayerInfoText => ThemeWidgetLifetime.PlayerInfo,
        _ => ThemeWidgetLifetime.ExternalLui
    };
}
