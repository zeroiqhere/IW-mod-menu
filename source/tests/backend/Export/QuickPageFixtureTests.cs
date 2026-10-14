using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class QuickPageFixtureTests {
    public static void ActualQuickRootExportsOnlyUnavailableRespawnAndRootCloseMetadata() {
        var profiles = new Dictionary<string, StateDocument>();
        QuickPageFixtureProfiles.Add(profiles);
        TestAssert.Equal(1, profiles.Count);
        var packet = profiles["quick-root-unbound.json"];
        // This view is an owned offline controller path, not a new native opening permission
        TestAssert.True(packet.IsOpen && packet.IsRootMenu, "The Quick family root closes locally");
        TestAssert.Equal("Quick Menu", packet.Title);
        TestAssert.Equal("PREVIEW", packet.Context);
        TestAssert.Equal(1, packet.Rows.Count);
        TestAssert.Equal("Respawn", packet.Rows[0].Label);
        TestAssert.Equal("UNAVAILABLE", packet.Rows[0].Value);
        TestAssert.True(!packet.Rows[0].Enabled, "Unknown facts cannot expose a feature");
        TestAssert.True(packet.MenuOpenTransition is null && packet.MenuOpening is null,
            "An owned display fixture cannot fabricate a Main or Quick opening timeline");
        TestAssert.True(packet.Notice.Contains("Quick opening is unavailable", StringComparison.Ordinal),
            "The view explains its actual runtime availability");
        TestAssert.True(packet.Serialize().Length <= StateDocument.MaximumBytes,
            "The actual view remains within the bounded native wire format");
    }
}
