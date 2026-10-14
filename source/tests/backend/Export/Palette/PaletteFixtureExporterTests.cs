using System.Text.Json;
using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class PaletteFixtureExporterTests {
    private static readonly string[] Designs = { "Apparition", "Classic", "Native", "AIO", "Basic" };
    private static JsonDocument ReadOracle() => JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "palette-source-oracle.json")));
    private static string Paint(StateDocument frame, int index) => frame.MenuBooleanPaint!.Rows.Single(row => row.Index == index).Value;

    public static void ActualControllerSelectionsMatchAllThirtySixPinnedColorsAcrossEveryDesign() {
        using var oracle = ReadOracle();
        TestAssert.Equal("307e94c1b1d6537ba7a2a71b32a22d563d3ea25c", oracle.RootElement.GetProperty("pin").GetString());
        TestAssert.Equal("Menu/utilities.gsc:558-566", oracle.RootElement.GetProperty("source").GetString());
        var colors = oracle.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        TestAssert.Equal(36, colors.Length);
        TestAssert.Equal(36, colors.Select(color => color.GetProperty("label").GetString()).Distinct().Count());
        TestAssert.Equal("e747595e05db2b319f33f73e068df306fb086c770d4928316a2ed7d4eaab2c0e",
            oracle.RootElement.GetProperty("sourceSha256").GetString());
        foreach (var color in colors) {
            var rgb = color.GetProperty("rgb").EnumerateArray().Select(channel => channel.GetInt32()).ToArray();
            TestAssert.Equal(3, rgb.Length);
            TestAssert.True(rgb.All(channel => channel is >= 0 and <= 255), "Every source RGB channel must retain its literal byte value");
            TestAssert.Equal("#" + string.Concat(rgb.Select(channel => channel.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))),
                color.GetProperty("hex").GetString());
        }
        var profiles = PaletteFixtureExporter.Build();
        TestAssert.Equal(192, profiles.Count);
        var graph = profiles.ToDictionary(profile => profile.Name, profile => profile.State);
        var clocks = profiles.ToDictionary(profile => profile.Name, profile => profile.ClockMilliseconds);

        // The expected names and RGB pairs come from the pinned source, not MenuThemes
        foreach (string design in Designs) {
            for (int index = 0; index < colors.Length; index++) {
                string name = $"palette-{design.ToLowerInvariant()}-{index:D2}.json";
                var frame = graph[name];
                string previous = index == 0 ? $"palette-{design.ToLowerInvariant()}-created.json"
                    : $"palette-{design.ToLowerInvariant()}-{index - 1:D2}.json";
                TestAssert.Equal(1000L, clocks[name] - clocks[previous]);
                TestAssert.Equal(design, frame.Appearance!.Design);
                TestAssert.Equal(index, frame.SelectedIndex);
                TestAssert.Equal(colors[index].GetProperty("hex").GetString(), frame.Accent);
                TestAssert.Equal(37, frame.Rows.Count);
                TestAssert.True(frame.MenuOptionCallback is null && !frame.SmoothRainbowTheme,
                    "Every settled selection must complete its actual caller wait");
                for (int row = 0; row < colors.Length; row++) {
                    TestAssert.Equal(row, colors[row].GetProperty("index").GetInt32());
                    TestAssert.Equal(colors[row].GetProperty("label").GetString(), frame.Rows[row].Label);
                    TestAssert.Equal(row == index ? "ON" : "OFF", Paint(frame, row));
                    TestAssert.True(frame.Rows[row].Enabled, "Local source colors remain selectable without gameplay capability");
                }
                TestAssert.Equal("Smooth Rainbow", frame.Rows[36].Label);
                TestAssert.Equal("OFF", Paint(frame, 36));
                using var json = JsonDocument.Parse(frame.Serialize());
                TestAssert.Equal(frame.Accent, json.RootElement.GetProperty("accent").GetString());
            }
        }
    }

    public static void ActualPaletteChronologyPreservesCapturedIndicatorsAndAppearanceVariants() {
        var graph = PaletteFixtureExporter.Build().ToDictionary(profile => profile.Name, profile => profile.State);
        foreach (string design in Designs) {
            var created = graph[$"palette-{design.ToLowerInvariant()}-created.json"];
            TestAssert.Equal(design, created.Appearance!.Design);
            TestAssert.Equal(0, created.SelectedIndex);
            TestAssert.Equal("#3998FE", created.Accent);
            TestAssert.Equal("ON", Paint(created, 6));
            TestAssert.Equal(design is not ("Classic" or "Native"), created.Appearance.ColoredCursor);
            TestAssert.True(created.MenuOptionCallback is null, "A newly created page is not a fabricated color callback");
        }
        var first = graph["palette-apparition-black-0.json"];
        foreach (int elapsed in new[] { 0, 179, 180, 429, 430 }) {
            var frame = graph[$"palette-apparition-black-{elapsed}.json"];
            TestAssert.Equal("#000000", frame.Accent);
            TestAssert.Equal(3, frame.SelectedIndex);
            TestAssert.Equal(first.NavigationRevision, frame.NavigationRevision);
            TestAssert.Equal(first.MenuChromeRevision, frame.MenuChromeRevision);
            TestAssert.Equal(first.MenuRevision!.Value + (elapsed >= 180 ? 1UL : 0UL), frame.MenuRevision!.Value);
            TestAssert.Equal(elapsed < 180 ? "OFF" : "ON", Paint(frame, 3));
            TestAssert.Equal(elapsed < 180 ? "ON" : "OFF", Paint(frame, 6));
            TestAssert.Equal(elapsed == 430 ? null : elapsed < 180 ? "pending" : "cooldown", frame.MenuOptionCallback?.Phase);
        }
        var uncolored = graph["palette-apparition-cursor-off.json"];
        TestAssert.True(!uncolored.Appearance!.ColoredCursor && uncolored.Accent == "#000000",
            "The actual cursor preference can keep the selected row white while the target stays Black");
        TestAssert.Equal("ON", Paint(uncolored, 3));
        var textColor = graph["palette-apparition-textcolor-on.json"];
        TestAssert.Equal("Text Color", textColor.Appearance!.BooleanDisplay);
        TestAssert.Equal("#000000", textColor.Accent);
        TestAssert.Equal(3, textColor.SelectedIndex);
        TestAssert.Equal("ON", Paint(textColor, 3));
    }

    public static void PaletteExportPublishesExactlyTheControllerGraphWithoutOverwriting() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-palette-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string destination = Path.Combine(root, "packets");
            TestAssert.Equal(192, PaletteFixtureExporter.Export(destination));
            TestAssert.Equal(192, Directory.GetFiles(destination).Length);
            foreach (var profile in PaletteFixtureExporter.Build()) {
                TestAssert.True(File.ReadAllBytes(Path.Combine(destination, profile.Name)).SequenceEqual(profile.State.Serialize()),
                    "The raw exported packet must preserve its actual producer state");
            }
            TestAssert.Throws<IOException>(() => PaletteFixtureExporter.Export(destination));
            TestAssert.Throws<ArgumentException>(() => PaletteFixtureExporter.Export("relative"));
            TestAssert.Equal(1, PaletteFixtureExporter.Run(new[] { "--export-palette-fixtures" }));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
