using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The same real controller graph drives both native readers and CPU-rendered evidence
internal static class InstructionOptionFixtureExporter {
    public const int ProfileCount = 16;
    public static IReadOnlyDictionary<string, StateDocument> Build() {
        var fixtures = new Dictionary<string, StateDocument>(StringComparer.Ordinal);
        void Add(string name, ScopedOpeningFixture rig) {
            if (!rig.Session.State.Connected || rig.Session.State.Preview || rig.Session.Executed.Count != 0)
                throw new InvalidOperationException("Local evidence requires a connected inert session without gameplay dispatch");
            fixtures.Add(name + ".json", rig.Controller.Snapshot());
        }
        // Each graph uses a real admitted visit without claiming submenu thread scheduling
        ScopedOpeningFixture Ready() => LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Instructions);
        void Select(ScopedOpeningFixture rig, int index) => LocalOptionCallbackControllerTests.SelectIndex(rig, index);
        var disable = Ready(); Add("instructions-before", disable);
        disable.Send(OverlayInput.Select); Add("disable0", disable);
        disable.Clock.Advance(TimeSpan.FromMilliseconds(179)); Add("disable179", disable);
        disable.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("disable180", disable);
        disable.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("disable430", disable);
        foreach (var (name, index) in new[] { ("alternate", 1), ("background", 2), ("adaptive", 3) }) {
            var rig = Ready(); Select(rig, index); rig.Send(OverlayInput.Select); Add(name + "0", rig);
            rig.Clock.Advance(TimeSpan.FromMilliseconds(180)); Add(name + "180", rig);
        }
        var rejected = Ready(); Select(rejected, 1); rejected.Send(OverlayInput.Select);
        rejected.Clock.Advance(TimeSpan.FromMilliseconds(430)); rejected.Controller.Snapshot();
        Select(rejected, 3); rejected.Send(OverlayInput.Select); Add("adaptive-rejected0", rejected);
        rejected.Clock.Advance(TimeSpan.FromMilliseconds(180)); Add("adaptive-rejected180", rejected);
        rejected.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("adaptive-rejected430", rejected);
        var cleared = Ready(); Select(cleared, 3); cleared.Send(OverlayInput.Select);
        cleared.Clock.Advance(TimeSpan.FromMilliseconds(430)); cleared.Controller.Snapshot();
        Select(cleared, 1); cleared.Send(OverlayInput.Select); Add("alternate-clears0", cleared);
        cleared.Clock.Advance(TimeSpan.FromMilliseconds(180)); Add("alternate-clears180", cleared);
        if (fixtures.Count != ProfileCount) throw new InvalidOperationException("Local option fixture graph is incomplete");
        return fixtures;
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-instruction-option-fixtures")
                throw new ArgumentException("Expected --export-instruction-option-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} connected inert local option snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Instruction option export failed: " + exception.Message); return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture directory must not already exist");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("A parent directory is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent directory must exist");
        var fixtures = Build(); string staging = Path.Combine(parent, ".apparition-instruction-options-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            // Only a complete producer graph is moved; concurrent output never gets merged
            foreach (var fixture in fixtures.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
                using var output = new FileStream(Path.Combine(staging, fixture.Key), FileMode.CreateNew, FileAccess.Write);
                output.Write(fixture.Value.Serialize());
            }
            Directory.Move(staging, destination); return fixtures.Count;
        } finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }
}
