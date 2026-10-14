namespace Backend.Tests;

internal static class ScopedOpeningFixtureTests {
    public static void ActualScopedOpeningProfilesPublishTheCorrectFamiliesWaitsEpochsAndInertFeatures() {
        var all = MenuFixtureExporter.Build();
        var profiles = all.Where(pair => pair.Key.StartsWith("opening-", StringComparison.Ordinal)).ToArray();
        TestAssert.Equal(ScopedOpeningFixtureProfiles.ProfileCount, profiles.Length);
        foreach (var (name, frame) in profiles) {
            TestAssert.Equal(!ScopedOpeningFixtureProfiles.ClosedProfiles.Contains(name), frame.IsOpen);
            TestAssert.True((name == "opening-cleanup-retry.json" ? frame.LocalMenuEligibility is null : frame.LocalMenuEligibility is not null)
                && frame.MenuSurface is "main" or "quick",
                "The actual producer must publish its family and owned eligibility");
            TestAssert.True(frame.Rows.All(row => !row.Enabled || row.Value != "ON"), "Offline profiles cannot expose an enabled game toggle");
            TestAssert.True(!System.Text.Encoding.UTF8.GetString(frame.Serialize()).Contains(ScopedOpeningFixture.Actor.Guid, StringComparison.Ordinal),
                "Public packets must not contain private account identity");
        }
        TestAssert.True(all["opening-pending.json"].LocalMenuEligibility!.Blocked, "Queued proof must block another opening");
        TestAssert.Equal("quick", all["opening-quick-drawn.json"].MenuSurface!);
        TestAssert.Equal(14, all["opening-quick-drawn.json"].Rows.Count);
        TestAssert.Equal(0, all["opening-quick-drawn.json"].MenuOpening!.ElapsedMilliseconds);
        TestAssert.Equal(549, all["opening-quick-wait.json"].MenuOpening!.ElapsedMilliseconds);
        TestAssert.True(all["opening-quick-ready.json"].MenuOpenTransition is null, "Quick controls must resume at 550 ms");
        TestAssert.True(all["opening-quick-scrolled.json"].MenuOpening is null, "Accepted navigation retires the fade");
        TestAssert.Equal(3, all["opening-quick-dead.json"].Rows.Count);
        TestAssert.Equal(11UL, all["opening-quick-dead.json"].LocalMenuEligibility!.Revision);
        TestAssert.Equal("rebuild", all["opening-main-rebuild.json"].MenuOpenTransition!.Phase);
        TestAssert.True(all["opening-main-dead-wait.json"].IsOpen && !all["opening-main-dead-closed.json"].IsOpen,
            "Main death close must follow its ordinary monitor wait");
    }
}
