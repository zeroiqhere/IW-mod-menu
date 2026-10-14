using System.Text.Json;

namespace Backend.Tests;

internal static class InstructionOptionFixtureExporterTests {
    public static void ActualInstructionGraphPreservesHandlerRefreshAndMonitorRedrawSeparately() {
        var graph = InstructionOptionFixtureExporter.Build(); TestAssert.Equal(16, graph.Count);
        var before = graph["instructions-before.json"]; var disable = graph["disable0.json"];
        TestAssert.Equal(4, before.MenuBooleanPaint!.Rows.Count);
        TestAssert.Equal(before.MenuRevision!.Value + 1, disable.MenuRevision!.Value);
        TestAssert.Equal("ON", disable.MenuBooleanPaint!.Rows[0].Value);
        TestAssert.Equal(disable.MenuRevision!.Value + 1, graph["disable180.json"].MenuRevision!.Value);
        TestAssert.Equal("OFF", graph["alternate0.json"].MenuBooleanPaint!.Rows[1].Value);
        TestAssert.Equal("ON", graph["alternate0.json"].Rows[1].Value);
        TestAssert.Equal("ON", graph["alternate180.json"].MenuBooleanPaint!.Rows[1].Value);
        TestAssert.True(graph["adaptive-rejected0.json"].Rows[3].Enabled, "The source rejects inside its selectable callback");
        TestAssert.Equal("OFF", graph["adaptive-rejected180.json"].Rows[3].Value);
        TestAssert.True(graph["adaptive-rejected430.json"].MenuOptionCallback is null, "Rejection still has a finite source caller wait");
        TestAssert.Equal("ON", graph["alternate-clears0.json"].MenuBooleanPaint!.Rows[3].Value);
        TestAssert.Equal("OFF", graph["alternate-clears0.json"].Rows[3].Value);
        foreach (var frame in graph.Values) {
            using var json = JsonDocument.Parse(frame.Serialize());
            TestAssert.Equal("CONNECTED", frame.Context);
            TestAssert.True(!System.Text.Encoding.UTF8.GetString(frame.Serialize()).Contains("1234567890ABCDEF", StringComparison.Ordinal),
                "Local option metadata must not expose private source identity");
        }
    }

    public static void InstructionExportOwnsExactlySixteenFreshPacketsAndCannotOverwrite() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-instruction-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string output = Path.Combine(root, "packets"); TestAssert.Equal(16, InstructionOptionFixtureExporter.Export(output));
            foreach (var pair in InstructionOptionFixtureExporter.Build())
                TestAssert.True(File.ReadAllBytes(Path.Combine(output, pair.Key)).SequenceEqual(pair.Value.Serialize()),
                    "Export must preserve the actual controller packet bytes");
            TestAssert.Throws<IOException>(() => InstructionOptionFixtureExporter.Export(output));
            TestAssert.Equal(1, Directory.GetDirectories(root).Length);
            TestAssert.Throws<ArgumentException>(() => InstructionOptionFixtureExporter.Export("relative"));
            TestAssert.Equal(1, InstructionOptionFixtureExporter.Run(new[] { "--export-instruction-option-fixtures" }));
        } finally { Directory.Delete(root, recursive: true); }
    }
}
