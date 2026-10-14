using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// These packets come from the actual controller over recorded resources for native routing tests
internal static class OwnedCleanupFixtureExporter {
    internal static async Task<int> RunAsync(string[] arguments) {
        if (arguments.Length != 2) return 2;
        string directory = Path.GetFullPath(arguments[1]);
        if (Directory.Exists(directory) || File.Exists(directory)) return 2;
        var frames = new Dictionary<string, StateDocument>();
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        frames.Add("healthy-initial-closed.json", rig.Controller.Snapshot());
        await rig.OpenNormalAsync();
        frames.Add("healthy-open.json", rig.Controller.Snapshot());
        await rig.LoseAdmissionAsync();
        frames.Add("lost-closed.json", rig.Controller.Snapshot());
        int writes = rig.Transport.Inner.FlagWrites;
        rig.Send(OverlayInput.Toggle);
        frames.Add("cleanup-open.json", rig.Controller.Snapshot());
        rig.Send(OverlayInput.Back);
        frames.Add("cleanup-closed.json", rig.Controller.Snapshot());
        rig.Send(OverlayInput.Toggle);
        frames.Add("cleanup-reopened.json", rig.Controller.Snapshot());
        await rig.RecoverAdmissionAsync();
        frames.Add("healthy-closed.json", rig.Controller.Snapshot());
        await rig.OpenNormalAsync();
        frames.Add("healthy-reopened.json", rig.Controller.Snapshot());
        Check(rig.Transport.Inner.FlagWrites == writes, "Fixture publication automatically changed original flags");
        Directory.CreateDirectory(directory);
        foreach (var (name, frame) in frames) {
            using var stream = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write);
            stream.Write(frame.Serialize());
        }
        Console.WriteLine($"Exported {frames.Count} actual owned cleanup packets without automatic feature writes");
        return 0;
    }
}
