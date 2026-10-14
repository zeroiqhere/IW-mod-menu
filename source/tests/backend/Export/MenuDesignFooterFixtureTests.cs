namespace Backend.Tests;

internal static class MenuDesignFooterFixtureTests
{
    public static void TwoActualAdaptiveAioGapProfilesRetainPaintRowsAndEpochsWhileDeclaringCommittedPlacement()
    {
        var fixtures = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        var first = fixtures["preferences-design-adaptive-aio-rebuild.json"];
        var last = fixtures["preferences-design-adaptive-aio-pre-rebuild.json"];

        foreach (var state in new[] { first, last })
        {
            TestAssert.Equal("Menu Preferences", state.Title);
            TestAssert.Equal("rebuild", state.MenuDesignTransition?.Phase);
            TestAssert.Equal("AIO", MenuDesignFooterTests.FooterDesign(state));
            TestAssert.Equal("Apparition", state.Appearance!.Design);
            TestAssert.True(state.Appearance.AdaptiveInstructions && state.Appearance.ColoredCursor && !state.Appearance.OptionCounter,
                "The actual source path preserves adaptive instructions and compatible committed flags");
            TestAssert.Equal("AIO", state.Rows[0].Value);
            TestAssert.Equal(12, state.Rows.Count);
            TestAssert.Equal(0, state.SelectedIndex);
            TestAssert.Equal("PREVIEW", state.Context);
            TestAssert.True(state.MenuOpening is null, "An absent rebuilt body has no row fade yet");
        }
        TestAssert.Equal(first.MenuRevision, last.MenuRevision);
        TestAssert.Equal(first.NavigationRevision, last.NavigationRevision);
        TestAssert.Equal(first.LastSequence, last.LastSequence);
        TestAssert.True(first.Rows.SequenceEqual(last.Rows), "A 49 ms observation cannot invent a redraw or alter row capability");

        // Existing named profiles retain their source identities and descriptor omission rules
        foreach (string name in new[] { "main.json", "preferences-design-draft.json", "preferences-design-same-cooldown.json" })
        {
            TestAssert.True(MenuDesignFooterTests.FooterDesign(fixtures[name]) is null,
                "Ordinary and same-design profiles have no committed placement descriptor");
        }
    }
}
