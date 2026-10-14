namespace syndishanx_iw_tool.Overlay;

// The supported source preferences determine the number of ordinary visible rows
public static class MenuOptionCapacity {
    public static int For(MenuAppearance appearance) {
        System.ArgumentNullException.ThrowIfNull(appearance);
        if (!appearance.IsSupported()) throw new System.ArgumentException("Unsupported menu appearance", nameof(appearance));
        // Stealth fixes capacity before all other source adjustments
        if (appearance.StealthUI) return 5;
        int hidden = appearance.InstructionsVisible ? 0 : 1;
        return 10 + hidden + (appearance.BooleanDisplay == "Boxes" ? 0 : 2 + hidden) - (appearance.OptionCounter ? 2 : 0);
    }

    public static int OpeningDurationMilliseconds(int rowCount, MenuAppearance appearance) {
        if (rowCount is < 0 or > 128) throw new System.ArgumentOutOfRangeException(nameof(rowCount));
        // Durations use visible ordinals rather than absolute indices or delayed starts
        int shown = System.Math.Min(rowCount, For(appearance));
        return System.Math.Max(0, shown - 1) * 100;
    }
}
