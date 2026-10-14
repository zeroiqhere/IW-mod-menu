using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The producer graph carries immutable creation facts rather than fabricated presented RGB history
internal static class InstructionCreationColorFixtureExporterTests {
    public static void ActualControllerGraphRetainsFixedAndRainbowCreationColorFactsAcrossSourceCauses() {
        var profiles = InstructionCreationColorFixtureExporter.Build();
        TestAssert.Equal(31, profiles.Count);
        var frames = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        StateDocument Frame(string name) => frames["instruction-color-" + name + ".json"];
        void Capture(string name, ulong revision, string accent, bool rainbow) {
            var hud = Frame(name).InstructionHud!;
            TestAssert.Equal<ulong?>(revision, hud.CreationRevision);
            TestAssert.Equal(accent, hud.CreationAccent);
            TestAssert.Equal<bool?>(rainbow, hud.CreationRainbow);
            TestAssert.Equal(hud.AdmissionRevision, Frame(name).LocalMenuEligibility!.Revision);
        }
        foreach (var profile in profiles) {
            TestAssert.True(profile.State.Context != "PREVIEW", "The chronology requires real controller proof with inert source observations");
            TestAssert.True(profile.State.InstructionHud is not null, "Every graph frame must declare present or unavailable instructions");
        }
        foreach (string name in new[] { "aio-created-blue", "fixed-red0", "fixed-red180", "fixed-red430", "fixed-red1000",
            "parent", "closed", "reopen0", "reopen50", "reopen600" }) Capture(name, 2, "#3998FE", false);
        TestAssert.Equal("#FF0000", Frame("fixed-red0").Accent);
        TestAssert.True(!Frame("closed").IsOpen, "The retained creation must survive an actual close");
        Capture("alternate-red", 3, "#FF0000", false);
        Capture("rainbow0", 3, "#FF0000", false);
        Capture("rainbow430", 3, "#FF0000", false);
        Capture("rainbow-recreated", 4, "#FF0000", true);
        foreach (string name in new[] { "fixed-green0", "fixed-green180", "fixed-green430", "fixed-green1000" }) {
            Capture(name, 4, "#FF0000", true);
            TestAssert.True(!Frame(name).SmoothRainbowTheme, "The current fixed mode must not erase the captured Rainbow creation mode");
            TestAssert.Equal("#00FF00", Frame(name).Accent);
        }
        Capture("background-disabled", 5, "#00FF00", false);
        Capture("background-recreated", 6, "#00FF00", false);
        TestAssert.Equal(InstructionHudState.Absent, Frame("disabled").InstructionHud);
        Capture("re-enabled", 7, "#00FF00", false);
        Capture("dead-closed", 8, "#00FF00", false);
        Capture("alive-again", 8, "#00FF00", false);
        TestAssert.Equal(InstructionHudState.Absent, Frame("permission-lost").InstructionHud);
        Capture("permission-restored", 9, "#00FF00", false);
        Capture("owner-replaced", 10, "#00FF00", false);
        Capture("gap-rainbow", 10, "#00FF00", false);
        foreach (string name in new[] { "design-out0", "design-out49" }) {
            Capture(name, 10, "#00FF00", false);
            TestAssert.Equal("AIO", Frame(name).InstructionHud!.CreationDesign);
            TestAssert.Equal("Classic", Frame(name).MenuDesignTransition!.FooterDesign);
            TestAssert.True(Frame(name).SmoothRainbowTheme, "The active source sample callback can target the old outline using the committed design");
        }
        Capture("design-out50", 11, "#00FF00", true);
        TestAssert.Equal("Classic", Frame("design-out50").InstructionHud!.CreationDesign);
        TestAssert.True(profiles.Zip(profiles.Skip(1)).All(pair => pair.First.ClockMilliseconds <= pair.Second.ClockMilliseconds),
            "The controller chronology must not regress time");
    }

    public static void ActualTestDriverPublishesThirtyOneCreationColorPacketsWithoutOverwriting() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-instruction-color-driver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets");
            // The shipped regression entry point must dispatch this graph instead of the legacy fallback
            TestAssert.Equal(0, Program.Main(new[] { "--export-instruction-color-fixtures", output }));
            TestAssert.Equal(31, Directory.GetFiles(output, "*.json").Length);
            foreach (var profile in InstructionCreationColorFixtureExporter.Build()) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "The actual CLI must publish unmodified controller bytes");
            }
            TestAssert.Equal(1, Program.Main(new[] { "--export-instruction-color-fixtures", output }));
            TestAssert.Equal(1, Program.Main(new[] { "--export-instruction-color-fixtures" }));
            TestAssert.Throws<ArgumentException>(() => InstructionCreationColorFixtureExporter.Export("relative"));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
