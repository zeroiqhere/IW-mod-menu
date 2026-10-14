using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// These assertions join actual controller visits to immutable exported bytes
internal static class InstructionHudFixtureExporterTests {
    public static void ActualInstructionChronologyKeepsCreationSeparateFromMenuAndHintLifetimes() {
        var profiles = InstructionHudFixtureExporter.Build();
        TestAssert.Equal(35, profiles.Count);
        var frames = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        StateDocument Frame(string name) => frames["instruction-hud-" + name + ".json"];
        foreach (var profile in profiles) {
            TestAssert.True(profile.State.Context != "PREVIEW", "Footer proof must come from an inert verified controller");
            if (profile.State.InstructionHud is { Exists: true } hud) {
                TestAssert.Equal(hud.AdmissionRevision, profile.State.LocalMenuEligibility!.Revision);
                TestAssert.True(hud.IsSupported(), "Every present footer must own its captured creation fields");
            }
        }
        foreach (string name in new[] { "alive-closed", "main-rebuild0", "main-drawn50", "main-ready600",
            "main-closed", "quick-open0", "quick-ready550" })
            TestAssert.Equal<ulong?>(1, Frame(name).InstructionHud!.CreationRevision);
        TestAssert.True(!Frame("alive-closed").IsOpen && Frame("alive-closed").InstructionHud!.Exists,
            "The known living closed footer is independent of menu creation");
        foreach (string name in new[] { "quick-dead", "dead-closed", "alive-again-closed" })
            TestAssert.Equal<ulong?>(2, Frame(name).InstructionHud!.CreationRevision);
        TestAssert.Equal("dead", Frame("quick-dead").LocalMenuEligibility!.Life);
        TestAssert.Equal(InstructionHudState.Absent, Frame("permission-lost").InstructionHud);
        TestAssert.Equal<ulong?>(3, Frame("permission-restored").InstructionHud!.CreationRevision);
        TestAssert.Equal<ulong?>(4, Frame("owner-replaced").InstructionHud!.CreationRevision);
        TestAssert.Equal("Apparition", Frame("design-rebuild0").InstructionHud!.CreationDesign);
        TestAssert.Equal<ulong?>(4, Frame("design-rebuild0").InstructionHud!.CreationRevision);
        foreach (string name in new[] { "design-drawn50", "design-ready250" }) {
            TestAssert.Equal<ulong?>(5, Frame(name).InstructionHud!.CreationRevision);
            TestAssert.Equal("Classic", Frame(name).InstructionHud!.CreationDesign);
        }
        TestAssert.Equal<ulong?>(6, Frame("alternate").InstructionHud!.CreationRevision);
        TestAssert.Equal<bool?>(true, Frame("alternate").InstructionHud!.CreationAlternate);
        TestAssert.Equal<ulong?>(7, Frame("background-off").InstructionHud!.CreationRevision);
        TestAssert.Equal<bool?>(false, Frame("background-off").InstructionHud!.CreationBackground);
        TestAssert.Equal(InstructionHudState.Absent, Frame("disabled").InstructionHud);
        foreach (string name in new[] { "re-enabled", "width-soft-lock", "width-return0", "width-return150" })
            TestAssert.Equal<ulong?>(8, Frame(name).InstructionHud!.CreationRevision);
        TestAssert.Equal(InstructionHudState.Absent, Frame("focus-hidden").InstructionHud);
        TestAssert.Equal<ulong?>(2, Frame("design-death25").InstructionHud!.CreationRevision);
        TestAssert.Equal<ulong?>(3, Frame("design-death-drawn50").InstructionHud!.CreationRevision);
        TestAssert.Equal(InstructionHudState.Absent, Frame("design-unknown25").InstructionHud);
        // Unknown life withdraws current authority without inventing an earlier source recreation
        TestAssert.Equal<ulong?>(1, Frame("design-recovered30").InstructionHud!.CreationRevision);
        TestAssert.Equal("Apparition", Frame("design-recovered30").InstructionHud!.CreationDesign);
        TestAssert.Equal<ulong?>(2, Frame("design-recovered-drawn50").InstructionHud!.CreationRevision);
        foreach (string name in new[] { "design-permission-restored30", "design-permission-canceled50",
            "design-owner-replaced25", "design-owner-canceled50" }) {
            TestAssert.Equal<ulong?>(2, Frame(name).InstructionHud!.CreationRevision);
            TestAssert.True(!Frame(name).IsOpen && Frame(name).MenuDesignTransition is null,
                "Canceled body callers cannot later refresh another observed footer");
        }
        var disabledQuick = Frame("dead-closed-quick-disabled");
        TestAssert.True(!disabledQuick.IsOpen && disabledQuick.OpeningPolicies!.QuickDisabled,
            "Quick disabling must come from the actual preference callback before the dead closed observation");
        TestAssert.Equal("dead", disabledQuick.LocalMenuEligibility!.Life);
        TestAssert.True(disabledQuick.InstructionHud!.Exists, "Unavailable Quick opening does not destroy source dead instructions");
    }

    public static void ActualTestDriverRoutesInstructionExportToAllThirtyFiveControllerPackets() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-instruction-driver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets");
            // The actual suite entry point must recognize this CLI rather than its generic fallback
            TestAssert.Equal(0, Program.Main(new[] { "--export-instruction-hud-fixtures", output }));
            var profiles = InstructionHudFixtureExporter.Build();
            TestAssert.Equal(35, Directory.GetFiles(output, "*.json").Length);
            foreach (var profile in profiles) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "The actual CLI must publish all authentic controller bytes");
            }
            TestAssert.Equal(1, Program.Main(new[] { "--export-instruction-hud-fixtures", output }));
            TestAssert.Equal(1, Program.Main(new[] { "--export-instruction-hud-fixtures" }));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    public static void InstructionExportOwnsThirtyFiveControllerPacketsWithoutOverwriting() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-instruction-hud-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets");
            TestAssert.Equal(35, InstructionHudFixtureExporter.Export(output));
            foreach (var profile in InstructionHudFixtureExporter.Build()) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "Export must preserve actual controller bytes");
            }
            TestAssert.Throws<IOException>(() => InstructionHudFixtureExporter.Export(output));
            TestAssert.Throws<ArgumentException>(() => InstructionHudFixtureExporter.Export("relative"));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
