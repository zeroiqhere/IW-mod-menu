using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class StealthFixtureTests {
    public static void ActualStealthAndCounterProfilesPreserveCapturedPresentationAndPublishOneNewContext() {
        var fixtures = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        string[] stages = { "rebuild", "pre-rebuild", "cooldown", "pre-unlock", "unlocked" };
        foreach (string prefix in new[] { "counter-on", "counter-off", "stealth-rebuild" }) {
            var first = fixtures[prefix + "-rebuild.json"];
            for (int index = 0; index < stages.Length; index++) {
                var state = fixtures[prefix + "-" + stages[index] + ".json"];
                TestAssert.True(state.Rows.Select(row => row.Label).SequenceEqual(StealthTests.Labels), "Every new profile uses the source inventory");
                TestAssert.Equal(first.SelectedIndex, state.SelectedIndex);
                TestAssert.Equal(first.NavigationRevision, state.NavigationRevision);
                TestAssert.Equal(first.MenuRevision + (index < 2 ? 0UL : 1UL), state.MenuRevision);
                TestAssert.Equal(index < 2 ? "rebuild" : index < 4 ? "cooldown" : null, state.MenuPreferenceRebuild?.Phase);
                TestAssert.True(state.MenuDesignTransition is null && state.MenuOpenTransition is null, "Counter handlers cannot impersonate another transition");
                TestAssert.True(state.Appearance!.IsSupported(), "Old and new packet appearances remain valid");
                if (prefix == "stealth-rebuild") {
                    TestAssert.Equal(index >= 2, state.Appearance.StealthUI);
                    TestAssert.Equal(index < 2, state.Appearance.OptionCounter);
                    TestAssert.Equal(index < 2 ? "Boxes" : "Text", state.Appearance.BooleanDisplay);
                    TestAssert.Equal(10, state.SelectedIndex);
                } else {
                    bool target = prefix == "counter-on";
                    TestAssert.Equal(index < 2 ? !target : target, state.Appearance.OptionCounter);
                    TestAssert.Equal(9, state.SelectedIndex);
                }
            }
        }
        var last = fixtures["stealth-opening-last.json"];
        TestAssert.Equal(399, last.MenuOpening!.ElapsedMilliseconds);
        TestAssert.True(fixtures["stealth-opening-complete.json"].MenuOpening is null, "Five visible source rows finish after400ms");
        TestAssert.Equal("Boxes", fixtures["stealth-boxes-rejected.json"].Rows[1].Value);
        TestAssert.Equal("Text", fixtures["stealth-boxes-rejected.json"].Appearance!.BooleanDisplay);
        TestAssert.Equal("Text Color", fixtures["stealth-text-color.json"].Appearance!.BooleanDisplay);
        TestAssert.True(!fixtures["stealth-disabled-unlocked.json"].Appearance!.StealthUI, "Disabling retains the forced Text without restoring Stealth");
        var pending = fixtures["stealth-retained-opening-pending.json"];
        var prior = fixtures["stealth-retained-opening-pre-refresh.json"];
        var applied = fixtures["stealth-retained-opening-cooldown.json"];
        TestAssert.Equal(550, pending.MenuOpening!.ElapsedMilliseconds);
        TestAssert.Equal(729, prior.MenuOpening!.ElapsedMilliseconds);
        TestAssert.Equal(pending.MenuOpening.Revision, prior.MenuOpening.Revision);
        TestAssert.True(applied.MenuOpening is null && applied.Appearance!.StealthUI, "The actual180ms redraw owns opacity cancellation");
        TestAssert.Equal(pending.MenuRevision + 1, applied.MenuRevision);
    }
}
