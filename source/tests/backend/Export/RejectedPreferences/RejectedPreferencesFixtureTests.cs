using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class RejectedPreferencesFixtureTests {
    public static void ActualRejectedPreferenceProfilesSerializeFiveControllerGraphsAndOneCounterRebuild() {
        var profiles = RejectedPreferencesFixtureProfiles.Build();
        TestAssert.Equal(41, profiles.Count);
        TestAssert.Equal(7, profiles.Count(profile => profile.Command is not null));
        TestAssert.Equal(41, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());

        foreach (var scenario in RejectedPreferencesFixtureProfiles.Scenarios) {
            var before = Profile(scenario.Prefix + "-before");
            TestAssert.Equal(MenuPage.Preferences, before.Page);
            TestAssert.Equal(scenario.SourceIndex, before.SelectedSourceIndex);
            TestAssert.Equal(scenario.RowLabel, before.State.Rows[scenario.SourceIndex].Label);
            TestAssert.Equal(0, before.SessionActionCount);

            var select = Profile(scenario.Prefix + "-select0");
            AssertCapturedSelect(before, select);
            AssertElapsed(scenario.Prefix + "-wait179", 179);
            AssertElapsed(scenario.Prefix + "-redraw180", 180);
            AssertElapsed(scenario.Prefix + "-wait429", 429);
            AssertElapsed(scenario.Prefix + "-ready430", 430);
        }

        AssertElapsed("stealth-counter-second-select430", 430);
        AssertElapsed("stealth-counter-second-wait609", 609);
        AssertElapsed("stealth-counter-second-redraw610", 610);
        AssertElapsed("stealth-counter-second-wait859", 859);
        AssertElapsed("stealth-counter-second-ready860", 860);

        var rebuild = Profile("counter-rebuild-before");
        TestAssert.Equal(MenuPage.Preferences, rebuild.Page);
        TestAssert.Equal("Option Counter", rebuild.State.Rows[rebuild.SelectedSourceIndex].Label);
        AssertCapturedSelect(rebuild, Profile("counter-rebuild-select0"));
        AssertElapsed("counter-rebuild-wait49", 49);
        AssertElapsed("counter-rebuild-redraw50", 50);
        AssertElapsed("counter-rebuild-wait249", 249);
        AssertElapsed("counter-rebuild-ready250", 250);

        RejectedPreferencesFixtureProfile Profile(string name) =>
            profiles.Single(profile => profile.Name == name + ".json");

        void AssertElapsed(string name, long milliseconds) {
            var profile = Profile(name);
            TestAssert.Equal(milliseconds, profile.RelativeMilliseconds);
            TestAssert.Equal(0, profile.SessionActionCount);
        }

        static void AssertCapturedSelect(RejectedPreferencesFixtureProfile before,
            RejectedPreferencesFixtureProfile selected) {
            TestAssert.True(selected.Command is not null && selected.Capture is not null,
                "A source Select keeps the command and its pre-command scope together");
            TestAssert.Equal(before.State.LastSequence + 1, selected.Command!.Sequence);
            TestAssert.Equal(before.State.NavigationRevision, selected.Capture!.NavigationRevision);
            TestAssert.Equal(OpeningRevision(before.State), selected.Capture.OpeningControlsRevision);
            TestAssert.Equal(before.State.LastSequence, selected.Capture.LastSequence);
            TestAssert.Equal(before.SelectedSourceIndex, selected.Capture.SelectedSourceIndex);
            TestAssert.Equal(before.State.Rows[before.SelectedSourceIndex].Label, selected.Capture.SelectedSourceLabel);
            TestAssert.Equal(before.State.NavigationRevision, selected.Command.NavigationRevision);
            TestAssert.Equal(OpeningRevision(before.State), selected.Command.OpeningControlsRevision);
            TestAssert.True(selected.Command.LocalAdmissionRevision is null && selected.Command.MenuRevision is null,
                "The source Select carries no admission or presentation scope");
        }

        static ulong OpeningRevision(StateDocument state) => state.OpeningPolicies?.Revision
            ?? state.MainOpeningControls?.Revision
            ?? throw new InvalidOperationException("The actual Preferences frame has no opening policy revision");
    }

    public static void ActualProgramExportsRejectedPreferencePacketsAndManifestWithoutReplacingEvidence() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-rejected-preferences-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        string relative = "rejected-preferences-relative-" + Guid.NewGuid().ToString("N");
        try {
            TestAssert.Equal(1, Program.Main(new[] { "--export-rejected-preferences-fixtures" }));
            TestAssert.Equal(1, Program.Main(new[] { "--export-rejected-preferences-fixtures", relative }));
            TestAssert.True(!Directory.Exists(relative) && !File.Exists(relative), "A relative output cannot create local evidence");
            TestAssert.Equal(0, Program.Main(new[] { "--export-rejected-preferences-fixtures", destination }));

            var profiles = RejectedPreferencesFixtureProfiles.Build();
            TestAssert.Equal(42, Directory.GetFiles(destination).Length);
            foreach (var profile in profiles) {
                byte[] actual = File.ReadAllBytes(Path.Combine(destination, profile.Name));
                TestAssert.True(actual.SequenceEqual(profile.State.Serialize()), "The packet contains the actual controller bytes: " + profile.Name);
            }

            using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(destination, "rejected-preferences-manifest.json")));
            var root = manifest.RootElement;
            TestAssert.Equal("rejected-preferences", root.GetProperty("graph").GetString());
            TestAssert.Equal("Menu/base.gsc", root.GetProperty("source").GetProperty("file").GetString());
            TestAssert.Equal("307e94c1b1d6537ba7a2a71b32a22d563d3ea25c", root.GetProperty("source").GetProperty("commit").GetString());
            TestAssert.True(!root.GetProperty("session").GetProperty("processMemoryWriterUsed").GetBoolean(),
                "The scoped source session has no process memory writer");
            TestAssert.Equal(0, root.GetProperty("session").GetProperty("actionCount").GetInt32());
            TestAssert.Equal(41, root.GetProperty("frames").GetArrayLength());
            var commands = root.GetProperty("commands").EnumerateArray().ToArray();
            TestAssert.Equal(7, commands.Length);
            foreach (var command in commands) {
                TestAssert.Equal("select", command.GetProperty("input").GetString());
                TestAssert.True(command.GetProperty("sequence").GetUInt64() > command.GetProperty("capture").GetProperty("lastSequence").GetUInt64(),
                    "A Select follows the exact controller frame captured before the write");
                TestAssert.Equal(command.GetProperty("capture").GetProperty("navigationRevision").GetUInt64(),
                    command.GetProperty("wire").GetProperty("navigationRevision").GetUInt64());
                TestAssert.Equal(command.GetProperty("capture").GetProperty("openingControlsRevision").GetUInt64(),
                    command.GetProperty("wire").GetProperty("openingControlsRevision").GetUInt64());
                TestAssert.Equal(JsonValueKind.Null, command.GetProperty("wire").GetProperty("localAdmissionRevision").ValueKind);
                TestAssert.Equal(JsonValueKind.Null, command.GetProperty("wire").GetProperty("menuRevision").ValueKind);
                TestAssert.True(File.Exists(Path.Combine(destination, command.GetProperty("capture").GetProperty("file").GetString()!)),
                    "A command capture names its existing pre-command packet");
            }

            string existing = Path.Combine(parent, "existing");
            Directory.CreateDirectory(existing);
            string sentinel = Path.Combine(existing, "keep.txt");
            File.WriteAllText(sentinel, "preserved");
            TestAssert.Equal(1, Program.Main(new[] { "--export-rejected-preferences-fixtures", existing }));
            TestAssert.Equal("preserved", File.ReadAllText(sentinel));
        } finally {
            Directory.Delete(parent, recursive: true);
            if (Directory.Exists(relative)) Directory.Delete(relative, recursive: true);
            if (File.Exists(relative)) File.Delete(relative);
        }
    }
}
