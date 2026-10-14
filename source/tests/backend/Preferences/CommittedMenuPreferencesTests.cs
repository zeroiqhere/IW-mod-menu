using Apparition.Backend.OpenControls;
using Apparition.Backend.Preferences;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Committed choices are values, never a saved policy revision or feature permission
internal static class CommittedMenuPreferencesTests {
    public static void CompleteCommittedValueRetainsSettingsWithoutRuntimeAuthority() {
        var appearance = MenuAppearance.Default with {
            BooleanDisplay = "Text Color", BoolBoxLocation = "Left", InstructionsVisible = false,
            ColoredCursor = false, SpotlightCursor = false, LargeCursor = true, QuickExit = true,
            MenuWidth = 390, InstructionsBackground = false, AdaptiveInstructions = true,
            InstructionPosition = new(-255, 480), ScrollAnimationMilliseconds = 250,
            MenuPosition = new(18, -85), Design = "Classic", StealthUI = true
        };
        var value = new CommittedMenuPreferences(appearance, "#00FF00", false,
            new[] { OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Jump },
            new[] { OpeningAction.Aim, OpeningAction.SecondaryOffhand }, true);
        TestAssert.Equal(appearance, value.Appearance);
        TestAssert.Equal("#00FF00", value.Accent);
        TestAssert.True(!value.SmoothRainbowTheme && value.QuickDisabled, "Committed mode flags changed");
        TestAssert.True(value.MainOpeningActions!.SequenceEqual(new[] { OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Jump })
            && value.QuickOpeningActions!.SequenceEqual(new[] { OpeningAction.Aim, OpeningAction.SecondaryOffhand }),
            "Both independent tuples must retain source slot order");
        var names = typeof(CommittedMenuPreferences).GetProperties().Select(property => property.Name).Order().ToArray();
        TestAssert.True(names.SequenceEqual(new[] { "Accent", "Appearance", "MainOpeningActions", "QuickDisabled", "QuickOpeningActions", "SmoothRainbowTheme" }),
            "The committed model must contain only settings, not drafts, epochs, owner tokens, or feature authority");
    }

    public static void EverySupportedFixedAccentIsCanonicalized() {
        foreach (var theme in MenuThemes.All) {
            var value = new CommittedMenuPreferences(MenuAppearance.Default, theme.Hex.ToLowerInvariant(), false);
            TestAssert.Equal(theme.Hex, value.Accent);
        }
        // A syntactically valid color outside the source palette is still unsupported
        TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(MenuAppearance.Default, "#123456", false));
        TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(MenuAppearance.Default, "00FF00", false));
        TestAssert.Throws<ArgumentNullException>(() => new CommittedMenuPreferences(MenuAppearance.Default, null!, false));
    }

    public static void RainbowReloadDoesNotRecoverPriorFixedAccent() {
        var value = new CommittedMenuPreferences(MenuAppearance.Default, "#FF0000", true);
        TestAssert.True(value.SmoothRainbowTheme && value.Accent == MenuThemes.DefaultAccent,
            "The source Rainbow sentinel must not save the previously selected fixed palette");
        TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(MenuAppearance.Default, "#123456", true));
    }

    public static void InvalidAppearancesRejectWithoutNormalizingDependencies() {
        MenuAppearance[] invalid = {
            MenuAppearance.Default with { StealthUI = true },
            MenuAppearance.Default with { StealthUI = true, BooleanDisplay = "Text", OptionCounter = true },
            MenuAppearance.Default with { AlternateInstructions = true, AdaptiveInstructions = true },
            MenuAppearance.Default with { Design = "Native" },
            MenuAppearance.Default with { Design = "AIO", OptionCounter = true },
            MenuAppearance.Default with { BooleanDisplay = "Invalid" },
            MenuAppearance.Default with { BoolBoxLocation = "Center" },
            MenuAppearance.Default with { MenuWidth = 199 },
            MenuAppearance.Default with { ScrollAnimationMilliseconds = 125 },
            MenuAppearance.Default with { Design = "Unknown" }
        };
        foreach (var appearance in invalid)
            TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(appearance, MenuThemes.DefaultAccent, false));
        TestAssert.Throws<ArgumentNullException>(() => new CommittedMenuPreferences(null!, MenuThemes.DefaultAccent, false));
    }

    public static void AbsentTuplesRemainAbsentWithoutInventingDefaults() {
        var missing = new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false);
        TestAssert.True(missing.MainOpeningActions is null && missing.QuickOpeningActions is null,
            "Missing saved action choices must remain absent until the loader uses verified bindings");
        // Stored structure does not pretend that an action currently has a physical binding
        var quickOnly = new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false,
            quickOpeningActions: new[] { OpeningAction.Sprint }, quickDisabled: true);
        TestAssert.True(quickOnly.MainOpeningActions is null && quickOnly.QuickOpeningActions!.Single() == OpeningAction.Sprint,
            "Independent nullable tuples must not create a fake Main owner");
    }

    public static void InvalidOpeningTuplesRejectBeforePublication() {
        OpeningAction[][] invalid = {
            Array.Empty<OpeningAction>(),
            new[] { OpeningAction.Aim, OpeningAction.Melee, OpeningAction.Use, OpeningAction.Jump },
            new[] { (OpeningAction)(-1) }, new[] { (OpeningAction)13 },
            new[] { OpeningAction.Aim, OpeningAction.Aim },
            new[] { OpeningAction.Frag, OpeningAction.SecondaryOffhand }
        };
        foreach (var actions in invalid) {
            TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false, actions));
            TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false,
                quickOpeningActions: actions));
        }
        var unbounded = new UnboundedOpeningActions();
        TestAssert.Throws<ArgumentException>(() => new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false, unbounded));
        TestAssert.Equal(0, unbounded.IndexReads);
    }

    public static void CallerArraysCannotChangeCommittedOpeningActions() {
        OpeningAction[] main = { OpeningAction.Aim, OpeningAction.Melee };
        OpeningAction[] quick = { OpeningAction.Aim, OpeningAction.SecondaryOffhand };
        var value = new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false, main, quick);
        main[0] = OpeningAction.Attack;
        quick[1] = OpeningAction.Frag;
        TestAssert.True(value.MainOpeningActions!.SequenceEqual(new[] { OpeningAction.Aim, OpeningAction.Melee })
            && value.QuickOpeningActions!.SequenceEqual(new[] { OpeningAction.Aim, OpeningAction.SecondaryOffhand }),
            "Caller arrays must not alter saved combinations after construction");
    }

    public static void PublishedActionListsRejectMutation() {
        var value = new CommittedMenuPreferences(MenuAppearance.Default, MenuThemes.DefaultAccent, false,
            new[] { OpeningAction.Aim }, new[] { OpeningAction.Melee });
        var main = (IList<OpeningAction>)value.MainOpeningActions!;
        var quick = (IList<OpeningAction>)value.QuickOpeningActions!;
        TestAssert.True(main.IsReadOnly && quick.IsReadOnly, "Both published tuple lists must be read-only");
        TestAssert.Throws<NotSupportedException>(() => main[0] = OpeningAction.Jump);
        TestAssert.Throws<NotSupportedException>(() => quick.Add(OpeningAction.Jump));
        TestAssert.Equal(OpeningAction.Aim, value.MainOpeningActions![0]);
        TestAssert.Equal(OpeningAction.Melee, value.QuickOpeningActions![0]);
    }
}
