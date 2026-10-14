using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// The state packets stay byte-exact while command capture stays in its own manifest
internal static class OrdinarySourceLevelFixtureExporter {
    private const string OutputManifest = "source-level-manifest.json";
    private const string Graph = "living-quick-use";
    private const string SourceCommit = "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c";
    private const string SourceReferenceSha256 = "5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11";

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-ordinary-source-level-fixtures")
                throw new ArgumentException("Expected --export-ordinary-source-level-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} actual source-level packets and one capture manifest");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Source-level export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");

        var profiles = OrdinarySourceLevelFixtureProfiles.Build();
        var packets = profiles.Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        byte[] manifest = SerializeManifest(profiles);
        string staging = Path.Combine(parent, ".apparition-source-level-" + Guid.NewGuid().ToString("N"));
        // Publish the complete packet set with one directory move
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            foreach (var packet in packets) WriteNew(Path.Combine(staging, packet.Name), packet.Bytes);
            WriteNew(Path.Combine(staging, OutputManifest), manifest);
            Directory.Move(staging, destination);
            return packets.Length;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static byte[] SerializeManifest(IReadOnlyList<OrdinarySourceLevelFixtureProfile> profiles) {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        // Keep captured admission scope separate from the original wire envelope
        var commands = profiles.Where(profile => profile.OriginalCommand is not null)
            .GroupBy(profile => profile.OriginalCommand!.Sequence)
            .Select(group => group.First()).OrderBy(profile => profile.OriginalCommand!.Sequence)
            .Select(profile => {
                var command = profile.OriginalCommand!;
                var capture = profile.Capture ?? throw new InvalidOperationException("An original command requires its captured source scope");
                return new {
                    sequence = command.Sequence,
                    input = WireInput(command.Input),
                    capture = new {
                        capture.NavigationRevision,
                        capture.OpeningControlsRevision,
                        capture.AdmissionRevision,
                        capture.MenuRevision,
                        capture.LastSequence
                    },
                    wire = new {
                        command.NavigationRevision,
                        command.OpeningControlsRevision,
                        command.LocalAdmissionRevision,
                        command.MenuRevision
                    }
                };
            }).ToArray();
        var frames = profiles.Select(profile => new {
            file = profile.Name,
            profile.Event,
            profile.ClockMilliseconds,
            profile.RelativeMilliseconds,
            lastSequence = profile.State.LastSequence,
            commandSequence = profile.OriginalCommand?.Sequence,
            sessionActions = profile.SessionActions.Select(action => action.ToString()).ToArray()
        }).ToArray();
        var document = new {
            graph = Graph,
            source = new {
                commit = SourceCommit,
                file = "Menu/base.gsc",
                sha256 = SourceReferenceSha256,
                branchLines = "66-163",
                quickOpenLines = "41-47"
            },
            timing = new {
                quickReadyAfterOpenMilliseconds = 550,
                booleanRefreshAfterSelectMilliseconds = 180,
                booleanReadyAfterSelectMilliseconds = 430,
                limits = "Controller and fake-session timings only; GSC VM callback-thread ordering and live-game acceptance are not asserted"
            },
            session = new { kind = "inert-test-session", processMemoryWriterUsed = false },
            commands,
            frames
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, options);
    }

    private static string WireInput(OverlayInput? input) => input switch {
        OverlayInput.OpenQuick => "openQuick",
        OverlayInput.Select => "select",
        _ => throw new InvalidOperationException("The source-level manifest contains an unsupported input")
    };

    private static void WriteNew(string path, byte[] bytes) {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        output.Write(bytes);
    }
}
