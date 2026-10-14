using Apparition.Backend.Ipc;

namespace Backend.Tests;

// The registered driver exports the same real controller graph consumed by the native readers
internal static class MenuMonitorWaitFixtureTests {
    public static void ActualGraphKeepsChoiceBackFocusOwnerAndClockBoundariesSeparate() {
        var profiles = MenuMonitorWaitFixtureExporter.Build();
        TestAssert.Equal(26, profiles.Count);
        StateDocument Find(string name) => profiles.Single(profile => profile.Name == name + ".json").State;
        foreach (string name in new[] { "choice0", "choice179", "focus-owner0", "focus-return100", "owner-before0", "rollback-before100", "rollback-recovered10", "numeric-wrap0" }) {
            TestAssert.Equal("choice", Find(name).MenuMonitorWait!.Kind);
            TestAssert.Equal<bool?>(false, Find(name).LocalMenuEligibility!.ControlsDisabled);
        }
        foreach (string name in new[] { "back-parent0", "back-parent249", "back-root-closed0", "back-root-closed249", "dead-jump0", "hold-close0" })
            TestAssert.Equal("back", Find(name).MenuMonitorWait!.Kind);
        foreach (string name in new[] { "choice180", "back-parent250", "back-root-closed250", "dead-jump250", "hold-close250", "focus-hidden0", "focus-ready180", "owner-replaced0", "rollback-ready190", "numeric-bound250", "choice-before0", "back-root-before0" })
            TestAssert.True(Find(name).MenuMonitorWait is null, "The actual source boundary must retire its own wait: " + name);
        TestAssert.True(!Find("back-root-closed0").IsOpen && !Find("dead-jump0").IsOpen, "Root and dead Jump waits survive body close");
        TestAssert.Equal("250 ms", Find("numeric-bound250").Rows[Find("numeric-bound250").SelectedIndex].Value);
        TestAssert.Equal("100 ms", Find("numeric-wrap0").Rows[Find("numeric-wrap0").SelectedIndex].Value);
        foreach (var profile in profiles) profile.State.Serialize();
    }

    public static void ActualProgramExportsTheWholeGraphAndNeverOverwritesExistingEvidence() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-monitor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-monitor-wait-fixtures", destination }));
            var expected = MenuMonitorWaitFixtureExporter.Build();
            TestAssert.Equal(26, Directory.GetFiles(destination).Length);
            foreach (var profile in expected) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(destination, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "Actual Program dispatch must preserve each controller packet: " + profile.Name);
            }
            TestAssert.Equal(1, Program.Main(new[] { "--export-monitor-wait-fixtures", destination }));
            TestAssert.Equal(26, Directory.GetFiles(destination).Length);
        } finally { Directory.Delete(parent, true); }
    }
}
