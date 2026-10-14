using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Only serialized controller snapshots become packets; captures stay beside the wire in the manifest
internal static class RejectedPreferencesFixtureExporter {
    private const string ManifestName = "rejected-preferences-manifest.json";
    private const string SourceCommit = "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c";
    private const string SourceSha256 = "5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11";

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-rejected-preferences-fixtures") {
                throw new ArgumentException("Expected --export-rejected-preferences-fixtures followed by a new absolute directory");
            }
            Console.WriteLine($"Exported {Export(arguments[1])} actual rejected-preference snapshots and one capture manifest");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Rejected preference export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");

        // Controller frames and manifest bytes validate before an output location is reserved
        var profiles = RejectedPreferencesFixtureProfiles.Build();
        var packets = profiles.Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        byte[] manifest = SerializeManifest(profiles);
        string staging = Path.Combine(parent, ".apparition-rejected-preferences-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            foreach (var packet in packets) WriteNew(Path.Combine(staging, packet.Name), packet.Bytes);
            WriteNew(Path.Combine(staging, ManifestName), manifest);
            Directory.Move(staging, destination);
            return packets.Length;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static byte[] SerializeManifest(IReadOnlyList<RejectedPreferencesFixtureProfile> profiles) {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        // Each source command keeps its original frame outside the unchanged Select envelope
        var commands = profiles.Where(profile => profile.Command is not null).Select(profile => {
            var command = profile.Command!;
            var capture = profile.Capture ?? throw new InvalidOperationException("A source Select requires its original capture");
            return new {
                profile.Graph,
                file = profile.Name,
                profile.Event,
                sequence = command.Sequence,
                input = WireInput(command.Input),
                capture = new {
                    file = capture.File,
                    capture.NavigationRevision,
                    capture.OpeningControlsRevision,
                    admissionRevision = capture.AdmissionRevision,
                    menuRevision = capture.MenuRevision,
                    capture.LastSequence,
                    selectedSourceIndex = capture.SelectedSourceIndex,
                    selectedSourceLabel = capture.SelectedSourceLabel
                },
                wire = new {
                    command.NavigationRevision,
                    command.OpeningControlsRevision,
                    command.LocalAdmissionRevision,
                    command.MenuRevision
                }
            };
        }).ToArray();

        // Frame timing and cursor values are observations from the actual controller snapshots
        var frames = profiles.Select(profile => new {
            file = profile.Name,
            profile.Graph,
            profile.Event,
            page = PageName(profile.Page),
            profile.ClockMilliseconds,
            profile.RelativeMilliseconds,
            selectedSourceIndex = profile.SelectedSourceIndex,
            selectedRow = profile.State.SelectedIndex >= 0 && profile.State.SelectedIndex < profile.State.Rows.Count
                ? profile.State.Rows[profile.State.SelectedIndex].Label : null,
            navigationRevision = profile.State.NavigationRevision,
            menuRevision = profile.State.MenuRevision,
            openingControlsRevision = OpeningRevision(profile.State),
            lastSequence = profile.State.LastSequence,
            commandSequence = profile.Command?.Sequence,
            sessionActionCount = profile.SessionActionCount
        }).ToArray();
        var document = new {
            graph = "rejected-preferences",
            source = new {
                commit = SourceCommit,
                file = "Menu/base.gsc",
                sha256 = SourceSha256
            },
            timing = new {
                rejectedBooleanRedrawMilliseconds = 180,
                rejectedBooleanReadyMilliseconds = 430,
                supportedCounterRedrawMilliseconds = 50,
                supportedCounterReadyMilliseconds = 250,
                evidence = "Manual TimeProvider snapshots record controller chronology; game VM scheduling and live-game timing are not asserted"
            },
            session = new {
                kind = "inert-scoped-test-session",
                processMemoryWriterUsed = false,
                actionCount = profiles.Max(profile => profile.SessionActionCount)
            },
            commands,
            frames
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, options);
    }

    private static ulong? OpeningRevision(StateDocument state) =>
        state.OpeningPolicies?.Revision ?? state.MainOpeningControls?.Revision;

    private static string PageName(MenuPage page) => page switch {
        MenuPage.Preferences => "preferences",
        _ => throw new InvalidOperationException("A rejected preference graph contains an unsupported page")
    };

    private static string WireInput(OverlayInput? input) => input switch {
        OverlayInput.Select => "select",
        _ => throw new InvalidOperationException("A rejected preference capture must describe a Select")
    };

    private static void WriteNew(string path, byte[] bytes) {
        // Fresh staging files prevent overwriting an earlier source capture
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        output.Write(bytes);
    }
}
