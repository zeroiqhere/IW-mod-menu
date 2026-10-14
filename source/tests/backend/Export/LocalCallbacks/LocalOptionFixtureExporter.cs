using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The same real controller graph drives both native readers and CPU-rendered evidence
internal static class LocalOptionFixtureExporter {
    public const int ProfileCount = 12;
    public static IReadOnlyDictionary<string, StateDocument> Build() {
        var fixtures = new Dictionary<string, StateDocument>(StringComparer.Ordinal);
        void Add(string name, ScopedOpeningFixture rig) {
            if (!rig.Session.State.Connected || rig.Session.State.Preview || rig.Session.Executed.Count != 0)
                throw new InvalidOperationException("Local evidence requires a connected inert session without gameplay dispatch");
            fixtures.Add(name + ".json", rig.Controller.Snapshot());
        }
        var palette = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors);
        LocalOptionCallbackControllerTests.SelectIndex(palette, 6); Add("palette-before", palette);
        LocalOptionCallbackControllerTests.SelectIndex(palette, 7); palette.Send(OverlayInput.Select); Add("palette0", palette);
        palette.Clock.Advance(TimeSpan.FromMilliseconds(179)); Add("palette179", palette);
        palette.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("palette180", palette);
        palette.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("palette430", palette);
        var rainbow = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors);
        LocalOptionCallbackControllerTests.SelectIndex(rainbow, 36); rainbow.Send(OverlayInput.Select); Add("rainbow0", rainbow);
        rainbow.Clock.Advance(TimeSpan.FromMilliseconds(180)); Add("rainbow180", rainbow);
        var slider = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(slider, 1); slider.StageChoice(OverlayInput.NextChoice);
        slider.Send(OverlayInput.Select); Add("slider-changed0", slider);
        slider.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("slider250", slider);
        var rejected = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Preferences);
        LocalOptionCallbackControllerTests.SelectIndex(rejected, 10); rejected.Send(OverlayInput.Select);
        rejected.Clock.Advance(TimeSpan.FromMilliseconds(430)); rejected.Controller.Snapshot();
        LocalOptionCallbackControllerTests.SelectIndex(rejected, 1); rejected.Send(OverlayInput.Select); Add("slider-rejected0", rejected);
        var reset = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.Instructions);
        LocalOptionCallbackControllerTests.SelectIndex(reset, 5); reset.Send(OverlayInput.Select); Add("reset0", reset);
        reset.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("reset250", reset);
        if (fixtures.Count != ProfileCount) throw new InvalidOperationException("Local option fixture graph is incomplete");
        return fixtures;
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-local-option-fixtures")
                throw new ArgumentException("Expected --export-local-option-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} connected inert local option snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Local option export failed: " + exception.Message); return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture directory must not already exist");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("A parent directory is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent directory must exist");
        var fixtures = Build(); string staging = Path.Combine(parent, ".apparition-local-options-" + Guid.NewGuid().ToString("N"));
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
