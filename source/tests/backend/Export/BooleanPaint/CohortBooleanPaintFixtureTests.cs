using System.Text.Json;

namespace Backend.Tests;

// The public test driver exports real controller frames without rebuilding wire documents
internal static class CohortBooleanPaintFixtureTests {
    public static void ActualCohortGraphPreservesRawResultsAndSourcePaintAcrossTheBooleanCallerWait() {
        string root = Path.Combine(Path.GetTempPath(), "apparition-cohort-paint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string packets = Path.Combine(root, "packets");
            TestAssert.Equal(0, Program.Main(new[] { "--export-boolean-paint-fixtures", packets }));
            TestAssert.Equal(16, Directory.GetFiles(packets).Length);
            foreach (var expected in new[] {
                ("cohort-off", "OFF", "OFF", (string?)null),
                ("cohort-on179", "ON", "OFF", "pending"),
                ("cohort-on180", "ON", "ON", "cooldown"),
                ("cohort-on430", "ON", "ON", null),
                ("cohort-unknown179", "UNKNOWN", "OFF", "pending"),
                ("cohort-unknown180", "UNKNOWN", "UNKNOWN", "cooldown"),
                ("cohort-complete430", "ON", "UNKNOWN", null),
                ("cohort-worker-busy", "UNKNOWN", "OFF", null)
            }) {
                using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(packets, expected.Item1 + ".json")));
                var frame = json.RootElement;
                TestAssert.Equal("God Mode", frame.GetProperty("rows")[5].GetProperty("label").GetString()!);
                TestAssert.Equal(expected.Item2, frame.GetProperty("rows")[5].GetProperty("value").GetString()!);
                var paint = frame.GetProperty("menuBooleanPaint").GetProperty("rows");
                TestAssert.Equal(1, paint.GetArrayLength());
                TestAssert.Equal(5, paint[0].GetProperty("index").GetInt32());
                TestAssert.Equal(expected.Item3, paint[0].GetProperty("value").GetString()!);
                bool hasCallback = frame.TryGetProperty("menuOptionCallback", out var callback);
                TestAssert.Equal(expected.Item4 is not null, hasCallback);
                if (hasCallback) TestAssert.Equal(expected.Item4!, callback.GetProperty("phase").GetString()!);
                TestAssert.True(!frame.GetRawText().Contains("ExpectedCohort") && !frame.GetRawText().Contains("AccountId"),
                    "Actual published group rows cannot expose private cohort authority");
            }
            // The same actual CLI must reject an existing graph rather than replace packet bytes
            TestAssert.Equal(1, Program.Main(new[] { "--export-boolean-paint-fixtures", packets }));
            TestAssert.Equal(16, Directory.GetFiles(packets).Length);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
