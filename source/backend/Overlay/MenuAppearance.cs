namespace syndishanx_iw_tool.Overlay;

// Preference indices describe local menu settings rather than game operations
public enum MenuPreference {
    BooleanDisplay, BoolBoxLocation, InstructionsVisible, SpotlightCursor,
    ColoredCursor, LargeCursor, OptionCounter, QuickExit, AlternateInstructions, InstructionsBackground, AdaptiveInstructions, ScrollAnimationTime, Design, StealthUI, DisableQuickMenu
}

// Defaults match Apparition's unsaved menu variables and remain immutable
public sealed record MenuAppearance(string BooleanDisplay = "Boxes", string BoolBoxLocation = "Right",
    bool InstructionsVisible = true, bool OptionCounter = false, bool ColoredCursor = true,
    bool SpotlightCursor = true, bool LargeCursor = false, bool QuickExit = false, int MenuWidth = 260,
    bool AlternateInstructions = false, bool InstructionsBackground = true, bool AdaptiveInstructions = false,
    InstructionPosition? InstructionPosition = null, int ScrollAnimationMilliseconds = 120, MenuPosition? MenuPosition = null, string Design = "Apparition", bool StealthUI = false) {
    public static MenuAppearance Default { get; } = new();

    public bool IsSupported() => BooleanDisplay is "Boxes" or "Text" or "Text Color"
        && !(StealthUI && (BooleanDisplay == "Boxes" || OptionCounter))
        && BoolBoxLocation is "Right" or "Left"
        && !(AlternateInstructions && AdaptiveInstructions)
        && MenuWidth is >= LocalWidthEditorState.MinimumWidth and <= LocalWidthEditorState.MaximumWidth
        && ScrollAnimationDraft.IsSupportedMilliseconds(ScrollAnimationMilliseconds) && MenuDesignDraft.IsSupportedDesign(Design)
        && (MenuDesignDraft.AllowsColoredCursor(Design) || !ColoredCursor)
        && (MenuDesignDraft.AllowsOptionCounter(Design) || !OptionCounter);

    // A rejected index or unsupported state keeps the original appearance unchanged
    public bool TryChange(int preferenceIndex, out MenuAppearance updated) {
        updated = this;
        if (!IsSupported() || !System.Enum.IsDefined(typeof(MenuPreference), preferenceIndex)
            || MenuChoiceDrafts.IsChoice(preferenceIndex) || preferenceIndex is (int)MenuPreference.ScrollAnimationTime or (int)MenuPreference.Design or (int)MenuPreference.DisableQuickMenu) return false;
        if ((MenuPreference)preferenceIndex == MenuPreference.AdaptiveInstructions && AlternateInstructions) return false;
        if (preferenceIndex == (int)MenuPreference.ColoredCursor && !MenuDesignDraft.AllowsColoredCursor(Design)
            || preferenceIndex == (int)MenuPreference.OptionCounter && (StealthUI || !MenuDesignDraft.AllowsOptionCounter(Design))) return false;
        // Policy-owned Quick disable and staged choices cannot use appearance mutation
        // Boolean Select actions cannot bypass the separate staged choice commit route
        updated = (MenuPreference)preferenceIndex switch {
            MenuPreference.InstructionsVisible => this with { InstructionsVisible = !InstructionsVisible },
            MenuPreference.SpotlightCursor => this with { SpotlightCursor = !SpotlightCursor },
            MenuPreference.ColoredCursor => this with { ColoredCursor = !ColoredCursor },
            MenuPreference.LargeCursor => this with { LargeCursor = !LargeCursor },
            MenuPreference.OptionCounter => this with { OptionCounter = !OptionCounter },
            // Enabling Stealth removes incompatible settings without restoring them on disable
            MenuPreference.StealthUI => this with { StealthUI = !StealthUI,
                BooleanDisplay = !StealthUI && BooleanDisplay == "Boxes" ? "Text" : BooleanDisplay,
                OptionCounter = !StealthUI ? false : OptionCounter },
            MenuPreference.QuickExit => this with { QuickExit = !QuickExit },
            // The source clears Adaptive Position before either Alternate Style toggle
            MenuPreference.AlternateInstructions => this with { AlternateInstructions = !AlternateInstructions, AdaptiveInstructions = false, InstructionPosition = null },
            MenuPreference.InstructionsBackground => this with { InstructionsBackground = !InstructionsBackground },
            MenuPreference.AdaptiveInstructions => this with { AdaptiveInstructions = !AdaptiveInstructions },
            _ => this
        };
        return true;
    }
}
