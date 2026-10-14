using Apparition.Backend.Ipc;
using System.Security.Cryptography;

namespace Backend.Tests;

// The assertions exercise admitted controller visits rather than fabricated wire records
internal static class ClosedLevelOpeningFixtureTests {
    private static Dictionary<string, ClosedLevelOpeningFixtureExporter.Profile> Graph() {
        var profiles = ClosedLevelOpeningFixtureExporter.Build();
        TestAssert.Equal(ClosedLevelOpeningFixtureExporter.ProfileCount, profiles.Count);
        TestAssert.Equal(profiles.Count, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        TestAssert.True(profiles.Select(profile => profile.Graph).ToHashSet(StringComparer.Ordinal).SetEquals(new[] {
            "main", "quick", "dead", "permission", "disabled", "unknown", "missing", "stale-navigation", "stale-policy",
            "stale-admission", "queued-focus", "queued-admission", "queued-owner" }), "The graph must include every independent admitted or rejected source visit");
        foreach (var profile in profiles) {
            TestAssert.True(profile.State.Serialize().Length > 0, "Every actual packet must pass the strict wire factory");
            if (profile.Command is { } command && command.Input is syndishanx_iw_tool.Overlay.OverlayInput.OpenMain
                or syndishanx_iw_tool.Overlay.OverlayInput.OpenQuick) {
                TestAssert.True(command.NavigationRevision is not null && command.OpeningControlsRevision > 0
                    && command.LocalAdmissionRevision > 0, "Opening commands retain the captured native scope trio");
                var source = profile.CommandScope!;
                TestAssert.Equal(source.NavigationRevision, command.NavigationRevision!.Value);
                TestAssert.Equal(source.PolicyRevision!.Value, command.OpeningControlsRevision!.Value);
                TestAssert.Equal(source.AdmissionRevision, command.LocalAdmissionRevision!.Value);
                TestAssert.True(command.Sequence > source.LastSequence, "The actual sent command follows its original copied ACK");
            }
        }
        return profiles.ToDictionary(profile => profile.Name, StringComparer.Ordinal);
    }
    public static void ActualClosedGraphOwnsBackExpiryAndMainQuickCreationWithoutAnExtraWait() {
        var graph = Graph();
        StateDocument Frame(string name) => graph[name + ".json"].State;
        TestAssert.True(!Frame("main-back249").IsOpen && Frame("main-back249").MenuMonitorWait is not null,
            "The closed root remains mechanically blocked until250");
        TestAssert.True(!Frame("main-back250").IsOpen && Frame("main-back250").MenuMonitorWait is null,
            "The source Back caller has no extra50 after250");
        TestAssert.True(Frame("main-pending250").LocalMenuEligibility!.Blocked, "Validation is queued before any Main HUD exists");
        TestAssert.Equal("rebuild", Frame("main-rebuild299").MenuOpenTransition!.Phase);
        TestAssert.True(Frame("main-drawn300").Rows.Count > 0, "Main creates actual rows at50 after opening admission");
        TestAssert.True(Frame("main-ready850").MenuOpenTransition is null, "Main input admission finishes600 after opening");
        TestAssert.True(Frame("quick-drawn0").IsOpen && Frame("quick-drawn0").Rows.Count == 14,
            "Living Quick creates its actual HUD without a Main50 gap");
        TestAssert.True(Frame("quick-wait549").MenuOpenTransition is not null && Frame("quick-ready550").MenuOpenTransition is null,
            "Quick owns exactly its independent550 nominal input wait");
        TestAssert.Equal("dead", Frame("dead-before0").LocalMenuEligibility!.Life);
        TestAssert.True(Frame("dead-drawn0").IsOpen && Frame("dead-drawn0").Rows.Count == 3,
            "Known dead Quick uses the declared dead catalog without a living feature");
        TestAssert.True(!Frame("dead-jump799").IsOpen && Frame("dead-jump799").MenuMonitorWait is not null,
            "Actual dead JumpBack retains its250 caller wait");
        TestAssert.True(Frame("dead-reopened800").IsOpen, "The fresh current dead proof can reopen after its own Back wait");
        TestAssert.Equal(250L, graph["main-back250.json"].RelativeMilliseconds);
        TestAssert.Equal(800L, graph["dead-reopened800.json"].RelativeMilliseconds);
    }
    public static void ActualClosedGraphRejectsMissingDisabledUnknownAndStaleOpeningAuthority() {
        var graph = Graph();
        StateDocument Frame(string name) => graph[name + ".json"].State;
        foreach (string name in new[] { "permission-denied0", "disabled-denied0", "unknown-denied0", "missing-denied0",
            "stale-navigation-denied800", "stale-policy-denied1780", "stale-admission-denied0",
            "queued-focus-retired0", "queued-admission-retired0", "queued-owner-retired0" }) {
            TestAssert.True(!Frame(name).IsOpen && Frame(name).MenuOpenTransition is null,
                "A denied or retired captured source must not open: " + name);
        }
        TestAssert.True(Frame("permission-drawn0").IsOpen, "Current permission recovery can authorize a newly captured command");
        TestAssert.True(Frame("disabled-before0").OpeningPolicies!.QuickDisabled, "Disable Quick comes from an actual controller setting action");
        TestAssert.True(Frame("disabled-main50").IsOpen, "Disabling Quick does not remove independently configured Main");
        TestAssert.Equal("unknown", Frame("unknown-before0").LocalMenuEligibility!.Life);
        TestAssert.True(Frame("missing-before0").LocalMenuEligibility is null, "Missing current proof is not fabricated from a process");
        foreach (string graphName in new[] { "queued-focus", "queued-admission", "queued-owner" })
            TestAssert.True(Frame(graphName + "-pending0").LocalMenuEligibility!.Blocked,
                "Every queued rejection starts with an actual captured validation task");
    }
    public static void ActualProgramExportsOnlyTheClosedOpeningGraphAndRejectsExistingOutputs() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-closed-open-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string output = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-closed-level-opening-fixtures", output }));
            var expected = ClosedLevelOpeningFixtureExporter.Build();
            TestAssert.Equal(ClosedLevelOpeningFixtureExporter.ProfileCount, Directory.GetFiles(output).Length);
            foreach (var profile in expected) {
                var bytes = File.ReadAllBytes(Path.Combine(output, profile.Name));
                TestAssert.True(bytes.SequenceEqual(profile.State.Serialize()), "Actual Program must export the unchanged controller packet");
            }
            var before = Directory.GetFiles(output).ToDictionary(path => Path.GetFileName(path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-closed-level-opening-fixtures", output }));
            foreach (var path in Directory.GetFiles(output))
                TestAssert.Equal(before[Path.GetFileName(path)], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-closed-level-opening-fixtures", "relative-packets" }));
        } finally { Directory.Delete(parent, recursive: true); }
    }
}
