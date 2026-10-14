using System.Text.Json;

namespace Backend.Tests;

// The real test-driver CLI is the cross-language producer, not a parallel private entry point
internal static class ChromeThemeFixtureExporterTests {
    public static void ActualControllerGraphsKeepSourceMembershipCallbacksAndCreationChronology() {
        var profiles = ChromeThemeFixtureExporter.Build();
        TestAssert.Equal(54, profiles.Count);
        TestAssert.Equal(profiles.Count, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        var source = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        foreach (string design in new[] { "apparition", "classic", "native", "aio", "basic" }) {
            var first = source["chrome-theme-" + design + "-red0.json"];
            var redraw = source["chrome-theme-" + design + "-red180.json"];
            TestAssert.Equal(first.ChromeTheme!.CreationRevision, redraw.ChromeTheme!.CreationRevision);
            TestAssert.Equal(first.MenuRevision!.Value + 1, redraw.MenuRevision!.Value);
            TestAssert.Equal(180UL, redraw.ChromeTheme.Events[0].ElapsedMilliseconds);
        }
        TestAssert.Equal(27, source["chrome-theme-width.json"].ChromeTheme!.CurrentRoleMask);
        TestAssert.Equal(28, source["chrome-theme-width.json"].ChromeTheme!.Events[0].TargetMask);
        TestAssert.True(source["chrome-theme-focus-withdrawn.json"].ChromeTheme is null, "Focus projection omits active history");
        TestAssert.True(!source["chrome-theme-closed.json"].ChromeTheme!.Exists, "Actual close declares source destruction");
        TestAssert.Equal(source["chrome-theme-width-return50.json"].ChromeTheme!.CreationRevision,
            source["chrome-theme-width-return-rollback.json"].ChromeTheme!.CreationRevision);
        TestAssert.True(source["chrome-theme-delayed-fixed-stop.json"].ChromeTheme!.CreationRainbow == true,
            "A stopped current mode cannot rewrite Rainbow creation metadata");
        TestAssert.True(source["chrome-theme-delayed-fixed-stop.json"].ChromeTheme!.Events.Single().StoppedRainbow,
            "Delayed native observation retains the actual accepted mode stop");
        TestAssert.True(!source["chrome-theme-overlap9.json"].ChromeTheme!.PrefixComplete, "Bounded history declares the missing prefix");
    }

    public static void ActualProgramExportsEveryPacketWithoutOverwritingExistingEvidence() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-chrome-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string directory = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-chrome-theme-fixtures", directory }));
            TestAssert.Equal(54, Directory.GetFiles(directory, "*.json").Length);
            string first = Path.Combine(directory, "chrome-theme-apparition-created.json");
            byte[] before = File.ReadAllBytes(first);
            using var json = JsonDocument.Parse(before);
            TestAssert.True(json.RootElement.GetProperty("chromeTheme").GetProperty("exists").GetBoolean(),
                "The actual CLI must export the typed controller descriptor");
            TestAssert.Equal(1, Program.Main(new[] { "--export-chrome-theme-fixtures", directory }));
            TestAssert.True(before.SequenceEqual(File.ReadAllBytes(first)), "Rejected export cannot overwrite earlier evidence");
            TestAssert.Equal(1, Program.Main(new[] { "--export-chrome-theme-fixtures" }));
        } finally {
            Directory.Delete(parent, recursive: true);
        }
    }
}
