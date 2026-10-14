using Apparition.Backend.Ipc;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// The native command producer and managed parser join over the same actual controller fixture
internal static class OwnedCleanupCommandJoin {
    internal static async Task<int> RunAsync(string[] arguments) {
        if (arguments.Length != 2) return 2;
        var command = CommandEnvelope.Parse(await File.ReadAllBytesAsync(arguments[1]));
        Check(command?.Valid == true, "The actual native cleanup command failed managed parsing");
        await using var rig = new OwnedCleanupAccessRig();
        await rig.ConnectAsync();
        await rig.OwnFlagsAsync();
        await rig.OpenNormalAsync();
        await rig.LoseAdmissionAsync();
        var before = rig.Controller.Snapshot();
        Check(command!.NavigationRevision == before.NavigationRevision
            && command.OpeningControlsRevision == before.MainOpeningControls!.Revision,
            "The native opening command did not retain both actual displayed epochs");
        int writes = rig.Transport.Inner.FlagWrites;
        rig.Controller.Consume(command, () => true);
        var frame = rig.Controller.Snapshot();
        Check(frame.IsOpen && frame.Rows.Count == 1 && frame.Rows[0].Label == "Exit"
            && frame.Rows[0].Enabled && rig.Transport.Inner.FlagWrites == writes && !rig.Controller.ExitRequested,
            "The actual native command did not open only administrative Exit without writes");
        Console.WriteLine("PASS actual native CommandJson to CommandEnvelope to owned Exit-only controller join");
        return 0;
    }
}
