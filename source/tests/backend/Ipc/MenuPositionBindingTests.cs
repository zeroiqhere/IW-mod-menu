using System.Text;
using System.Text.Json;
using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class MenuPositionBindingTests
{
    public static void AllTwelveExactGroupsEnablePositionWhileLegacyAndPartialPacketsPreserveExistingCapabilities()
    {
        var groups = Complete();
        var complete = Parse(groups);
        TestAssert.Equal(new SavedBindingCapabilities(true, true, true, true), complete);
        foreach (string missing in new[] { "frag", "secondaryOffhand" })
        {
            var partial = Complete(); partial.Remove(missing);
            TestAssert.Equal(new SavedBindingCapabilities(true, true, true, false), Parse(partial));
            partial[missing] = Array.Empty<int>();
            TestAssert.Equal(new SavedBindingCapabilities(true, true, true, false), Parse(partial));
        }
        var legacy = Complete(); legacy.Remove("frag"); legacy.Remove("secondaryOffhand");
        TestAssert.Equal(new SavedBindingCapabilities(true, true, true, false), Parse(legacy));
        foreach (string missing in new[] { "moveUp", "moveDown", "previousChoice", "nextChoice" })
        {
            var partial = Complete(); partial.Remove(missing);
            TestAssert.True(!Parse(partial).MenuPositionEditorControlsAvailable, "Every exact source direction is required");
        }
        var maximum = Complete(); maximum["frag"] = Enumerable.Range(100, 16).ToArray(); maximum["secondaryOffhand"] = Enumerable.Range(130, 16).ToArray();
        TestAssert.True(Parse(maximum).MenuPositionEditorControlsAvailable, "Both grenade groups accept their full bounded size");
        maximum["frag"] = Enumerable.Range(100, 17).ToArray();
        TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(maximum));
    }

    public static void MalformedDuplicateAndOverlappingGrenadeGroupsRejectTheEntirePrivatePacket()
    {
        string valid = JsonSerializer.Serialize(Complete());
        foreach (string field in new[] { "frag", "secondaryOffhand" })
        {
            string original = field == "frag" ? "[71]" : "[72]";
            foreach (string invalid in new[] { "null", "true", "[true]", "[71.0]", "[\"71\"]", "[0]", "[255]", "[71,71]" })
            {
                var bytes = Encoding.UTF8.GetBytes(valid.Replace("\"" + field + "\":" + original, "\"" + field + "\":" + invalid, StringComparison.Ordinal));
                TestAssert.Equal(SavedBindingCapabilities.Unavailable, SavedBindingCapabilities.Parse(bytes));
            }
        }
        foreach (int alias in new[] { 2, 86, 38, 1, 40, 70, 51, 52, 72 })
        {
            var overlap = Complete(); overlap["frag"] = new[] { alias };
            TestAssert.Equal(SavedBindingCapabilities.Unavailable, Parse(overlap));
        }
        TestAssert.Equal(SavedBindingCapabilities.Unavailable, SavedBindingCapabilities.Parse(Encoding.UTF8.GetBytes(valid[..^1] + ",\"frag\":[73]}")));
    }

    public static void LaunchRequestCannotEnablePositionWithoutTheSameStrictDerivedPacketCapability()
    {
        var capable = Parse(Complete());
        TestAssert.True(!capable.AllowMenuPositionEditor(false), "A complete packet cannot override an absent launch request");
        TestAssert.True(capable.AllowMenuPositionEditor(true), "Both verified inputs permit the local editor");
        var incomplete = Complete(); incomplete.Remove("frag");
        TestAssert.True(!Parse(incomplete).AllowMenuPositionEditor(true), "The flag alone cannot enable an incomplete packet");
        TestAssert.True(!SavedBindingCapabilities.Unavailable.AllowMenuPositionEditor(true), "Malformed packets cannot use the launch flag as a fallback");
    }

    private static Dictionary<string, object> Complete() => new()
    {
        ["version"] = 1,
        ["aim"] = new[] { 2 },
        ["melee"] = new[] { 86 },
        ["up"] = new[] { 2, 38 },
        ["down"] = new[] { 1, 40 },
        ["select"] = new[] { 70 },
        ["previousChoice"] = new[] { 51 },
        ["nextChoice"] = new[] { 52 },
        ["attack"] = new[] { 1 },
        ["moveUp"] = new[] { 38 },
        ["moveDown"] = new[] { 40 },
        ["frag"] = new[] { 71 },
        ["secondaryOffhand"] = new[] { 72 }
    };
    private static SavedBindingCapabilities Parse(Dictionary<string, object> groups) => SavedBindingCapabilities.Parse(JsonSerializer.SerializeToUtf8Bytes(groups));
}
