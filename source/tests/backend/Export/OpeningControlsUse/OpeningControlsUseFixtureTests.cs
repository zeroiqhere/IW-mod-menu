using System.Security.Cryptography;
using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Source callbacks stay attached to the controller packets that produced them
internal static class OpeningControlsUseFixtureTests {
    private static Dictionary<string, OpeningControlsUseFixtureProfile> Graph() {
        var profiles = OpeningControlsUseFixtureProfiles.Build();
        TestAssert.Equal(36, profiles.Count);
        TestAssert.Equal(9, profiles.Count(profile => profile.Command is not null));
        TestAssert.True(profiles.All(profile => profile.SessionActionCount == 0),
            "Source-menu callbacks must not dispatch a game-session action");
        return profiles.ToDictionary(profile => profile.Name, StringComparer.Ordinal);
    }

    public static void ActualControllerExportsOpeningControlUseAndSelectorGraphsWithCapturedScopes() {
        var graph = Graph();
        StateDocument Frame(string name) => graph[name + ".json"].State;

        var before = Frame("binding-before0");
        var accepted = Frame("binding-select0");
        TestAssert.Equal("Open Controls", before.Title);
        TestAssert.Equal("Use", before.Rows[before.SelectedIndex].Label);
        TestAssert.Equal(before.OpeningPolicies!.Revision + 1, accepted.OpeningPolicies!.Revision);
        TestAssert.True(accepted.OpeningPolicies.Main.Actions.Contains(Apparition.Backend.OpenControls.OpeningAction.Use),
            "A successful source Use must update the applied Main tuple immediately");
        TestAssert.Equal("OFF", accepted.Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("OFF", Frame("binding-wait179").Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("ON", Frame("binding-redraw180").Rows.Single(row => row.Label == "Use").Value);
        TestAssert.Equal("ON", Frame("binding-ready430").Rows.Single(row => row.Label == "Use").Value);

        var duplicate = Frame("binding-second-select430");
        TestAssert.Equal(accepted.OpeningPolicies.Revision, duplicate.OpeningPolicies!.Revision);
        TestAssert.True(duplicate.Notice.Contains("already assigned", StringComparison.OrdinalIgnoreCase),
            "Repeating the successful source choice must report the actual duplicate result");
        TestAssert.Equal("ON", Frame("binding-second-redraw610").Rows.Single(row => row.Label == "Use").Value);
        TestAssert.True(CallbackPhase(Frame("binding-second-select430")) is not null
            && CallbackPhase(Frame("binding-second-ready860")) is null,
            "The repeated Boolean callback must have reached its actual 430 ms deadline");

        var choiceReady = Frame("changed-slot-choice-ready180");
        var slot2 = Frame("changed-slot-select180");
        TestAssert.Equal("2", choiceReady.Rows[1].Value);
        TestAssert.True(slot2.NavigationRevision > choiceReady.NavigationRevision,
            "Applying a changed selector must advance its captured navigation scope");
        TestAssert.Equal("2", slot2.Rows.Single(row => row.Label == "Bind Slot").Value);
        TestAssert.True(slot2.Rows.Any(row => row.Label == "None"),
            "Applying slot 2 must publish the actual source None row");
        TestAssert.True(slot2.MenuRevision > choiceReady.MenuRevision,
            "Applying slot 2 must publish its changed row topology");
        TestAssert.True(CallbackPhase(slot2) is not null && CallbackPhase(Frame("changed-slot-wait249")) is not null
            && CallbackPhase(Frame("changed-slot-ready250")) is null,
            "The changed Bind Slot callback must retain only its plain250 wait");

        var denied = Frame("duplicate-melee-select0");
        TestAssert.Equal(Frame("duplicate-melee-before0").OpeningPolicies!.Revision, denied.OpeningPolicies!.Revision);
        TestAssert.True(denied.Notice.Contains("already assigned", StringComparison.OrdinalIgnoreCase),
            "The real Melee callback must preserve its duplicate rejection");
        TestAssert.True(CallbackPhase(denied) is not null && CallbackPhase(Frame("duplicate-melee-ready430")) is null,
            "The rejected Boolean callback must retain its source redraw and complete wait");

        var quickBefore = Frame("disable-quick-before0");
        var quickChanged = Frame("disable-quick-select0");
        TestAssert.True(!quickBefore.OpeningPolicies!.QuickDisabled && quickChanged.OpeningPolicies!.QuickDisabled,
            "Disable Quick must update the actual typed policy during its callback");
        TestAssert.Equal("OFF", quickChanged.Rows.Single(row => row.Label == "Disable Quick Menu").Value);
        TestAssert.Equal("ON", Frame("disable-quick-redraw180").Rows.Single(row => row.Label == "Disable Quick Menu").Value);
        TestAssert.True(CallbackPhase(quickChanged) is not null && CallbackPhase(Frame("disable-quick-ready430")) is null,
            "Disable Quick must complete the source Boolean wait at430");

        var sameMenuBefore = Frame("menu-selector-before0");
        var sameMenuSelect = Frame("menu-selector-select0");
        TestAssert.Equal(sameMenuBefore.Rows[0].Value, sameMenuSelect.Rows[0].Value);
        TestAssert.Equal(sameMenuBefore.MenuRevision, sameMenuSelect.MenuRevision);
        TestAssert.True(CallbackPhase(sameMenuSelect) is not null
            && CallbackPhase(Frame("menu-selector-wait249")) is not null
            && CallbackPhase(Frame("menu-selector-ready250")) is null,
            "The unchanged Menu selector must own only its plain250 wait");

        var sameSlotBefore = Frame("slot-selector-before0");
        var sameSlotSelect = Frame("slot-selector-select0");
        TestAssert.Equal("1", sameSlotBefore.Rows[1].Value);
        TestAssert.Equal(sameSlotBefore.MenuRevision + 1, sameSlotSelect.MenuRevision);
        TestAssert.True(CallbackPhase(sameSlotSelect) is not null
            && CallbackPhase(Frame("slot-selector-wait249")) is not null
            && CallbackPhase(Frame("slot-selector-ready250")) is null,
            "The unchanged Bind Slot selector must publish immediately and retain its plain250 wait");

        AssertCapturedSelect(graph["binding-select0.json"], graph["binding-before0.json"]);
        AssertCapturedSelect(graph["binding-second-select430.json"], graph["binding-ready430.json"]);
        AssertCapturedSelect(graph["binding-third-select860.json"], graph["binding-second-ready860.json"]);
        AssertCapturedSelect(graph["duplicate-melee-select0.json"], graph["duplicate-melee-before0.json"]);
        AssertCapturedSelect(graph["disable-quick-select0.json"], graph["disable-quick-before0.json"]);
        AssertCapturedSelect(graph["menu-selector-select0.json"], graph["menu-selector-before0.json"]);
        AssertCapturedSelect(graph["slot-selector-select0.json"], graph["slot-selector-before0.json"]);
        AssertCapturedSelect(graph["changed-slot-select180.json"], graph["changed-slot-choice-ready180.json"]);
        var nextChoice = graph["changed-slot-next-choice0.json"];
        TestAssert.True(nextChoice.Command is { Input: OverlayInput.NextChoice, LocalAdmissionRevision: not null, MenuRevision: not null },
            "The mechanical choice command must retain its actual admission and row scope");
        TestAssert.True(nextChoice.CapturedFrame is { } choiceCapture
            && nextChoice.Command!.NavigationRevision == choiceCapture.NavigationRevision
            && nextChoice.Command.OpeningControlsRevision == choiceCapture.OpeningPolicies?.Revision,
            "The choice command must use the frame it actually observed");

        var manifest = JsonDocument.Parse(OpeningControlsUseFixtureExporter.SerializeManifest(graph.Values.ToArray()));
        var source = manifest.RootElement.GetProperty("source");
        TestAssert.Equal("307e94c1b1d6537ba7a2a71b32a22d563d3ea25c", source.GetProperty("commit").GetString());
        TestAssert.Equal("5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11", source.GetProperty("baseSha256").GetString());
        TestAssert.Equal("29767ad03de621b1c1491c46ec769dd352b05654f73452e8034cf320c497f7da", source.GetProperty("menuCustomizationSha256").GetString());
        TestAssert.Equal(9, manifest.RootElement.GetProperty("commands").GetArrayLength());
        TestAssert.Equal(36, manifest.RootElement.GetProperty("frames").GetArrayLength());
    }

    public static void ActualProgramRejectsExistingOutputWithoutReplacingPackets() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-open-controls-use-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string output = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-opening-controls-use-fixtures", output }));
            var expected = OpeningControlsUseFixtureProfiles.Build();
            TestAssert.Equal(37, Directory.GetFiles(output).Length);
            foreach (var profile in expected) {
                byte[] bytes = File.ReadAllBytes(Path.Combine(output, profile.Name));
                TestAssert.True(bytes.SequenceEqual(profile.State.Serialize()),
                    "The exporter must publish the original controller serialization bytes");
            }
            TestAssert.True(File.Exists(Path.Combine(output, "opening-controls-use-manifest.json")),
                "The source scope manifest must accompany the actual packets");
            var before = Directory.GetFiles(output).ToDictionary(path => Path.GetFileName(path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-opening-controls-use-fixtures", output }));
            foreach (string path in Directory.GetFiles(output))
                TestAssert.Equal(before[Path.GetFileName(path)], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-opening-controls-use-fixtures", "relative-packets" }));
        } finally { Directory.Delete(parent, recursive: true); }
    }

    private static void AssertCapturedSelect(OpeningControlsUseFixtureProfile sent,
        OpeningControlsUseFixtureProfile observed) {
        var command = sent.Command ?? throw new InvalidOperationException("The Select packet requires its actual command");
        var capture = sent.CapturedFrame ?? throw new InvalidOperationException("The Select packet requires its pre-write frame");
        TestAssert.Equal("Select", command.Input.ToString());
        TestAssert.Equal(observed.State.LastSequence + 1, command.Sequence);
        TestAssert.Equal(observed.State.NavigationRevision, command.NavigationRevision);
        TestAssert.Equal(observed.State.OpeningPolicies?.Revision ?? observed.State.MainOpeningControls!.Revision,
            command.OpeningControlsRevision);
        TestAssert.Equal(observed.State.NavigationRevision, capture.NavigationRevision);
        TestAssert.Equal(observed.State.OpeningPolicies?.Revision ?? observed.State.MainOpeningControls!.Revision,
            capture.OpeningPolicies?.Revision ?? capture.MainOpeningControls!.Revision);
        TestAssert.Equal(observed.State.LocalMenuEligibility!.Revision, capture.LocalMenuEligibility!.Revision);
        TestAssert.Equal(observed.State.MenuRevision, capture.MenuRevision);
        TestAssert.True(command.LocalAdmissionRevision is null && command.MenuRevision is null,
            "A native Select must not serialize admission or presentation context");
    }

    private static string? CallbackPhase(StateDocument state) =>
        state.MenuOptionCallback?.Phase ?? state.MenuPreferenceRefresh?.Phase;
}
