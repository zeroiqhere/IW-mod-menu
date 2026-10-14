using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Backend.Tests;

// These packets are actual admitted controller visits, separate from the earlier graph inventories
internal static class MenuMonitorWaitFixtureExporter {
    public const int ProfileCount = 26;
    public sealed record Profile(string Name, string Graph, long ClockMilliseconds, StateDocument State);
    public static IReadOnlyList<Profile> Build() {
        var profiles = new List<Profile>();
        void Add(string name, string graph, MenuChromeFixture rig, bool focused = true) {
            rig.NoGameWrites();
            var frame = rig.Current();
            profiles.Add(new(name + ".json", graph, rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond,
                StateFocusProjection.Apply(frame, focused)));
        }
        MenuChromeFixture Preferences() {
            var rig = new MenuChromeFixture(); rig.SettleMain(); rig.Preferences(); rig.Highlight("Design"); return rig;
        }
        var choice = Preferences();
        Add("choice-before0", "choice", choice);
        choice.Send(OverlayInput.NextChoice); Add("choice0", "choice", choice);
        choice.Wait(179); Add("choice179", "choice", choice);
        choice.Wait(1); Add("choice180", "choice", choice);

        var back = Preferences();
        back.Send(OverlayInput.Back); Add("back-parent0", "back", back);
        back.Wait(249); Add("back-parent249", "back", back);
        back.Wait(1); Add("back-parent250", "back", back);
        back.Send(OverlayInput.Back); back.Wait(250);
        Add("back-root-before0", "back", back);
        back.Send(OverlayInput.Back);
        Add("back-root-closed0", "back", back);
        back.Wait(249); Add("back-root-closed249", "back", back);
        back.Wait(1); Add("back-root-closed250", "back", back);

        var dead = new MenuChromeFixture(); dead.Session.Change(LocalPlayerLife.Dead);
        dead.Open(OverlayInput.OpenQuick); dead.Wait(550);
        var quick = dead.Current();
        dead.Controller.Consume(new(100, 1, 42, OverlayInput.JumpBack, true, quick.NavigationRevision,
            null, dead.Session.Admission.Revision), () => true);
        Add("dead-jump0", "dead-jump", dead);
        dead.Wait(250); Add("dead-jump250", "dead-jump", dead);

        var close = Preferences(); close.Highlight("Quick Exit"); close.Send(OverlayInput.Select); close.Wait(430);
        close.Send(OverlayInput.Close); Add("hold-close0", "hold-close", close);
        close.Wait(250); Add("hold-close250", "hold-close", close);

        var focus = Preferences(); focus.Send(OverlayInput.NextChoice);
        Add("focus-owner0", "focus", focus); Add("focus-hidden0", "focus", focus, false);
        focus.Wait(100); Add("focus-return100", "focus", focus);
        focus.Wait(80); Add("focus-ready180", "focus", focus);

        var replacement = Preferences(); replacement.Send(OverlayInput.NextChoice);
        Add("owner-before0", "replacement", replacement);
        replacement.Session.Change(LocalPlayerLife.Alive, replaceOwner: true);
        Add("owner-replaced0", "replacement", replacement);

        var rollback = Preferences(); rollback.Send(OverlayInput.NextChoice);
        rollback.Wait(100); Add("rollback-before100", "rollback", rollback);
        // Source clock faults are distinct from native renderer or input clocks
        rollback.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(90)); Add("rollback-recovered10", "rollback", rollback);
        rollback.Wait(180); Add("rollback-ready190", "rollback", rollback);

        var numeric = Preferences(); numeric.Highlight("Scroll Animation Time (ms)");
        for (int step = 0; step < 13; step++) { numeric.Send(OverlayInput.NextChoice); numeric.Wait(180); }
        Add("numeric-bound250", "numeric-bound", numeric);
        numeric.Send(OverlayInput.NextChoice); Add("numeric-wrap0", "numeric-bound", numeric);
        if (profiles.Count != ProfileCount || profiles.Select(profile => profile.Name).Distinct().Count() != ProfileCount)
            throw new InvalidOperationException("The source monitor graph is incomplete");
        return profiles.AsReadOnly();
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-monitor-wait-fixtures")
                throw new ArgumentException("Expected --export-monitor-wait-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} admitted mechanical monitor snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Monitor fixture export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");
        // Complete controller construction and serialization precede output reservation
        var profiles = Build();
        var packets = profiles.Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        string staging = Path.Combine(parent, ".apparition-monitor-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(staging);
        else Directory.CreateDirectory(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try {
            foreach (var packet in packets) {
                using var output = new FileStream(Path.Combine(staging, packet.Name), FileMode.CreateNew, FileAccess.Write);
                output.Write(packet.Bytes);
            }
            Directory.Move(staging, destination);
            return packets.Length;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
