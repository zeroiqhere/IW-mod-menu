using Apparition.Backend.Ipc;
using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuAppearanceTests {
    public static void DefaultsMatchSourcePresentationAndLegacyStatesRemainNullable() {
        var initial = MenuAppearance.Default;
        TestAssert.Equal("Boxes", initial.BooleanDisplay);
        TestAssert.Equal("Right", initial.BoolBoxLocation);
        TestAssert.True(initial.InstructionsVisible && initial.ColoredCursor && initial.SpotlightCursor,
            "Source instructions and cursor defaults must be visible");
        TestAssert.True(!initial.OptionCounter && !initial.LargeCursor, "Unset source boolean options default off");
        var state = StateDocument.Create(42, 1, false, new("Apparition", "PREVIEW", Array.Empty<MenuRow>(), ""), 0);
        using var json = JsonDocument.Parse(state.Serialize());
        TestAssert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("appearance").ValueKind);
    }

    public static void SupportedDisplayAndLocationChoicesCycleAndReturnToTheirDefaults() {
        var current = MenuAppearance.Default;
        var drafts = MenuChoiceDrafts.FromAppearance(current);
        foreach (string expected in new[] { "Text", "Text Color", "Boxes" }) {
            TestAssert.True(drafts.TryMove((int)MenuPreference.BooleanDisplay, 1, out drafts), "Supported display draft must cycle");
            TestAssert.True(drafts.TryCommit((int)MenuPreference.BooleanDisplay, current, out current), "Use must apply the chosen display");
            TestAssert.Equal(expected, current.BooleanDisplay);
        }
        foreach (string expected in new[] { "Left", "Right" }) {
            TestAssert.True(drafts.TryMove((int)MenuPreference.BoolBoxLocation, 1, out drafts), "Supported location draft must cycle");
            TestAssert.True(drafts.TryCommit((int)MenuPreference.BoolBoxLocation, current, out current), "Use must apply the chosen location");
            TestAssert.Equal(expected, current.BoolBoxLocation);
        }
        TestAssert.Equal(MenuAppearance.Default, current);
    }

    public static void BooleanToggleHelperCannotBypassStagedChoiceCommit() {
        foreach (int index in new[] { 0, 1 }) {
            TestAssert.True(!MenuAppearance.Default.TryChange(index, out var unchanged), "Choice values require their staged commit route");
            TestAssert.Equal(MenuAppearance.Default, unchanged);
        }
    }

    public static void UnknownPreferenceIndicesAndUnsupportedStringsRejectWithoutChangingState() {
        foreach (int index in new[] { -1, 12, 14, int.MinValue, int.MaxValue }) {
            TestAssert.True(!MenuAppearance.Default.TryChange(index, out var unchanged), "Unknown preference cannot apply");
            TestAssert.Equal(MenuAppearance.Default, unchanged);
        }
        foreach (var invalid in new[] { MenuAppearance.Default with { BooleanDisplay = "Rainbow" },
            MenuAppearance.Default with { BoolBoxLocation = "Center" },
            MenuAppearance.Default with { BooleanDisplay = "boxes" } }) {
            TestAssert.True(!invalid.IsSupported(), "Unknown enum text cannot be advertised as supported");
            TestAssert.True(!invalid.TryChange((int)MenuPreference.LargeCursor, out var unchanged),
                "An unsupported appearance cannot become a valid partial update");
            TestAssert.Equal(invalid, unchanged);
        }
    }

    public static void PreferencesExposeOnlyImplementedChoicesAndTheirActualValues() {
        var menu = OverlayMenuBuilder.Build(MenuPage.Preferences, new SessionBoundary().State, choiceControlsAvailable: true);
        TestAssert.Equal("Menu Preferences", menu.Title);
        TestAssert.Equal(12, menu.Rows.Count);
        TestAssert.True(menu.Rows.Select(row => row.Label).SequenceEqual(StealthTests.Labels), "The source inventory must be exact");
        TestAssert.Equal("Apparition", menu.Rows[0].Value);
        TestAssert.Equal("Boxes", menu.Rows[1].Value);
        TestAssert.Equal("Right", menu.Rows[2].Value);
        TestAssert.Equal("120 ms", menu.Rows[3].Value);
        TestAssert.Equal("OFF", menu.Rows[4].Value);
        TestAssert.Equal("ON", menu.Rows[6].Value);
        TestAssert.Equal("ON", menu.Rows[7].Value);
        TestAssert.Equal("OFF", menu.Rows[8].Value);
        TestAssert.Equal("OFF", menu.Rows[9].Value);
        TestAssert.Equal("OFF", menu.Rows[10].Value);
        foreach (int index in new[] { 5, 11 }) {
            TestAssert.True(!menu.Rows[index].Enabled && menu.Rows[index].Value == "UNAVAILABLE", "Unsupported actions stay honest");
        }
    }

    public static void EachBooleanPreferenceChangesOnlyItsOwnPresentationField() {
        var original = MenuAppearance.Default;
        foreach (var pair in new[] {
            (MenuPreference.InstructionsVisible, original with { InstructionsVisible = false }),
            (MenuPreference.SpotlightCursor, original with { SpotlightCursor = false }),
            (MenuPreference.ColoredCursor, original with { ColoredCursor = false }),
            (MenuPreference.LargeCursor, original with { LargeCursor = true }),
            (MenuPreference.OptionCounter, original with { OptionCounter = true }),
            (MenuPreference.QuickExit, original with { QuickExit = true })
        }) {
            TestAssert.True(original.TryChange((int)pair.Item1, out var changed), "A supported toggle must succeed");
            TestAssert.Equal(pair.Item2, changed);
            TestAssert.True(changed.TryChange((int)pair.Item1, out var restored), "A second toggle must succeed");
            TestAssert.Equal(original, restored);
            TestAssert.Equal(new MenuAppearance(), original);
        }
    }
}
