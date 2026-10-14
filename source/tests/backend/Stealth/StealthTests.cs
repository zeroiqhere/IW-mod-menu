using System.Text.Json;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// These checks drive the real controller through owned local clocks and rows
internal static class StealthTests {
    internal static readonly string[] Labels = {
        "Design", "Bool Display", "Bool Box Location", "Scroll Animation Time (ms)",
        "Quick Exit", "Disable Quick Menu", "Spotlight Cursor", "Colored Cursor",
        "Large Cursor", "Option Counter", "Stealth UI", "No Target While In Menu"
    };

    public static void SourcePreferenceInventoryPreservesActionIndicesAndUnavailableRows() {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        TestAssert.True(rig.Current().Rows.Select(row => row.Label).SequenceEqual(Labels), "The twelve source rows must retain their exact order");
        foreach (string label in new[] { "Disable Quick Menu", "No Target While In Menu" }) {
            var row = rig.Current().Rows.Single(row => row.Label == label);
            TestAssert.Equal("UNAVAILABLE", row.Value);
            TestAssert.True(!row.Enabled, "Unsupported controls cannot advertise a working action");
            rig.Select(label);
        }
        rig.NoGameWork();
    }

    public static void StealthWithoutCounterUsesDeferredRowsAndDoesNotRestoreTheForcedDisplay() {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        var before = rig.Current();
        rig.Select("Stealth UI");
        var pending = rig.Current();
        TestAssert.Equal("pending", Phase(pending, "menuPreferenceRefresh"));
        TestAssert.True(!Flag(pending), "The old valid presentation remains visible before refresh");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(179));
        TestAssert.True(!Flag(rig.Current()), "The pending presentation lasts through179ms");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var applied = rig.Current();
        TestAssert.True(Flag(applied), "Stealth must apply at180ms");
        TestAssert.Equal("Text", applied.Appearance!.BooleanDisplay);
        TestAssert.Equal(5, MenuOptionCapacity.For(applied.Appearance));
        TestAssert.Equal(before.MenuRevision + 1, applied.MenuRevision);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
        rig.Select("Stealth UI");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(430));
        TestAssert.True(!Flag(rig.Current()), "The second selection disables Stealth");
        TestAssert.Equal("Text", rig.Current().Appearance!.BooleanDisplay);
        rig.NoGameWork();
    }

    public static void CounterAndStealthRebuildUseIndependentPhasesWithOneObservedRedraw() {
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Select("Option Counter");
        TestAssert.Equal("rebuild", Phase(rig.Current(), "menuPreferenceRebuild"));
        TestAssert.True(!rig.Current().Appearance!.OptionCounter, "The captured rebuild appearance must remain valid");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        TestAssert.True(rig.Current().Appearance!.OptionCounter, "Counter applies at the observed redraw");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        rig.Select("Stealth UI");
        var pending = rig.Current();
        TestAssert.Equal("rebuild", Phase(pending, "menuPreferenceRebuild"));
        rig.Clock.Advance(TimeSpan.FromMilliseconds(49));
        TestAssert.True(!Flag(rig.Current()), "Old Stealth must survive the hidden rebuild gap");
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var applied = rig.Current();
        TestAssert.True(Flag(applied) && !applied.Appearance!.OptionCounter, "Stealth clears the existing counter");
        TestAssert.Equal(pending.MenuRevision + 1, applied.MenuRevision);
        TestAssert.Equal("cooldown", Phase(applied, "menuPreferenceRebuild"));
        TestAssert.Equal(0, applied.MenuOpening!.ElapsedMilliseconds);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(200));
        TestAssert.True(Phase(rig.Current(), "menuPreferenceRebuild") is null, "The chosen native policy unlocks at250ms");
        TestAssert.True(rig.Current().Rows.Single(row => row.Label == "Option Counter").Enabled,
            "The source BOOL remains selectable while its Stealth conflict is handled by the callback");
        rig.NoGameWork();
    }

    internal static bool Flag(Apparition.Backend.Ipc.StateDocument state) {
        using var document = JsonDocument.Parse(state.Serialize());
        return document.RootElement.GetProperty("appearance").TryGetProperty("stealthUI", out var value) && value.GetBoolean();
    }

    internal static string? Phase(Apparition.Backend.Ipc.StateDocument state, string name) {
        using var document = JsonDocument.Parse(state.Serialize());
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value.GetProperty("phase").GetString() : null;
    }
}
