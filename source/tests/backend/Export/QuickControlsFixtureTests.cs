using System.Text.Json;

namespace Backend.Tests;

internal static class QuickControlsFixtureTests {
    public static void TwentyTwoRealControllerProfilesJoinMirroredPoliciesWithIndependentEpochsAndCachedRows() {
        var frames = new Dictionary<string, Apparition.Backend.Ipc.StateDocument>();
        QuickControlsFixtureExporter.Add(frames);
        TestAssert.Equal(QuickControlsFixtureExporter.ProfileCount, frames.Count);
        string[] initial = { "default", "menu-draft", "menu-applied", "quick-slot2",
            "selector-opening-wait", "selector-opening-unlocked" };
        string[] changed = { "use-pending", "use-pre-refresh", "use-redraw", "use-pre-unlock", "use-unlocked", "duplicate-pending" };
        string[] trimmed = { "none-pending", "none-unlocked" };
        string[] disabled = { "disable-pending", "disable-redraw", "disable-unlocked" };
        string[] enabled = { "reenable-pending", "reenable-unlocked", "hidden", "closed" };
        foreach (string name in initial) Verify(name, 1, 1, new[] { 5, 10 }, false);
        foreach (string name in changed) Verify(name, 2, 2, new[] { 5, 8 }, false);
        foreach (string name in trimmed) Verify(name, 3, 3, new[] { 5 }, false);
        foreach (string name in disabled) Verify(name, 4, 3, new[] { 5 }, true);
        foreach (string name in enabled) Verify(name, 5, 3, new[] { 5 }, false);

        // The pending row belongs to the old Quick tuple while both published policies are actual
        foreach (string name in new[] { "use-pending", "use-pre-refresh" }) {
            TestAssert.Equal("OFF", Row(name, "Use"));
            TestAssert.Equal("ON", Row(name, "Secondary Offhand"));
        }
        TestAssert.Equal("ON", Row("use-redraw", "Use"));
        TestAssert.Equal("OFF", Row("use-redraw", "Secondary Offhand"));
        TestAssert.Equal("OFF", Row("disable-pending", "Disable Quick Menu"));
        TestAssert.Equal("ON", Row("disable-redraw", "Disable Quick Menu"));
        TestAssert.Equal("ON", Row("reenable-pending", "Disable Quick Menu"));
        TestAssert.Equal("OFF", Row("reenable-unlocked", "Disable Quick Menu"));
        TestAssert.True(frames["quick-controls-use-unlocked.json"].MenuPreferenceRefresh is null,
            "The source input wait retires without changing actual policy");
        var waiting = frames["quick-controls-selector-opening-wait.json"];
        var unlocked = frames["quick-controls-selector-opening-unlocked.json"];
        TestAssert.Equal("selector", waiting.MenuPreferenceRefresh?.Phase);
        TestAssert.Equal(550, waiting.MenuOpening?.ElapsedMilliseconds);
        TestAssert.True(unlocked.MenuPreferenceRefresh is null && unlocked.MenuOpening?.ElapsedMilliseconds == 800,
            "Selector cooldown cannot own or cancel the surviving source opening fade");
        TestAssert.Equal(waiting.MenuRevision, unlocked.MenuRevision);
        using var legacy = JsonDocument.Parse(frames["quick-controls-main-only.json"].Serialize());
        TestAssert.True(!legacy.RootElement.TryGetProperty("openingPolicies", out _),
            "A partial saved packet cannot publish an unverified pair");
        TestAssert.True(legacy.RootElement.TryGetProperty("mainOpeningControls", out _),
            "Missing Quick proof cannot remove the verified Main descriptor");

        void Verify(string name, ulong global, ulong quickRevision, int[] actions, bool disabledValue) {
            var frame = frames["quick-controls-" + name + ".json"];
            using var json = JsonDocument.Parse(frame.Serialize());
            var root = json.RootElement;
            var pair = root.GetProperty("openingPolicies");
            TestAssert.Equal(global, pair.GetProperty("revision").GetUInt64());
            TestAssert.Equal(1UL, pair.GetProperty("main").GetProperty("revision").GetUInt64());
            TestAssert.Equal(quickRevision, pair.GetProperty("quick").GetProperty("revision").GetUInt64());
            TestAssert.Equal(disabledValue, pair.GetProperty("quickDisabled").GetBoolean());
            TestAssert.True(pair.GetProperty("quick").GetProperty("actions").EnumerateArray()
                .Select(item => item.GetInt32()).SequenceEqual(actions), "Quick history must retain its independent tuple");
            TestAssert.True(pair.GetProperty("main").GetRawText() == root.GetProperty("mainOpeningControls").GetRawText(),
                "The mirror must derive from the same immutable Main owner");
            TestAssert.True(pair.GetProperty("main").GetProperty("actions").EnumerateArray()
                .Select(item => item.GetInt32()).SequenceEqual(new[] { 5, 4 }), "Quick edits cannot replace Main");
            TestAssert.True(!root.TryGetProperty("playerLife", out _) && !root.TryGetProperty("hasMenu", out _),
                "Configuration publication cannot fabricate live opening permission");
            TestAssert.Equal(name is not "hidden" and not "closed", frame.IsOpen);
        }

        string Row(string name, string label) => frames["quick-controls-" + name + ".json"].Rows.Single(row => row.Label == label).Value;
    }
}
