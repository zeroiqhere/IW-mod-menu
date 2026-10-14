using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Offline profiles prove the owner transition without claiming native input or animation
internal static class SpacerScrollFixtureTests {
    public static void TwelveActualProfilesSkipTheSourceSpacerWithOneCursorEpochAndNoContextChange() {
        var profiles = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, profiles.Count);
        foreach (string mode in new[] { "ordinary", "counter", "stealth" }) {
            foreach (string direction in new[] { "down", "up" }) {
                string prefix = "spacer-scroll-" + mode + "-" + direction;
                var before = profiles[prefix + "-before.json"];
                var after = profiles[prefix + "-after.json"];
                TestAssert.Equal(direction == "down" ? 1 : 3, before.SelectedIndex);
                TestAssert.Equal(direction == "down" ? 3 : 1, after.SelectedIndex);
                TestAssert.Equal(before.NavigationRevision + 1, after.NavigationRevision);
                TestAssert.Equal(before.MenuRevision, after.MenuRevision);
                TestAssert.Equal(before.LastSequence + 1, after.LastSequence);
                TestAssert.True(before.Rows[2].IsSpacer && after.Rows[2].IsSpacer,
                    "Both endpoints retain the same typed source blank");
                TestAssert.True(before.Rows[2].Label.Length == 0 && !before.Rows[2].Enabled,
                    "A blank is distinct from an unavailable action");
                TestAssert.True(!before.Rows[before.SelectedIndex].IsSpacer && !after.Rows[after.SelectedIndex].IsSpacer,
                    "Only the final nonspacer receives the owner cursor");
                TestAssert.Equal(before.Appearance, after.Appearance);
                TestAssert.Equal(mode == "counter", after.Appearance!.OptionCounter);
                TestAssert.Equal(mode == "stealth", after.Appearance.StealthUI);
                TestAssert.Equal(mode == "stealth" ? "Text" : "Boxes", after.Appearance.BooleanDisplay);
                TestAssert.True(before.MenuPreferenceRefresh is null && after.MenuPreferenceRefresh is null
                    && before.MenuPreferenceRebuild is null && after.MenuPreferenceRebuild is null,
                    "Fixture movement occurs only after the appearance handler has completed");
                TestAssert.Equal(OpeningControlsRig.Revision(before), OpeningControlsRig.Revision(after));
                TestAssert.True(OpeningControlsRig.Actions(before).SequenceEqual(OpeningControlsRig.Actions(after)),
                    "Scrolling cannot mutate the opening policy");
            }
        }
    }
}
