using System.Text;
using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class SavedBindingCapabilitiesTests {
    private const string Required = "\"version\":1,\"aim\":[2],\"melee\":[86],\"up\":[2,38],\"down\":[1,40],\"select\":[70]";
    private const string Choices = ",\"previousChoice\":[51],\"nextChoice\":[52]";
    public static void AttackRequiresItsExactDownSubsetWhileLegacyChoicesRemainAvailable() {
        var legacy = Parse("{" + Required + Choices + "}");
        TestAssert.True(legacy.ChoiceControlsAvailable && !legacy.WidthEditorControlsAvailable, "Legacy packets must retain choices without guessing attack");
        var supported = Parse("{" + Required + Choices + ",\"attack\":[1]}");
        TestAssert.True(supported.ChoiceControlsAvailable && supported.WidthEditorControlsAvailable, "Exact saved attack must enable the editor");
        TestAssert.True(ChoiceBindingAvailability.Parse(Encoding.UTF8.GetBytes("{" + Required + Choices + ",\"attack\":[1]}")), "The compatibility wrapper must use the same packet validation");
        foreach (string body in new[] { "{" + Required + ",\"attack\":[1]}", "{" + Required + Choices + ",\"attack\":[]}" }) {
            TestAssert.True(!Parse(body).WidthEditorControlsAvailable, "Missing choices or attack cannot enable width controls");
        }
    }

    public static void MalformedAttackOrAnyAmbiguousOverlapRejectsTheWholeBindingPacket() {
        string good = "{" + Required + Choices + ",\"attack\":[1]}";
        foreach (string attack in new[] { "[3]", "[2]", "[51]", "[86]", "[70]", "[1,1]", "[true]", "[1.0]", "[\"1\"]", "[255]", "null" }) {
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(good.Replace("\"attack\":[1]", "\"attack\":" + attack)));
        }
        foreach (string packet in new[] { good[..^1] + ",\"attack\":[1]}", good.Replace("[52]", "[51]"),
            good.Replace("\"aim\":[2]", "\"aim\":[]"), good[..^1] + ",\"unknown\":0}", new string('x', 4097) }) {
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(packet));
        }
    }

    public static void OneBoundedPrivateReadReturnsBothCapabilitiesAndRejectsLinkedPackets() {
        string directory = Path.Combine(Path.GetTempPath(), "apparition-width-bindings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var files = new SessionFiles(directory);
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, files.ReadSavedBindingCapabilities());
            string path = Path.Combine(directory, "bindings.json");
            File.WriteAllText(path, "{" + Required + Choices + ",\"attack\":[1]}");
            TestAssert.Equal(new SavedBindingCapabilities(true, true), files.ReadSavedBindingCapabilities());
            File.WriteAllText(path, new string('x', 4097));
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, files.ReadSavedBindingCapabilities());
            File.Delete(path);
            string target = Path.Combine(directory, "target.json");
            File.WriteAllText(target, "{" + Required + Choices + ",\"attack\":[1]}");
            File.CreateSymbolicLink(path, target);
            TestAssert.Throws<SessionFileException>(() => files.ReadSavedBindingCapabilities());
        } finally { Directory.Delete(directory, true); }
    }
    private static SavedBindingCapabilities Parse(string text) => SavedBindingCapabilities.Parse(Encoding.UTF8.GetBytes(text));
}
