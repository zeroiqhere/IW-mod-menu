using System.Text.Json;

namespace Backend.Tests;

internal static class LocalOptionFixtureExporterTests {
    public static void ActualLocalGraphKeepsThemeStatePaintAndCallerClocksSeparate() {
        var graph = LocalOptionFixtureExporter.Build(); TestAssert.Equal(12, graph.Count);
        var palette = graph["palette0.json"]; TestAssert.Equal("#0064C8", palette.Accent);
        TestAssert.Equal("ON", palette.Rows[7].Value); TestAssert.Equal("OFF", palette.MenuBooleanPaint!.Rows[7].Value);
        TestAssert.Equal("ON", palette.MenuBooleanPaint.Rows[6].Value);
        TestAssert.Equal("ON", graph["palette180.json"].MenuBooleanPaint!.Rows[7].Value);
        TestAssert.True(graph["palette430.json"].MenuOptionCallback is null, "Palette deadline retires only its source wait");
        TestAssert.Equal("OFF", graph["rainbow0.json"].Rows[6].Value);
        TestAssert.Equal("ON", graph["rainbow0.json"].MenuBooleanPaint!.Rows[6].Value);
        TestAssert.Equal("Text", graph["slider-changed0.json"].Appearance!.BooleanDisplay);
        TestAssert.Equal("plain", graph["slider-rejected0.json"].MenuOptionCallback!.Kind);
        foreach (var frame in graph.Values) {
            using var json = JsonDocument.Parse(frame.Serialize());
            TestAssert.Equal("CONNECTED", frame.Context);
            TestAssert.True(!System.Text.Encoding.UTF8.GetString(frame.Serialize()).Contains("1234567890ABCDEF", StringComparison.Ordinal),
                "Local option metadata must not expose private source identity");
        }
    }

    public static void LocalExportOwnsExactlyTwelveFreshPacketsAndCannotOverwrite() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-local-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets"); TestAssert.Equal(12, LocalOptionFixtureExporter.Export(output));
            foreach (var pair in LocalOptionFixtureExporter.Build())
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, pair.Key)).SequenceEqual(pair.Value.Serialize()),
                    "Export must preserve the actual controller packet bytes");
            TestAssert.Throws<IOException>(() => LocalOptionFixtureExporter.Export(output));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
            TestAssert.Throws<ArgumentException>(() => LocalOptionFixtureExporter.Export("relative"));
            TestAssert.Equal(1, LocalOptionFixtureExporter.Run(new[] { "--export-local-option-fixtures" }));
        } finally { Directory.Delete(root, recursive: true); }
    }
}
