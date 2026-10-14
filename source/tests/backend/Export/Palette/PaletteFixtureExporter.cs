using Apparition.Backend.Ipc;

namespace Backend.Tests;

// The exporter is a test-only graph and cannot dispatch a game callback
internal static class PaletteFixtureExporter {
    public const int ProfileCount = 192;
    public sealed record Profile(string Name, long ClockMilliseconds, StateDocument State);
    public static IReadOnlyList<Profile> Build() => PaletteFixtureProfiles.Build();

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-palette-fixtures")
                throw new ArgumentException("Expected --export-palette-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} connected inert palette snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Palette export failed: " + exception.Message);
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
        string staging = Path.Combine(parent, ".apparition-palette-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            // A complete graph is published once; existing evidence is never merged or replaced
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
