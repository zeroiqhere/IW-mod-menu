using System.Text.Json;

namespace Backend.Tests;

internal static class BasicChromeFixtureExporterTests {
    public static void ActualBasicChronologyKeepsRowRedrawSeparateFromChromeRecreation() {
        var profiles = BasicChromeFixtureExporter.Build();
        TestAssert.Equal(16, profiles.Count);
        var frames = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        foreach (var profile in profiles) {
            TestAssert.Equal("Basic", profile.State.Appearance!.Design);
            TestAssert.True(profile.State.Context != "PREVIEW", "The chronology requires a verified inert source owner");
            using var json = JsonDocument.Parse(profile.State.Serialize());
            TestAssert.Equal(profile.State.ProcessId, json.RootElement.GetProperty("processId").GetInt32());
        }
        foreach (var profile in profiles.Take(10)) TestAssert.Equal<ulong?>(2, profile.State.MenuChromeRevision);
        TestAssert.True(frames["basic-rainbow0.json"].SmoothRainbowTheme, "The actual Rainbow callback must commit its mode");
        TestAssert.True(!frames["basic-fixed0.json"].SmoothRainbowTheme, "The fixed callback must stop the actual mode");
        TestAssert.Equal("#FF0000", frames["basic-fixed0.json"].Accent);
        TestAssert.Equal(frames["basic-rainbow0.json"].MenuRevision!.Value + 1, frames["basic-rainbow180.json"].MenuRevision!.Value);
        TestAssert.Equal(frames["basic-fixed0.json"].MenuRevision!.Value + 1, frames["basic-fixed180.json"].MenuRevision!.Value);
        TestAssert.True(!frames["basic-closed.json"].IsOpen, "The chronological close must be observable independently of history");
        TestAssert.Equal("rebuild", frames["basic-reopening0.json"].MenuOpenTransition!.Phase);
        TestAssert.Equal<ulong?>(3, frames["basic-reopened50.json"].MenuChromeRevision);
        TestAssert.Equal<ulong?>(3, frames["basic-width-locked.json"].MenuChromeRevision);
        TestAssert.Equal<ulong?>(3, frames["basic-unlock0.json"].MenuChromeRevision);
        TestAssert.Equal<ulong?>(4, frames["basic-unlock-late150.json"].MenuChromeRevision);
        TestAssert.True(frames["basic-unlock-late150.json"].MenuUnlock is null, "The late return is already unlocked and still recreated once");
        TestAssert.Equal<ulong?>(4, frames["basic-owner-retired.json"].MenuChromeRevision);
        TestAssert.Equal<ulong?>(5, frames["basic-owner-reopened50.json"].MenuChromeRevision);
        TestAssert.True(profiles.Zip(profiles.Skip(1)).All(pair => pair.First.ClockMilliseconds <= pair.Second.ClockMilliseconds),
            "The actual controller chronology must never regress its clock");
    }

    public static void BasicChronologyExportOwnsSixteenFreshPacketsWithoutOverwriting() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-basic-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets");
            TestAssert.Equal(16, BasicChromeFixtureExporter.Export(output));
            foreach (var profile in BasicChromeFixtureExporter.Build()) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "Export must preserve the actual controller bytes");
            }
            TestAssert.Throws<IOException>(() => BasicChromeFixtureExporter.Export(output));
            TestAssert.Throws<ArgumentException>(() => BasicChromeFixtureExporter.Export("relative"));
            TestAssert.Equal(1, BasicChromeFixtureExporter.Run(new[] { "--export-basic-chrome-fixtures" }));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
