using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class OpeningControlsFixtureTests {
    public static void TwentyTwoActualProfilesSeparateAppliedPolicyCachedRowsAndSelectorDraftsWithoutGameWork() {
        var all = MenuFixtureExporter.Build();
        TestAssert.Equal(MenuFixtureExporter.TotalFixtureCount, all.Count);
        var initial = all["open-controls-default.json"];
        TestAssert.True(OpeningControlsRig.Actions(initial).SequenceEqual(new[] { 5, 4 }), "The producer starts from the verified source default");
        var pending = all["open-controls-change-pending.json"];
        TestAssert.True(OpeningControlsRig.Actions(pending).SequenceEqual(new[] { 8, 4 }), "The producer commits the actual input policy immediately");
        TestAssert.Equal("OFF", pending.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("ON", all["open-controls-change-cooldown.json"].Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("pending", all["open-controls-change-pre-refresh.json"].MenuPreferenceRefresh?.Phase);
        TestAssert.Equal("cooldown", all["open-controls-change-pre-unlock.json"].MenuPreferenceRefresh?.Phase);
        TestAssert.True(all["open-controls-change-unlocked.json"].MenuPreferenceRefresh is null, "Owner metadata ends at430");
        TestAssert.True(!all["open-controls-hidden.json"].IsOpen && !all["open-controls-closed.json"].IsOpen, "Closed and projected profiles retain only a global policy");
        foreach (var item in all.Where(item => item.Key.StartsWith("open-controls-", StringComparison.Ordinal))) {
            TestAssert.True(item.Value.MainOpeningControls is not null, "Each configured offline profile owns its typed descriptor");
            TestAssert.True(!item.Value.Rows.Any(row => row.IsSpacer && row == item.Value.Rows[item.Value.SelectedIndex]), "No profile can select a source spacer");
        }
    }
}
