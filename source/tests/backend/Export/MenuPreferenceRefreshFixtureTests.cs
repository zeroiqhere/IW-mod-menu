using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class MenuPreferenceRefreshFixtureTests
{
    public static void TwentyActualProfilesKeepTheirCommittedAndDisplayedFlagsDistinctUntilOneRefreshAndOneUnlock()
    {
        var fixtures = MenuFixtureExporter.Build(); TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, fixtures.Count);
        string[] prefixes = { "spotlight", "colored", "large", "quick-exit" };
        string[] stages = { "pending", "pre-refresh", "cooldown", "pre-unlock", "unlocked" };
        string?[] phases = { "pending", "pending", "cooldown", "cooldown", null };
        int[] rows = { 6, 7, 8, 4 };
        // Each series uses one production-controller visit with an owned monotonic clock
        for (int flag = 0; flag < prefixes.Length; flag++)
        {
            var committed = MenuPreferenceRefreshTests.Changed(MenuAppearance.Default, MenuPreferenceRefreshTests.Flags[flag].Preference);
            var initial = fixtures["preferences-refresh-" + prefixes[flag] + "-pending.json"];
            for (int stage = 0; stage < stages.Length; stage++)
            {
                var state = fixtures["preferences-refresh-" + prefixes[flag] + "-" + stages[stage] + ".json"];
                TestAssert.Equal(phases[stage], state.MenuPreferenceRefresh?.Phase); TestAssert.Equal("Menu Preferences", state.Title);
                TestAssert.True(state.IsOpen && !state.IsRootMenu && state.Rows.Count == 12 && state.Rows.Count(row => !row.Enabled) == 2, "The paused panel retains its actual available local options");
                TestAssert.Equal(rows[flag], state.SelectedIndex); TestAssert.Equal(initial.NavigationRevision, state.NavigationRevision);
                TestAssert.Equal(initial.MenuRevision + (stage >= 2 ? 1UL : 0UL), state.MenuRevision);
                var expected = stage < 2 ? MenuAppearance.Default with { QuickExit = committed.QuickExit } : committed;
                TestAssert.Equal(expected, state.Appearance);
                string oldValue = flag < 2 ? "ON" : "OFF";
                TestAssert.Equal(stage < 2 ? oldValue : oldValue == "ON" ? "OFF" : "ON", state.Rows[rows[flag]].Value);
                TestAssert.Equal("PREVIEW", state.Context); TestAssert.Equal("#3998FE", state.Accent);
                TestAssert.True((stage == 4) != state.Notice.Contains("Updating menu; controls paused", StringComparison.Ordinal), "Only actual paused phases advertise their cooldown");
            }
        }
        foreach (var old in fixtures.Where(item => !item.Key.StartsWith("open-controls-", StringComparison.Ordinal) && !item.Key.StartsWith("quick-controls-", StringComparison.Ordinal) && !item.Key.StartsWith("preferences-refresh-", StringComparison.Ordinal) && !item.Key.StartsWith("stealth-", StringComparison.Ordinal)))
            TestAssert.True(old.Value.MenuPreferenceRefresh is null, "The original fifty-two producer profiles preserve their completed state");
    }
}
