using Apparition.Backend.Ipc;

namespace Backend.Tests;

internal static class FamilyNavigationFixtureTests {
    public static void ExportedParentProfilesUseControllerBackAndKeepAllGameActionsInactive() {
        var profiles = new Dictionary<string, StateDocument>();
        FamilyNavigationFixtureProfiles.Add(profiles);
        TestAssert.Equal(3, profiles.Count);
        var before = profiles["navigation-shared-parent-before-back.json"];
        var after = profiles["navigation-shared-parent-after-back.json"];
        var reopened = profiles["navigation-shared-parent-after-reopen.json"];
        TestAssert.Equal("Main Design Color", before.Title);
        TestAssert.Equal(5, before.SelectedIndex);
        TestAssert.Equal("Menu Customization", after.Title);
        TestAssert.Equal(4, after.SelectedIndex);
        TestAssert.Equal(4, reopened.SelectedIndex);
        TestAssert.Equal(after.Title, reopened.Title);
        TestAssert.True(after.IsOpen && reopened.IsOpen && !after.IsRootMenu && !reopened.IsRootMenu,
            "Retained child snapshots stay open within their own parent path");
        TestAssert.True(after.NavigationRevision < reopened.NavigationRevision,
            "A reopened menu has a fresh command context");
        foreach (var packet in profiles.Values) {
            TestAssert.Equal("PREVIEW", packet.Context);
            TestAssert.True(packet.Serialize().Length <= StateDocument.MaximumBytes,
                "An actual navigation snapshot remains within the wire limit");
        }
    }
}
