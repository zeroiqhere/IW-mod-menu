using System.Text.Json;
using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class BooleanPaintFixtureExporterTests {
    public static void ActualProducerGraphKeepsPaintSeparateFromCurrentFactsAndTiming() {
        var graph = BooleanPaintFixtureExporter.Build();
        TestAssert.Equal(BooleanPaintFixtureExporter.ProfileCount, graph.Count);
        var pending = graph["boolean179-current-on.json"];
        var redraw = graph["boolean180-current-on.json"];
        var late = graph["late430-complete.json"];
        TestAssert.Equal("ON", pending.Rows[0].Value); TestAssert.Equal("OFF", pending.MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal("ON", redraw.MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal("UNKNOWN", graph["late180-busy.json"].Rows[0].Value);
        TestAssert.Equal("ON", late.Rows[0].Value); TestAssert.Equal("UNKNOWN", late.MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal(late.MenuBooleanPaint.Revision, graph["late-scroll.json"].MenuBooleanPaint!.Revision);
        TestAssert.Equal("ON", graph["late-reopened.json"].MenuBooleanPaint!.Rows[0].Value);
        TestAssert.True(graph["focus-hidden179.json"].MenuBooleanPaint is null, "Focus projection cannot publish active paint");
        foreach (var frame in graph.Values) {
            using var json = JsonDocument.Parse(frame.Serialize());
            TestAssert.True(frame.Context.StartsWith("CONNECTED", StringComparison.Ordinal)
                && !System.Text.Encoding.UTF8.GetString(frame.Serialize()).Contains("1234567890ABCDEF", StringComparison.Ordinal),
                "The real producer graph must remain connected and keep private identity off the wire");
        }
    }

    public static void ExportRejectsExistingDestinationsAndPublishesExactlyTheOwnedGraph() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-paint-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string target = Path.Combine(root, "packets");
            TestAssert.Equal(BooleanPaintFixtureExporter.ProfileCount, BooleanPaintFixtureExporter.Export(target));
            var expected = BooleanPaintFixtureExporter.Build();
            TestAssert.Equal(BooleanPaintFixtureExporter.ProfileCount, Directory.GetFiles(target).Length);
            foreach (var pair in expected) TestAssert.True(File.ReadAllBytes(Path.Combine(target, pair.Key)).SequenceEqual(pair.Value.Serialize()),
                "The exporter cannot rewrite controller packets");
            TestAssert.Throws<IOException>(() => BooleanPaintFixtureExporter.Export(target));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
            TestAssert.Throws<ArgumentException>(() => BooleanPaintFixtureExporter.Export("relative"));
            TestAssert.Equal(1, BooleanPaintFixtureExporter.Run(new[] { "--export-boolean-paint-fixtures" }));
        } finally { Directory.Delete(root, recursive: true); }
    }
}
