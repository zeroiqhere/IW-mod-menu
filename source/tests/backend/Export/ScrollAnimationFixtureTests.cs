namespace Backend.Tests;

internal static class ScrollAnimationFixtureTests {
    public static void ActualNumericProfilesDistinguishDraftCommitAndBothTrueMillisecondBounds() {
        var fixtures = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        var draft = fixtures["preferences-scroll-draft.json"];
        var committed = fixtures["preferences-scroll-committed.json"];
        var minimum = fixtures["preferences-scroll-min.json"];
        var maximum = fixtures["preferences-scroll-max.json"];
        TestAssert.Equal(120, draft.Appearance?.ScrollAnimationMilliseconds);
        TestAssert.Equal("130 ms", draft.Rows[draft.SelectedIndex].Value);
        TestAssert.True(draft.Notice.Contains("Candidate: 130 ms; Applied: 120 ms", StringComparison.Ordinal), "Draft help must report the actual committed duration");
        foreach (var pair in new[] { (committed, 130), (minimum, 100), (maximum, 250) }) {
            TestAssert.Equal(pair.Item2, pair.Item1.Appearance?.ScrollAnimationMilliseconds);
            TestAssert.Equal(pair.Item2 + " ms", pair.Item1.Rows[pair.Item1.SelectedIndex].Value);
            TestAssert.Equal(draft.MenuRevision, pair.Item1.MenuRevision);
            TestAssert.Equal(draft.NavigationRevision, pair.Item1.NavigationRevision);
            TestAssert.Equal(12, pair.Item1.Rows.Count);
        }
    }

    public static void ActualCursorAndWindowProfilesRetainMenuContextUntilBackCloseOrReopen() {
        var fixtures = MenuFixtureExporter.Build();
        var root = fixtures["scroll-motion-root-start.json"];
        var down = fixtures["scroll-motion-root-down.json"];
        var reopened = fixtures["scroll-motion-root-reopened.json"];
        TestAssert.Equal(0, root.SelectedIndex); TestAssert.Equal(1, down.SelectedIndex); TestAssert.Equal(1, reopened.SelectedIndex);
        TestAssert.Equal(root.MenuRevision, down.MenuRevision);
        TestAssert.True(down.NavigationRevision > root.NavigationRevision && reopened.MenuRevision > down.MenuRevision,
            "Cursor movement keeps the animation context while reopen starts a new one");
        var before = fixtures["scroll-motion-colors-start.json"];
        var window = fixtures["scroll-motion-colors-window.json"];
        var parent = fixtures["scroll-motion-customization.json"];
        TestAssert.Equal(4, before.SelectedIndex); TestAssert.Equal(5, window.SelectedIndex);
        TestAssert.Equal(before.MenuRevision, window.MenuRevision);
        TestAssert.True(window.NavigationRevision > before.NavigationRevision && parent.MenuRevision > window.MenuRevision,
            "A one-row window shift shares context while Back rebuilds the parent");
        TestAssert.Equal("Main Design Color", before.Title); TestAssert.Equal("Menu Customization", parent.Title);
        foreach (var pair in fixtures) {
            var fixture = pair.Value;
            bool roster = pair.Key is "menu-initial-open-single-drawn.json" or "menu-initial-open-dynamic-drawn.json" or "menu-initial-open-dynamic-changed.json";
            TestAssert.True(fixture.MenuRevision.HasValue, "Every actual producer profile carries its current presentation context");
            TestAssert.Equal(!ScopedOpeningFixtureProfiles.ClosedProfiles.Contains(pair.Key) && pair.Key is not "menu-initial-open-focus-hidden.json" and not "open-controls-hidden.json" and not "open-controls-closed.json" and not "quick-controls-hidden.json" and not "quick-controls-closed.json", fixture.IsOpen);
            TestAssert.Equal(roster ? "DISCONNECTED" : pair.Key == "opening-pending.json" ? "PREVIEW · WORKING" : "PREVIEW", fixture.Context);
        }
    }
}
