using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Callback evidence uses its own real-controller graph, separate from inert preview profiles
internal static class CallbackFixtureExporter {
    public const int ProfileCount = 14;
    public static IReadOnlyDictionary<string, StateDocument> Build() {
        var fixtures = new Dictionary<string, StateDocument>(StringComparer.Ordinal);
        void Add(string name, ScopedOpeningFixture rig, bool foreground = true) {
            // The fixture's source is connected before any callback, never a rewritten packet
            if (!rig.Session.State.Connected || rig.Session.State.Preview || rig.Session.Executed.Count != 1) {
                throw new InvalidOperationException("Callback evidence requires one controlled connected action");
            }
            var frame = rig.Controller.Snapshot();
            fixtures.Add(name + ".json", foreground ? frame : StateFocusProjection.Apply(frame, false));
        }
        var immediate = MenuOptionCallbackControllerTests.StartBoolean();
        Add("boolean-pending-immediate", immediate);
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(179)); Add("boolean179", immediate);
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("boolean180", immediate);
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(249)); Add("boolean429", immediate);
        immediate.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("boolean430-ready", immediate);

        var busy = MenuOptionCallbackControllerTests.StartBoolean(held: true);
        Add("boolean-pending-busy", busy);
        busy.Clock.Advance(TimeSpan.FromMilliseconds(180)); Add("boolean180-busy", busy);
        busy.Clock.Advance(TimeSpan.FromMilliseconds(250)); Add("boolean430-busy", busy);
        // The saved packets own their rows before the controlled task is released
        busy.Session.PendingAction!.SetResult(true);
        busy.Controller.ObserveCompletion();

        var plain = StartPlainLobby(); Add("plain-lobby-cooldown0", plain);
        plain.Clock.Advance(TimeSpan.FromMilliseconds(249)); Add("plain-lobby-cooldown249", plain);
        plain.Clock.Advance(TimeSpan.FromMilliseconds(1)); Add("plain-lobby250-ready", plain);

        var hidden = MenuOptionCallbackControllerTests.StartBoolean();
        Add("focus-hidden-pending", hidden, foreground: false);
        var replaced = MenuOptionCallbackControllerTests.StartBoolean();
        replaced.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add("replaced-owner-closed", replaced);
        var shutdown = MenuOptionCallbackControllerTests.StartBoolean();
        shutdown.Controller.RequestShutdown(); Add("shutdown-no-callback", shutdown);
        if (fixtures.Count != ProfileCount) throw new InvalidOperationException("Callback fixture graph is incomplete");
        return fixtures;
    }

    private static ScopedOpeningFixture StartPlainLobby() {
        var rig = new ScopedOpeningFixture();
        // Real Main opening owns its redraw and 550 ms monitor pause before navigation
        rig.Open(OverlayInput.OpenMain);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50)); rig.Controller.Snapshot();
        rig.Clock.Advance(TimeSpan.FromMilliseconds(550)); rig.Controller.Snapshot();
        var root = rig.Controller.Snapshot();
        int target = root.Rows.ToList().FindIndex(row => row.Label == "All Players Menu");
        if (target < 0) throw new InvalidOperationException("The actual All Players route is missing");
        for (int step = 0; step < root.Rows.Count && rig.Navigation.SelectedIndex != target; ++step) rig.Send(OverlayInput.Down);
        if (rig.Navigation.SelectedIndex != target) throw new InvalidOperationException("The actual All Players route is unreachable");
        rig.Send(OverlayInput.Select);
        var page = rig.Controller.Snapshot();
        int ammo = page.Rows.ToList().FindIndex(row => row.Label == "Infinite Ammo (IW lobby)");
        if (ammo < 0) throw new InvalidOperationException("The named IW extension is missing");
        while (rig.Navigation.SelectedIndex != ammo) rig.Send(OverlayInput.Down);
        page = rig.Controller.Snapshot();
        if (page.Rows.Count == 0 || page.Rows[page.SelectedIndex].Label != "Infinite Ammo (IW lobby)" || !page.Rows[page.SelectedIndex].Enabled) {
            throw new InvalidOperationException("The actual lobby adapter action is missing");
        }
        rig.Send(OverlayInput.Select); rig.Controller.StartQueuedAction();
        if (rig.Session.Executed.Count != 1 || rig.Session.Executed[0].Kind != MenuActionKind.ToggleLobbyAmmo) {
            throw new InvalidOperationException("The actual lobby callback was not dispatched");
        }
        return rig;
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-callback-fixtures") {
                throw new ArgumentException("Expected --export-callback-fixtures followed by a new absolute directory");
            }
            int count = Export(arguments[1]);
            Console.WriteLine($"Exported {count} offline connected callback snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Callback fixture export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Callback directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Callback directory must not already exist");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("A parent directory is required");
        if (!Directory.Exists(parent)) throw new IOException("Callback parent directory must exist");
        // Build before reserving output so a controller failure leaves no partial export
        var fixtures = Build();
        string staging = Path.Combine(parent, ".apparition-callback-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            foreach (var fixture in fixtures.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
                using var output = new FileStream(Path.Combine(staging, fixture.Key), FileMode.CreateNew, FileAccess.Write);
                output.Write(fixture.Value.Serialize());
            }
            // Moving the complete graph cannot merge with a concurrent destination
            Directory.Move(staging, destination);
            return fixtures.Count;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
