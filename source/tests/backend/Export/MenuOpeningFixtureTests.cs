using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuOpeningFixtureTests {
    public static void ActualOpeningProfilesExposeObservedDrawProgressIndependentReadinessAndScrollHandoff() {
        var fixtures = MenuFixtureExporter.Build(); TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        var first = fixtures["menu-opening-start.json"]; TestAssert.Equal(0, first.MenuOpening!.ElapsedMilliseconds);
        foreach (var pair in new[] { ("progress", 100), ("input-ready", 200), ("last", 899), ("draft", 200), ("same", 200) }) {
            var state = fixtures["menu-opening-" + pair.Item1 + ".json"]; TestAssert.Equal(pair.Item2, state.MenuOpening!.ElapsedMilliseconds);
            TestAssert.Equal(first.MenuOpening.Revision, state.MenuOpening.Revision); TestAssert.Equal(first.MenuRevision, state.MenuRevision); TestAssert.Equal(first.NavigationRevision, state.NavigationRevision);
            TestAssert.Equal("Classic", state.Appearance!.Design); TestAssert.True(state.IsOpen && state.Context == "PREVIEW" && state.Rows.Count == 12, "Opening fixtures use actual disconnected controller rows");
        }
        TestAssert.True(fixtures["menu-opening-input-ready.json"].MenuDesignTransition is null, "Drawing cannot delay ready controls");
        TestAssert.Equal("Native", fixtures["menu-opening-draft.json"].Rows[0].Value); TestAssert.Equal("cooldown", fixtures["menu-opening-same.json"].MenuDesignTransition?.Phase);
        var scroll = fixtures["menu-opening-scroll.json"]; TestAssert.True(scroll.MenuOpening is null, "Accepted scrolling supersedes opening");
        TestAssert.Equal(first.MenuRevision, scroll.MenuRevision); TestAssert.True(scroll.NavigationRevision > first.NavigationRevision && scroll.SelectedIndex == 1, "Scroll capture retains context with a new cursor epoch");
        var late = fixtures["menu-opening-late.json"]; TestAssert.Equal(0, late.MenuOpening!.ElapsedMilliseconds); TestAssert.True(late.MenuDesignTransition is null, "A late actual draw starts at zero after control cooldown already elapsed");
        var eight = fixtures["menu-opening-eight-start.json"]; TestAssert.True(eight.Appearance!.OptionCounter && !eight.Appearance.ColoredCursor && eight.Appearance.Design == "Native", "The actual normalized appearance selects eight visible rows");
        TestAssert.Equal(700, MenuOptionCapacity.OpeningDurationMilliseconds(eight.Rows.Count, eight.Appearance)); TestAssert.Equal(699, fixtures["menu-opening-eight-last.json"].MenuOpening!.ElapsedMilliseconds);
        foreach (string name in new[] { "menu-opening-complete.json", "menu-opening-eight-complete.json" }) TestAssert.True(fixtures[name].MenuOpening is null, "Completed profiles cannot retain an active event");
    }
}
