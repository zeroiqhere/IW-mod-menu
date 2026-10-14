namespace Backend.Tests;

internal static class MenuInitialOpenFixtureTests
{
    public static void ActualInitialOpenProfilesPreserveRetainedNavigationAndBoundedIndependentWaitsWithoutGameActions()
    {
        var fixtures = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        var initial = fixtures["menu-initial-open-rebuild.json"];
        TestAssert.Equal("rebuild", initial.MenuOpenTransition?.Phase);
        foreach (var pair in new[] { ("drawn", 0), ("pre-unlock", 549), ("input-ready", 550), ("last", 699) })
        {
            var state = fixtures["menu-initial-open-" + pair.Item1 + ".json"];
            TestAssert.Equal(pair.Item2, state.MenuOpening!.ElapsedMilliseconds);
            TestAssert.Equal(1UL, state.MenuOpening.Revision);
            TestAssert.Equal(initial.MenuRevision, state.MenuRevision);
            TestAssert.Equal(initial.NavigationRevision, state.NavigationRevision);
        }
        TestAssert.True(fixtures["menu-initial-open-input-ready.json"].MenuOpenTransition is null,
            "Controls become ready before the last visible row finishes");
        TestAssert.True(fixtures["menu-initial-open-complete.json"].MenuOpening is null,
            "The final visible ordinal completes without an extra row delay");
        TestAssert.Equal(0, fixtures["menu-initial-open-late.json"].MenuOpening!.ElapsedMilliseconds);
        TestAssert.Equal("cooldown", fixtures["menu-initial-open-late-pre-unlock.json"].MenuOpenTransition?.Phase);
        TestAssert.True(fixtures["menu-initial-open-late-ready.json"].MenuOpenTransition is null,
            "Late redraw still receives its complete remaining 550 ms wait");
        var retained = fixtures["menu-initial-open-retained-drawn.json"];
        TestAssert.Equal("Menu Preferences", retained.Title);
        TestAssert.Equal(8, retained.SelectedIndex);
        TestAssert.Equal("Large Cursor", retained.Rows[retained.SelectedIndex].Label);
        TestAssert.Equal(2UL, retained.MenuOpening!.Revision);
        foreach (string name in new[] { "empty-rebuild", "empty-drawn", "single-drawn" })
        {
            TestAssert.True(fixtures["menu-initial-open-" + name + ".json"].MenuOpening is null,
                "Zero or one ordinary row cannot fabricate a row fade");
        }
        var dynamic = fixtures["menu-initial-open-dynamic-drawn.json"];
        var changed = fixtures["menu-initial-open-dynamic-changed.json"];
        TestAssert.Equal(3, dynamic.Rows.Count);
        TestAssert.Equal(1, changed.Rows.Count);
        TestAssert.True(changed.MenuOpening is null && changed.MenuOpenTransition?.Phase == "cooldown",
            "Changed roster structure cancels only presentation opacity");
        var hidden = fixtures["menu-initial-open-focus-hidden.json"];
        TestAssert.True(!hidden.IsOpen && hidden.MenuOpenTransition is null && hidden.MenuOpening is null,
            "Focus-hidden profiles retain no invalid active drawing metadata");
        foreach (string name in new[] { "main.json", "scroll-motion-root-start.json", "scroll-motion-root-reopened.json" })
        {
            TestAssert.True(fixtures[name].MenuOpenTransition is null && fixtures[name].MenuOpening is null,
                "Legacy static profiles finish every real opening before export");
        }
    }
}
