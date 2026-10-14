using System.Text.Json;

namespace Backend.Tests;

// Literal assertions bind the real controller graph before either native reader consumes it
internal static class PlayerNavigationFixtureExporterTests {
    public static void ActualSourcePlayerGraphKeepsHierarchyTargetsPaintAndUnsupportedCallbacksSeparate() {
        var profiles = PlayerNavigationFixtureExporter.Build();
        TestAssert.Equal(20, profiles.Count);
        TestAssert.Equal(20, profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count());
        var states = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        var peer = states["player-navigation-peer-options.json"];
        TestAssert.Equal("Friend", peer.Title); TestAssert.Equal(14, peer.Rows.Count);
        TestAssert.True(peer.Rows.Select(row => row.Label).SequenceEqual(SourcePlayerInventoryTests.Options), "Options preserve source order");
        TestAssert.True(peer.Rows[1].Enabled && peer.Rows.Where((_, i) => i != 1).All(row => !row.Enabled), "Only implemented navigation is offered");
        var first = states["player-navigation-peer-boolean0.json"];
        var redraw = states["player-navigation-peer-boolean180.json"];
        TestAssert.Equal(31, first.Rows.Count); TestAssert.Equal("OFF", first.Rows[0].Value);
        TestAssert.Equal("ON", first.MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal("OFF", redraw.MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal(first.MenuRevision!.Value + 1, redraw.MenuRevision!.Value);
        TestAssert.Equal(first.NavigationRevision, redraw.NavigationRevision);
        TestAssert.True(states["player-navigation-peer-boolean429.json"].MenuOptionCallback is not null
            && states["player-navigation-peer-boolean430.json"].MenuOptionCallback is null, "The actual source caller unlocks only at 430 ms");
        TestAssert.Equal("ON", states["player-navigation-self-boolean0.json"].Rows[0].Value);
        TestAssert.Equal("OFF", states["player-navigation-self-boolean0.json"].MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal("Quick Menu", states["player-navigation-quick-forced-self.json"].Title);
        TestAssert.Equal("OFF", states["player-navigation-quick-forced-self.json"].Rows[0].Value);
        TestAssert.Equal("Friend", states["player-navigation-main-peer-return.json"].Title);
        TestAssert.Equal("UNKNOWN", states["player-navigation-stale-peer.json"].Rows[0].Value);
        TestAssert.True(states["player-navigation-self-metadata-options.json"].Rows[1].Enabled, "Metadata-only self can browse");
        var unavailable = states["player-navigation-self-metadata-basic.json"].Rows[0];
        TestAssert.True(!unavailable.Enabled && unavailable.Value == "UNKNOWN", "A metadata receipt cannot borrow an unrelated flags writer");
        TestAssert.Equal(12, states["player-navigation-allplayers-source.json"].Rows.Count);
        foreach (var profile in profiles) {
            using var json = JsonDocument.Parse(profile.State.Serialize());
            string wire = json.RootElement.GetRawText();
            TestAssert.True(!wire.Contains("ExpectedTarget", StringComparison.OrdinalIgnoreCase)
                && !wire.Contains("SelectionScope", StringComparison.OrdinalIgnoreCase)
                && !wire.Contains("1234567890ABCDEF", StringComparison.Ordinal), "Private target identity never enters the public packet");
        }
    }

    public static void ActualPlayerProgramExportsOnlyFreshPacketsAndRejectsOverwriting() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-player-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent); string directory = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-player-navigation-fixtures", directory }));
            TestAssert.Equal(20, Directory.GetFiles(directory, "*.json").Length);
            string first = Path.Combine(directory, "player-navigation-peer-options.json");
            byte[] before = File.ReadAllBytes(first);
            TestAssert.Equal(1, Program.Main(new[] { "--export-player-navigation-fixtures", directory }));
            TestAssert.True(before.SequenceEqual(File.ReadAllBytes(first)), "Refused exports cannot replace earlier packets");
            TestAssert.Equal(1, Program.Main(new[] { "--export-player-navigation-fixtures" }));
        } finally { Directory.Delete(parent, recursive: true); }
    }
}
