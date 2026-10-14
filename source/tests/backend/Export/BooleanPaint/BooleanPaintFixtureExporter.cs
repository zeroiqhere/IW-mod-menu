using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Every packet comes from the real controller with an inert connected session
internal static class BooleanPaintFixtureExporter {
    public const int ProfileCount = 8 + CohortBooleanPaintProfiles.ProfileCount;
    public static IReadOnlyDictionary<string, StateDocument> Build() {
        var fixtures = new Dictionary<string, StateDocument>(StringComparer.Ordinal);
        void Add(string name, ScopedOpeningFixture rig) {
            if (!rig.Session.State.Connected || rig.Session.State.Preview) {
                throw new InvalidOperationException("Boolean paint evidence requires a connected controlled session");
            }
            fixtures.Add(name + ".json", rig.Controller.Snapshot());
        }
        var opening = new ScopedOpeningFixture(); opening.Open(OverlayInput.OpenQuick);
        Add("quick-open", opening);
        var immediate = MenuOptionCallbackControllerTests.StartBoolean();
        immediate.Session.State = immediate.Session.State with { PersonalGodmodeValue = "ON" };
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(179)); Add("boolean179-current-on", immediate);
        // Focus changes project the owned frame without changing any feature fact
        fixtures.Add("focus-hidden179.json", StateFocusProjection.Apply(fixtures["boolean179-current-on.json"], false));
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("boolean180-current-on", immediate);
        var late = MenuOptionCallbackControllerTests.StartBoolean(held: true);
        late.Clock.Advance(TimeSpan.FromMilliseconds(180)); Add("late180-busy", late);
        late.Session.State = late.Session.State with { PersonalGodmodeValue = "ON" };
        late.Session.PendingAction!.SetResult(true);
        late.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("late430-complete", late);
        late.Send(OverlayInput.Down); Add("late-scroll", late);
        late.Send(OverlayInput.Back); late.Clock.Advance(TimeSpan.FromMilliseconds(250)); late.Open(OverlayInput.OpenQuick); Add("late-reopened", late);
        foreach (var packet in CohortBooleanPaintProfiles.Build()) fixtures.Add(packet.Key, packet.Value);
        if (fixtures.Count != ProfileCount) throw new InvalidOperationException("Boolean paint fixture graph is incomplete");
        return fixtures;
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-boolean-paint-fixtures") {
                throw new ArgumentException("Expected --export-boolean-paint-fixtures followed by a new absolute directory");
            }
            Console.WriteLine($"Exported {Export(arguments[1])} offline connected boolean paint snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Boolean paint fixture export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture directory must not already exist");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("A parent directory is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent directory must exist");
        var fixtures = Build();
        string staging = Path.Combine(parent, ".apparition-boolean-paint-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            // A complete graph moves once; a concurrent destination can never be overwritten
            foreach (var fixture in fixtures.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
                using var output = new FileStream(Path.Combine(staging, fixture.Key), FileMode.CreateNew, FileAccess.Write);
                output.Write(fixture.Value.Serialize());
            }
            Directory.Move(staging, destination);
            return fixtures.Count;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
