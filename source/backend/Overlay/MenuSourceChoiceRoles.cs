using Apparition.Backend.QuickMenu;

namespace syndishanx_iw_tool.Overlay;

// These facts follow exact source declarations and never grant their missing callbacks
internal static class MenuSourceChoiceRoles {
    internal static MenuSourceChoiceRole ForBasicScripts(int index) => index switch {
        // Increment sliders differ from the source string-choice rows
        11 or 24 => MenuSourceChoiceRole.IncrementSlider,
        5 or 8 or 14 or 20 or 21 or 22 or 30 => MenuSourceChoiceRole.Slider,
        >= 0 and < 31 => MenuSourceChoiceRole.NonSlider,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    internal static MenuSourceChoiceRole ForQuickOption(QuickMenuOptionId option) => option switch {
        QuickMenuOptionId.UnlimitedAmmo or QuickMenuOptionId.ModifyScore or QuickMenuOptionId.RestartGame => MenuSourceChoiceRole.Slider,
        // Every other declared Quick ID is a boolean, submenu or ordinary action
        >= QuickMenuOptionId.GodMode and <= QuickMenuOptionId.Disconnect => MenuSourceChoiceRole.NonSlider,
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    internal static MenuSourceChoiceRole ForPageRow(MenuPage page, int index, MenuAction action) {
        ArgumentNullException.ThrowIfNull(action);
        // The source preferences and instruction lists retain their declaration order
        if (page == MenuPage.Preferences) {
            MenuPreference? declared = index switch {
                0 => MenuPreference.Design, 1 => MenuPreference.BooleanDisplay, 2 => MenuPreference.BoolBoxLocation,
                3 => MenuPreference.ScrollAnimationTime, 4 => MenuPreference.QuickExit, 5 => MenuPreference.DisableQuickMenu,
                6 => MenuPreference.SpotlightCursor, 7 => MenuPreference.ColoredCursor, 8 => MenuPreference.LargeCursor,
                9 => MenuPreference.OptionCounter, 10 => MenuPreference.StealthUI, _ => null
            };
            // The unavailable No Target row has a source BOOL declaration but no dispatch intent
            if (index == 11 && action.Kind == MenuActionKind.None) return MenuSourceChoiceRole.NonSlider;
            if (index == 5 && action.Kind == MenuActionKind.None) return MenuSourceChoiceRole.NonSlider;
            if (declared is null || action.Kind != MenuActionKind.ChangePreference || action.Slot != (int)declared.Value)
                return MenuSourceChoiceRole.Unknown;
            return index is 0 or 1 or 2 ? MenuSourceChoiceRole.Slider
                : index == 3 ? MenuSourceChoiceRole.IncrementSlider : MenuSourceChoiceRole.NonSlider;
        }
        if (page == MenuPage.Customization && index is >= 0 and < 7
            || page == MenuPage.Instructions && index is >= 0 and < 6) return MenuSourceChoiceRole.NonSlider;
        if (page == MenuPage.ThemeColors
            && (index is >= 0 and < 36 && action.Kind == MenuActionKind.ChangeAccent && action.Slot == index
                || index == 36 && action.Kind == MenuActionKind.EnableSmoothRainbowTheme)) return MenuSourceChoiceRole.NonSlider;
        // Typed source links exclude local Connect, Messages, Support and Exit extensions
        if (page == MenuPage.Main && action.Kind == MenuActionKind.OpenPage
            && action.Page is MenuPage.Personal or MenuPage.Players or MenuPage.AllPlayers or MenuPage.Customization)
            return MenuSourceChoiceRole.NonSlider;
        // The roster links correspond to source player submenu declarations, not the local Refresh row
        if (page == MenuPage.Players && action.Kind == MenuActionKind.SelectPlayer) return MenuSourceChoiceRole.NonSlider;
        return MenuSourceChoiceRole.Unknown;
    }
}
