using System.Security.Cryptography;
using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class FooterWithdrawalExporterTests {
    public static void RealControllerChronologyKeepsOwnedHistoryAndDistinctRecreationCauses() {
        var profiles = FooterWithdrawalFixtureExporter.Build();
        TestAssert.Equal(46, profiles.Count);
        TestAssert.Equal(46, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        var states = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        foreach (var profile in profiles) {
            // Every packet crosses the production DTO validation and serialized wire boundary
            using var document = JsonDocument.Parse(profile.State.Serialize());
            if (profile.State.LocalMenuEligibility is { Life: "unknown" }) {
                TestAssert.Equal(InstructionHudState.Absent, profile.State.InstructionHud);
                TestAssert.True(profile.State.InstructionTheme is null, "Unknown life must not borrow journal authority");
            }
        }
        foreach (string graph in new[] { "open", "closed" }) {
            var known = states["footer-withdrawal-" + graph + "-known.json"];
            var restored = states["footer-withdrawal-" + graph + "-restored.json"];
            TestAssert.Equal(known.InstructionHud!.CreationRevision, restored.InstructionHud!.CreationRevision);
            TestAssert.Equal(known.InstructionTheme!.Events.Single().ElapsedMilliseconds + 250,
                restored.InstructionTheme!.Events.Single().ElapsedMilliseconds);
        }
        var start = states["footer-withdrawal-delayed-started.json"].InstructionHud!;
        var beforeDraw = states["footer-withdrawal-delayed-restored30.json"].InstructionHud!;
        var drawn = states["footer-withdrawal-delayed-drawn50.json"].InstructionHud!;
        TestAssert.Equal(start.CreationRevision, beforeDraw.CreationRevision);
        TestAssert.Equal("Apparition", beforeDraw.CreationDesign);
        TestAssert.Equal(start.CreationRevision!.Value + 1, drawn.CreationRevision!.Value);
        TestAssert.Equal("Classic", drawn.CreationDesign);
        var created = states["footer-withdrawal-rainbow-created.json"].InstructionHud!;
        var retained = states["footer-withdrawal-rainbow-restored430.json"];
        TestAssert.Equal<bool?>(true, created.CreationRainbow);
        TestAssert.Equal(created.CreationRevision, retained.InstructionHud!.CreationRevision);
        TestAssert.Equal(created.CreationAlternate, retained.InstructionHud.CreationAlternate);
        TestAssert.True(retained.InstructionTheme!.Events.Single().StoppedRainbow, "A fixed stop must retain its captured prior creation mode");
        var recovered = states["footer-withdrawal-prefix-recovered-timing.json"];
        TestAssert.True(!recovered.InstructionTheme!.PrefixComplete, "Rollback cannot restore a complete prefix");
        TestAssert.Equal(0, recovered.InstructionTheme.Events.Count);
    }

    public static void ActualProgramPublishesTheCompleteGraphWithoutOverwritingEvidence() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-footer-withdrawal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "fixtures");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-footer-withdrawal-fixtures", destination }));
            var files = Directory.GetFiles(destination).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            TestAssert.Equal(46, files.Length);
            var original = files.ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-footer-withdrawal-fixtures", destination }));
            foreach (string path in files) TestAssert.Equal(original[path], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-footer-withdrawal-fixtures" }));
        } finally {
            Directory.Delete(parent, recursive: true);
        }
    }
}
