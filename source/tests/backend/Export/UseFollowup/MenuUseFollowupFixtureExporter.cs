using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Separate source graphs will join ordinary branches after a confirmed Use callback expiry
internal static class MenuUseFollowupFixtureExporter {
    public const int ProfileCount = 51;
    public sealed record Profile(string Name, string Graph, string Purpose, long ClockMilliseconds, StateDocument State);
    public static IReadOnlyList<Profile> Build() {
        var profiles = new List<Profile>();
        void Add(string name, string graph, MenuChromeFixture rig, string purpose = "causal-use-followup") {
            // Local choices and Back visits cannot dispatch a player or lobby feature
            rig.NoGameWrites();
            profiles.Add(new(name + ".json", graph, purpose,
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, rig.Current()));
        }
        MenuChromeFixture Preferences(bool quickExit = false) {
            var rig = new MenuChromeFixture(); rig.SettleMain(); rig.Preferences();
            if (quickExit) { rig.Select("Quick Exit"); rig.Wait(430); }
            rig.Highlight("Bool Display"); return rig;
        }
        MenuChromeFixture PlainPrefix(string graph, bool quickExit = false) {
            var rig = Preferences(quickExit);
            Add(graph + "-before0", graph, rig); rig.Send(OverlayInput.Select); Add(graph + "-use0", graph, rig);
            rig.Wait(249); Add(graph + "-use249", graph, rig);
            rig.Wait(1); Add(graph + "-ready250", graph, rig);
            return rig;
        }
        var next = PlainPrefix("plain-next"); next.Send(OverlayInput.NextChoice); Add("plain-next-choice250", "plain-next", next);
        next.Wait(179); Add("plain-next-choice429", "plain-next", next); next.Wait(1); Add("plain-next-choice430", "plain-next", next);
        next.Send(OverlayInput.NextChoice); Add("plain-next-repeat430", "plain-next", next);
        next.Wait(180); Add("plain-next-repeat610", "plain-next", next);

        var previous = PlainPrefix("plain-previous"); previous.Send(OverlayInput.PreviousChoice); Add("plain-previous-choice250", "plain-previous", previous);
        previous.Wait(179); Add("plain-previous-choice429", "plain-previous", previous);
        previous.Wait(1); Add("plain-previous-choice430", "plain-previous", previous);

        var back = PlainPrefix("plain-back");
        back.Send(OverlayInput.Back); Add("plain-back-child250", "plain-back", back);
        back.Wait(249); Add("plain-back-child499", "plain-back", back); back.Wait(1); Add("plain-back-child500", "plain-back", back);
        back.Send(OverlayInput.Back); Add("plain-back-main500", "plain-back", back);
        back.Wait(249); Add("plain-back-main749", "plain-back", back); back.Wait(1); Add("plain-back-main750", "plain-back", back);
        back.Send(OverlayInput.Back); Add("plain-back-root750", "plain-back", back);
        back.Wait(249); Add("plain-back-root999", "plain-back", back); back.Wait(1); Add("plain-back-root1000", "plain-back", back);

        var close = PlainPrefix("quick-exit-child", quickExit: true);
        // The native held-knife schedule owns its90ms threshold; the producer records the actual Close command
        close.Wait(89); Add("quick-exit-child-ready339", "quick-exit-child", close);
        close.Wait(1); Add("quick-exit-child-ready340", "quick-exit-child", close);
        close.Send(OverlayInput.Close); Add("quick-exit-child-close340", "quick-exit-child", close);
        close.Wait(249); Add("quick-exit-child-close589", "quick-exit-child", close);
        close.Wait(1); Add("quick-exit-child-close590", "quick-exit-child", close);

        var root = Preferences(quickExit: true);
        root.Send(OverlayInput.Back); root.Wait(250); root.Send(OverlayInput.Back); root.Wait(250);
        // This independent root shape has no claimed Use predecessor or borrowed child receipt
        const string rootPurpose = "producer-only-root-shape";
        Add("quick-exit-root-before0", "quick-exit-root", root, rootPurpose);
        root.Send(OverlayInput.Back); Add("quick-exit-root-back0", "quick-exit-root", root, rootPurpose);
        root.Wait(249); Add("quick-exit-root-back249", "quick-exit-root", root, rootPurpose);
        root.Wait(1); Add("quick-exit-root-back250", "quick-exit-root", root, rootPurpose);

        var boolean = new MenuChromeFixture(); boolean.SettleMain(); boolean.Select("Menu Customization"); boolean.Select("Main Design Color");
        boolean.Highlight("Red"); Add("boolean-back-before0", "boolean-back", boolean);
        boolean.Send(OverlayInput.Select); Add("boolean-back-use0", "boolean-back", boolean);
        boolean.Wait(179); Add("boolean-back-use179", "boolean-back", boolean); boolean.Wait(1); Add("boolean-back-use180", "boolean-back", boolean);
        boolean.Wait(249); Add("boolean-back-use429", "boolean-back", boolean); boolean.Wait(1); Add("boolean-back-ready430", "boolean-back", boolean);
        boolean.Send(OverlayInput.Back); Add("boolean-back-back430", "boolean-back", boolean);
        boolean.Wait(249); Add("boolean-back-back679", "boolean-back", boolean); boolean.Wait(1); Add("boolean-back-back680", "boolean-back", boolean);
        if (profiles.Count != ProfileCount || profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count() != ProfileCount)
            throw new InvalidOperationException("The actual Use followup graph is incomplete");
        return profiles.AsReadOnly();
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-use-followup-fixtures")
                throw new ArgumentException("Expected --export-use-followup-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} actual Use followup snapshots"); return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Use followup export failed: " + exception.Message); return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Fixture directory must be absolute", nameof(directory));
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Fixture output already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Fixture parent is required");
        if (!Directory.Exists(parent)) throw new IOException("Fixture parent must exist");
        // Validate the full real-controller graph before reserving a staging directory
        var packets = Build().Select(profile => (profile.Name, Bytes: profile.State.Serialize())).ToArray();
        string staging = Path.Combine(parent, ".apparition-use-followup-" + Guid.NewGuid().ToString("N"));
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
