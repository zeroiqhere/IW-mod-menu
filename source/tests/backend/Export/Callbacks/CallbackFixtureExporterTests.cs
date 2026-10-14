using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class CallbackFixtureExporterTests {
    private static readonly string[] Names = {
        "boolean-pending-immediate.json", "boolean179.json", "boolean180.json", "boolean429.json", "boolean430-ready.json",
        "boolean-pending-busy.json", "boolean180-busy.json", "boolean430-busy.json",
        "plain-lobby-cooldown0.json", "plain-lobby-cooldown249.json", "plain-lobby250-ready.json",
        "focus-hidden-pending.json", "replaced-owner-closed.json", "shutdown-no-callback.json"
    };

    public static void ActualCallbackProfilesKeepConnectedFactsAndIndependentWorkTiming() {
        var all = CallbackFixtureExporter.Build();
        TestAssert.Equal(14, all.Count);
        TestAssert.True(all.Keys.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(Names.OrderBy(name => name, StringComparer.Ordinal)),
            "Callback inventory must match its independent controller intent list");
        foreach (var frame in all.Values) {
            TestAssert.True(frame.Context.StartsWith("CONNECTED", StringComparison.Ordinal),
                "Connected fake facts must reach the real controller without a PREVIEW rewrite");
            string bytes = System.Text.Encoding.UTF8.GetString(frame.Serialize());
            TestAssert.True(!bytes.Contains(ScopedOpeningFixture.Actor.Guid, StringComparison.Ordinal),
                "Callback packets cannot export the private binding GUID");
        }
        var before = all["boolean-pending-immediate.json"];
        TestAssert.Equal("pending", before.MenuOptionCallback!.Phase);
        TestAssert.Equal("boolean", before.MenuOptionCallback.Kind);
        TestAssert.Equal(1UL, before.MenuOptionCallback.Revision);
        TestAssert.True(before.LocalMenuEligibility!.ControlsDisabled == true && !before.LocalMenuEligibility.Blocked,
            "An immediate task leaves a monitor wait without inventing Busy");
        TestAssert.Equal(before.MenuRevision!.Value, all["boolean179.json"].MenuRevision!.Value);
        TestAssert.Equal(before.MenuRevision.Value + 1, all["boolean180.json"].MenuRevision!.Value);
        TestAssert.Equal(all["boolean180.json"].MenuRevision!.Value, all["boolean430-ready.json"].MenuRevision!.Value);
        TestAssert.True(before.MenuOpening is not null && all["boolean180.json"].MenuOpening is null,
            "Actual boolean redraw must retire the retained opening once");
        TestAssert.True(all["boolean430-ready.json"].MenuOptionCallback is null
            && all["boolean430-ready.json"].LocalMenuEligibility!.ControlsDisabled == false,
            "Callback cadence must release independently at 430 ms");
        foreach (string name in new[] { "boolean-pending-busy.json", "boolean180-busy.json", "boolean430-busy.json" }) {
            TestAssert.Equal("UNKNOWN", all[name].Rows[0].Value);
            TestAssert.True(all[name].LocalMenuEligibility!.Blocked, "Long work must remain independently busy");
        }
        TestAssert.True(all["boolean430-busy.json"].MenuOptionCallback is null,
            "Work completion cannot own the already finished monitor cadence");
        var plain = all["plain-lobby-cooldown0.json"];
        TestAssert.Equal("plain", plain.MenuOptionCallback!.Kind);
        TestAssert.Equal("cooldown", plain.MenuOptionCallback.Phase);
        TestAssert.True(all["plain-lobby250-ready.json"].MenuOptionCallback is null,
            "The actual lobby adapter must end its ordinary wait at 250 ms");
        TestAssert.Equal(plain.MenuRevision!.Value, all["plain-lobby250-ready.json"].MenuRevision!.Value);
        foreach (string name in new[] { "focus-hidden-pending.json", "replaced-owner-closed.json", "shutdown-no-callback.json" }) {
            TestAssert.True(all[name].MenuOptionCallback is null, "Foreign or administrative frames cannot retain a callback owner");
        }
        TestAssert.True(!all["focus-hidden-pending.json"].IsOpen && !all["replaced-owner-closed.json"].IsOpen,
            "Focus projection and owner replacement must keep their actual closed frames");
    }

    public static void CallbackExportPublishesOnlyFreshFilesAndNeverOverwrites() {
        var directory = Directory.CreateTempSubdirectory("iw-callback-export-test-");
        try {
            string destination = Path.Combine(directory.FullName, "profiles");
            TestAssert.Equal(14, CallbackFixtureExporter.Export(destination));
            var entries = Directory.GetFiles(destination).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
            TestAssert.Equal(14, entries.Length);
            foreach (string entry in entries) {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(entry));
                TestAssert.Equal(42, document.RootElement.GetProperty("processId").GetInt32());
            }
            var before = entries.ToDictionary(path => path, File.ReadAllBytes);
            TestAssert.Throws<IOException>(() => CallbackFixtureExporter.Export(destination));
            TestAssert.True(before.All(pair => pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))),
                "An existing callback export cannot be overwritten");
            string blocker = Path.Combine(directory.FullName, "blocker"); File.WriteAllText(blocker, "retained");
            TestAssert.Throws<IOException>(() => CallbackFixtureExporter.Export(blocker));
            TestAssert.Equal("retained", File.ReadAllText(blocker));
            TestAssert.True(Directory.GetDirectories(directory.FullName).Length == 1,
                "Failed callback exports cannot leave staging directories");
        } finally {
            directory.Delete(recursive: true);
        }
    }

    public static void CallbackExportCliRejectsMalformedArgumentsWithoutWriting() {
        var directory = Directory.CreateTempSubdirectory("iw-callback-cli-test-");
        try {
            string target = Path.Combine(directory.FullName, "not-created");
            foreach (string[] arguments in new[] { Array.Empty<string>(), new[] { "--export-callback-fixtures" },
                new[] { "--wrong", target }, new[] { "--export-callback-fixtures", "relative-output" },
                new[] { "--export-callback-fixtures", target, "extra" } }) {
                TestAssert.Equal(1, CallbackFixtureExporter.Run(arguments));
                TestAssert.True(!Directory.Exists(target) && Directory.GetFileSystemEntries(directory.FullName).Length == 0,
                    "Rejected callback CLI arguments cannot publish output");
            }
        } finally {
            directory.Delete(recursive: true);
        }
    }
}
