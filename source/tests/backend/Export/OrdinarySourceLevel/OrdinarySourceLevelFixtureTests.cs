using System.Security.Cryptography;
using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The source-level packet graph keeps input intent separate from controller effects
internal static class OrdinarySourceLevelFixtureTests {
    public static void ActualLivingQuickUsePublishesOwnedGodmodeEffectThroughReady430() {
        var profiles = OrdinarySourceLevelFixtureProfiles.Build();
        TestAssert.Equal(11, profiles.Count);
        TestAssert.Equal(profiles.Count, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        OrdinarySourceLevelFixtureProfile Profile(string name) => profiles.Single(profile => profile.Name == name + ".json");
        StateDocument Frame(string name) => Profile(name).State;

        TestAssert.True(Frame("quick-drawn0").IsOpen && Frame("quick-drawn0").Rows.Count > 0,
            "The actual Quick HUD is visible as soon as its opening command completes");
        TestAssert.True(Frame("quick-wait549").MenuOpenTransition is not null,
            "The actual Quick input wait remains active through549ms");
        var ready = Profile("quick-ready550");
        var initialRow = ready.State.Rows[ready.State.SelectedIndex];
        TestAssert.Equal("quick", ready.State.MenuSurface);
        TestAssert.Equal(550L, ready.RelativeMilliseconds);
        TestAssert.True(ready.State.MenuOpenTransition is null, "The actual Quick input wait ends at550ms");
        TestAssert.Equal("God Mode", initialRow.Label);
        TestAssert.Equal("OFF", initialRow.Value);
        TestAssert.True(initialRow.Enabled, "The actual living local target enables its existing Quick God Mode action");
        TestAssert.Equal(OverlayInput.OpenQuick, ready.OriginalCommand!.Input);
        var openCommand = ready.OriginalCommand!;
        var openCapture = ready.Capture!;
        TestAssert.Equal(1UL, openCommand.Sequence);
        TestAssert.Equal(openCommand.Sequence, ready.State.LastSequence);
        var beforeOpen = Profile("quick-before0");
        TestAssert.Equal(beforeOpen.State.LastSequence + 1, openCommand.Sequence);
        TestAssert.Equal(beforeOpen.State.NavigationRevision, openCapture.NavigationRevision);
        TestAssert.Equal(beforeOpen.State.OpeningPolicies!.Revision, openCapture.OpeningControlsRevision);
        TestAssert.Equal(beforeOpen.State.LocalMenuEligibility!.Revision, openCapture.AdmissionRevision);
        TestAssert.Equal(beforeOpen.State.MenuRevision!.Value, openCapture.MenuRevision);
        TestAssert.Equal(openCapture.NavigationRevision, openCommand.NavigationRevision!.Value);
        TestAssert.Equal(openCapture.OpeningControlsRevision, openCommand.OpeningControlsRevision!.Value);
        TestAssert.Equal(openCapture.AdmissionRevision, openCommand.LocalAdmissionRevision!.Value);
        TestAssert.True(openCommand.MenuRevision is null, "Quick opening does not carry a mechanical visit revision");

        var accepted = Profile("quick-use-command550");
        // An accepted Select advances sequence while its row effect is still unknown
        TestAssert.Equal(OverlayInput.Select, accepted.OriginalCommand!.Input);
        var useCommand = accepted.OriginalCommand!;
        var useCapture = accepted.Capture!;
        TestAssert.Equal(2UL, useCommand.Sequence);
        TestAssert.Equal(useCapture.LastSequence + 1, useCommand.Sequence);
        TestAssert.Equal(accepted.State.LastSequence, useCommand.Sequence);
        TestAssert.Equal(ready.State.NavigationRevision, useCapture.NavigationRevision);
        TestAssert.Equal(ready.State.MenuRevision!.Value, useCapture.MenuRevision);
        TestAssert.Equal(ready.State.LocalMenuEligibility!.Revision, useCapture.AdmissionRevision);
        TestAssert.Equal(useCapture.NavigationRevision, useCommand.NavigationRevision!.Value);
        TestAssert.Equal(useCapture.OpeningControlsRevision, useCommand.OpeningControlsRevision!.Value);
        TestAssert.True(useCommand.LocalAdmissionRevision is null && useCommand.MenuRevision is null,
            "Select carries its observed opening policy while the local actor scope stays in the manifest");
        TestAssert.Equal("UNKNOWN", accepted.State.Rows[accepted.State.SelectedIndex].Value);
        TestAssert.Equal(0, accepted.SessionActions.Count);

        var operationPending = Profile("quick-use-operation-pending550");
        TestAssert.Equal("UNKNOWN", operationPending.State.Rows[operationPending.State.SelectedIndex].Value);
        TestAssert.Equal("pending", operationPending.State.MenuOptionCallback!.Phase);
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, operationPending.SessionActions.Single());

        var effect = Profile("quick-use-effect550");
        // The held session completion is the point where the displayed BOOL changes
        TestAssert.Equal("ON", effect.State.Rows[effect.State.SelectedIndex].Value);
        TestAssert.Equal("boolean", effect.State.MenuOptionCallback!.Kind);
        TestAssert.Equal("pending", effect.State.MenuOptionCallback.Phase);
        TestAssert.Equal(useCommand.Sequence, effect.State.MenuUseContinuation!.Sequence);
        TestAssert.Equal(MenuActionKind.TogglePersonalGodmode, effect.SessionActions.Single());

        var refresh = Frame("quick-use-refresh730");
        TestAssert.Equal("ON", refresh.Rows[refresh.SelectedIndex].Value);
        TestAssert.Equal("cooldown", refresh.MenuOptionCallback!.Phase);
        TestAssert.True(refresh.MenuRevision > effect.State.MenuRevision,
            "The owned 180 ms redraw preserves the state returned by the local session action");
        TestAssert.Equal(effect.State.MenuUseContinuation, refresh.MenuUseContinuation);
        var beforeReady = Frame("quick-use-before-ready979");
        TestAssert.Equal("ON", beforeReady.Rows[beforeReady.SelectedIndex].Value);
        TestAssert.Equal("cooldown", beforeReady.MenuOptionCallback!.Phase);
        var complete = Frame("quick-use-ready980");
        TestAssert.Equal("ON", complete.Rows[complete.SelectedIndex].Value);
        TestAssert.True(complete.MenuOptionCallback is null, "The ordinary BOOL caller wait ends after 430 ms");
        TestAssert.Equal(effect.State.MenuUseContinuation, complete.MenuUseContinuation);
        TestAssert.Equal(980L, Profile("quick-use-ready980").RelativeMilliseconds);
    }

