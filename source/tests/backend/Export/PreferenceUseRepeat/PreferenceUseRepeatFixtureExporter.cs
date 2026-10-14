using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Packet bytes remain actual snapshots and command scope stays in a side manifest
internal static class PreferenceUseRepeatFixtureExporter {
    private const string ManifestName = "preference-use-manifest.json";
    private const string SourceCommit = "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c";
    private const string SourceSha256 = "5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11";

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-preference-use-repeat-fixtures") {
                throw new ArgumentException("Expected --export-preference-use-repeat-fixtures followed by a new absolute directory");
            }
            Console.WriteLine($"Exported {Export(arguments[1])} actual preference-use snapshots and one capture manifest");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Preference-use export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");

        // Build every controller transition before reserving the destination
        var profiles = PreferenceUseRepeatFixtureProfiles.Build();
        var packets = profiles.Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        byte[] manifest = SerializeManifest(profiles);
        string staging = Path.Combine(parent, ".apparition-preference-use-" + Guid.NewGuid().ToString("N"));
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

    private static byte[] SerializeManifest(IReadOnlyList<PreferenceUseRepeatFixtureProfile> profiles) {
        // The native reader keeps a fixed packet-size limit, so the side manifest stays compact
        var options = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        // Each Select keeps the exact frame scope captured immediately before consumption
        var commands = profiles.Where(profile => profile.OriginalCommand is not null)
            .Select(profile => {
                var command = profile.OriginalCommand!;
                var capture = profile.Capture ?? throw new InvalidOperationException("A source command requires its captured scope");
                return new {
                    profile.Graph,
                    file = profile.Name,
                    profile.Event,
                    sequence = command.Sequence,
                    input = WireInput(command.Input),
                    capture = new {
                        capture.NavigationRevision,
                        capture.OpeningControlsRevision,
                        capture.AdmissionRevision,
                        capture.MenuRevision,
                        capture.LastSequence,
                        capture.SelectedSourceIndex,
                        capture.SelectedSourceLabel
                    },
                    wire = new {
                        command.NavigationRevision,
                        command.OpeningControlsRevision,
                        command.LocalAdmissionRevision,
                        command.MenuRevision
                    }
                };
            }).ToArray();
        // Observed frame facts stay separate from the fields actually sent on each command
        var frames = profiles.Select(profile => new {
            file = profile.Name,
            profile.Graph,
            profile.Event,
            page = PageName(profile.Page),
            clockMilliseconds = profile.ClockMilliseconds,
            relativeMilliseconds = profile.RelativeMilliseconds,
            selectedSourceIndex = profile.SelectedSourceIndex,
            selectedRow = profile.State.SelectedIndex >= 0 && profile.State.SelectedIndex < profile.State.Rows.Count
                ? profile.State.Rows[profile.State.SelectedIndex].Label : null,
            lastSequence = profile.State.LastSequence,
            commandSequence = profile.OriginalCommand?.Sequence,
            sessionActionCount = profile.SessionActionCount
        }).ToArray();
        var document = new {
            graph = "preference-use-repeat",
            source = new {
                commit = SourceCommit,
                file = "Menu/base.gsc",
                sha256 = SourceSha256,
                booleanCallLines = "89-96",
                refreshLine = 94,
                designUsePath = new {
                    sliderDispatchLine = 85,
                    callerWaitLine = 99,
                    outerMonitorWaitLine = 168,
                    callerWaitMilliseconds = 200,
                    outerMonitorWaitMilliseconds = 50,
                    nominalUseCycleMilliseconds = 250,
                    handlerFile = "Menu/menu_customization.gsc",
                    handlerSha256 = "29767ad03de621b1c1491c46ec769dd352b05654f73452e8034cf320c497f7da",
                    handlerLines = "450-474",
                    unchangedValueReturnLine = 453
                }
            },
            timing = new {
                booleanPreference = new {
                    firstSelectAfterReadyMilliseconds = 0,
                    firstDisplayRefreshAfterSelectMilliseconds = 180,
                    firstReadyAfterSelectMilliseconds = 430,
                    secondSelectAfterFirstReadyMilliseconds = 0,
                    secondDisplayRefreshAfterSelectMilliseconds = 180,
                    secondReadyAfterSelectMilliseconds = 430
                },
                changedDesign = new {
                    controllerRedrawMilliseconds = 50,
                    controllerReadyMilliseconds = 250
                },
                limits = "Controller snapshots and fake-clock timing only; source VM thread scheduling and live-game behavior are not asserted"
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

    private static string PageName(MenuPage page) => page switch {
        MenuPage.Preferences => "preferences",
        _ => throw new InvalidOperationException("The preference-use graph contains an unsupported page")
    };

    private static string WireInput(OverlayInput? input) => input switch {
        OverlayInput.Select => "select",
        _ => throw new InvalidOperationException("The preference-use manifest contains a non-Select source command")
    };

    private static void WriteNew(string path, byte[] bytes) {
        // Staging entries must never replace an existing capture
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        output.Write(bytes);
    }
}
