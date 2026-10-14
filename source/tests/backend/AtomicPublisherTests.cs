using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class AtomicPublisherTests {
    public static void TemporaryCreationFailureIdentifiesOperationWithoutExposingPrivatePath() {
        InPrivateDirectory(directory => {
            Directory.CreateDirectory(Path.Combine(directory, "state.json.tmp"));
            var exception = CaptureFailure(() => new SessionFiles(directory).WriteState(EmptyState()));
            TestAssert.Equal("state.json", exception.FileName);
            TestAssert.Equal("create temporary", exception.Operation);
            TestAssert.Equal(exception.InnerException!.HResult, exception.NativeHResult);
            TestAssert.True(!exception.Message.Contains(directory, StringComparison.Ordinal), "Private paths must not enter diagnostic messages");
        });
    }

    public static void FailedReplacementDoesNotAdvanceCommittedGenerationAndReportsExactOperation() {
        InPrivateDirectory(directory => {
            string target = Path.Combine(directory, "state.json");
            Directory.CreateDirectory(target);
            var files = new SessionFiles(directory);
            var exception = CaptureFailure(() => files.WriteState(EmptyState()));
            TestAssert.Equal("replace destination", exception.Operation);
            TestAssert.Equal("state.json", exception.FileName);
            TestAssert.True(Directory.Exists(target), "A rejected destination directory must remain intact");
            Directory.Delete(target);
            files.WriteState(EmptyState());
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(target));
            TestAssert.Equal(1UL, json.RootElement.GetProperty("generation").GetUInt64());
        });
    }

    public static void ReadOnlyAndLinkedDestinationsAreRejectedBeforeReplacement() {
        InPrivateDirectory(directory => {
            string target = Path.Combine(directory, "state.json");
            File.WriteAllText(target, "retained");
            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
            try {
                CaptureFailure(() => new SessionFiles(directory).WriteState(EmptyState()));
                TestAssert.Equal("retained", File.ReadAllText(target));
            } finally {
                File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
            }
            File.Delete(target);
            string linked = Path.Combine(directory, "linked-frame.json");
            File.WriteAllText(linked, "retained link target");
            File.CreateSymbolicLink(target, linked);
            CaptureFailure(() => new SessionFiles(directory).WriteState(EmptyState()));
            TestAssert.True((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0, "Rejected links must remain unchanged");
            TestAssert.Equal("retained link target", File.ReadAllText(linked));
        });
    }

    public static void RejectedCommandAndStopReadsIdentifyTheFixedProtocolFile() {
        InPrivateDirectory(directory => {
            var files = new SessionFiles(directory);
            Directory.CreateDirectory(Path.Combine(directory, "command.json"));
            var command = CaptureFailure(() => files.ReadCommand());
            TestAssert.Equal("command.json", command.FileName);
            TestAssert.Equal("read command", command.Operation);
            Directory.CreateDirectory(Path.Combine(directory, "stop-request.json"));
            var stop = CaptureFailure(() => files.HasStopRequest(42));
            TestAssert.Equal("stop-request.json", stop.FileName);
            TestAssert.Equal("read stop request", stop.Operation);
        });
    }

    private static StateDocument EmptyState() => StateDocument.Create(42, 0, false,
        new MenuSnapshot("Main", "Disconnected", Array.Empty<MenuRow>(), ""), 0);

    private static SessionFileException CaptureFailure(Action action) {
        try { action(); } catch (SessionFileException exception) { return exception; }
        throw new InvalidOperationException("The rejected file operation must produce contextual diagnostics");
    }

    private static void InPrivateDirectory(Action<string> action) {
        string directory = Path.Combine(Path.GetTempPath(), "apparition-atomic-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); } finally { Directory.Delete(directory, true); }
    }
}
