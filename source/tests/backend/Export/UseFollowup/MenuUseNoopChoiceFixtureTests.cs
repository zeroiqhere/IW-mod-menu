using Apparition.Backend.Ipc;

namespace Backend.Tests;

// Literal boundaries keep nonslider50ms distinct from slider180ms and callback430ms
internal static class MenuUseNoopChoiceFixtureTests {
    public static void ActualNonsliderGraphRetiresReadyUseAndEndsItsOuterWaitAt50Milliseconds() {
        var profiles = MenuUseNoopChoiceFixtureExporter.Build(); TestAssert.Equal(17, profiles.Count);
        TestAssert.Equal(17, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        StateDocument Find(string name) => profiles.Single(profile => profile.Name == name + ".json").State;
        var original = Find("palette-before0"); var selected = Find("palette-use0");
        TestAssert.Equal(original.LastSequence + 1, selected.LastSequence);
        TestAssert.Equal(selected.LastSequence, selected.MenuUseContinuation!.Sequence);
        TestAssert.Equal("boolean", selected.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", Find("palette-use179").MenuOptionCallback!.Phase);
        TestAssert.Equal("cooldown", Find("palette-use180").MenuOptionCallback!.Phase);
        TestAssert.True(Find("palette-use429").MenuOptionCallback is not null, "The actual predecessor owns its full430ms callback");
        var ready = Find("palette-ready430"); TestAssert.True(ready.MenuOptionCallback is null, "Only normal expiry admits the causal ready visit");
        TestAssert.Equal(selected.MenuUseContinuation, ready.MenuUseContinuation);
        foreach (var entry in new[] { (Graph: "palette", Before: "ready430", Start: "noop430", EndBefore: "noop479", End: "noop480"),
            (Graph: "quick-exit", Before: "before0", Start: "noop0", EndBefore: "noop49", End: "noop50"),
            (Graph: "root", Before: "before0", Start: "noop0", EndBefore: "noop49", End: "noop50") }) {
            var before = Find(entry.Graph + "-" + entry.Before); var start = Find(entry.Graph + "-" + entry.Start);
            var endBefore = Find(entry.Graph + "-" + entry.EndBefore); var end = Find(entry.Graph + "-" + entry.End);
            TestAssert.Equal(before.LastSequence + 1, start.LastSequence); TestAssert.Equal(start.LastSequence, end.LastSequence);
            TestAssert.Equal("choiceNoop", start.MenuMonitorWait!.Kind); TestAssert.Equal(start.MenuMonitorWait, endBefore.MenuMonitorWait);
            TestAssert.True(end.MenuMonitorWait is null, "The real branch expires at50ms: " + entry.Graph);
            TestAssert.True(start.MenuUseContinuation is null && end.MenuUseContinuation is null,
                "A later lower branch cannot borrow the ready Select proof: " + entry.Graph);
            TestAssert.True(before.Rows.SequenceEqual(start.Rows) && start.Rows.SequenceEqual(end.Rows),
                "A nonslider has no draft or feature result: " + entry.Graph);
            TestAssert.Equal(before.MenuRevision, end.MenuRevision); TestAssert.Equal(before.NavigationRevision, end.NavigationRevision);
            TestAssert.Equal(before.LocalMenuEligibility!.Revision, end.LocalMenuEligibility!.Revision);
            TestAssert.Equal<bool?>(false, start.LocalMenuEligibility!.ControlsDisabled);
        }
        TestAssert.True(Find("root-before0").IsRootMenu, "The root graph is an actual open Main visit");
        TestAssert.Equal("Basic Scripts", Find("root-before0").Rows[Find("root-before0").SelectedIndex].Label);
        TestAssert.Equal("Quick Exit", Find("quick-exit-before0").Rows[Find("quick-exit-before0").SelectedIndex].Label);
        foreach (var profile in profiles) {
            TestAssert.Equal(profile.Graph == "palette" ? "causal-use-noop" : "producer-only-noop", profile.Purpose);
            profile.State.Serialize();
        }
    }

    public static void ActualProgramExportsTheSeparateNonsliderGraphWithoutOverwritingEvidence() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-use-noop-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-use-noop-choice-fixtures", destination }));
            TestAssert.Equal(17, Directory.GetFiles(destination).Length);
            foreach (var profile in MenuUseNoopChoiceFixtureExporter.Build())
                TestAssert.True(File.ReadAllBytes(Path.Combine(destination, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "Actual Program preserves each separately exported packet: " + profile.Name);
            TestAssert.Equal(1, Program.Main(new[] { "--export-use-noop-choice-fixtures", destination }));
            TestAssert.Equal(17, Directory.GetFiles(destination).Length);
        } finally { Directory.Delete(parent, true); }
    }
}
