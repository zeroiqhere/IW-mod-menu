using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuDesignFixtureTests
{
    public static void ActualDesignProfilesExposeFiveGeometriesDraftCommitRestrictionsAndBothIndependentWaits()
    {
        var fixtures = MenuFixtureExporter.Build(); TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        string[] phases = { "rebuild", "rebuild", "cooldown", "cooldown", null! };
        string[] stages = { "rebuild", "pre-rebuild", "cooldown", "pre-unlock", "unlocked" };
        foreach (string name in MenuDesignTests.Names.Skip(1))
        {
            var first = fixtures["preferences-design-" + name.ToLowerInvariant() + "-rebuild.json"];
            for (int stage = 0; stage < stages.Length; stage++)
            {
                var state = fixtures["preferences-design-" + name.ToLowerInvariant() + "-" + stages[stage] + ".json"];
                TestAssert.Equal(phases[stage], state.MenuDesignTransition?.Phase); TestAssert.Equal(stage < 2 ? "Apparition" : name, state.Appearance!.Design);
                TestAssert.Equal(12, state.Rows.Count); TestAssert.Equal(0, state.SelectedIndex); TestAssert.Equal(name, state.Rows[0].Value);
                TestAssert.Equal(first.NavigationRevision, state.NavigationRevision); TestAssert.Equal(first.MenuRevision + (stage < 2 ? 0UL : 1UL), state.MenuRevision);
                bool color = MenuDesignDraft.AllowsColoredCursor(name), counter = MenuDesignDraft.AllowsOptionCounter(name);
                TestAssert.Equal(color, state.Appearance.ColoredCursor); TestAssert.Equal(counter, state.Appearance.OptionCounter);
                TestAssert.True(state.Rows[7].Enabled && state.Rows[9].Enabled,
                    "Source BOOL rows remain selectable when their handlers reject the current design");
                TestAssert.Equal(color ? "ON" : "OFF", state.Rows[7].Value); TestAssert.Equal(counter ? "ON" : "OFF", state.Rows[9].Value);
                TestAssert.True(state.IsOpen && !state.IsRootMenu && state.Context == "PREVIEW", "Actual offline profiles cannot claim a game connection");
            }
        }
        var draft = fixtures["preferences-design-draft.json"]; TestAssert.Equal("Classic", draft.Rows[0].Value); TestAssert.Equal("Apparition", draft.Appearance!.Design); TestAssert.True(draft.MenuDesignTransition is null, "Draft has no transition");
        var same = fixtures["preferences-design-same-cooldown.json"];
        foreach (string stage in new[] { "cooldown", "pre-unlock", "unlocked" })
        {
            var frame = fixtures["preferences-design-same-" + stage + ".json"]; TestAssert.Equal(stage == "unlocked" ? null : "cooldown", frame.MenuDesignTransition?.Phase);
            TestAssert.Equal(same.MenuRevision, frame.MenuRevision); TestAssert.Equal(same.NavigationRevision, frame.NavigationRevision); TestAssert.Equal(MenuAppearance.Default, frame.Appearance);
        }
        var native = fixtures["main-design-native.json"]; TestAssert.Equal("Main Menu", native.Title); TestAssert.True(native.IsRootMenu, "Native title is presentation, not root identity");
        TestAssert.Equal("Native", native.Appearance!.Design); TestAssert.True(native.Appearance.OptionCounter, "Compatible counter survives the design commit");
        var width = fixtures["width-editor-design-classic.json"]; TestAssert.Equal("Classic", width.Appearance!.Design); TestAssert.True(width.WidthEditor is not null && width.MenuDesignTransition is null && width.Rows.Count == 0, "A completed design remains coherent in a width editor");
    }
}
