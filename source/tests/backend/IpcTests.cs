using System.Text;
using System.Text.Json;
using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class IpcTests {
    public static void FifteenHundredUnicodeNoticeCharactersRoundTripWithinTheNativeByteBudget() {
        string notice = new('\u2603', 1500);
        var state = StateDocument.Create(42, 1, true,
            new("Apparition", "DISCONNECTED", Array.Empty<MenuRow>(), notice), 0);
        TestAssert.Equal(1500, state.Notice.Length);
        TestAssert.Equal(4500, Encoding.UTF8.GetByteCount(state.Notice));
        byte[] bytes = state.Serialize();
        TestAssert.True(bytes.Length <= StateDocument.MaximumBytes, "Unicode notice must fit the native state buffer");
        using var json = JsonDocument.Parse(bytes);
        TestAssert.Equal(notice, json.RootElement.GetProperty("notice").GetString());
    }

    public static void ValidCommandParsesAndAdvancesAcknowledgmentOnce() {
        var command = Parse("{\"version\":1,\"sequence\":1,\"input\":\"toggle\",\"processId\":42}")!;
        TestAssert.True(command.Valid, "A complete supported envelope must be valid");
        var gate = new CommandGate(42);
        TestAssert.True(gate.Consume(command, true), "A focused fresh command must dispatch");
        TestAssert.Equal(1UL, gate.LastSequence);
        TestAssert.True(!gate.Consume(command, true), "A command must dispatch only once");
    }

    public static void InvalidVersionPidAndInputAreAcknowledgedWithoutDispatch() {
        var gate = new CommandGate(42);
        string[] bodies = {
            "{\"version\":2,\"sequence\":1,\"input\":\"select\",\"processId\":42}",
            "{\"version\":1,\"sequence\":2,\"input\":\"select\",\"processId\":43}",
            "{\"version\":1,\"sequence\":3,\"input\":\"execute\",\"processId\":42}",
            "{\"sequence\":4}",
            "{\"version\":1,\"version\":1,\"sequence\":5,\"input\":\"select\",\"processId\":42}"
        };
        for (int index = 0; index < bodies.Length; index++) {
            TestAssert.True(!gate.Consume(Parse(bodies[index])!, true), "Rejected envelopes must not dispatch");
            TestAssert.Equal((ulong)index + 1, gate.LastSequence);
        }
    }

    public static void FocusLostCommandsNeverReplayAfterFocusReturns() {
        var gate = new CommandGate(42);
        var command = new CommandEnvelope(11, 1, 42, OverlayInput.Select, true);
        TestAssert.True(!gate.Consume(command, false), "Focus loss must reject dispatch");
        TestAssert.Equal(11UL, gate.LastSequence);
        TestAssert.True(!gate.Consume(command, true), "Focus return cannot replay an acknowledged command");
    }

    public static void StaleSequenceNeverLowersAcknowledgment() {
        var gate = new CommandGate(42);
        gate.Consume(new(ulong.MaxValue, 1, 42, OverlayInput.Toggle, true), true);
        TestAssert.True(!gate.Consume(new(1, 1, 42, OverlayInput.Select, true), true), "Old commands must remain stale");
        TestAssert.Equal(ulong.MaxValue, gate.LastSequence);
    }

    public static void MalformedOrUntrustedSequenceReturnsNoEnvelope() {
        foreach (string body in new[] { "{", "[]", "{}", "{\"sequence\":-1}",
            "{\"sequence\":\"1\"}", "{\"sequence\":1,\"sequence\":2}" }) {
            TestAssert.True(Parse(body) is null, "An unusable sequence cannot be acknowledged or dispatched");
        }
        TestAssert.True(CommandEnvelope.Parse(new byte[4097]) is null, "Oversized commands must be rejected");
    }

    public static void StateIncludesSelectedDetailAndStaysWithinEncodedSizeLimit() {
        string escaped = new('"', 2000);
        string emoji = string.Concat(Enumerable.Repeat("😀\u2028\\", 1000));
        var rows = Enumerable.Range(0, 128).Select(_ =>
            new MenuRow(emoji, escaped, true, new(MenuActionKind.None), emoji)).ToArray();
        var document = StateDocument.Create(42, 77, true,
            new MenuSnapshot(emoji, emoji, rows, escaped), 3);
        byte[] bytes = document.Serialize();
        TestAssert.True(bytes.Length <= 32768, "Maximum rows and escaping must fit the native reader");
        TestAssert.True(document.Notice.Length <= 1500, "Notice length must remain bounded");
        using var json = JsonDocument.Parse(bytes);
        TestAssert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        TestAssert.Equal(77UL, json.RootElement.GetProperty("lastSequence").GetUInt64());
        TestAssert.Equal("#3998FE", json.RootElement.GetProperty("accent").GetString());
        var detail = StateDocument.Create(42, 1, true, new("Main", "Connected",
            new[] { new MenuRow("Ammo", "OFF", true, new(MenuActionKind.None), "Shared ammo") }, "Notice"), 0);
        TestAssert.Equal("Notice\nShared ammo", detail.Notice);
    }

    public static void AtomicSessionFilesRoundTripWithoutTemporaryFrameLeaks() {
        string directory = Path.Combine(Path.GetTempPath(), "apparition-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var files = new SessionFiles(directory);
            TestAssert.True(files.ReadCommand() is null, "Absent commands must be harmless");
            File.WriteAllText(Path.Combine(directory, "command.json"), "{\"version\":1,\"sequence\":8,\"input\":\"back\",\"processId\":42}");
            TestAssert.Equal(8UL, files.ReadCommand()!.Sequence);
            var state = StateDocument.Create(42, 8, false, new("Main", "Disconnected", Array.Empty<MenuRow>(), ""), 0);
            files.WriteState(state);
            files.WriteState(state with { LastSequence = 9 });
            using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "state.json")));
            TestAssert.Equal(9UL, json.RootElement.GetProperty("lastSequence").GetUInt64());
            TestAssert.Equal(2UL, json.RootElement.GetProperty("generation").GetUInt64());
            files.WriteState(state with { LastSequence = 9 });
            using var unchanged = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "state.json")));
            TestAssert.Equal(9UL, unchanged.RootElement.GetProperty("lastSequence").GetUInt64());
            TestAssert.Equal(3UL, unchanged.RootElement.GetProperty("generation").GetUInt64());
            TestAssert.True(!File.Exists(Path.Combine(directory, "state.json.tmp")), "Only committed frames may remain");
            files.WriteStatus(new string('x', 2000));
            using var status = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "status.json")));
            TestAssert.Equal(1500, status.RootElement.GetProperty("message").GetString()!.Length);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    public static void StopRequestsRequireExactVersionPidAndBoundedJson() {
        string directory = Path.Combine(Path.GetTempPath(), "apparition-stop-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var files = new SessionFiles(directory);
            string path = Path.Combine(directory, "stop-request.json");
            TestAssert.True(!files.HasStopRequest(42), "An absent request must not shut down");
            foreach (string body in new[] { "{", "[]", "{\"version\":1,\"processId\":43}",
                "{\"version\":2,\"processId\":42}", "{\"version\":1,\"processId\":42,\"extra\":0}",
                "{\"version\":1,\"version\":1,\"processId\":42}", new string('x', 1025) }) {
                File.WriteAllText(path, body);
                TestAssert.True(!files.HasStopRequest(42), "Only the exact supported stop request is accepted");
            }
            File.WriteAllText(path, "{\"version\":1,\"processId\":42}");
            TestAssert.True(files.HasStopRequest(42), "The launcher must be able to request cleanup without foreground focus");
        } finally {
            Directory.Delete(directory, true);
        }
    }

    public static void OptionsRejectIncompletePidAndRelativeDirectory() {
        TestAssert.Throws<ArgumentException>(() => BackendOptions.Parse(Array.Empty<string>()));
        TestAssert.Throws<ArgumentException>(() => BackendOptions.Parse(new[] { "--game-pid", "0", "--session-directory", Path.GetTempPath() }));
        TestAssert.Throws<ArgumentException>(() => BackendOptions.Parse(new[] { "--game-pid", "42", "--session-directory", "relative" }));
        var options = BackendOptions.Parse(new[] { "--game-pid", "42", "--session-directory", Path.GetTempPath() });
        TestAssert.Equal(42, options.ProcessId);
    }

    private static CommandEnvelope? Parse(string body) => CommandEnvelope.Parse(Encoding.UTF8.GetBytes(body));
}
