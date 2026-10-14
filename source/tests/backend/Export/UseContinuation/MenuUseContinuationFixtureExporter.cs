using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Every packet is built by the actual controller; input release is a separate native test schedule
internal static class MenuUseContinuationFixtureExporter {
    public const int ProfileCount = 49;
    public sealed record Profile(string Name, string Graph, long ClockMilliseconds, StateDocument State);
    public static IReadOnlyList<Profile> Build() {
        var profiles = new List<Profile>();
        void Add(string name, string graph, MenuChromeFixture rig, int executed = 0) {
            TestAssert.Equal(executed, rig.Session.Executed.Count);
            profiles.Add(new(name + ".json", graph, rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, rig.Current()));
        }
        MenuChromeFixture Page(string name) {
            var rig = new MenuChromeFixture(); rig.SettleMain(); rig.Select("Menu Customization"); rig.Select(name); return rig;
        }
        var plain = Page("Menu Preferences"); plain.Highlight("Bool Display");
        Add("plain-before0", "plain", plain); plain.Send(OverlayInput.Select); Add("plain-first0", "plain", plain);
        plain.Wait(249); Add("plain-first249", "plain", plain); plain.Wait(1); Add("plain-first250", "plain", plain);
        plain.Send(OverlayInput.Select); Add("plain-second250", "plain", plain);
        plain.Wait(249); Add("plain-second499", "plain", plain); plain.Wait(1); Add("plain-second500", "plain", plain);
        plain.Wait(250); Add("plain-release750", "plain", plain);

        var boolean = Page("Main Design Color"); boolean.Highlight("Red");
        Add("boolean-before0", "boolean", boolean); boolean.Send(OverlayInput.Select); Add("boolean-first0", "boolean", boolean);
        boolean.Wait(179); Add("boolean-first179", "boolean", boolean); boolean.Wait(1); Add("boolean-first180", "boolean", boolean);
        boolean.Wait(249); Add("boolean-first429", "boolean", boolean); boolean.Wait(1); Add("boolean-first430", "boolean", boolean);
        boolean.Send(OverlayInput.Select); Add("boolean-second430", "boolean", boolean);
        boolean.Wait(179); Add("boolean-second609", "boolean", boolean); boolean.Wait(1); Add("boolean-second610", "boolean", boolean);
        boolean.Wait(249); Add("boolean-second859", "boolean", boolean); boolean.Wait(1); Add("boolean-second860", "boolean", boolean);
        boolean.Wait(140); Add("boolean-release1000", "boolean", boolean);

        var instructions = Page("Menu Instructions"); instructions.Highlight("Disable");
        Add("instructions-before0", "instructions", instructions);
        instructions.Send(OverlayInput.Select); Add("instructions-first0", "instructions", instructions);
        instructions.Wait(180); Add("instructions-first180", "instructions", instructions);
        instructions.Wait(250); Add("instructions-first430", "instructions", instructions);
        instructions.Send(OverlayInput.Select); Add("instructions-second430", "instructions", instructions);
        instructions.Wait(180); Add("instructions-second610", "instructions", instructions);
        instructions.Wait(250); Add("instructions-second860", "instructions", instructions);

        var queued = new MenuChromeFixture(); queued.SettleMain(); queued.Select("Basic Scripts");
        Add("queued-before0", "queued", queued); queued.Session.PendingAction = new(TaskCreationOptions.RunContinuationsAsynchronously);
        queued.Send(OverlayInput.Select); Add("queued-intent0", "queued", queued);
        queued.Send(OverlayInput.Select); Add("queued-later-ack0", "queued", queued);
        queued.Controller.StartQueuedAction(); Add("queued-start0", "queued", queued, 1);
        queued.Wait(430); Add("queued-expired-busy430", "queued", queued, 1);
        queued.Session.PendingAction.SetResult(true); queued.Controller.ObserveCompletion(); Add("queued-complete430", "queued", queued, 1);

        var nav = Page("Main Design Color"); nav.Send(OverlayInput.Select); nav.Wait(430);
        Add("navigation-ready430", "navigation", nav); nav.Send(OverlayInput.Down); Add("navigation-moved430", "navigation", nav);
        var owner = Page("Main Design Color"); owner.Send(OverlayInput.Select);
        Add("owner-active0", "owner", owner); owner.Session.Change(LocalPlayerLife.Alive, replaceOwner: true); Add("owner-replaced0", "owner", owner);

        // Focus projection changes only publication; a real focus-lost command retires the private intent
        var focus = LocalOptionCallbackControllerTests.ReadyPage(MenuPage.ThemeColors);
        focus.Send(OverlayInput.Select);
        void AddFocused(string name) => profiles.Add(new(name + ".json", "focus", focus.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond,
            StateFocusProjection.Apply(focus.Controller.Snapshot(), focus.Focus)));
        AddFocused("focus-active0"); focus.Focus = false; AddFocused("focus-hidden0");
        focus.Clock.Advance(TimeSpan.FromMilliseconds(430)); focus.Focus = true; AddFocused("focus-return430");

        var peer = new MenuChromeFixture(); peer.SettleMain(); peer.Select("Players Menu"); peer.Select("Friend");
        peer.Controller.StartQueuedAction(); peer.Controller.ObserveCompletion(); peer.Wait(250); peer.Select("Basic Scripts");
        peer.Send(OverlayInput.Select); peer.Controller.StartQueuedAction(); peer.Controller.ObserveCompletion();
        Add("peer-active0", "peer", peer, 2); peer.Wait(430); Add("peer-ready430", "peer", peer, 2);
        var target = peer.Session.State.SelectedPlayerTarget!;
        peer.Session.State = peer.Session.State with { SelectedPlayerTarget = new(target.Identity with { Guid = "FEDCBA0987654321" },
            target.Route, target.OwnerToken, target.SelectionRevision + 1) };
        Add("peer-replaced430", "peer", peer, 2);

        var cohort = new CohortGodmodeMenuFixture(); cohort.EnterAllPlayers(); cohort.SelectGodmode(); cohort.Controller.StartQueuedAction();
        void AddCohort(string name) => profiles.Add(new(name + ".json", "cohort", cohort.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond,
            cohort.Controller.Snapshot()));
        AddCohort("cohort-active0"); cohort.Clock.Advance(TimeSpan.FromMilliseconds(430)); AddCohort("cohort-ready430");
        cohort.Session.State = cohort.Session.State with { AllPlayersGodmodeTarget = CohortGodmodeMenuFixture.Target() }; AddCohort("cohort-replaced430");

        var rollback = Page("Main Design Color"); rollback.Send(OverlayInput.Select); rollback.Wait(100); Add("rollback-before100", "rollback", rollback);
        rollback.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(90)); Add("rollback-recovered10", "rollback", rollback);
        rollback.Wait(430); Add("rollback-ready440", "rollback", rollback);
        if (profiles.Count != ProfileCount || profiles.Select(profile => profile.Name).Distinct().Count() != ProfileCount)
            throw new InvalidOperationException("The actual Use continuation graph is incomplete");
        return profiles.AsReadOnly();
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-use-continuation-fixtures")
                throw new ArgumentException("Expected --export-use-continuation-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} admitted Use continuation snapshots"); return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Use fixture export failed: " + exception.Message); return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");
        // All controller actions and byte validation finish before output is reserved
        var packets = Build().Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        string staging = Path.Combine(parent, ".apparition-use-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            foreach (var packet in packets) {
                using var output = new FileStream(Path.Combine(staging, packet.Name), FileMode.CreateNew, FileAccess.Write);
                output.Write(packet.Bytes);
            }
            Directory.Move(staging, destination); return packets.Length;
        } finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }
}
