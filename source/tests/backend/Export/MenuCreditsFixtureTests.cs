using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuCreditsFixtureTests
{
    public static void ActualCreditsProfilesExposeBoundedElapsedMotionAndTheSourceParentUnlockWithoutGameFeatures()
    {
        var fixtures = MenuFixtureExporter.Build(); TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        string[] names = { "menu-credits-enter.json", "menu-credits-fade-in.json", "menu-credits-overlap.json", "menu-credits-late.json" };
        int[] elapsed = { 0, 500, 1000, 39999 }; var first = fixtures[names[0]];
        // Every profile comes from the same real controller visit and owned clock
        for (int index = 0; index < names.Length; index++)
        {
            var state = fixtures[names[index]];
            TestAssert.True(state.IsOpen && !state.IsRootMenu && state.Rows.Count == 0 && state.SelectedIndex == 0 && state.MenuUnlock is null,
                "Real credits profiles must contain no fabricated ordinary options");
            TestAssert.Equal("Menu Customization", state.Title); TestAssert.Equal("PREVIEW", state.Context);
            TestAssert.Equal(elapsed[index], state.MenuCredits?.ElapsedMilliseconds);
            TestAssert.Equal(first.NavigationRevision, state.NavigationRevision); TestAssert.Equal(first.MenuRevision, state.MenuRevision);
            TestAssert.Equal(MenuAppearance.Default, state.Appearance);
        }
        string[] parents = { "menu-credits-unlock-rebuild.json", "menu-credits-unlock-pause.json", "customization-after-credits.json" };
        string?[] phases = { "rebuild", "pause", null };
        for (int index = 0; index < parents.Length; index++)
        {
            var state = fixtures[parents[index]];
            TestAssert.True(state.MenuCredits is null && state.IsOpen && state.Rows.Count == 7 && state.SelectedIndex == 0,
                "Completion must return to its actual first customization row");
            TestAssert.Equal("Menu Credits", state.Rows[0].Label); TestAssert.Equal(phases[index], state.MenuUnlock);
            TestAssert.True(state.NavigationRevision > first.NavigationRevision && state.MenuRevision > first.MenuRevision,
                "The credits return must retire its presentation and input context");
            TestAssert.Equal(fixtures[parents[0]].NavigationRevision, state.NavigationRevision);
            TestAssert.Equal(fixtures[parents[0]].MenuRevision, state.MenuRevision);
        }
        foreach (var original in fixtures.Where(pair => !names.Contains(pair.Key) && !parents.Contains(pair.Key)))
            TestAssert.True(original.Value.MenuCredits is null, "The previous forty-five profiles cannot gain active credits metadata");
    }
}
