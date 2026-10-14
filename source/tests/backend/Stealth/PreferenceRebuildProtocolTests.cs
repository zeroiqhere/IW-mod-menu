using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

internal static class PreferenceRebuildProtocolTests {
    public static void RebuildMetadataRequiresAnOpenPopulatedExclusiveChildAndRejectsHiddenOpening() {
        var snapshot = OverlayMenuBuilder.Build(MenuPage.Preferences, new SessionBoundary().State);
        foreach (string phase in new[] { "rebuild", "cooldown" }) {
            var frame = StateDocument.Create(42, 1, true, snapshot, 9, appearance: MenuAppearance.Default,
                isRootMenu: false, menuPreferenceRebuild: new(phase));
            TestAssert.Equal(phase, frame.MenuPreferenceRebuild!.Phase);
            var hidden = StateFocusProjection.Apply(frame, false);
            TestAssert.True(!hidden.IsOpen && hidden.MenuPreferenceRebuild is null, "Focus projection clears only active metadata");
            TestAssert.Equal(frame.Appearance, hidden.Appearance);
            TestAssert.Equal(frame.MenuRevision, hidden.MenuRevision);
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, false, snapshot, 0,
                isRootMenu: false, menuPreferenceRebuild: new(phase)));
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
                menuPreferenceRebuild: new(phase)));
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true,
                new("Empty", "PREVIEW", Array.Empty<MenuRow>(), ""), 0, isRootMenu: false, menuPreferenceRebuild: new(phase)));
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
                isRootMenu: false, menuPreferenceRefresh: new("pending"), menuPreferenceRebuild: new(phase)));
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
                isRootMenu: false, menuDesignTransition: new("cooldown"), menuPreferenceRebuild: new(phase)));
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
                isRootMenu: false, menuOpenTransition: new("cooldown"), menuPreferenceRebuild: new(phase)));
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
                isRootMenu: false, menuUnlock: "pause", menuPreferenceRebuild: new(phase)));
        }
        foreach (string phase in new[] { "pending", "Rebuild", "", "pause" }) {
            TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
                isRootMenu: false, menuPreferenceRebuild: new(phase)));
        }
        TestAssert.Throws<InvalidOperationException>(() => StateDocument.Create(42, 1, true, snapshot, 0,
            isRootMenu: false, menuOpening: new(1, 0), menuPreferenceRebuild: new("rebuild")));
        var drawn = StateDocument.Create(42, 1, true, snapshot, 0, isRootMenu: false,
            menuOpening: new(1, 0), menuPreferenceRebuild: new("cooldown"));
        TestAssert.True(drawn.MenuOpening is not null, "Opacity can continue through the visible input cooldown");
    }
}
