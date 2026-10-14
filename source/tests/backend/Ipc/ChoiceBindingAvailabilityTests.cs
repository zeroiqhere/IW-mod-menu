using Apparition.Backend.Ipc;
using System.Text;

namespace Backend.Tests;

internal static class ChoiceBindingAvailabilityTests {
    private const string Required = "\"version\":1,\"aim\":[2],\"melee\":[86],\"up\":[2,38],\"down\":[1,40],\"select\":[70]";
    public static void OnlyCompleteSupportedChoiceGroupsEnableAvailability() {
        TestAssert.True(Parse("{" + Required + ",\"previousChoice\":[51],\"nextChoice\":[52]}"), "Saved source groups must enable availability");
        foreach (string body in new[] { "{" + Required + "}", "{" + Required + ",\"previousChoice\":[51]}",
            "{" + Required + ",\"previousChoice\":[],\"nextChoice\":[52]}" }) {
            TestAssert.True(!Parse(body), "Missing or empty choice groups must not invent bindings");
        }
    }
    public static void InvalidDuplicateOverlappingUnknownOrOversizedPacketsCannotEnableChoices() {
        string good = "{" + Required + ",\"previousChoice\":[51],\"nextChoice\":[52]}";
        foreach (string bad in new[] { "{", "[]", good.Replace("\"version\":1", "\"version\":true"),
            good.Replace("[51]", "[true]"), good.Replace("[51]", "[2]"), good.Replace("[51]", "[51,51]"),
            good.Replace("[51]", "[255]"), good.Replace("[51]", "[0]"), good.Replace("[51]", "[51.0]"),
            good.Replace("[51]", "[\"51\"]"), good.Replace("[51]", "[" + string.Join(',', Enumerable.Range(50, 17)) + "]"),
            good[..^1] + ",\"unknown\":1}", good[..^1] + ",\"version\":1}", new string('x', 4097) }) {
            TestAssert.True(!Parse(bad), "Invalid private bindings must remain unavailable");
        }
    }
    public static void SessionReaderBoundsRealFilesAndRejectsLinkedPackets() {
        string directory = Path.Combine(Path.GetTempPath(), "apparition-choice-bindings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var files = new SessionFiles(directory);
            TestAssert.True(!files.ReadChoiceControlsAvailable(), "Missing packet must leave choices disabled");
            string path = Path.Combine(directory, "bindings.json");
            File.WriteAllText(path, "{" + Required + ",\"previousChoice\":[51],\"nextChoice\":[52]}");
            TestAssert.True(files.ReadChoiceControlsAvailable(), "Bounded ordinary packet must read");
            File.WriteAllText(path, new string('x', 4097));
            TestAssert.True(!files.ReadChoiceControlsAvailable(), "Oversized packet cannot be read as available");
            File.Delete(path);
            File.WriteAllText(Path.Combine(directory, "target.json"), "{" + Required + "}");
            File.CreateSymbolicLink(path, Path.Combine(directory, "target.json"));
            TestAssert.Throws<SessionFileException>(() => files.ReadChoiceControlsAvailable());
        } finally { Directory.Delete(directory, true); }
    }
    private static bool Parse(string text) => ChoiceBindingAvailability.Parse(Encoding.UTF8.GetBytes(text));
}
