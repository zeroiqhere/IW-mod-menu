using Apparition.Backend.Ipc;

namespace Backend.Tests;

// Named literal boundaries assert real producer transitions, not manufactured native predecessors
internal static class MenuUseFollowupFixtureTests {
    public static void ActualFollowupGraphRetiresUseAndOwnsChoiceBackAndQuickExitWaits() {
        var profiles = MenuUseFollowupFixtureExporter.Build(); TestAssert.Equal(51, profiles.Count);
        TestAssert.Equal(51, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        StateDocument Find(string name) => profiles.Single(profile => profile.Name == name + ".json").State;
        foreach (string graph in new[] { "plain-next", "plain-previous", "plain-back", "quick-exit-child" }) {
            var before = Find(graph + "-before0"); var selected = Find(graph + "-use0");
            TestAssert.True(before.MenuUseContinuation is null, "A pre-Select frame cannot invent a predecessor");
            TestAssert.Equal(before.LastSequence + 1, selected.LastSequence);
            TestAssert.Equal(selected.LastSequence, selected.MenuUseContinuation!.Sequence);
            TestAssert.Equal("plain", selected.MenuOptionCallback!.Kind);
            var cooldown = Find(graph + "-use249"); TestAssert.Equal(selected.MenuUseContinuation, cooldown.MenuUseContinuation);
            TestAssert.True(cooldown.MenuOptionCallback is not null, "The callback remains waiting through249ms");
            var ready = Find(graph + "-ready250"); TestAssert.True(ready.MenuOptionCallback is null, "The actual callback normally expires at250ms");
            TestAssert.Equal(selected.MenuUseContinuation, ready.MenuUseContinuation);
            TestAssert.Equal(before.NavigationRevision, ready.NavigationRevision);
            TestAssert.Equal(before.LocalMenuEligibility!.Revision, ready.LocalMenuEligibility!.Revision);
        }
        foreach (string graph in new[] { "plain-next", "plain-previous" }) {
            var ready = Find(graph + "-ready250"); var choice = Find(graph + "-choice250");
            TestAssert.Equal(ready.LastSequence + 1, choice.LastSequence); TestAssert.True(choice.MenuUseContinuation is null,
                "A newly admitted source choice retires the older ready Use invocation");
            TestAssert.Equal("choice", choice.MenuMonitorWait!.Kind); TestAssert.Equal(ready.NavigationRevision, choice.NavigationRevision);
            TestAssert.Equal(ready.MenuRevision, choice.MenuRevision); TestAssert.Equal<bool?>(false, choice.LocalMenuEligibility!.ControlsDisabled);
            TestAssert.True(ready.Rows[ready.SelectedIndex].Value != choice.Rows[choice.SelectedIndex].Value,
                "The actual slider draft changes independently from its committed appearance");
            TestAssert.Equal(ready.Appearance!.BooleanDisplay, choice.Appearance!.BooleanDisplay);
            TestAssert.Equal(choice.MenuMonitorWait, Find(graph + "-choice429").MenuMonitorWait);
            TestAssert.True(Find(graph + "-choice430").MenuMonitorWait is null, "Choice wait ends exactly180ms after its own admission");
        }
        var repeat = Find("plain-next-repeat430"); TestAssert.Equal(Find("plain-next-choice250").MenuMonitorWait!.Revision + 1,
            repeat.MenuMonitorWait!.Revision); TestAssert.True(Find("plain-next-repeat610").MenuMonitorWait is null,
            "A second held direction remains a distinct180ms branch");
        foreach (var pair in new[] { ("plain-back-child250", "plain-back-child499", "plain-back-child500"),
            ("plain-back-main500", "plain-back-main749", "plain-back-main750"),
            ("plain-back-root750", "plain-back-root999", "plain-back-root1000"),
            ("quick-exit-child-close340", "quick-exit-child-close589", "quick-exit-child-close590"),
            ("quick-exit-root-back0", "quick-exit-root-back249", "quick-exit-root-back250"),
            ("boolean-back-back430", "boolean-back-back679", "boolean-back-back680") }) {
            var first = Find(pair.Item1); TestAssert.True(first.MenuUseContinuation is null && first.MenuOptionCallback is null,
                "The lower Back branch cannot retain a ready Select receipt: " + pair.Item1);
            TestAssert.Equal("back", first.MenuMonitorWait!.Kind);
            TestAssert.Equal(first.MenuMonitorWait, Find(pair.Item2).MenuMonitorWait);
            TestAssert.True(Find(pair.Item3).MenuMonitorWait is null, "Each final Back wait expires after250ms: " + pair.Item3);
        }
        TestAssert.Equal("Menu Customization", Find("plain-back-child250").Title);
        TestAssert.True(Find("plain-back-main500").IsRootMenu && Find("plain-back-main500").IsOpen,
            "The second ordinary Back reaches the actual Main root");
        TestAssert.True(!Find("plain-back-root750").IsOpen && !Find("quick-exit-root-back0").IsOpen,
            "Root Back closes instead of inventing a parent");
        foreach (string name in new[] { "quick-exit-child-ready250", "quick-exit-child-ready339", "quick-exit-child-ready340" }) {
            TestAssert.True(Find(name).IsOpen && Find(name).MenuUseContinuation is not null && Find(name).Appearance!.QuickExit,
                "The source child stays open before the actual scoped hold-close command");
        }
        TestAssert.True(!Find("quick-exit-child-close340").IsOpen, "Actual scoped Close closes the child without returning through parents");
        TestAssert.Equal("producer-only-root-shape", profiles.Single(profile => profile.Name == "quick-exit-root-before0.json").Purpose);
        var boolean = Find("boolean-back-use0"); TestAssert.Equal("boolean", boolean.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", Find("boolean-back-use179").MenuOptionCallback!.Phase);
        TestAssert.Equal("cooldown", Find("boolean-back-use180").MenuOptionCallback!.Phase);
        TestAssert.True(Find("boolean-back-use429").MenuOptionCallback is not null && Find("boolean-back-ready430").MenuOptionCallback is null,
            "The boolean predecessor retains its own180/430 source boundaries");
        TestAssert.Equal(boolean.MenuUseContinuation, Find("boolean-back-ready430").MenuUseContinuation);
        foreach (var profile in profiles) profile.State.Serialize();
    }

    public static void ActualProgramExportsTheSeparateFollowupGraphAndRejectsOverwrite() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-use-followup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-use-followup-fixtures", destination }));
            var expected = MenuUseFollowupFixtureExporter.Build(); TestAssert.Equal(51, Directory.GetFiles(destination).Length);
            foreach (var profile in expected) TestAssert.True(File.ReadAllBytes(Path.Combine(destination, profile.Name)).SequenceEqual(profile.State.Serialize()),
                "Actual Program preserves each source packet: " + profile.Name);
            TestAssert.Equal(1, Program.Main(new[] { "--export-use-followup-fixtures", destination }));
            TestAssert.Equal(51, Directory.GetFiles(destination).Length);
        } finally { Directory.Delete(parent, true); }
    }
}
