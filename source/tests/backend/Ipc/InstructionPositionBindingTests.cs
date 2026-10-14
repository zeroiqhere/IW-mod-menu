using System.Text;
using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class InstructionPositionBindingTests {
    private const string Required = "\"version\":1,\"aim\":[2],\"melee\":[86],\"up\":[2,38],\"down\":[1,40],\"select\":[70]";
    private const string Choices = ",\"previousChoice\":[51],\"nextChoice\":[52]";
    private const string Movement = ",\"moveUp\":[38],\"moveDown\":[40]";

    public static void ExactMovementGroupsEnableOnlyTheMatchingEditorCapabilities() {
        // Position editing needs all four slots, but does not need the fire-only width key
        TestAssert.Equal(new SavedBindingCapabilities(true, false, true), Parse("{" + Required + Choices + Movement + "}"));
        TestAssert.Equal(new SavedBindingCapabilities(true, true, true), Parse("{" + Required + Choices + Movement + ",\"attack\":[1]}"));
        string remapped = "{" + Required.Replace("[2,38]", "[2,81,82]").Replace("[1,40]", "[1,69,84]")
            + Choices + ",\"moveUp\":[81,82],\"moveDown\":[69,84]}";
        TestAssert.Equal(new SavedBindingCapabilities(true, false, true), Parse(remapped));
    }

    public static void MissingMovementGroupsCannotBeInferredFromCombinedScrollKeys() {
        foreach (string addition in new[] { "", ",\"moveUp\":[38]", ",\"moveDown\":[40]",
            ",\"moveUp\":[],\"moveDown\":[40]", ",\"moveUp\":[38],\"moveDown\":[]" }) {
            var parsed = Parse("{" + Required + Choices + ",\"attack\":[1]" + addition + "}");
            TestAssert.Equal(new SavedBindingCapabilities(true, true, false), parsed);
        }
        // Optional choice directions cannot be invented even when both vertical slots exist
        TestAssert.Equal(new SavedBindingCapabilities(false, false, false), Parse("{" + Required + Movement + "}"));
        TestAssert.Equal(new SavedBindingCapabilities(true, true, false), Parse("{" + Required + Choices + ",\"attack\":[1]}"));
    }

    public static void MalformedMovementOrAmbiguousMembershipRejectsAllCapabilities() {
        string good = "{" + Required + Choices + Movement + ",\"attack\":[1]}";
        foreach (string invalid in new[] { "[2]", "[1]", "[40]", "[51]", "[86]", "[70]", "[37]",
            "[38,38]", "[true]", "[38.0]", "[\"38\"]", "[255]", "null", "{}" }) {
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(good.Replace("\"moveUp\":[38]", "\"moveUp\":" + invalid)));
        }
        foreach (string invalid in new[] { "[1]", "[2]", "[38]", "[52]", "[40,40]", "[0]", "null", "[false]" }) {
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(good.Replace("\"moveDown\":[40]", "\"moveDown\":" + invalid)));
        }
        // Repeated and escaped aliases cannot silently replace the confirmed movement group
        foreach (string extra in new[] { ",\"moveUp\":[38]", ",\"move\\u0055p\":[38]", ",\"unexpected\":[]" })
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(good[..^1] + extra + "}"));
        string tooMany = string.Join(",", Enumerable.Range(20, 17));
        TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(good.Replace("\"moveUp\":[38]", "\"moveUp\":[" + tooMany + "]")));
    }

    private static SavedBindingCapabilities Parse(string text) => SavedBindingCapabilities.Parse(Encoding.UTF8.GetBytes(text));
}
