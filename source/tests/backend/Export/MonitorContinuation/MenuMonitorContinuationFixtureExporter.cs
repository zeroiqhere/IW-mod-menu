using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// Real controller visits provide the source facts; graphics and held-key schedules remain native tests
internal static class MenuMonitorContinuationFixtureExporter {
    public const int ProfileCount = 55;
    public sealed record Profile(string Name, string Graph, long ClockMilliseconds, long RelativeMilliseconds, StateDocument State);
    public static IReadOnlyList<Profile> Build() {
        var profiles = new List<Profile>();
        var origins = new Dictionary<string, long>(StringComparer.Ordinal);
        void Add(string name, string graph, MenuMonitorContinuationTests.Rig rig, bool hidden = false) {
            rig.Visit.NoGameWrites();
            long timestamp = rig.Visit.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond;
            if (!origins.TryGetValue(graph, out long origin)) { origin = timestamp; origins.Add(graph, origin); }
            var state = rig.Frame;
            profiles.Add(new(name + ".json", graph, timestamp, timestamp - origin,
                hidden ? StateFocusProjection.Apply(state, false) : state));
        }
        MenuMonitorContinuationTests.Rig Preferences(string selected) {
            var rig = new MenuMonitorContinuationTests.Rig(); rig.Preferences(); rig.Visit.Highlight(selected); return rig;
        }
        var choice = Preferences("Bool Display");
        Add("choice-before0", "choice", choice); choice.Send(OverlayInput.NextChoice); Add("choice-next0", "choice", choice);
        choice.Wait(179); Add("choice-next179", "choice", choice); choice.Wait(1); Add("choice-ready180", "choice", choice);
        choice.Send(OverlayInput.PreviousChoice); Add("choice-previous180", "choice", choice);
        choice.Wait(179); Add("choice-previous359", "choice", choice); choice.Wait(1); Add("choice-ready360", "choice", choice);
        choice.Send(OverlayInput.Select); Add("choice-use360", "choice", choice);
        choice.Wait(249); Add("choice-use609", "choice", choice); choice.Wait(1); Add("choice-use610", "choice", choice);

        var noop = new MenuMonitorContinuationTests.Rig(); noop.Visit.Select("Menu Customization"); noop.Visit.Select("Main Design Color"); noop.Visit.Highlight("Red");
        Add("noop-before0", "noop", noop); noop.Send(OverlayInput.NextChoice); Add("noop-next0", "noop", noop);
        noop.Wait(49); Add("noop-next49", "noop", noop); noop.Wait(1); Add("noop-ready50", "noop", noop);
        noop.Send(OverlayInput.NextChoice); Add("noop-repeat50", "noop", noop);
        noop.Wait(49); Add("noop-repeat99", "noop", noop); noop.Wait(1); Add("noop-ready100", "noop", noop);

        var back = Preferences("Design"); Add("back-before0", "back", back);
        back.Send(OverlayInput.Back); Add("back-child0", "back", back);
        back.Wait(249); Add("back-child249", "back", back); back.Wait(1); Add("back-child250", "back", back);
        back.Send(OverlayInput.Back); Add("back-parent250", "back", back);
        back.Wait(249); Add("back-parent499", "back", back); back.Wait(1); Add("back-parent500", "back", back);
        back.Send(OverlayInput.Back); Add("back-root500", "back", back);
        back.Wait(249); Add("back-root749", "back", back); back.Wait(1); Add("back-root750", "back", back);

        var close = Preferences("Quick Exit"); close.Visit.Send(OverlayInput.Select); close.Wait(430); close.Visit.Highlight("Design");
        Add("close-before0", "close", close); close.Send(OverlayInput.Back); Add("close-back0", "close", close);
        close.Wait(250); Add("close-ready250", "close", close);
        // The native knife loop starts at250; only its final actual Close is a backend command
        close.Wait(89); Add("close-ready339", "close", close); close.Wait(1); Add("close-ready340", "close", close);
        close.Send(OverlayInput.Close); Add("close-child340", "close", close);
        close.Wait(249); Add("close-child589", "close", close); close.Wait(1); Add("close-child590", "close", close);

        var quickVisit = new MenuChromeFixture(); quickVisit.Session.Change(LocalPlayerLife.Dead);
        quickVisit.Open(OverlayInput.OpenQuick); quickVisit.Wait(550);
        var jump = new MenuMonitorContinuationTests.Rig(quickVisit, settleMain: false);
        Add("jump-before0", "jump", jump); jump.Send(OverlayInput.JumpBack); Add("jump0", "jump", jump);
        jump.Wait(249); Add("jump249", "jump", jump); jump.Wait(1); Add("jump250", "jump", jump);

        var withdrawn = Preferences("Design"); Add("withdraw-before0", "withdraw", withdrawn);
        withdrawn.Send(OverlayInput.NextChoice); Add("withdraw-choice0", "withdraw", withdrawn);
        withdrawn.Wait(180); Add("withdraw-ready180", "withdraw", withdrawn);
        withdrawn.Focused = false; Add("withdraw-focus180", "withdraw", withdrawn, hidden: true);
        withdrawn.Focused = true; Add("withdraw-return180", "withdraw", withdrawn);
        // Keep the independent cursor change in a real packet without rewriting the old receipt
        withdrawn.Send(OverlayInput.Down); Add("withdraw-nav180", "withdraw", withdrawn);

        var rollback = Preferences("Design"); Add("rollback-before0", "rollback", rollback);
        rollback.Send(OverlayInput.NextChoice); Add("rollback-choice0", "rollback", rollback);
        rollback.Wait(100); Add("rollback100", "rollback", rollback);
        rollback.Visit.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(90)); Add("rollback10", "rollback", rollback);
        rollback.Wait(179); Add("rollback189", "rollback", rollback); rollback.Wait(1); Add("rollback190", "rollback", rollback);

        var ammo = new MenuMonitorContinuationTests.Rig(); ammo.Visit.Select("Basic Scripts"); ammo.Visit.Highlight("Unlimited Ammo");
        Add("ammo-before0", "ammo", ammo); ammo.Send(OverlayInput.NextChoice); Add("ammo-choice0", "ammo", ammo);
        ammo.Wait(179); Add("ammo-choice179", "ammo", ammo); ammo.Wait(1); Add("ammo-ready180", "ammo", ammo);
        if (profiles.Count != ProfileCount || profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count() != ProfileCount)
            throw new InvalidOperationException("The actual mechanical continuation graph is incomplete");
        return profiles.AsReadOnly();
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-monitor-continuation-fixtures")
                throw new ArgumentException("Expected --export-monitor-continuation-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} actual mechanical continuation snapshots"); return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Mechanical continuation export failed: " + exception.Message); return 1;
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
        string staging = Path.Combine(parent, ".apparition-mechanical-" + Guid.NewGuid().ToString("N"));
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
