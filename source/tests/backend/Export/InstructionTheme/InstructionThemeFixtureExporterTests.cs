using System.Text.Json;

namespace Backend.Tests;

// Both the actual chronology and the shipped test driver must carry the same unmodified producer packets
internal static class InstructionThemeFixtureExporterTests {
    public static void ActualControllerGraphCapturesOverlapsStopsTargetsAndEmptyRectangleHistory() {
        var profiles = InstructionThemeFixtureExporter.Build();
        TestAssert.Equal(34, profiles.Count);
        TestAssert.Equal(34, profiles.Select(profile => profile.Name).Distinct().Count());
        var frames = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        Apparition.Backend.Ipc.StateDocument Frame(string name) => frames["instruction-theme-" + name + ".json"];
        var first = Frame("red0").InstructionTheme!;
        TestAssert.Equal(1UL, first.Events.Single().Revision);
        TestAssert.Equal("#FF0000", first.Events.Single().Accent);
        foreach (int elapsed in new[] { 179, 180, 430 }) {
            TestAssert.Equal((ulong)elapsed, Frame("red" + elapsed).InstructionTheme!.Events.Single().ElapsedMilliseconds);
            TestAssert.Equal(first.CreationRevision, Frame("red" + elapsed).InstructionTheme!.CreationRevision);
        }
        var repeated = Frame("same-red70").InstructionTheme!;
        TestAssert.Equal(500UL, repeated.Events[0].ElapsedMilliseconds);
        TestAssert.Equal(70UL, repeated.Events[1].ElapsedMilliseconds);
        TestAssert.Equal<int?>(430, repeated.Events[1].PredecessorGapMilliseconds);
        TestAssert.Equal(2, Frame("rainbow430").InstructionTheme!.Events.Count);
        var stop = Frame("stop-red0").InstructionTheme!.Events.Last();
        TestAssert.Equal(3UL, stop.Revision);
        TestAssert.True(stop.StoppedRainbow, "The same-color fixed callback must identify its prior Rainbow mode");
        TestAssert.Equal<int?>(500, stop.PredecessorGapMilliseconds);
        foreach (string name in new[] { "parent", "closed", "reopen0", "reopen50", "reopen600", "design-aio0", "design-aio49" }) {
            TestAssert.Equal(first.CreationRevision, Frame(name).InstructionTheme!.CreationRevision);
            TestAssert.Equal("outline", Frame(name).InstructionTheme!.Events.Last().Target);
        }
        TestAssert.True(!Frame("closed").IsOpen, "The chronology must contain an actual closed frame");
        TestAssert.True(Frame("focus-withdrawn").InstructionTheme is null, "Withdrawn focus cannot carry invocation authority");
        TestAssert.Equal(0, Frame("design-aio50").InstructionTheme!.Events.Count);
        TestAssert.Equal("background", Frame("green-background0").InstructionTheme!.Events.Single().Target);
        foreach (string name in new[] { "no-background-created", "no-background-red0" }) {
            TestAssert.Equal<bool?>(false, Frame(name).InstructionHud!.CreationBackground);
            TestAssert.Equal(0, Frame(name).InstructionTheme!.Events.Count);
            TestAssert.True(Frame(name).InstructionTheme!.PrefixComplete, "No rectangle means no missing callback prefix");
        }
        TestAssert.Equal(0, Frame("owner-replaced").InstructionTheme!.Events.Count);
        TestAssert.True(Frame("overlap-eight").InstructionTheme!.PrefixComplete, "The first eight invocations remain complete");
        var pruned = Frame("overlap-nine0").InstructionTheme!;
        TestAssert.Equal(8, pruned.Events.Count);
        TestAssert.True(!pruned.PrefixComplete, "The ninth overlap must declare prefix pruning");
        TestAssert.Equal(2UL, pruned.Events[0].Revision);
        TestAssert.Equal(3010UL, pruned.Events[0].ElapsedMilliseconds);
        TestAssert.Equal(9UL, pruned.Events[^1].Revision);
        TestAssert.Equal(500UL, Frame("overlap-nine500").InstructionTheme!.Events[^1].ElapsedMilliseconds);
        TestAssert.True(!Frame("overlap-nine500").InstructionTheme!.PrefixComplete, "Quiet recovery cannot restore a pruned prefix");
        TestAssert.True(Frame("permission-withdrawn").InstructionTheme is null, "Lost proof must withdraw the journal");
        TestAssert.Equal(0, Frame("permission-restored").InstructionTheme!.Events.Count);
        foreach (var profile in profiles) {
            TestAssert.True(profile.State.Context != "PREVIEW", "Every packet must come from the connected inert controller");
            using var parsed = JsonDocument.Parse(profile.State.Serialize());
            TestAssert.Equal(profile.State.LastSequence, parsed.RootElement.GetProperty("lastSequence").GetUInt64());
        }
    }

    public static void ActualTestDriverExportsAllThemePacketsWithoutOverwritingOrInventingSourceFrames() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-instruction-theme-driver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets");
            TestAssert.Equal(0, Program.Main(new[] { "--export-instruction-theme-fixtures", output }));
            TestAssert.Equal(34, Directory.GetFiles(output, "*.json").Length);
            foreach (var profile in InstructionThemeFixtureExporter.Build()) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "The actual test driver must publish exact controller bytes");
            }
            TestAssert.Equal(1, Program.Main(new[] { "--export-instruction-theme-fixtures", output }));
            TestAssert.Equal(1, Program.Main(new[] { "--export-instruction-theme-fixtures" }));
            TestAssert.Throws<ArgumentException>(() => InstructionThemeFixtureExporter.Export("relative"));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
