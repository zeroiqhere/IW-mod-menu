using System.Security.Cryptography;

namespace Backend.Tests;

// Literal boundaries remain independent of the exporter command sequence and private targets
internal static class MenuMonitorContinuationFixtureTests {
    public static void ActualMechanicalGraphOwnsItsResultsAndPreservesFiniteSourceDeadlines() {
        var graph = MenuMonitorContinuationFixtureExporter.Build();
        TestAssert.Equal(55, graph.Count); TestAssert.Equal(55, graph.Select(row => row.Name).Distinct().Count());
        TestAssert.Equal(8, graph.Select(row => row.Graph).Distinct().Count());
        var packets = graph.ToDictionary(row => row.Name);
        foreach (var expected in new[] { ("choice-next0.json", "choice", true), ("choice-next179.json", "choice", true),
            ("choice-ready180.json", "choice", false), ("noop-next49.json", "choiceNoop", true), ("noop-ready50.json", "choiceNoop", false),
            ("back-child249.json", "back", true), ("back-parent250.json", "back", true), ("back-root749.json", "back", true),
            ("back-root750.json", "back", false), ("close-child589.json", "back", true), ("close-child590.json", "back", false),
            ("jump249.json", "back", true), ("jump250.json", "back", false), ("rollback189.json", "choice", true),
            ("rollback190.json", "choice", false), ("ammo-choice179.json", "choice", true), ("ammo-ready180.json", "choice", false) }) {
            var state = packets[expected.Item1].State;
            TestAssert.Equal(expected.Item2, MenuMonitorContinuationTests.Receipt(state).Kind);
            TestAssert.Equal(expected.Item3, state.MenuMonitorWait is not null);
            TestAssert.True(state.MenuUseContinuation is null && state.MenuOptionCallback is null, "Mechanical receipts cannot borrow Use callback authority");
        }
        TestAssert.True(!packets["close-child590.json"].State.IsOpen && !packets["close-child590.json"].State.IsRootMenu,
            "The real child Close must not be rewritten as a root frame");
        TestAssert.True(!packets["back-root750.json"].State.IsOpen && packets["back-root750.json"].State.IsRootMenu, "Root Back owns its actual closed result");
        TestAssert.True(packets["choice-use360.json"].State.MenuMonitorContinuation is null
            && packets["choice-use360.json"].State.MenuOptionCallback?.Kind == "plain", "The lower Use branch begins its own actual callback");
        foreach (string name in new[] { "withdraw-focus180.json", "withdraw-return180.json", "withdraw-nav180.json" })
            TestAssert.True(packets[name].State.MenuMonitorContinuation is null, "Withdrawal must not publish a ready mechanical ticket");
        var ammo = packets["ammo-choice0.json"].State; var row = ammo.Rows[ammo.SelectedIndex];
        TestAssert.True(!row.Enabled && row.Value == "UNAVAILABLE", "Unsupported per-player ammo remains unavailable during its source slider pause");
        foreach (var profile in graph) {
            var receipt = profile.State.MenuMonitorContinuation;
            TestAssert.True(receipt is null || receipt.Sequence <= profile.State.LastSequence, "Every receipt keeps the original acknowledged command");
            profile.State.Serialize();
        }
    }
    public static void ActualProgramExportsOnlyTheNewMechanicalGraphAndRejectsOverwrite() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-mechanical-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        string directory = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-monitor-continuation-fixtures", directory }));
            var files = Directory.GetFiles(directory); TestAssert.Equal(55, files.Length);
            var original = files.ToDictionary(file => Path.GetFileName(file)!, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            TestAssert.Equal(1, Program.Main(new[] { "--export-monitor-continuation-fixtures", directory }));
            foreach (string file in files) TestAssert.Equal(original[Path.GetFileName(file)!], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
        } finally { Directory.Delete(parent, recursive: true); }
    }
}
