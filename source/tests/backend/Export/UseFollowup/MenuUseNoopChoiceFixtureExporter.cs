using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Nonslider input has a separate source outer wait and cannot be labeled as slider adjustment
internal static class MenuUseNoopChoiceFixtureExporter {
    public const int ProfileCount = 17;
    public sealed record Profile(string Name, string Graph, string Purpose, long ClockMilliseconds, StateDocument State);
    public static IReadOnlyList<Profile> Build() {
        var profiles = new List<Profile>();
        void Add(string name, string graph, MenuChromeFixture rig) {
            rig.NoGameWrites();
            profiles.Add(new(name + ".json", graph, graph == "palette" ? "causal-use-noop" : "producer-only-noop",
                rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond, rig.Current()));
        }
        var palette = new MenuChromeFixture(); palette.SettleMain(); palette.Select("Menu Customization");
        palette.Select("Main Design Color"); palette.Highlight("Red");
        // The literal no-op graph follows a real normal BOOL expiry, without manufactured native state
        Add("palette-before0", "palette", palette); palette.Send(OverlayInput.Select); Add("palette-use0", "palette", palette);
        palette.Wait(179); Add("palette-use179", "palette", palette); palette.Wait(1); Add("palette-use180", "palette", palette);
        palette.Wait(249); Add("palette-use429", "palette", palette); palette.Wait(1); Add("palette-ready430", "palette", palette);
        palette.Send(OverlayInput.NextChoice); Add("palette-noop430", "palette", palette);
        palette.Wait(49); Add("palette-noop479", "palette", palette); palette.Wait(1); Add("palette-noop480", "palette", palette);
        var quickExit = new MenuChromeFixture(); quickExit.SettleMain(); quickExit.Preferences(); quickExit.Highlight("Quick Exit");
        var root = new MenuChromeFixture(); root.SettleMain(); root.Highlight("Basic Scripts");
        foreach (var item in new[] { (Graph: "quick-exit", Rig: quickExit), (Graph: "root", Rig: root) }) {
            // These shapes prove producer admission only, with no claimed causal Select predecessor
            Add(item.Graph + "-before0", item.Graph, item.Rig);
            item.Rig.Send(OverlayInput.PreviousChoice); Add(item.Graph + "-noop0", item.Graph, item.Rig);
            item.Rig.Wait(49); Add(item.Graph + "-noop49", item.Graph, item.Rig);
            item.Rig.Wait(1); Add(item.Graph + "-noop50", item.Graph, item.Rig);
        }
        if (profiles.Count != ProfileCount || profiles.Select(profile => profile.Name).Distinct(StringComparer.Ordinal).Count() != ProfileCount)
            throw new InvalidOperationException("The actual nonslider choice graph is incomplete");
        return profiles.AsReadOnly();
    }

    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-use-noop-choice-fixtures")
                throw new ArgumentException("Expected --export-use-noop-choice-fixtures followed by a new absolute directory");
            Console.WriteLine($"Exported {Export(arguments[1])} actual nonslider choice snapshots"); return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Nonslider choice export failed: " + exception.Message); return 1;
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
        string staging = Path.Combine(parent, ".apparition-use-noop-" + Guid.NewGuid().ToString("N"));
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