    public static void ActualProgramExportsCommandCaptureManifestAndFreshPackets() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-source-level-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(1, Program.Main(new[] { "--export-ordinary-source-level-fixtures" }));
            string relative = "source-level-relative-" + Guid.NewGuid().ToString("N");
            TestAssert.Equal(1, Program.Main(new[] { "--export-ordinary-source-level-fixtures", relative }));
            TestAssert.True(!Directory.Exists(relative) && !File.Exists(relative), "Invalid relative output cannot create a local directory");
            TestAssert.Equal(0, Program.Main(new[] { "--export-ordinary-source-level-fixtures", destination }));
            var expected = OrdinarySourceLevelFixtureProfiles.Build();
            TestAssert.Equal(expected.Count + 1, Directory.GetFiles(destination).Length);
            foreach (var profile in expected) {
                var output = File.ReadAllBytes(Path.Combine(destination, profile.Name));
                TestAssert.True(output.SequenceEqual(profile.State.Serialize()), "The exporter preserves the actual packet bytes: " + profile.Name);
            }
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(destination, "source-level-manifest.json")));
            TestAssert.Equal("living-quick-use", manifest.RootElement.GetProperty("graph").GetString());
            var commands = manifest.RootElement.GetProperty("commands").EnumerateArray().ToArray();
            TestAssert.Equal(2, commands.Length);
            TestAssert.Equal(1UL, commands[0].GetProperty("sequence").GetUInt64());
            TestAssert.Equal("openQuick", commands[0].GetProperty("input").GetString());
            TestAssert.Equal(2UL, commands[1].GetProperty("sequence").GetUInt64());
            TestAssert.Equal("select", commands[1].GetProperty("input").GetString());
            TestAssert.True(!manifest.RootElement.GetProperty("session").GetProperty("processMemoryWriterUsed").GetBoolean(),
                "The exported result comes from the inert test session only");

            var original = Directory.GetFiles(destination).ToDictionary(path => Path.GetFileName(path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-ordinary-source-level-fixtures", destination }));
            foreach (string path in Directory.GetFiles(destination))
                TestAssert.Equal(original[Path.GetFileName(path)], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        } finally { Directory.Delete(parent, recursive: true); }
    }
}
