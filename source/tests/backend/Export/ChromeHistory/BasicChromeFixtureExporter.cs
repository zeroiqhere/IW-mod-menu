using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Every packet comes from one actual controller chronology with an inert verified owner
internal static class BasicChromeFixtureExporter {
    public const int ProfileCount = 16;
    public sealed record Profile(string Name, long ClockMilliseconds, StateDocument State);

    public static IReadOnlyList<Profile> Build() {
        var rig = new MenuChromeFixture();
        var profiles = new List<Profile>();
        void Add(string name) {
            rig.NoGameWrites();
            profiles.Add(new(name + ".json", rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, rig.Current()));
        }
        rig.SettleMain();
        rig.Preferences();
        rig.Highlight("Design");
        // Four staged choices reach the source Basic design without injecting a preference
        for (int choice = 0; choice < 4; choice++) rig.StageChoice(OverlayInput.NextChoice);
        rig.Send(OverlayInput.Select);
        rig.Wait(50);
        rig.Wait(200);
        rig.Send(OverlayInput.Back);
        rig.Select("Main Design Color");
        Add("basic-black");

        rig.Select("Smooth Rainbow");
        Add("basic-rainbow0");
        rig.Wait(180);
        Add("basic-rainbow180");
        rig.Wait(250);
        Add("basic-rainbow430");
        rig.Select("Red");
        Add("basic-fixed0");
        rig.Wait(180);
        Add("basic-fixed180");
        rig.Wait(250);
        Add("basic-fixed430");
        rig.Send(OverlayInput.Back);
        Add("basic-parent");
        rig.Wait(250);
        rig.Send(OverlayInput.Toggle);
        Add("basic-closed");
        rig.Open();
        Add("basic-reopening0");
        rig.Wait(50);
        Add("basic-reopened50");
        rig.Wait(550);
        rig.Select("Width Editor");
        Add("basic-width-locked");
        rig.Send(OverlayInput.FinishWidthEditor);
        Add("basic-unlock0");
        // Deliberately omit the intermediate return frame to prove one late recreation
        rig.Wait(150);
        Add("basic-unlock-late150");
        rig.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add("basic-owner-retired");
        rig.Open();
        rig.Wait(50);
        Add("basic-owner-reopened50");
        if (profiles.Count != ProfileCount) throw new InvalidOperationException("The chrome chronology is incomplete");
        return profiles.AsReadOnly();
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-basic-chrome-fixtures")
                throw new ArgumentException("Expected --export-basic-chrome-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} inert Basic chrome snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Basic chrome export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture directory must not already exist");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("A parent directory is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent directory must exist");
        var profiles = Build();
        string staging = Path.Combine(parent, ".apparition-basic-chrome-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            // Publish only a complete producer graph and never merge concurrent output
            foreach (var profile in profiles) {
                using var output = new FileStream(Path.Combine(staging, profile.Name), FileMode.CreateNew, FileAccess.Write);
                output.Write(profile.State.Serialize());
            }
            Directory.Move(staging, destination);
            return profiles.Count;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
