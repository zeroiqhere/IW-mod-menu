using Apparition.Backend.Ipc;

namespace Backend.Tests;

// The named graph publishes only actual controller frames and leaves prior inventories separate
internal static class ClosedLevelOpeningFixtureExporter {
    public const int ProfileCount = 57;
    public sealed record Profile(string Name, string Graph, long ClockMilliseconds, long RelativeMilliseconds,
        StateDocument State, CommandEnvelope? Command, ClosedLevelOpeningFixtureProfiles.CommandScope? CommandScope, bool ForegroundAllowed);
    public static IReadOnlyList<Profile> Build() => ClosedLevelOpeningFixtureProfiles.Build();
    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-closed-level-opening-fixtures")
                throw new ArgumentException("Expected --export-closed-level-opening-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} actual closed opening snapshots"); return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Closed opening export failed: " + exception.Message); return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");
        // All real-controller packets validate before any destination is reserved
        var packets = Build().Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        string staging = Path.Combine(parent, ".apparition-closed-opening-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            foreach (var packet in packets) {
                using var output = new FileStream(Path.Combine(staging, packet.Name), FileMode.CreateNew, FileAccess.Write);
                output.Write(packet.Bytes);
            }
            Directory.Move(staging, destination); return packets.Length;
        } catch { Directory.Delete(staging, recursive: true); throw; }
    }
}
