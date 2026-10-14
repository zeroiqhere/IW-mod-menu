using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class StealthRecoveryTests {
    public static void StealthClockRecoveryRetainsAValidCapturedAppearanceAndCleanupNeverRestoresForcedSettings() {
        foreach (bool applied in new[] { false, true }) {
            var rig = new MenuDesignTests.Rig();
            rig.Open();
            rig.Select("Stealth UI");
            if (applied) rig.Clock.Advance(TimeSpan.FromMilliseconds(180));
            var before = rig.Current();
            rig.Clock.RollBackTimestamp(TimeSpan.FromMilliseconds(1));
            var recovered = rig.Current();
            TestAssert.Equal(applied ? "cooldown" : "pending", recovered.MenuPreferenceRefresh!.Phase);
            TestAssert.Equal(before.Appearance, recovered.Appearance);
            TestAssert.Equal(before.MenuRevision, recovered.MenuRevision);
            TestAssert.True(recovered.Appearance!.IsSupported(), "Captured Stealth and display fields must remain mutually consistent");
            rig.Controller.RequestShutdown();
            var closed = rig.Controller.ClosedState();
            TestAssert.True(closed.Appearance!.StealthUI && closed.Appearance.BooleanDisplay == "Text", "Committed forced display survives cleanup even before180ms");
            TestAssert.True(!closed.IsOpen && closed.MenuPreferenceRefresh is null, "Cleanup discards only the pending presentation");
            rig.Clock.Advance(TimeSpan.FromSeconds(2));
            TestAssert.Equal(closed.MenuRevision, rig.Current().MenuRevision);
            rig.NoGameWork();
        }
    }

    public static void LegacyPacketsOmitInactiveRebuildAndOnlyThePhaseIsSerializedForAnActiveVisit() {
        var controller = new Apparition.Backend.Controller.BackendController(42, new SessionBoundary());
        using var initial = System.Text.Json.JsonDocument.Parse(controller.Snapshot().Serialize());
        TestAssert.True(!initial.RootElement.TryGetProperty("menuPreferenceRebuild", out _), "The new strict optional metadata must be absent when inactive");
        var rig = new MenuDesignTests.Rig();
        rig.Open();
        rig.Select("Option Counter");
        using var active = System.Text.Json.JsonDocument.Parse(rig.Current().Serialize());
        var metadata = active.RootElement.GetProperty("menuPreferenceRebuild");
        TestAssert.Equal(1, metadata.EnumerateObject().Count());
        TestAssert.Equal("rebuild", metadata.GetProperty("phase").GetString());
        TestAssert.True(!metadata.TryGetProperty("cause", out _), "The internal handler cause is not a protocol field");
        rig.NoGameWork();
    }
}
