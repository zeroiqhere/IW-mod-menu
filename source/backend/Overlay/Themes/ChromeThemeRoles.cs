namespace syndishanx_iw_tool.Overlay.Themes;

// Each bit identifies an existing source chrome object, independently of its visibility
public static class ChromeThemeRoles {
    public const int Scroller = 1;
    public const int Banner = 2;
    public const int Title = 4;
    public const int Separator = 8;
    public const int BottomLine = 16;

    public static int CreationMask(string? design) => design switch {
        "Apparition" => 31, "Classic" => 7, "Native" => 15, "AIO" => 29, "Basic" => 7, _ => 0
    };
    public static int FixedMask(string? design) => design switch {
        "Apparition" => 28, "Classic" or "Native" => 3, "AIO" => 25, "Basic" => 4, _ => 0
    };
    public static int Bit(ThemeWidgetRole role) => role switch {
        ThemeWidgetRole.Scroller => Scroller, ThemeWidgetRole.Banner => Banner,
        ThemeWidgetRole.Title => Title, ThemeWidgetRole.Separator => Separator,
        ThemeWidgetRole.BottomLine => BottomLine, _ => 0
    };
    public static bool Hex(string? accent) {
        if (accent is null || accent.Length != 7 || accent[0] != '#') return false;
        foreach (char digit in accent.AsSpan(1)) {
            if (digit is not (>= '0' and <= '9') and not (>= 'A' and <= 'F') and not (>= 'a' and <= 'f')) return false;
        }
        return true;
    }
}
