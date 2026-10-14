using Apparition.Backend.Ipc;

namespace Backend.Tests;

// Literal sequence checks keep the native replay tied to the actual command route
internal static class MenuUseContinuationFixtureTests {
    public static void ActualGraphPreservesCommandAcknowledgementsOwnedRedrawsAndPrivateWithdrawals() {
        var profiles = MenuUseContinuationFixtureExporter.Build(); TestAssert.Equal(49, profiles.Count);
        StateDocument Find(string name) => profiles.Single(profile => profile.Name == name + ".json").State;
        TestAssert.Equal<ulong>(13, Find("plain-first0").MenuUseContinuation!.Sequence);
        TestAssert.Equal<ulong>(14, Find("plain-second250").MenuUseContinuation!.Sequence);
        TestAssert.Equal<ulong>(11, Find("boolean-first0").MenuUseContinuation!.Sequence);
        TestAssert.Equal<ulong>(12, Find("boolean-second430").MenuUseContinuation!.Sequence);
        foreach (string name in new[] { "plain-first250", "plain-second500", "boolean-first430", "boolean-second860", "instructions-first430", "instructions-second860", "queued-complete430", "rollback-ready440" }) {
            var frame = Find(name); TestAssert.True(frame.MenuOptionCallback is null && frame.MenuUseContinuation is not null,
                "Only normal expiry preserves this exact source invocation: " + name);
        }
        foreach (string name in new[] { "plain-before0", "boolean-before0", "instructions-before0", "queued-intent0", "queued-later-ack0", "navigation-moved430", "owner-replaced0", "focus-hidden0", "focus-return430", "peer-replaced430", "cohort-replaced430" })
            TestAssert.True(Find(name).MenuUseContinuation is null, "The actual graph must withdraw unsupported continuation: " + name);
        var queued = Find("queued-start0"); TestAssert.Equal(queued.LastSequence - 1, queued.MenuUseContinuation!.Sequence);
        TestAssert.Equal(Find("instructions-first0").MenuUseContinuation, Find("instructions-first180").MenuUseContinuation);
        TestAssert.True(Find("instructions-first180").MenuRevision > Find("instructions-first0").MenuRevision,
            "Immediate and scheduled source row redraws preserve the captured action");
        TestAssert.Equal(Find("boolean-second860").LastSequence, Find("boolean-release1000").LastSequence);
        TestAssert.Equal(Find("plain-second500").LastSequence, Find("plain-release750").LastSequence);
        foreach (var profile in profiles) profile.State.Serialize();
    }
    public static void ActualProgramExportsTheWholeUseGraphWithoutOverwritingPackets() {
        string parent = Path.Combine(Path.GetTempPath(), "apparition-use-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        string destination = Path.Combine(parent, "packets");
        try {
            TestAssert.Equal(0, Program.Main(new[] { "--export-use-continuation-fixtures", destination }));
            var expected = MenuUseContinuationFixtureExporter.Build(); TestAssert.Equal(49, Directory.GetFiles(destination).Length);
            foreach (var profile in expected) TestAssert.True(File.ReadAllBytes(Path.Combine(destination, profile.Name)).SequenceEqual(profile.State.Serialize()),
                "Actual Program must preserve the original packet: " + profile.Name);
            TestAssert.Equal(1, Program.Main(new[] { "--export-use-continuation-fixtures", destination }));
            TestAssert.Equal(49, Directory.GetFiles(destination).Length);
        } finally { Directory.Delete(parent, true); }
    }
}
