using System.Text.Json;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Serialized packet bytes come directly from each recorded controller snapshot
internal static class OpeningControlsUseFixtureExporter {
    private const string ManifestName = "opening-controls-use-manifest.json";
    private const string SourceCommit = "307e94c1b1d6537ba7a2a71b32a22d563d3ea25c";
    private const string BaseSourceSha256 = "5c92cf211e3d772e196cbc856fb3e0cb9ab46e620ab8b67d1f63ef13c2276d11";
    private const string MenuCustomizationSourceSha256 = "29767ad03de621b1c1491c46ec769dd352b05654f73452e8034cf320c497f7da";

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-opening-controls-use-fixtures") {
                throw new ArgumentException("Expected --export-opening-controls-use-fixtures followed by a new absolute directory");
            }
            Console.WriteLine($"Exported {Export(arguments[1])} actual Open Controls Use snapshots and one capture manifest");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Open Controls Use export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");

        IReadOnlyList<OpeningControlsUseFixtureProfile> profiles = OpeningControlsUseFixtureProfiles.Build();
        var packets = profiles.Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        byte[] manifest = SerializeManifest(profiles);
        string staging = Path.Combine(parent, ".apparition-open-controls-use-" + Guid.NewGuid().ToString("N"));
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

    internal static byte[] SerializeManifest(IReadOnlyList<OpeningControlsUseFixtureProfile> profiles) {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        // A command records the pre-write frame apart from its unchanged serialized wire fields
        var commands = profiles.Where(profile => profile.Command is not null).Select(profile => {
            var command = profile.Command!;
            var captured = profile.CapturedFrame ?? throw new InvalidOperationException("A source command requires its captured frame");
            if (profile.CapturedFrameName is null) throw new InvalidOperationException("A source command requires its captured frame name");
            return new {
                profile.Graph,
                file = profile.Name,
                profile.Event,
                sequence = command.Sequence,
                input = WireInput(command.Input ?? throw new InvalidOperationException("A source command requires its input")),
                capture = new {
                    file = profile.CapturedFrameName,
                    navigationRevision = captured.NavigationRevision,
                    openingControlsRevision = OpeningRevision(captured),
                    admissionRevision = captured.LocalMenuEligibility?.Revision,
                    menuRevision = captured.MenuRevision,
                    lastSequence = captured.LastSequence,
                    selectedSourceIndex = captured.SelectedIndex,
                    selectedSourceLabel = SourceLabel(captured)
                },
                wire = new {
                    navigationRevision = command.NavigationRevision,
                    openingControlsRevision = command.OpeningControlsRevision,
                    localAdmissionRevision = command.LocalAdmissionRevision,
                    menuRevision = command.MenuRevision
                }
            };
        }).ToArray();
        // Frame metadata describes the actual post-command or post-clock snapshot
        var frames = profiles.Select(profile => new {
            file = profile.Name,
            profile.Graph,
            profile.Event,
            page = PageName(profile.Page),
            profile.ClockMilliseconds,
            profile.RelativeMilliseconds,
            profile.SelectedSourceIndex,
            selectedRow = SourceLabel(profile.State),
            navigationRevision = profile.State.NavigationRevision,
            menuRevision = profile.State.MenuRevision,
            openingControlsRevision = OpeningRevision(profile.State),
            lastSequence = profile.State.LastSequence,
            commandSequence = profile.Command?.Sequence,
            sessionActionCount = profile.SessionActionCount
        }).ToArray();
        var document = new {
            graph = "opening-controls-use",
            source = new {
                commit = SourceCommit,
                baseFile = "Menu/base.gsc",
                baseSha256 = BaseSourceSha256,
                monitorSelectAndCallerWaitLines = "78-100",
                buttonCallbackAndSelectorLines = "1205-1263",
                menuCustomizationFile = "Menu/menu_customization.gsc",
                menuCustomizationSha256 = MenuCustomizationSourceSha256,
                openingControlsRowsLines = "16-35",
                disableQuickLines = "634-638"
            },
            timing = new {
                booleanRefreshAfterSelectMilliseconds = 180,
                booleanReadyAfterSelectMilliseconds = 430,
                plainSelectorReadyAfterSelectMilliseconds = 250,
                mechanicalChoiceWaitMilliseconds = 180,
                slot2Topology = "Applying Bind Slot 2 adds the source None row and changes navigation scope",
                evidence = "Manual TimeProvider snapshots record controller chronology; BO3 VM thread scheduling and live-game timing are not asserted"
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

    private static string? SourceLabel(StateDocument state) =>
        state.SelectedIndex >= 0 && state.SelectedIndex < state.Rows.Count
            ? state.Rows[state.SelectedIndex].Label : null;

    private static string PageName(MenuPage page) => page switch {
        MenuPage.OpenControls => "open-controls",
        MenuPage.Preferences => "preferences",
        _ => throw new InvalidOperationException("The Open Controls Use graph contains an unsupported page")
    };

    private static string WireInput(OverlayInput input) => input switch {
        OverlayInput.Select => "select",
        OverlayInput.NextChoice => "nextChoice",
        _ => throw new InvalidOperationException("The Open Controls Use manifest contains an unsupported command")
    };

    private static void WriteNew(string path, byte[] bytes) {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        output.Write(bytes);
    }
}
